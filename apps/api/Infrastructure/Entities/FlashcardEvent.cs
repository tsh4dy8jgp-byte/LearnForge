namespace LearnForge.Api;

public sealed class FlashcardEvent
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public required string SessionId { get; set; }
    public required string RequestId { get; set; }
    public required string Fingerprint { get; set; }
    public FlashcardOperation Operation { get; set; }
    public required string UnitId { get; set; }
    public ReviewRating? Rating { get; set; }
    public DateTime At { get; set; }
    public required string ViewJson { get; set; }
}
