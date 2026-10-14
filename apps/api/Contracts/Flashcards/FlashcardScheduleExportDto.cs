namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardScheduleExportDto(string PackId, string UnitId, string ContentHash, int Stage, DateTime DueAt,
    bool Introduced, bool Suspended, int SchedulerVersion);
