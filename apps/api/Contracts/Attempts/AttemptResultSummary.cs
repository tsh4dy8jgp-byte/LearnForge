namespace LearnForge.Api.Contracts.Attempts;

public sealed record AttemptResultSummary(decimal Earned, decimal Possible, decimal Score, decimal CorrectPercent,
    bool Eligible, decimal FreshPercent, decimal? PassPoints = null, bool? Passed = null);
