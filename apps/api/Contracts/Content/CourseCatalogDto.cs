namespace LearnForge.Api.Contracts.Content;

// Public course data only; personal progress lives under /api/me/courses/{id}.
public sealed record CourseCatalogDto(string Id, string Title, string Description, string Version, string License,
    Objective[] Objectives, Lesson[] Lessons, Blueprint[] Blueprints, SourceReference[] Sources,
    ReadinessPolicy Readiness, CourseGoal Goal, int QuestionCount, ProductProfile Profile, PackCapabilities Capabilities)
{
    public static CourseCatalogDto From(Pack pack) => new(pack.Id, pack.Title, pack.Description, pack.Version,
        pack.License, pack.Objectives, pack.Lessons, pack.Blueprints, pack.Sources, pack.Readiness ?? new(),
        pack.Goal, pack.Questions.Length, pack.Profile, pack.Features);
}
