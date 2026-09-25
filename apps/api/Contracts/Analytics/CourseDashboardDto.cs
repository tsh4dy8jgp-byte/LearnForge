namespace LearnForge.Api.Contracts.Analytics;

public sealed record CourseDashboardDto(
    string Id,
    string Title,
    string Version,
    int LessonCount,
    int CompletedLessons,
    ObjectiveDashboardDto[] Objectives,
    RecommendationDto[] Recommendations,
    ReadinessResult Readiness);
