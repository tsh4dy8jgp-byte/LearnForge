namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardSummaryDto(int Total, int New, int Due, int Suspended, int Introduced,
    bool UpToDate, string? ActiveSessionId, FlashcardStatusDto[] Cards);
