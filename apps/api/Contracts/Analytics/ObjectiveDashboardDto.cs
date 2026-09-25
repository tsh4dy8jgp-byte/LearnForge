namespace LearnForge.Api.Contracts.Analytics;

public sealed record ObjectiveDashboardDto(
    string Id,
    string Title,
    string[] Prerequisites,
    int Samples,
    int IndependentSamples,
    decimal? CorrectPercent,
    string[] LessonIds);
