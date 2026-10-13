namespace LearnForge.Api;

public sealed class FlashcardSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string UserId { get; set; }
    public required string PackId { get; set; }
    public required string ReleaseId { get; set; }
    public required string StartKey { get; set; }
    public required string FilterHash { get; set; }
    public string? ActiveKey { get; set; }
    public required string UnitsJson { get; set; }
    public int Position { get; set; }
    public bool Revealed { get; set; }
    public int Revision { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
