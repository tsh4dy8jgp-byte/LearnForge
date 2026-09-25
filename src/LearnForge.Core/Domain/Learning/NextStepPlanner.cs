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

        // Objectives whose questions all sit in case studies cannot be practised alone; point at a session that includes them.
        string? SessionBlueprint(string objectiveId) =>
            pack.Questions.Any(q => q.ScenarioId is null && q.ObjectiveIds.Contains(objectiveId)) ? null
                : pack.Blueprints.Where(b => b.ObjectiveIds.Contains(objectiveId)).OrderBy(b => b.Size == AssessmentSize.Short ? 0 : 1).FirstOrDefault()?.Id;
        NextStepReason ReadingReason(string objectiveId) =>
            states.TryGetValue(objectiveId, out var m) && m.State != MasteryState.NotStarted ? NextStepReason.ContinueReading : NextStepReason.StartObjective;

        var steps = new List<NextStep>();
        // A completion goal is met by reading, so unread lessons come first in pack order, whatever the mastery.
        if (pack.Goal == CourseGoal.Completion)
            steps.AddRange(pack.Lessons.Where(l => !completedLessons.Contains(l.Id))
                .Select(l => new NextStep(NextStepKind.ReadLesson, ReadingReason(l.ObjectiveIds[0]), l.ObjectiveIds[0], l.Id)));
        var frontier = pack.Objectives.Select((objective, index) => (Objective: objective, Index: index))
            .Where(x => !Proficient(x.Objective.Id) && x.Objective.Prerequisites.All(Proficient))
            .OrderBy(x => Depth(x.Objective.Id)).ThenBy(x => x.Index);
        foreach (var (objective, _) in frontier)
        {
            var state = states.TryGetValue(objective.Id, out var m) ? m.State : MasteryState.NotStarted;
            var lesson = pack.Lessons.FirstOrDefault(l => l.ObjectiveIds.Contains(objective.Id) && !completedLessons.Contains(l.Id));
            if (lesson is not null)
            {
                if (!steps.Any(s => s.LessonId == lesson.Id)) steps.Add(new(NextStepKind.ReadLesson, ReadingReason(objective.Id), objective.Id, lesson.Id));
            }
            else steps.Add(new(NextStepKind.Practise, state == MasteryState.Developing ? NextStepReason.BelowProficient : NextStepReason.NeedsEvidence,
                objective.Id, BlueprintId: SessionBlueprint(objective.Id)));
        }
        steps.AddRange(pack.Objectives.Where(o => states.TryGetValue(o.Id, out var m) && m.ReviewDue)
            .Select(o => new NextStep(NextStepKind.Review, NextStepReason.ReviewDue, o.Id, BlueprintId: SessionBlueprint(o.Id))));
        var mock = pack.Blueprints.FirstOrDefault(b => b.Size == AssessmentSize.Short) ?? pack.Blueprints.FirstOrDefault();
        if (pack.Goal == CourseGoal.Readiness && readiness is { Ready: false } && mock is not null && pack.Objectives.All(o => Proficient(o.Id)))
            steps.Add(new(NextStepKind.TakeMock, NextStepReason.ReadyForMock, BlueprintId: mock.Id));
        return steps.Take(limit).ToArray();
    }
}
