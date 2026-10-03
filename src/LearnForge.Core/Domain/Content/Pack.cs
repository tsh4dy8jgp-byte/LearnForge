namespace LearnForge.Core;

// Readiness, goal and mastery are optional in source JSON; packs that omit them keep the exam-readiness goal.
public sealed record Pack(int SchemaVersion, string Id, string Version, string Title, string Description,
    string License, Objective[] Objectives, Lesson[] Lessons, Question[] Questions, Scenario[] Scenarios,
    Blueprint[] Blueprints, SourceReference[] Sources, ReadinessPolicy? Readiness = null,
    CourseGoal Goal = CourseGoal.Readiness, MasteryPolicy? Mastery = null,
    ProductProfile Profile = ProductProfile.Hybrid, PackCapabilities? Capabilities = null)
{
    // Older immutable releases have no profile/capabilities and retain all existing features.
    [System.Text.Json.Serialization.JsonIgnore]
    public PackCapabilities Features => Capabilities ?? PackCapabilities.For(Profile);
}
