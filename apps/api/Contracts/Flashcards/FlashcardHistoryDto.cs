namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardHistoryDto(FlashcardState[] Schedules, FlashcardSession[] Sessions, FlashcardEvent[] Events);
