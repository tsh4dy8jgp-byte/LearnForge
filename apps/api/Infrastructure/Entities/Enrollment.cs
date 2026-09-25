using LearnForge.Core;

namespace LearnForge.Api;

public sealed class Enrollment
{
    public string UserId { get; set; } = "";
    public string PackId { get; set; } = "";
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}
