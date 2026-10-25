namespace LearnForge.Api;

public sealed class WritingState
{
    public required string UserId { get; set; }
    public required string PackId { get; set; }
    public required string CharacterId { get; set; }
    public required string ContentHash { get; set; }
    public int Stage { get; set; } = -1;
    public DateTime? DueAt { get; set; }
    public int AssistedCompletions { get; set; }
    public int IndependentCompletions { get; set; }
    public int SequenceCompletions { get; set; }
    public int Revision { get; set; }
}
