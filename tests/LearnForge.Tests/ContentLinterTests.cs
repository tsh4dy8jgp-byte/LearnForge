using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace LearnForge.Tests;

public class ContentLinterTests
{
    private static Option[] Options(params string[] texts) => texts.Select((t, i) => new Option("o" + i, t)).ToArray();

    // Option o0 is the key.
    private static Question Single(string prompt, params string[] options) => new("q", "q", QuestionKind.Single, prompt, ["http"],
        Options(options), [], 1, false, new(ScoringPolicy.Exact, ["o0"]), "A long enough explanation of why the key is right and the rest are wrong.");

    private static Pack With(params Question[] questions) => ExamSourceTests.Sample() with { Questions = questions, Scenarios = [] };

    private static string[] Codes(Question q, string? path = null) =>
        ContentLinter.Lint(With(q)).Where(d => d.Path == (path ?? q.Id) || d.Path.StartsWith(q.Id + ".")).Select(d => d.Code).ToArray();

    [Fact]
    public void Sample_exam_lints_clean()
    {
        Assert.Empty(ContentLinter.Lint(ExamSourceTests.Sample()));
    }

    [Fact]
    public void A_key_much_longer_than_its_distractors_is_flagged()
    {
        Assert.Contains("LF201", Codes(Single("Which status code marks a permanent move?",
            "301, because it tells clients and crawlers to update every stored link", "302", "304", "307")));
        Assert.DoesNotContain("LF201", Codes(Single("Which status code marks a permanent move?", "301 Moved Permanently", "302 Found", "304 Not Modified", "307 Temporary Redirect")));
        var dropdown = ExamSourceTests.Sample().Questions.Single(q => q.Id == "security-password-storage");
        var key = dropdown.Grading.Matches!["b1"];
        var longKey = dropdown with { Slots = [dropdown.Slots[0] with { Options = dropdown.Slots[0].Options
            .Select(o => o.Id == key ? o with { Text = "a salted, deliberately slow, memory-hard password hash such as Argon2id" } : o).ToArray() }] };
        Assert.Contains(ContentLinter.Lint(With(longKey)), d => d.Code == "LF201" && d.Path == dropdown.Id + ".b1");
    }

    [Fact]
    public void A_bank_whose_key_is_usually_longest_is_flagged()
    {
        var biased = Enumerable.Range(0, 20).Select(i => Single("Pick the best approach for case " + i + ".",
            "Option text that runs long", "Short one", "Brief text", "Tiny one") with { Id = "q" + i, FamilyId = "q" + i }).ToArray();
        Assert.Contains(ContentLinter.Lint(With(biased)), d => d.Code == "LF202");
        var varied = biased.Select((q, i) => i % 4 == 0 ? q : q with { Options = Options("Short one", "Option text that runs long", "Brief text", "Tiny one") }).ToArray();
        Assert.DoesNotContain(ContentLinter.Lint(With(varied)), d => d.Code == "LF202");
    }

    [Fact]
    public void Absolutes_only_in_distractors_and_hedges_only_in_the_key_are_flagged()
    {
        Assert.Contains("LF203", Codes(Single("How does caching affect repeat visits?", "Repeat visits load faster for most pages",
            "Repeat visits always fail", "Caching never helps repeat visits", "Repeat visits become slower")));
        Assert.Contains("LF203", Codes(Single("How does caching affect repeat visits?", "Repeat visits usually load faster",
            "Repeat visits always load faster", "Repeat visits load slower", "Repeat visits stay the same")));
        Assert.DoesNotContain("LF203", Codes(Single("How does caching affect repeat visits?", "Repeat visits load faster",
            "Repeat visits load slower", "Repeat visits stay the same", "Repeat visits fail")));
    }

    [Fact]
    public void Stem_words_echoed_only_in_the_key_are_flagged()
    {
        Assert.Contains("LF204", Codes(Single("Which protocol encrypts web traffic?", "A protocol that encrypts traffic", "Plain HTTP", "FTP over port 21", "Telnet")));
        Assert.DoesNotContain("LF204", Codes(Single("Which protocol encrypts web traffic?", "HTTPS", "HTTP", "FTP", "Telnet")));
    }

    [Fact]
    public void A_key_repeated_in_the_stem_is_flagged()
    {
        Assert.Contains("LF205", Codes(Single("Traffic to port 443 is usually HTTPS. Which port does HTTPS use by default?", "port 443", "port 80", "port 21", "port 25")));
        Assert.DoesNotContain("LF205", Codes(Single("Which port does HTTPS use by default?", "port 443", "port 80", "port 21", "port 25")));
    }

    [Fact]
    public void An_article_that_fits_only_the_key_is_flagged()
    {
        Assert.Contains("LF206", Codes(Single("Twelve is an", "even number", "prime number", "negative number", "fraction")));
        Assert.DoesNotContain("LF206", Codes(Single("Which kind of number is twelve?", "even", "prime", "negative", "fractional")));
    }

    [Fact]
    public void Position_dependent_options_and_letter_references_are_flagged()
    {
        Assert.Contains("LF207", Codes(Single("Which methods are safe?", "All of the above", "GET", "HEAD", "OPTIONS")));
        Assert.Contains("LF207", Codes(Single("Which method is safe?", "GET", "POST", "PUT", "DELETE") with { Explanation = "Option A is right because GET only reads data and never changes it." }));
        Assert.Contains("LF207", Codes(Single("Which method is safe?", "Both A and B", "GET", "POST", "PUT")));
        Assert.DoesNotContain("LF207", Codes(Single("Which method is safe?", "GET", "POST", "PUT", "DELETE") with { Explanation = "GET only reads data; it is an answer a learner should know." }));
    }

    [Fact]
    public void Near_duplicate_options_are_flagged()
    {
        Assert.Contains("LF208", Codes(Single("Which step comes first?", "Hash it.", "hash it", "Encrypt it", "Store it")));
    }

    [Fact]
    public void Too_few_options_are_flagged_per_kind()
    {
        Assert.Contains("LF209", Codes(Single("Which method is safe?", "GET", "POST", "PUT")));
        var sample = ExamSourceTests.Sample();
        var matching = sample.Questions.Single(q => q.Kind == QuestionKind.Matching);
        var usedTokens = matching.Grading.Matches!.Values.ToHashSet();
        Assert.Contains("LF209", Codes(matching with { Options = matching.Options.Where(o => usedTokens.Contains(o.Id)).ToArray() }));
        var dropdown = sample.Questions.Single(q => q.Id == "security-password-storage");
        var key = dropdown.Grading.Matches!["b1"];
        var twoChoices = dropdown with { Slots = [dropdown.Slots[0] with { Options = dropdown.Slots[0].Options.Where(o => o.Id == key).Append(new("other", "an unsalted fast hash")).ToArray() }, dropdown.Slots[1]] };
        Assert.Contains(ContentLinter.Lint(With(twoChoices)), d => d.Code == "LF209" && d.Path == dropdown.Id + ".b1");
        Assert.Empty(Codes(sample.Questions.Single(q => q.Kind == QuestionKind.Sequence)));
    }

    [Fact]
    public void Predictable_option_ids_are_flagged()
    {
        var revealing = Single("Which method is safe?", "GET", "POST", "PUT", "DELETE") with { Options = [new("correct", "GET"), new("b", "POST"), new("c", "PUT"), new("d", "DELETE")], Grading = new(ScoringPolicy.Exact, ["correct"]) };
        Assert.Contains("LF210", Codes(revealing));
        var steps = new Question("order", "order", QuestionKind.Sequence, "Arrange the request steps.", ["http"],
            [new("s3", "Send the request"), new("s1", "Resolve the name"), new("s2", "Open a connection")], [], 0, false,
            new(ScoringPolicy.Partial, ["s1", "s2", "s3"]), "Name resolution comes first, then the connection, then the request itself.");
        Assert.Contains("LF210", Codes(steps));
        Assert.DoesNotContain("LF210", Codes(steps with { Grading = new(ScoringPolicy.Partial, ["s2", "s1", "s3"]) }));
        Assert.Contains(ContentLinter.Lint(CoreTests.Demo()), d => d.Code == "LF210" && d.Path == "questions");
    }

    [Fact]
    public void Unemphasized_negatives_and_thin_explanations_are_flagged()
    {
        Assert.Contains("LF211", Codes(Single("Which of these is not a safe method?", "POST", "GET", "HEAD", "OPTIONS")));
        Assert.DoesNotContain("LF211", Codes(Single("Which of these is NOT a safe method?", "POST", "GET", "HEAD", "OPTIONS")));
        Assert.DoesNotContain("LF211", Codes(Single("The server does not cache. Which header would enable it?", "Cache-Control", "Accept", "Host", "Origin")));
        Assert.Contains("LF212", Codes(Single("Which method is safe?", "GET", "POST", "PUT", "DELETE") with { Explanation = "GET is safe." }));
    }

    [Fact]
    public void Copies_in_different_families_are_flagged()
    {
        var q = Single("Which method is safe?", "GET", "POST", "PUT", "DELETE");
        Assert.Contains(ContentLinter.Lint(With(q, q with { Id = "copy", FamilyId = "copy" })), d => d.Code == "LF213" && d.Path == "copy");
        Assert.DoesNotContain(ContentLinter.Lint(With(q, q with { Id = "variant" })), d => d.Code == "LF213");
        Assert.DoesNotContain(ContentLinter.Lint(With(q, q with { Id = "other", FamilyId = "other", Options = Options("HEAD", "POST", "PUT", "DELETE") })), d => d.Code == "LF213");
    }

    [Fact]
    public void Bank_shape_rules_flag_format_domain_readiness_and_case_study_problems()
    {
        var singles = Enumerable.Range(0, 24).Select(i => Single("Which method fits case " + i + "?", "GET", "POST", "PUT", "DELETE") with { Id = "q" + i, FamilyId = "q" + i }).ToArray();
        var lint = ContentLinter.Lint(With(singles));
        Assert.Contains(lint, d => d.Code == "LF220");
        Assert.Contains(lint, d => d.Code == "LF221" && d.Path == "security");

        var sample = ExamSourceTests.Sample();
        Assert.Contains(ContentLinter.Lint(sample with { Readiness = new() }), d => d.Code == "LF222" && d.Path == "readiness");
        var lonely = sample with { Questions = sample.Questions.Where(q => q.ScenarioId is null || q.Id == "harbor-first-fix").ToArray() };
        Assert.Contains(ContentLinter.Lint(lonely), d => d.Code == "LF223" && d.Path == "harbor-shop");
    }

    [Fact]
    public void Lint_messages_never_contain_key_text()
    {
        const string key = "Serve every page through a regional CDN with long cache lifetimes";
        var giveaway = Single("Which change most improves load time for distant visitors?", key, "Always add RAM", "Never cache", "Use FTP") with { Explanation = "Short." };
        var warnings = ContentLinter.Lint(With(giveaway));
        Assert.NotEmpty(warnings);
        Assert.All(warnings, w => Assert.DoesNotContain(key, w.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Prompt_example_matches_the_shipped_sample()
    {
        var prompt = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", "exam-question-generator.md"));
        var example = Regex.Match(prompt, "```json\\n(.*?)\\n```", RegexOptions.Singleline).Groups[1].Value;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ExamSourceTests.SampleSource()), JsonNode.Parse(example)));
    }

    [Fact]
    public void Prompt_starts_with_an_adaptive_interview_and_one_checkpoint()
    {
        var prompt = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", "exam-question-generator.md"));
        Assert.Contains("1. INTERVIEW ME", prompt);
        Assert.Contains("Ask one question per turn.", prompt);
        Assert.Contains("This is the only approval checkpoint", prompt);
        Assert.True(prompt.IndexOf("1. INTERVIEW ME", StringComparison.Ordinal) < prompt.IndexOf("6. WRITE IN BATCHES", StringComparison.Ordinal));
        foreach (var code in new[] { "LF201", "LF203", "LF204", "LF205", "LF206", "LF207", "LF209", "LF211", "LF212" }) Assert.Contains(code, prompt);
    }
}
