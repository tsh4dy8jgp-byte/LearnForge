namespace LearnForge.Core;

public sealed record FlashcardUnit(string Id, string DeckId, string NoteId, string Front, string Back,
    string Explanation, string[] ObjectiveIds, string[] Tags, string Language, SourceReference[] Sources, string ContentHash);
