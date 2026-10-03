using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Content;

public sealed record DraftSaveRequest([Required, StringLength(120)] string Title,
    [Required(AllowEmptyStrings = true), StringLength(ContentEngine.MaxSourceBytes)] string Source,
    [Range(0, int.MaxValue - 1)] int Revision);
