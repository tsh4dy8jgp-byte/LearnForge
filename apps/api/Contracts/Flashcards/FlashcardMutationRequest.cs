using System.ComponentModel.DataAnnotations;
namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardMutationRequest([Required, StringLength(100, MinimumLength = 1)] string RequestId,
    [Range(0, int.MaxValue)] int Revision, ReviewRating? Rating = null);
