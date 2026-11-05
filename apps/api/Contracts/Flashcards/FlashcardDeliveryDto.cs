namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardDeliveryDto(string Id, string DeckId, string NoteId, string Front, string Language,
    string? Back, string? Explanation, SourceReference[] Sources);
