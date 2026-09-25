namespace LearnForge.Api.Contracts.Content;

public sealed record ValidationResponseDto(bool Success, string Hash, Diagnostic[] Diagnostics,
    int? QuestionCount, int? LessonCount);
