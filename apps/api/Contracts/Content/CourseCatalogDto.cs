namespace LearnForge.Api.Contracts.Content;

// Public course data only; personal progress lives under /api/me/courses/{id}.
public sealed record CourseCatalogDto(string Id, string Title, string Description, string Version, string License,
    Objective[] Objectives, Lesson[] Lessons, Blueprint[] Blueprints, SourceReference[] Sources,
    ReadinessPolicy Readiness, CourseGoal Goal, int QuestionCount);
