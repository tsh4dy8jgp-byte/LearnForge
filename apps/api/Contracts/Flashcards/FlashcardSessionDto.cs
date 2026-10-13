namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardSessionDto(string Id, string PackId, string ReleaseId, int Revision, int Position,
    int Count, bool Revealed, bool Completed, FlashcardDeliveryDto? Card);
