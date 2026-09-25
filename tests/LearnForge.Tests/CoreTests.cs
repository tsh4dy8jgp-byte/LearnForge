using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class CoreTests
{
    public static Pack Demo() => ContentEngine.Compile(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "packs", "reasoning-foundations.json"))).Pack!;
    [Fact] public void Demo_compiles_and_every_blueprint_is_satisfiable()
    {
        var pack = Demo(); Assert.Empty(ContentEngine.Validate(pack));
        foreach (var b in pack.Blueprints)
        {
            var selection = ExamComposer.Compose(pack, b, [], "test");
            Assert.Equal(b.Count, selection.Length);
            Assert.All(b.RequiredKinds, kind => Assert.Contains(selection, q => q.Kind == kind));
            Assert.All(b.ObjectiveIds, id => Assert.Contains(selection, q => q.ObjectiveIds.Contains(id)));
        }
    }
    [Theory]
    [InlineData("single")][InlineData("multiple")][InlineData("matching")][InlineData("dropdown")][InlineData("sequence")]
    public void Every_kind_grades_correct_and_empty_responses(string kind)
    {
        var parsedKind = Enum.Parse<QuestionKind>(kind, ignoreCase: true);
        var q = Demo().Questions.First(q => q.Kind == parsedKind);
        var perfect = Grader.Score(q, new(q.Grading.Correct, q.Grading.Matches ?? []));
        Assert.True(perfect.FullyCorrect); Assert.Equal(q.Weight, perfect.Earned);
        Assert.Equal(0, Grader.Score(q, null).Earned);
    }
    [Fact] public void Partial_credit_is_normalized_and_excess_selections_are_rejected()
    {
        var q = Demo().Questions.First(q => q.Kind == QuestionKind.Multiple);
        Assert.Equal(.5m, Grader.Score(q, new([q.Grading.Correct[0]], [])).Earned);
        Assert.Throws<ArgumentException>(() => Grader.Score(q, new(q.Options.Select(o => o.Id).ToArray(), [])));
        Assert.Throws<ArgumentException>(() => Grader.Score(q, new([q.Grading.Correct[0],q.Grading.Correct[0]], [])));
    }
    [Fact] public void Ordering_is_positional_and_unknown_slots_are_rejected()
    {
        var sequence = Demo().Questions.First(q => q.Kind == QuestionKind.Sequence);
        Assert.False(Grader.Score(sequence, new(sequence.Grading.Correct.Reverse().ToArray(), [])).FullyCorrect);
        var dropdown = Demo().Questions.First(q => q.Kind == QuestionKind.Dropdown);
        Assert.NotNull(Grader.Validate(dropdown, new([], new() { ["unknown"] = "true" })));
    }
    [Fact] public void Compiler_catches_cycles_missing_values_and_bad_keys()
    {
        var p = Demo();
        var broken = p with { Objectives = p.Objectives.Select(o => o with { Prerequisites = [o.Id] }).ToArray() };
        Assert.Contains(ContentEngine.Validate(broken), e => e.Message.Contains("cycle"));
        Assert.False(ContentEngine.Compile("{\"templates\":{\"q\":\"{{missing}}\"},\"pack\":{\"$use\":\"q\"}}").Success);
        Assert.False(ContentEngine.Compile("{\"templates\":{\"q\":{\"$use\":\"q\"}},\"pack\":{\"$use\":\"q\"}}").Success);
        var question = p.Questions[0] with { Grading = new(ScoringPolicy.Exact, ["missing"]) };
        Assert.NotEmpty(ContentEngine.Validate(p with { Questions = [question] }));
    }
    [Fact] public void Public_delivery_omits_private_grading()
    {
        var text = Json.Write(Demo().Questions.Select(DeliveryQuestion.From));
        Assert.DoesNotContain("grading", text); Assert.DoesNotContain("correct", text); Assert.DoesNotContain("explanation", text);
    }
    [Fact] public void Readiness_uses_last_attempts_strict_threshold_and_either_length()
    {
        var now = DateTime.UtcNow; var policy = new ReadinessPolicy();
        var shorts = Enumerable.Range(0,5).Select(i => new ReadinessEvidence(i.ToString(), AssessmentSize.Short, now.AddDays(-i), 95, true)).ToArray();
        Assert.True(ReadinessEvaluator.Evaluate(shorts, policy, now).Ready);
        Assert.False(ReadinessEvaluator.Evaluate(shorts.Skip(1), policy, now).Ready);
        Assert.False(ReadinessEvaluator.Evaluate(shorts.Select(e => e with { CorrectPercent = 90 }), policy, now).Ready);
        Assert.False(ReadinessEvaluator.Evaluate(shorts.Prepend(new("failed", AssessmentSize.Short, now.AddMinutes(1), 50, true)), policy, now).Ready);
        Assert.False(ReadinessEvaluator.Evaluate(shorts.Prepend(new("assisted", AssessmentSize.Short, now.AddMinutes(1), 100, false)), policy, now).Ready);
        Assert.False(ReadinessEvaluator.Evaluate(shorts, policy, now.AddDays(100)).Ready);
        var fulls = shorts.Take(3).Select(e => e with { Size = AssessmentSize.Full });
        Assert.True(ReadinessEvaluator.Evaluate(fulls, policy, now).Ready);
    }
    [Fact] public void Composer_preserves_scenarios_and_rejects_impossible_blueprints()
    {
        var p = Demo();
        var pair = p.Questions.Take(2).Select(q => q with { ScenarioId = "case" }).ToArray();
        p = p with { Questions = pair, Scenarios = [new("case", "Case", "Shared background")] };
        Assert.Throws<InvalidOperationException>(() => ExamComposer.Compose(p, new("bad", "Bad", 1, 1, AssessmentSize.Short, ["sets"], []), [], "seed"));
        Assert.Equal(2, ExamComposer.Compose(p, new("ok", "OK", 2, 1, AssessmentSize.Short, ["sets"], []), [], "seed").Length);
    }

    [Fact] public void Scenario_groups_cannot_be_split_by_objective_filtering()
    {
        var p = Demo(); var questions = p.Questions.Take(2).Select((q, i) => q with { ScenarioId = "case", ObjectiveIds = [i == 0 ? "sets" : "logic"] }).ToArray();
        p = p with { Questions = questions, Scenarios = [new("case", "Case", "Shared background")] };
        Assert.Throws<InvalidOperationException>(() => ExamComposer.Compose(p, new("bad", "Bad", 1, 1, AssessmentSize.Short, ["sets"], []), [], "seed"));
    }

    [Fact] public void Every_bundled_pack_compiles_and_malformed_contracts_fail_closed()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "packs"), "*.json"))
            Assert.True(ContentEngine.Compile(File.ReadAllText(file)).Success, file);
        Assert.False(ContentEngine.Compile("null").Success);
        Assert.False(ContentEngine.Compile("{\"id\":\"incomplete\"}").Success);
        var source = Json.Write(Demo()).Replace("\"schemaVersion\": 1", "\"unknownProperty\": true, \"schemaVersion\": 1");
        Assert.False(ContentEngine.Compile(source).Success);
        var large = new string('x', 100_000);
        var amplified = System.Text.Json.JsonSerializer.Serialize(new { templates = new { repeated = string.Concat(Enumerable.Repeat("{{value}}", 50)) }, pack = new Dictionary<string, object> { ["$use"] = "repeated", ["values"] = new { value = large } } });
        Assert.Contains(ContentEngine.Compile(amplified).Diagnostics, d => d.Message.Contains("budget"));
    }
}
