using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Writing;

public sealed record WritingStartRequest([property: Required, MaxLength(100)] string CharacterId,
    WritingMode Mode, [property: Required, MaxLength(100)] string RequestId,
    [property: Required, MaxLength(100)] string Version);
