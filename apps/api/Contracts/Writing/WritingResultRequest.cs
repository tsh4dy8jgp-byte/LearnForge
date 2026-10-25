using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Writing;

public sealed record WritingResultRequest([property: Required, MaxLength(100)] string RequestId,
    [property: Range(0, int.MaxValue)] int Revision, bool Completed,
    [property: Range(0, 10000)] int Mistakes, [property: Range(0, 10000)] int Hints);
