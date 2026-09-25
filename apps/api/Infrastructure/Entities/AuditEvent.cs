namespace LearnForge.Api;

public sealed class AuditEvent
{
    public long Id { get; set; }
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
