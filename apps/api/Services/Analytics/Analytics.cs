namespace LearnForge.Api.Services.Analytics;

public static class Analytics
{
    public static AttemptSummaryDto Summary(Attempt a)
    {
        var snapshot = AttemptService.Snapshot(a);
        return new(a.Id, a.PackId, snapshot.Title, snapshot.Version, a.Size, a.Mode, a.Status,
            a.StartedAt, a.CompletedAt, a.Deadline, a.CorrectPercent, a.Eligible, a.TimedOut,
            a.FreshPercent, a.Focus, a.Possible == 0 ? 0 : a.Earned * 100 / a.Possible, snapshot.Questions.Length);
    }
}
