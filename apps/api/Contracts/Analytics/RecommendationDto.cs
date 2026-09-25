namespace LearnForge.Api.Contracts.Analytics;

public sealed record RecommendationDto(string ObjectiveId, string Title, string? LessonId, string Reason);
