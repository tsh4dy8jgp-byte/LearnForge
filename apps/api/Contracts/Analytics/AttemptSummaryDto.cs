namespace LearnForge.Api.Contracts.Analytics;

public sealed record AttemptSummaryDto(
    string Id,
    string PackId,
    string Title,
    string Version,
    AssessmentSize Size,
    AssessmentMode Mode,
    AttemptStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DateTime Deadline,
    decimal CorrectPercent,
    bool Eligible,
    bool TimedOut,
    decimal FreshPercent,
    PracticeFocus? Focus,
    decimal Score,
    int ItemCount,
    CourseGoal Goal);
