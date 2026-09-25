using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class MasteryEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Objective[] Objectives = [new("sets", "Sets", []), new("logic", "Logic", ["sets"])];
    private long order;

    private MasteryEvidence E(string family, bool correct, double daysAgo = 1, string objective = "sets", bool independent = false) =>
        new(++order, family, [objective], correct, independent, Now.AddDays(-daysAgo));

    private static ObjectiveMastery Sets(params MasteryEvidence[] evidence) =>
        MasteryEvaluator.Evaluate(Objectives, evidence, new MasteryPolicy(), Now).Single(m => m.ObjectiveId == "sets");

    [Fact] public void No_evidence_is_not_started()
    {
        var m = Sets();
        Assert.Equal(MasteryState.NotStarted, m.State);
        Assert.Equal((0, 0), (m.Correct, m.Considered));
        Assert.Null(m.LastEvidenceAt);
    }

    [Fact] public void Fewer_than_the_minimum_families_is_emerging_even_when_correct() =>
        Assert.Equal(MasteryState.Emerging, Sets(E("a", true), E("b", true)).State);

    [Fact] public void Four_of_the_last_five_families_is_proficient_and_three_is_developing()
    {
        Assert.Equal(MasteryState.Proficient, Sets(E("a", true), E("b", true), E("c", true), E("d", true), E("e", false)).State);
        Assert.Equal(MasteryState.Developing, Sets(E("a", true), E("b", true), E("c", true), E("d", false), E("e", false)).State);
    }

    [Fact] public void Only_the_latest_answer_per_family_counts()
    {
        var m = Sets(E("a", false, daysAgo: 5), E("a", true, daysAgo: 1), E("b", true), E("c", true));
        Assert.Equal((3, 3), (m.Correct, m.Considered));
        Assert.Equal(MasteryState.Proficient, m.State);
    }

    [Fact] public void Only_the_latest_window_of_families_counts()
    {
        var m = Sets(E("old1", false, 30), E("old2", false, 29), E("a", true, 5), E("b", true, 4), E("c", true, 3), E("d", true, 2), E("e", true, 1));
        Assert.Equal((5, 5), (m.Correct, m.Considered));
        Assert.Equal(MasteryState.Proficient, m.State);
    }

    [Fact] public void Ties_on_timestamp_are_broken_by_order()
    {
        var m = Sets(E("a", false, 2), E("a", true, 2), E("b", true), E("c", true));
        Assert.Equal(3, m.Correct);
    }

    [Fact] public void Proficiency_becomes_review_due_after_the_review_interval()
    {
        Assert.True(Sets(E("a", true, 70), E("b", true, 65), E("c", true, 61)).ReviewDue);
        Assert.False(Sets(E("a", true, 10), E("b", true, 9), E("c", true, 8)).ReviewDue);
        Assert.False(Sets(E("a", false, 70), E("b", false, 65), E("c", false, 61)).ReviewDue);
    }

    [Fact] public void Evidence_only_affects_its_own_objectives_and_counts_independent_answers()
    {
        var all = MasteryEvaluator.Evaluate(Objectives, [E("a", true, objective: "logic", independent: true), E("b", true, objective: "logic")], new MasteryPolicy(), Now);
        Assert.Equal(new[] { "sets", "logic" }, all.Select(m => m.ObjectiveId));
        Assert.Equal(MasteryState.NotStarted, all[0].State);
        Assert.Equal((2, 1), (all[1].Considered, all[1].Independent));
    }
}
