using System.ComponentModel.DataAnnotations;
namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardStartRequest([Required, StringLength(100, MinimumLength = 1)] string RequestId,
    string? Deck = null, string? Objective = null, string? Tag = null,
    [Range(1, 20)] int? Limit = null);
