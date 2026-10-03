namespace LearnForge.Api;

public sealed class AuthoringDraft
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OwnerId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Source { get; set; } = "";
    public int Revision { get; set; }
    public DateTime UpdatedAt { get; set; }
}
