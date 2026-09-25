namespace LearnForge.Api;

public sealed class Completion
{
    public string UserId { get; set; } = "";
    public string ReleaseId { get; set; } = "";
    public string LessonId { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
