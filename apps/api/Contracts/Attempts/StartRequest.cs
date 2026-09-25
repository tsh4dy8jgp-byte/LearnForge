using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Attempts;

public sealed record StartRequest(
    [property: Required, MaxLength(100)] string PackId,
    [property: Required, MaxLength(100)] string BlueprintId,
    [property: EnumDataType(typeof(AssessmentMode))] AssessmentMode Mode,
    [property: Required, MaxLength(64)] string RequestId,
    [property: EnumDataType(typeof(PracticeFocus))] PracticeFocus? Focus = null,
    [property: MaxLength(100)] string? ObjectiveId = null);
