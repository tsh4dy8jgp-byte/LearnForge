using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace LearnForge.Tests;

// The compact exam/1 source compiles through ContentEngine into an ordinary, validated exam pack.
public class ExamSourceTests
{
    public static string SampleSource() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", "exam-sample.json"));
    public static Pack Sample() => ContentEngine.Compile(SampleSource()).Pack!;

    private static Compilation Edit(Action<JsonObject> change)
    {
        var source = JsonNode.Parse(SampleSource())!.AsObject();
        change(source);
        return ContentEngine.Compile(source.ToJsonString());
    }

    private static JsonObject Question(JsonObject source, string id) =>
        source["questions"]!.AsArray().Select(q => q!.AsObject()).Single(q => (string?)q["id"] == id);

    private static IEnumerable<Option> AllOptions(Question q) => q.Options.Concat(q.Slots.SelectMany(s => s.Options));

    [Fact]
    public void Sample_exam_compiles_to_an_exam_pack_with_weighted_mocks()
    {
        var compiled = ContentEngine.Compile(SampleSource());
        Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}")));
        var pack = compiled.Pack!;
        Assert.Equal(ProductProfile.Exam, pack.Profile);
        Assert.Empty(pack.Lessons);
        Assert.False(pack.Features.Lessons);
        Assert.Equal(Enum.GetValues<QuestionKind>().Order(), pack.Questions.Select(q => q.Kind).Distinct().Order());
        Assert.Equal(["short", "full"], pack.Blueprints.Select(b => b.Id));
        Assert.All(pack.Blueprints, b =>
        {
            Assert.Equal(["http", "security", "performance"], b.ObjectiveIds);
            Assert.Equal(40, b.ObjectiveWeights!["http"]);
            Assert.True(b.LockSections);
            Assert.Empty(b.RequiredKinds);
            Assert.Equal(b.Count, ExamComposer.Compose(pack, b, [], "test").Length);
        });
        Assert.Equal(3, pack.Questions.Count(q => q.ScenarioId == "harbor-shop"));
        Assert.All(pack.Questions, q => Assert.Equal(q.Id, q.FamilyId));
    }

    [Theory]
    [InlineData("single")][InlineData("multiple")][InlineData("sequence")][InlineData("matching")]
    [InlineData("dropdown")][InlineData("numeric")][InlineData("codeOutput")]
    public void Every_kind_converts_to_a_gradable_question(string kind)
    {
        var q = Sample().Questions.First(q => q.Kind == Enum.Parse<QuestionKind>(kind, ignoreCase: true));
        Answer perfect = q.Kind is QuestionKind.Numeric or QuestionKind.CodeOutput
            ? new([], [], q.Grading.Correct[0])
            : new(q.Grading.Correct, q.Grading.Matches ?? []);
        Assert.Null(Grader.Validate(q, perfect));
        Assert.True(Grader.Score(q, perfect).FullyCorrect);
        Assert.Equal(0, Grader.Score(q, null).Earned);
    }

    [Fact]
    public void Defaults_follow_the_kind()
    {
        var pack = Sample();
        Question Of(string id) => pack.Questions.Single(q => q.Id == id);
        Assert.Equal(ScoringPolicy.Exact, Of("http-redirect-status").Grading.Policy);
        Assert.Equal(1, Of("http-redirect-status").SelectCount);
        Assert.Equal(ScoringPolicy.Partial, Of("http-idempotent-writes").Grading.Policy);
        Assert.Equal(2, Of("http-idempotent-writes").SelectCount);
        Assert.Equal(["3"], Of("performance-transfer-time").Grading.Correct);
        Assert.Equal(0.05m, Of("performance-transfer-time").Grading.Tolerance);
        Assert.Equal(["miss miss hit"], Of("performance-cache-trace").Grading.Correct);
        Assert.Equal("javascript", Of("performance-cache-trace").Code!.Language);
        Assert.Equal(["b1", "b2"], Of("security-password-storage").Slots.Select(s => s.Id));
    }

    [Fact]
    public void Option_ids_are_opaque_stable_and_independent_of_role()
    {
        var pack = Sample();
        Assert.All(pack.Questions.SelectMany(AllOptions), o => Assert.Matches(new Regex("^o[0-9a-f]{8}$"), o.Id));
        Assert.Equal(pack.Questions.SelectMany(AllOptions).Select(o => o.Id), Sample().Questions.SelectMany(AllOptions).Select(o => o.Id));

        // Swapping which text is the key keeps every option ID, so an ID can never be recomputed to reveal the key.
        var swapped = Edit(source =>
        {
            var q = Question(source, "http-redirect-status");
            q["answer"] = "302 Found";
            q["distractors"] = new JsonArray("301 Moved Permanently", "304 Not Modified", "307 Temporary Redirect");
        }).Pack!.Questions.Single(q => q.Id == "http-redirect-status");
        var original = pack.Questions.Single(q => q.Id == "http-redirect-status");
        Assert.Equal(original.Options, swapped.Options);
        Assert.NotEqual(original.Grading.Correct, swapped.Grading.Correct);
    }

    [Fact]
    public void Generated_banks_are_ordered_by_id_rather_than_listing_the_key_first()
    {
        foreach (var q in Sample().Questions)
            foreach (var bank in new[] { q.Options }.Concat(q.Slots.Select(s => s.Options)))
                Assert.Equal(bank.Select(o => o.Id).Order(StringComparer.Ordinal), bank.Select(o => o.Id));
        var sequence = Sample().Questions.Single(q => q.Kind == QuestionKind.Sequence);
        Assert.Equal(["Resolve the domain name to an IP address", "Open a TCP connection to the server", "Complete the TLS handshake", "Send the HTTP request for the page"],
            sequence.Grading.Correct.Select(id => sequence.Options.Single(o => o.Id == id).Text));
    }

    [Fact]
    public void Matching_infers_reuse_and_keeps_extra_matches()
    {
        var matching = Sample().Questions.Single(q => q.Id == "http-header-purpose");
        Assert.False(matching.Reuse);
        Assert.Equal(3, matching.Slots.Length);
        Assert.Equal(4, matching.Options.Length);
        Assert.All(matching.Slots, s => Assert.Empty(s.Options));

        var reused = Edit(source => Question(source, "http-header-purpose")["pairs"]!.AsArray().Add(
            new JsonObject { ["item"] = "Expires", ["match"] = "Sets how long a response may be reused" })).Pack!.Questions.Single(q => q.Id == "http-header-purpose");
        Assert.True(reused.Reuse);
        Assert.Equal(4, reused.Options.Length);
        Assert.Equal(reused.Grading.Matches!["p1"], reused.Grading.Matches["p4"]);
    }

    [Theory]
    [InlineData("http-redirect-status", "steps", "'steps' does not apply to single questions.")]
    [InlineData("http-first-visit-order", "distractors", "'distractors' does not apply to sequence questions.")]
    [InlineData("performance-transfer-time", "distractors", "'distractors' does not apply to numeric questions.")]
    public void Fields_that_do_not_apply_to_a_kind_are_rejected(string id, string field, string message)
    {
        var compiled = Edit(source => Question(source, id)[field] = new JsonArray("extra"));
        Assert.False(compiled.Success);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LF110" && d.Path == id && d.Message == message);
    }

    [Fact]
    public void Malformed_questions_fail_closed_with_their_id()
    {
        Diagnostic[] Errors(string id, Action<JsonObject> change) => Edit(source => change(Question(source, id))).Diagnostics;
        Assert.Contains(Errors("http-redirect-status", q => q["distractors"] = new JsonArray("301 moved permanently")),
            d => d.Code == "LF110" && d.Path == "http-redirect-status" && d.Message.Contains("same text"));
        Assert.Contains(Errors("http-redirect-status", q => q.Remove("domain")), d => d.Code == "LF110" && d.Message.Contains("'domain'"));
        Assert.Contains(Errors("http-idempotent-writes", q => q["answers"] = new JsonArray("PUT")), d => d.Message.Contains("at least two 'answers'"));
        Assert.Contains(Errors("http-redirect-status", q => q["answer"] = 301), d => d.Message.Contains("'answer' must be nonblank text"));
        Assert.Contains(Errors("security-password-storage", q => q["blanks"]![0]!["distractors"] = new JsonArray()), d => d.Message.Contains("Blank 1"));
        Assert.Contains(Errors("http-header-purpose", q => q["extraMatches"] = new JsonArray("Points to the target of a redirect")),
            d => d.Message.Contains("extra match repeats"));
        Assert.Contains(Errors("performance-transfer-time", q => q["answers"] = new JsonArray(3)), d => d.Message.Contains("either 'answer' or 'answers'"));
        // Converted questions still go through the ordinary validator, which knows the pack's domains.
        Assert.Contains(Errors("http-redirect-status", q => q["domain"] = "networking"), d => d.Code == "LF100" && d.Path == "http-redirect-status");
    }

    [Fact]
    public void Unknown_formats_fields_and_partial_weights_fail_closed()
    {
        var unknownFormat = Edit(source => source["format"] = "exam/2");
        Assert.Contains(unknownFormat.Diagnostics, d => d.Code == "LF001" && d.Message.Contains("Unknown source format 'exam/2'"));
        var unknownField = Edit(source => Question(source, "http-redirect-status")["hint"] = "Think about permanence.");
        Assert.Contains(unknownField.Diagnostics, d => d.Code == "LF001" && d.Message.Contains("hint"));
        var partialWeights = Edit(source => source["domains"]![0]!.AsObject().Remove("weight"));
        Assert.Contains(partialWeights.Diagnostics, d => d.Code == "LF110" && d.Path == "domains");
        var unweighted = Edit(source => { foreach (var domain in source["domains"]!.AsArray()) domain!.AsObject().Remove("weight"); });
        Assert.True(unweighted.Success);
        Assert.All(unweighted.Pack!.Blueprints, b => Assert.Null(b.ObjectiveWeights));
        // A direct pack still rejects the property, so the two shapes cannot be confused.
        Assert.False(ContentEngine.Compile(Json.Write(CoreTests.Demo()).Replace("\"schemaVersion\": 1", "\"format\": \"pack\", \"schemaVersion\": 1")).Success);
    }

    [Fact]
    public void Exam_sources_work_inside_the_template_wrapper()
    {
        var source = JsonNode.Parse(SampleSource())!.AsObject();
        var questions = source["questions"]!.AsArray();
        var template = questions[0]!.DeepClone().AsObject();
        template["explanation"] = "{{why}}";
        questions[0] = new JsonObject { ["$use"] = "redirect", ["values"] = new JsonObject { ["why"] = "301 marks the move as permanent, so links and caches follow the new address." } };
        var wrapped = new JsonObject { ["templates"] = new JsonObject { ["redirect"] = template }, ["pack"] = source };
        var compiled = ContentEngine.Compile(wrapped.ToJsonString());
        Assert.True(compiled.Success);
        Assert.StartsWith("301 marks the move", compiled.Pack!.Questions[0].Explanation);
    }

    [Fact]
    public void Exam_delivery_omits_keys_and_explanations()
    {
        foreach (var q in Sample().Questions)
        {
            var delivered = Json.Write(DeliveryQuestion.From(q));
            Assert.DoesNotContain("grading", delivered);
            Assert.DoesNotContain(q.Explanation, delivered);
            if (q.Kind is QuestionKind.Numeric or QuestionKind.CodeOutput) Assert.DoesNotContain("\"" + q.Grading.Correct[0] + "\"", delivered);
        }
    }
}
