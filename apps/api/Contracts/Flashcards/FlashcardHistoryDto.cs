namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardHistoryDto(FlashcardScheduleExportDto[] Schedules, FlashcardSessionExportDto[] Sessions, FlashcardEventExportDto[] Events);
