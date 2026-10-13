namespace LearnForge.Core;

public sealed record FlashcardNote(string Id, FlashcardKind Kind, string[] ObjectiveIds,
    string Explanation, string? Front = null, string? Back = null, string? Text = null,
    string[]? Tags = null, string Language = "en", SourceReference[]? Sources = null);
