namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardSessionExportDto(string Id, string PackId, string ReleaseId, string[] UnitIds, int Position,
    DateTime StartedAt, DateTime? CompletedAt);
