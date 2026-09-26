using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class ExamComposerTests
{
    // One objective with a large bank and many objectives with a single question each: a paper must still find the rare ones.
    private static Pack Skewed(int bulk, int rare)
    {
        var demo = CoreTests.Demo();
        var objectives = Enumerable.Range(0, rare + 1).Select(i => new Objective($"o{i}", $"Objective {i}", [])).ToArray();
        Question Item(string id, string objective) => new(id, id, QuestionKind.Single, "Pick a.", [objective],
            [new("a", "A"), new("b", "B")], [], 1, false, new(ScoringPolicy.Exact, ["a"]), "A is right.");
        var questions = Enumerable.Range(0, bulk).Select(i => Item($"bulk-{i}", "o0"))
            .Concat(Enumerable.Range(1, rare).Select(i => Item($"rare-{i}", $"o{i}"))).ToArray();
        var lesson = demo.Lessons[0] with { Id = "all", ObjectiveIds = objectives.Select(o => o.Id).ToArray() };
        return demo with { Objectives = objectives, Lessons = [lesson], Questions = questions, Scenarios = [], Blueprints = [] };
    }

    [Fact]
    public void A_paper_covering_every_objective_is_found_when_most_items_share_one_objective()
    {
        var pack = Skewed(bulk: 150, rare: 20);
        var paper = new Blueprint("paper", "Paper", 25, 30, AssessmentSize.Full, pack.Objectives.Select(o => o.Id).ToArray(), []);

        var selection = ExamComposer.Compose(pack, paper, [], "validation");

        Assert.Equal(25, selection.Length);
        Assert.All(pack.Objectives, o => Assert.Contains(selection, q => q.ObjectiveIds.Contains(o.Id)));
    }

    [Fact]
    public void An_uncoverable_paper_still_fails_explicitly()
    {
        var pack = Skewed(bulk: 30, rare: 20);
        var tooSmall = new Blueprint("paper", "Paper", 10, 30, AssessmentSize.Short, pack.Objectives.Select(o => o.Id).ToArray(), []);

        Assert.Throws<InvalidOperationException>(() => ExamComposer.Compose(pack, tooSmall, [], "validation"));
    }
}
