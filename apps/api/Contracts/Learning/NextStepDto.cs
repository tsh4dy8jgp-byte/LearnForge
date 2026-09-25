namespace LearnForge.Api.Contracts.Learning;

// Reasons travel as enums so the client can phrase (and later localize) them.
public sealed record NextStepDto(
    NextStepKind Kind,
    NextStepReason Reason,
    string? ObjectiveId,
    string? ObjectiveTitle,
    string? LessonId,
    string? LessonTitle,
    string? BlueprintId);
