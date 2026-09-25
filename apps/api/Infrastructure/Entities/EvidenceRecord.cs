using LearnForge.Core;

namespace LearnForge.Api;

// Append-only: one row per question per attempt, written when its feedback is released.
public sealed class EvidenceRecord
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public string PackId { get; set; } = "";
    public string ReleaseId { get; set; } = "";
    public string AttemptId { get; set; } = "";
    public string QuestionId { get; set; } = "";
    public string FamilyId { get; set; } = "";
    public string[] ObjectiveIds { get; set; } = [];
    public EvidenceSource Source { get; set; }
    public bool Answered { get; set; }
    public bool FullyCorrect { get; set; }
    public decimal Earned { get; set; }
    public decimal Possible { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
