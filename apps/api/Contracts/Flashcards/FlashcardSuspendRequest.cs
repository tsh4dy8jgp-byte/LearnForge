using System.ComponentModel.DataAnnotations;
namespace LearnForge.Api.Contracts.Flashcards;

public sealed record FlashcardSuspendRequest([Required, StringLength(250)] string UnitId, bool Suspended,
    [Range(0, int.MaxValue)] int Revision);
