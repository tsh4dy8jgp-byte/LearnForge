using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using LearnForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

// Numeric, code-output and short-answer questions take a typed text response instead of option IDs or slots.
public class TextResponseTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    public static Question Numeric(string id = "numeric-1", decimal? tolerance = 0.5m) =>
        new(id, id, QuestionKind.Numeric, "How many elements does {1, 2, 2, 3, 4} have?", ["sets"], [], [], 0, false,
            new(ScoringPolicy.Exact, ["4"], Tolerance: tolerance), "Repeats count once, so there are four elements.");

    public static Question CodeOutput(string id = "output-1") =>
        new(id, id, QuestionKind.CodeOutput, "What does this program print?", ["sets"], [], [], 0, false,
            new(ScoringPolicy.Exact, ["3 [1, 2]"]), "A set keeps each distinct value once.",
            Code: new("python", "print(len({x % 3 for x in range(10)}), sorted({1 % 4, 2 % 4}))"));

    public static Question ShortAnswer(string id = "short-1", bool? ignoreDiacritics = null, params string[] keys) =>
        new(id, id, QuestionKind.ShortAnswer, "Complétez au passé composé : « Hier, nous ___ (partir) tôt. »", ["sets"], [], [], 0, false,
            new(ScoringPolicy.Exact, keys.Length > 0 ? keys : ["sommes partis", "sommes parties"], IgnoreDiacritics: ignoreDiacritics),
            "Partir takes être in the passé composé, and its participle agrees with the subject nous.");

    private static Answer Text(string? text) => new([], [], text);

    [Theory]
    [InlineData("4", true)]
    [InlineData(" 4.4 ", true)]
    [InlineData("3.5", true)]
    [InlineData("4.6", false)]
    [InlineData("-4", false)]
    [InlineData("four", false)]
    [InlineData("", false)]
    public void Numeric_responses_are_correct_within_the_tolerance(string response, bool correct)
    {
        Assert.Equal(correct, Grader.Score(Numeric(), Text(response)).FullyCorrect);
    }

    [Fact]
    public void Numeric_keys_are_exact_without_a_tolerance()
    {
        Assert.True(Grader.Score(Numeric(tolerance: null), Text("4.0")).FullyCorrect);
        Assert.False(Grader.Score(Numeric(tolerance: null), Text("4.01")).FullyCorrect);
    }

    [Theory]
    [InlineData("3 [1, 2]", true)]
    [InlineData("  3   [1,\t2]\r\n", true)]
    [InlineData("3 [1,2]", false)]
    [InlineData("3", false)]
    public void Code_output_ignores_line_endings_surrounding_space_and_space_runs(string response, bool correct)
    {
        Assert.Equal(correct, Grader.Score(CodeOutput(), Text(response)).FullyCorrect);
    }

    [Theory]
    [InlineData("sommes partis", true)]
    [InlineData("sommes parties", true)]
    [InlineData("Sommes Partis", true)]
    [InlineData("  sommes   partis. ", true)]
    [InlineData("sommes\u00A0partis !", true)]
    [InlineData("sommes parti", false)]
    [InlineData("avons parti", false)]
    [InlineData("partis", false)]
    [InlineData("", false)]
    public void Short_answers_ignore_case_spacing_and_closing_punctuation(string response, bool correct)
    {
        Assert.Equal(correct, Grader.Score(ShortAnswer(), Text(response)).FullyCorrect);
    }

    [Theory]
    [InlineData("j'ai", "j’ai", true)]
    [InlineData("j'ai", "j' ai", true)]
    [InlineData("j'ai", "J ’ ai", true)]
    [InlineData("sœur", "soeur", true)]
    [InlineData("sœur", "SŒUR", true)]
    [InlineData("est-ce que", "est\u2011ce que", true)]
    [InlineData("est allée", "est alle\u0301e", true)]
    [InlineData("est allée", "EST ALLÉE ?", true)]
    [InlineData("est allée", "est allee", false)]
    [InlineData("à Milan", "a Milan", false)]
    public void Short_answers_count_diacritics_by_default(string key, string response, bool correct)
    {
        Assert.Equal(correct, Grader.Score(ShortAnswer(keys: key), Text(response)).FullyCorrect);
    }

    [Theory]
    [InlineData("est allee", true)]
    [InlineData("est allée", true)]
    [InlineData("EST ALLEE", true)]
    [InlineData("est alle", false)]
    public void Short_answers_can_ignore_diacritics(string response, bool correct)
    {
        Assert.Equal(correct, Grader.Score(ShortAnswer(ignoreDiacritics: true, keys: "est allée"), Text(response)).FullyCorrect);
    }

    [Fact]
    public void Unanswered_text_questions_score_zero()
    {
        Assert.Equal(0, Grader.Score(Numeric(), null).Earned);
        Assert.Equal(0, Grader.Score(CodeOutput(), null).Earned);
        Assert.Equal(0, Grader.Score(ShortAnswer(), null).Earned);
    }

    [Fact]
    public void Text_and_selection_responses_do_not_mix()
    {
        Assert.NotNull(Grader.Validate(Numeric(), new(["a"], [], "4")));
        Assert.NotNull(Grader.Validate(CodeOutput(), Text(new string('x', 2001))));
        Assert.NotNull(Grader.Validate(ShortAnswer(), new([], new() { ["b1"] = "a" }, "sommes partis")));
        var single = CoreTests.Demo().Questions.First(q => q.Kind == QuestionKind.Single);
        Assert.NotNull(Grader.Validate(single, new(single.Grading.Correct, [], "text")));
    }

    [Fact]
    public void Well_formed_text_questions_compile()
    {
        var pack = CoreTests.Demo();
        var extended = pack with { Questions = [.. pack.Questions, Numeric(), CodeOutput(), ShortAnswer(), ShortAnswer("short-2", ignoreDiacritics: true)] };

        Assert.Empty(ContentEngine.Validate(extended));
        var compiled = ContentEngine.Compile(Json.Write(extended));
        Assert.True(compiled.Success);
        Assert.Contains("\"codeOutput\"", Json.Write(extended));
        Assert.Contains("\"shortAnswer\"", Json.Write(extended));
        Assert.Equal("python", compiled.Pack!.Questions.Single(q => q.Id == "output-1").Code!.Language);
        Assert.True(compiled.Pack.Questions.Single(q => q.Id == "short-2").Grading.IgnoreDiacritics);
        // The flag is omitted when unset, so earlier releases and snapshots serialize exactly as before.
        Assert.DoesNotContain("ignoreDiacritics", Json.Write(pack));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(Json.Write(extended), "ignoreDiacritics"));
    }

    public static TheoryData<string, Question> Malformed => new()
    {
        { "numeric with options", Numeric() with { Options = [new("a", "4"), new("b", "5")] } },
        { "numeric with a word key", Numeric() with { Grading = new(ScoringPolicy.Exact, ["four"]) } },
        { "numeric without a key", Numeric() with { Grading = new(ScoringPolicy.Exact, []) } },
        { "numeric with a negative tolerance", Numeric(tolerance: -1) },
        { "numeric with a code sample", Numeric() with { Code = new("python", "print(4)") } },
        { "code output without code", CodeOutput() with { Code = null } },
        { "code output with a blank key", CodeOutput() with { Grading = new(ScoringPolicy.Exact, ["  "]) } },
        { "code output with an invalid language", CodeOutput() with { Code = new("Python 3!", "print(1)") } },
        { "code output with partial credit", CodeOutput() with { Grading = new(ScoringPolicy.Partial, ["3 [1, 2]"]) } },
        { "short answer with options", ShortAnswer() with { Options = [new("a", "sommes partis"), new("b", "avons parti")] } },
        { "short answer with a blank key", ShortAnswer(keys: ["sommes partis", " . "]) },
        { "short answer with keys that normalize alike", ShortAnswer(keys: ["J'ai", "j’ai."]) },
        { "short answer with partial credit", ShortAnswer() with { Grading = new(ScoringPolicy.Partial, ["sommes partis"]) } },
        { "short answer with a code sample", ShortAnswer() with { Code = new("python", "print(1)") } },
        { "numeric ignoring diacritics", Numeric() with { Grading = new(ScoringPolicy.Exact, ["4"], IgnoreDiacritics: true) } },
        { "choice with a code sample", CoreTests.Demo().Questions.First(q => q.Kind == QuestionKind.Single) with { Code = new("python", "print(1)") } },
        { "choice with a tolerance", CoreTests.Demo().Questions.First(q => q.Kind == QuestionKind.Single) is var s ? s with { Grading = s.Grading with { Tolerance = 1 } } : null! }
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void Malformed_text_contracts_are_rejected(string _, Question question)
    {
        var pack = CoreTests.Demo();
        var broken = pack with { Questions = [.. pack.Questions.Where(q => q.Id != question.Id), question with { Id = "broken-" + question.Id }] };

        Assert.Contains(ContentEngine.Validate(broken), d => d.Path == "broken-" + question.Id);
    }

    [Fact]
    public void Delivery_shows_the_program_but_never_the_accepted_answers()
    {
        var text = Json.Write(new[] { Numeric(), CodeOutput(), ShortAnswer(ignoreDiacritics: true) }.Select(DeliveryQuestion.From));

        Assert.Contains("sorted({1 % 4, 2 % 4})", text);
        Assert.DoesNotContain("3 [1, 2]", text);
        Assert.DoesNotContain("sommes partis", text);
        Assert.DoesNotContain("ignoreDiacritics", text);
        Assert.DoesNotContain("tolerance", text);
        Assert.DoesNotContain("grading", text);
    }

    [Fact]
    public async Task Text_answers_are_saved_checked_and_graded_on_the_server()
    {
        var publisher = await TestApi.Publisher(factory);
        var demo = CoreTests.Demo();
        var pack = demo with
        {
            Id = "text-" + Guid.NewGuid().ToString("N"),
            Questions = [.. demo.Questions, Numeric(), CodeOutput(), ShortAnswer()],
            Blueprints = [new("typed", "Typed answers", 3, 5, AssessmentSize.Short, ["sets"], [QuestionKind.Numeric, QuestionKind.CodeOutput, QuestionKind.ShortAnswer])]
        };
        (await publisher.PostAsJsonAsync("/api/authoring/publish", pack)).EnsureSuccessStatusCode();

        var attempt = await TestApi.Start(publisher, "learn", pack.Id, "typed");
        var delivered = attempt.GetProperty("questions").EnumerateArray().ToArray();
        Assert.Equal("python", delivered.Single(q => q.GetProperty("id").GetString() == "output-1").GetProperty("code").GetProperty("language").GetString());

        var revision = await TestApi.Save(publisher, attempt, 0, "numeric-1", Text("4.2"), check: true);
        revision = await TestApi.Save(publisher, attempt, revision, "output-1", Text("3 [1,2]"), check: true);
        await TestApi.Save(publisher, attempt, revision, "short-1", Text("Sommes  parties."), check: true);
        var view = await publisher.GetFromJsonAsync<JsonElement>($"/api/me/attempts/{TestApi.Id(attempt)}");
        var feedback = view.GetProperty("feedback");

        Assert.True(feedback.GetProperty("numeric-1").GetProperty("fullyCorrect").GetBoolean());
        Assert.False(feedback.GetProperty("output-1").GetProperty("fullyCorrect").GetBoolean());
        Assert.True(feedback.GetProperty("short-1").GetProperty("fullyCorrect").GetBoolean());
        Assert.Equal(["sommes partis", "sommes parties"], feedback.GetProperty("short-1").GetProperty("correct").EnumerateArray().Select(k => k.GetString()));
        Assert.Equal("4.2", view.GetProperty("answers").GetProperty("numeric-1").GetProperty("text").GetString());

        // Learning evidence only counts answered questions, so a typed answer must be recorded as answered.
        using var scope = factory.Services.CreateScope();
        var evidence = await scope.ServiceProvider.GetRequiredService<AppDb>().Evidence
            .Where(e => e.AttemptId == TestApi.Id(attempt)).ToListAsync();
        Assert.Equal(3, evidence.Count);
        Assert.All(evidence, e => Assert.True(e.Answered, $"{e.QuestionId} was recorded as unanswered."));
    }
}
