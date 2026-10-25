namespace LearnForge.Api;

public sealed class WritingSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string UserId { get; set; }
    public required string PackId { get; set; }
    public required string ReleaseId { get; set; }
    public required string StartKey { get; set; }
    public string? ActiveKey { get; set; }
    public required string CharacterJson { get; set; }
    public WritingMode Mode { get; set; }
    public int Revision { get; set; }
    public int Mistakes { get; set; }
    public int Hints { get; set; }
    public bool Completed { get; set; }
    public string? ResultKey { get; set; }
    public string? ResultHash { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
