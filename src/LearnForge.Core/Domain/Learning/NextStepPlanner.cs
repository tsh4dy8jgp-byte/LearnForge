namespace LearnForge.Core;

public static class NextStepPlanner
{
    // Suggests work on the "frontier": objectives that are not yet proficient but whose prerequisites are.
    public static NextStep[] Plan(Pack pack, IReadOnlyCollection<ObjectiveMastery> mastery, IReadOnlySet<string> completedLessons,
        ReadinessResult? readiness, int limit = 3)
    {
        var states = mastery.ToDictionary(m => m.ObjectiveId);
        bool Proficient(string id) => states.TryGetValue(id, out var m) && m.State == MasteryState.Proficient;
        var byId = pack.Objectives.ToDictionary(o => o.Id);
        var depths = new Dictionary<string, int>();
        // Prerequisites are validated to be acyclic, so this recursion terminates.
        int Depth(string id) => depths.TryGetValue(id, out var d) ? d
            : depths[id] = byId[id].Prerequisites.Length == 0 ? 0 : byId[id].Prerequisites.Max(Depth) + 1;

        var steps = new List<NextStep>();
        var frontier = pack.Objectives.Select((objective, index) => (Objective: objective, Index: index))
            .Where(x => !Proficient(x.Objective.Id) && x.Objective.Prerequisites.All(Proficient))
            .OrderBy(x => Depth(x.Objective.Id)).ThenBy(x => x.Index);
        foreach (var (objective, _) in frontier)
        {
            var state = states.TryGetValue(objective.Id, out var m) ? m.State : MasteryState.NotStarted;
            var lesson = pack.Lessons.FirstOrDefault(l => l.ObjectiveIds.Contains(objective.Id) && !completedLessons.Contains(l.Id));
            steps.Add(lesson is not null
                ? new(NextStepKind.ReadLesson, state == MasteryState.NotStarted ? NextStepReason.StartObjective : NextStepReason.ContinueReading, objective.Id, lesson.Id)
                : new(NextStepKind.Practise, state == MasteryState.Developing ? NextStepReason.BelowProficient : NextStepReason.NeedsEvidence, objective.Id));
        }
        steps.AddRange(pack.Objectives.Where(o => states.TryGetValue(o.Id, out var m) && m.ReviewDue)
            .Select(o => new NextStep(NextStepKind.Review, NextStepReason.ReviewDue, o.Id)));
        var mock = pack.Blueprints.FirstOrDefault(b => b.Size == AssessmentSize.Short) ?? pack.Blueprints.FirstOrDefault();
        if (pack.Goal == CourseGoal.Readiness && readiness is { Ready: false } && mock is not null && pack.Objectives.All(o => Proficient(o.Id)))
            steps.Add(new(NextStepKind.TakeMock, NextStepReason.ReadyForMock, BlueprintId: mock.Id));
        return steps.Take(limit).ToArray();
    }
}
