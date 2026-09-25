using LearnForge.Core;

namespace LearnForge.Api;

public sealed class Attempt
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "";
    public string PackReleaseId { get; set; } = "";
    public string PackId { get; set; } = "";
    public string StartKey { get; set; } = "";
    public string? ActiveKey { get; set; }
    public AssessmentMode Mode { get; set; } = AssessmentMode.Mock;
    public AssessmentSize Size { get; set; } = AssessmentSize.Short;
    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;
    public string SnapshotJson { get; set; } = "";
    public string AnswersJson { get; set; } = "{}";
    public string FeedbackJson { get; set; } = "{}";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime Deadline { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Revision { get; set; }
    public int SectionIndex { get; set; }
    public decimal FreshPercent { get; set; }
    public decimal Earned { get; set; }
    public decimal Possible { get; set; }
    public decimal CorrectPercent { get; set; }
    public bool Eligible { get; set; }
    public bool TimedOut { get; set; }
    public PracticeFocus? Focus { get; set; }
}
