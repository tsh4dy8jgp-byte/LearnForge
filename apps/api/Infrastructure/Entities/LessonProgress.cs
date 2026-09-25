namespace LearnForge.Api;

// Keyed by stable pack and lesson IDs so progress survives new releases.
public sealed class LessonProgress
{
    public string UserId { get; set; } = "";
    public string PackId { get; set; } = "";
    public string LessonId { get; set; } = "";
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public string? ContentHash { get; set; }
}
