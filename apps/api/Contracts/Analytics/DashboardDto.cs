namespace LearnForge.Api.Contracts.Analytics;

public sealed record DashboardDto(
    CourseProgressDto[] Courses,
    AttemptSummaryDto[] RecentAttempts,
    int CompletedAttempts,
    int ActiveAttempts,
    int CompletedLessons);
