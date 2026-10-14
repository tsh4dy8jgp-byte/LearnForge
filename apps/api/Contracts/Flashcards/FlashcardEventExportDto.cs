namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardEventExportDto(string SessionId, FlashcardOperation Operation, string UnitId, ReviewRating? Rating, DateTime At);
