namespace LearnForge.Core;

// Readiness, goal and mastery are optional in source JSON; packs that omit them keep the exam-readiness goal.
public sealed record Pack(int SchemaVersion, string Id, string Version, string Title, string Description,
    string License, Objective[] Objectives, Lesson[] Lessons, Question[] Questions, Scenario[] Scenarios,
    Blueprint[] Blueprints, SourceReference[] Sources, ReadinessPolicy? Readiness = null,
    CourseGoal Goal = CourseGoal.Readiness, MasteryPolicy? Mastery = null,
    ProductProfile Profile = ProductProfile.Hybrid, PackCapabilities? Capabilities = null,
    FlashcardDeck[]? FlashcardDecks = null,
    // Opt-in: keeps fixed-paper questions out of composed and focused practice until the learner has seen them.
    // Omitted from JSON when false, so earlier releases and attempt snapshots serialize exactly as before.
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool ReserveFixedPapers = false)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public FlashcardDeck[] Decks => FlashcardDecks ?? [];
    // Older immutable releases have no profile/capabilities and retain all existing features.
    [System.Text.Json.Serialization.JsonIgnore]
    public PackCapabilities Features => Capabilities ?? (PackCapabilities.For(Profile) with { Flashcards = Decks.Length > 0 });
}
