namespace LearnForge.Api.Contracts.Writing;

public sealed record WritingSessionDto(string Id, string PackId, string Version, WritingCharacter Character,
    WritingMode Mode, int Revision, DateTime StartedAt, DateTime? FinishedAt, bool Completed, int Mistakes, int Hints);
