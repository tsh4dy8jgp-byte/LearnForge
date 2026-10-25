namespace LearnForge.Api.Contracts.Writing;

public sealed record WritingSummaryDto(string PackId, string Version, int Due, string? ActiveSessionId,
    WritingCharacterProgressDto[] Characters);
