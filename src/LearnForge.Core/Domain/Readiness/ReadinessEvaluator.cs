namespace LearnForge.Core;

public static class ReadinessEvaluator
{
    public static ReadinessResult Evaluate(IEnumerable<ReadinessEvidence> history, ReadinessPolicy policy, DateTime now)
    {
        ReadinessTrack Track(AssessmentSize size, int required)
        {
            // Filter only by size: a recent failure interrupts the streak.
            var last = history.Where(e => e.Size == size).OrderByDescending(e => e.CompletedAt).ThenByDescending(e => e.Id).Take(required).ToArray();
            var streak = last.TakeWhile(e => e.Eligible && e.CompletedAt >= now.AddDays(-policy.LookbackDays) && e.CorrectPercent > policy.Threshold).Count();
            return new(size, required, streak, streak == required, last.Select(e => e.Id).ToArray());
        }

        var shortTrack = Track(AssessmentSize.Short, policy.ShortAttempts);
        var fullTrack = Track(AssessmentSize.Full, policy.FullAttempts);
        var ready = shortTrack.Met || fullTrack.Met;
        return new(ready, ready ? "Your recent practice supports considering the real exam." : "Build a consistent run of independent mock results.", shortTrack, fullTrack, policy.Threshold);
    }
}
