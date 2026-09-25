namespace LearnForge.Api.Contracts.Analytics;

public sealed record DashboardDto(
    CourseDashboardDto[] Courses,
    AttemptSummaryDto[] Attempts,
    int CompletedAttempts,
    int ActiveAttempts,
    int CompletedLessons);
