namespace LearnForge.Api.Contracts.Content;

public sealed record CourseCatalogDto(string Id, string Title, string Description, string Version, string License,
    Objective[] Objectives, Lesson[] Lessons, Blueprint[] Blueprints, SourceReference[] Sources,
    ReadinessPolicy Readiness, string[] CompletedLessons, int QuestionCount);
