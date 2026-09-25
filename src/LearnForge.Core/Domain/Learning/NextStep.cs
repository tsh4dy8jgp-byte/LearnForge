namespace LearnForge.Core;

public sealed record NextStep(NextStepKind Kind, NextStepReason Reason, string? ObjectiveId = null, string? LessonId = null, string? BlueprintId = null);
