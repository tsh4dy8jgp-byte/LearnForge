using LearnForge.Core;
using Xunit;
using static LearnForge.Core.MasteryState;

namespace LearnForge.Tests;

public class NextStepPlannerTests
{
    private static readonly Pack Demo = CoreTests.Demo();
    private static readonly ReadinessResult NotReady = new(false, "", new(AssessmentSize.Short, 5, 0, false, []), new(AssessmentSize.Full, 3, 0, false, []), 90);

    private static ObjectiveMastery M(string id, MasteryState state, bool reviewDue = false) => new(id, state, 0, 0, 0, null, reviewDue);
    private static ObjectiveMastery[] States(MasteryState sets, MasteryState logic, MasteryState ordering) => [M("sets", sets), M("logic", logic), M("ordering", ordering)];

    [Fact] public void A_new_learner_starts_with_the_first_lesson_of_the_root_objective()
    {
        var steps = NextStepPlanner.Plan(Demo, States(NotStarted, NotStarted, NotStarted), new HashSet<string>(), NotReady);
        Assert.Equal(new[] { new NextStep(NextStepKind.ReadLesson, NextStepReason.StartObjective, "sets", "sets-intro") }, steps);
    }

    [Fact] public void After_reading_the_learner_practises_until_proficient()
    {
        var read = new HashSet<string> { "sets-intro" };
        Assert.Equal(new NextStep(NextStepKind.Practise, NextStepReason.NeedsEvidence, "sets"), NextStepPlanner.Plan(Demo, States(Emerging, NotStarted, NotStarted), read, NotReady).Single());
        Assert.Equal(new NextStep(NextStepKind.Practise, NextStepReason.BelowProficient, "sets"), NextStepPlanner.Plan(Demo, States(Developing, NotStarted, NotStarted), read, NotReady).Single());
    }

    [Fact] public void Prerequisites_unlock_later_objectives_in_depth_order()
    {
        var steps = NextStepPlanner.Plan(Demo, States(Proficient, Developing, NotStarted), new HashSet<string> { "sets-intro" }, NotReady);
        Assert.Equal(new[] { new NextStep(NextStepKind.ReadLesson, NextStepReason.ContinueReading, "logic", "logic-intro") }, steps);
    }

    [Fact] public void Review_due_objectives_follow_frontier_work()
    {
        ObjectiveMastery[] mastery = [M("sets", Proficient, reviewDue: true), M("logic", NotStarted), M("ordering", NotStarted)];
        var steps = NextStepPlanner.Plan(Demo, mastery, new HashSet<string> { "sets-intro" }, NotReady);
        Assert.Equal(new[]
        {
            new NextStep(NextStepKind.ReadLesson, NextStepReason.StartObjective, "logic", "logic-intro"),
            new NextStep(NextStepKind.Review, NextStepReason.ReviewDue, "sets")
        }, steps);
    }

    [Fact] public void A_mock_is_suggested_only_for_readiness_goals_once_everything_is_proficient()
    {
        var all = States(Proficient, Proficient, Proficient);
        var lessons = Demo.Lessons.Select(l => l.Id).ToHashSet();
        Assert.Equal(new[] { new NextStep(NextStepKind.TakeMock, NextStepReason.ReadyForMock, BlueprintId: "short") }, NextStepPlanner.Plan(Demo, all, lessons, NotReady));
        Assert.Empty(NextStepPlanner.Plan(Demo, all, lessons, NotReady with { Ready = true }));
        Assert.Empty(NextStepPlanner.Plan(Demo with { Goal = CourseGoal.Mastery }, all, lessons, NotReady));
        Assert.Empty(NextStepPlanner.Plan(Demo, all, lessons, readiness: null));
    }

    [Fact] public void Output_is_limited_and_deterministic()
    {
        var flat = Demo with { Objectives = Demo.Objectives.Select(o => o with { Prerequisites = [] }).ToArray() };
        var first = NextStepPlanner.Plan(flat, States(NotStarted, NotStarted, NotStarted), new HashSet<string>(), NotReady, limit: 2);
        Assert.Equal(new[] { "sets", "logic" }, first.Select(s => s.ObjectiveId));
        Assert.Equal(first, NextStepPlanner.Plan(flat, States(NotStarted, NotStarted, NotStarted), new HashSet<string>(), NotReady, limit: 2));
    }

    [Fact] public void Objectives_practised_only_in_case_studies_point_to_a_blueprint()
    {
        var lab = TestApi.LoadPack("evidence-lab");
        var steps = NextStepPlanner.Plan(lab, [M("evaluate", Emerging)], new HashSet<string> { "evidence-first" }, NotReady);
        Assert.Equal(new[] { new NextStep(NextStepKind.Practise, NextStepReason.NeedsEvidence, "evaluate", BlueprintId: "short") }, steps);
        var review = NextStepPlanner.Plan(lab, [M("evaluate", Proficient, reviewDue: true)], new HashSet<string> { "evidence-first" }, NotReady with { Ready = true });
        Assert.Equal(new[] { new NextStep(NextStepKind.Review, NextStepReason.ReviewDue, "evaluate", BlueprintId: "short") }, review);
    }

    [Fact] public void Completion_goals_suggest_unread_lessons_in_pack_order_regardless_of_mastery()
    {
        var steps = NextStepPlanner.Plan(Demo with { Goal = CourseGoal.Completion }, States(NotStarted, NotStarted, NotStarted), new HashSet<string> { "sets-intro" }, readiness: null);
        Assert.Equal(new[]
        {
            new NextStep(NextStepKind.ReadLesson, NextStepReason.StartObjective, "logic", "logic-intro"),
            new NextStep(NextStepKind.ReadLesson, NextStepReason.StartObjective, "ordering", "ordering-intro"),
            new NextStep(NextStepKind.Practise, NextStepReason.NeedsEvidence, "sets")
        }, steps);
    }
}
