namespace LearnForge.Api.Contracts.Learning;

public sealed record CourseGoalStatusDto(
    CourseGoal Goal,
    bool Met,
    int ProficientObjectives,
    int ObjectiveCount,
    int CompletedLessons,
    int LessonCount,
    ReadinessResult? Readiness);
