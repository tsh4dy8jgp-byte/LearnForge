namespace LearnForge.Core;

public static class FlashcardScheduler
{
    private static readonly int[] Days = [1, 3, 7, 14, 30, 60, 120];
    public static ReviewSchedule Rate(int stage, ReviewRating rating, DateTime now)
    {
        if (stage is < -1 or > 6 || !Enum.IsDefined(rating)) throw new ArgumentException("Invalid review stage or rating.");
        return rating switch
        {
            ReviewRating.Again => new(-1, now.AddMinutes(10)),
            ReviewRating.Hard => new(stage, now.AddDays(Math.Max(1, Days[Math.Max(0, stage)] / 2.0))),
            _ => Advance(Math.Min(6, stage + (rating == ReviewRating.Easy ? 2 : 1)), now)
        };
    }
    private static ReviewSchedule Advance(int stage, DateTime now) => new(stage, now.AddDays(Days[stage]));
}
