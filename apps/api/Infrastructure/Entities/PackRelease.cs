namespace LearnForge.Api;

public sealed class PackRelease
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PackId { get; set; } = "";
    public string Version { get; set; } = "";
    public string ContentJson { get; set; } = "";
    public string Hash { get; set; } = "";
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}
