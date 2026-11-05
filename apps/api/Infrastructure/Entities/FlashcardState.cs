namespace LearnForge.Api;

public sealed class FlashcardState
{
    public required string UserId { get; set; }
    public required string PackId { get; set; }
    public required string UnitId { get; set; }
    public required string ContentHash { get; set; }
    public int Stage { get; set; } = -1;
    public DateTime DueAt { get; set; }
    public bool Introduced { get; set; }
    public bool Suspended { get; set; }
    public int Revision { get; set; }
    public int SchedulerVersion { get; set; } = 1;
}
