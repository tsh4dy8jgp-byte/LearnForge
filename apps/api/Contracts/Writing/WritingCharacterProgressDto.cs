namespace LearnForge.Api.Contracts.Writing;

public sealed record WritingCharacterProgressDto(string CharacterId, int AssistedCompletions,
    int IndependentCompletions, int SequenceCompletions, int Stage, DateTime? DueAt);
