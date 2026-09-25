namespace LearnForge.Api.Contracts.Content;

public sealed record CatalogSummaryDto(string Id, string Title, string Description, string Version,
    int LessonCount, int QuestionCount, int ObjectiveCount);
