namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardStatusDto(string UnitId, bool Introduced, bool Suspended, DateTime? DueAt, int Revision);
