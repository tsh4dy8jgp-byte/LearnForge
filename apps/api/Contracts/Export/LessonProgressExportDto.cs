namespace LearnForge.Api.Contracts.Export;

public sealed record LessonProgressExportDto(string PackId, string LessonId, DateTime CompletedAt, string? ContentHash);
