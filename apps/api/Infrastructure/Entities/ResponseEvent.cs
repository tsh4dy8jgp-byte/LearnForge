namespace LearnForge.Api;

public sealed class ResponseEvent
{
    public long Id { get; set; }
    public string AttemptId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
