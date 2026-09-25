namespace LearnForge.Core;

public sealed record ReadinessPolicy(int ShortAttempts = 5, int FullAttempts = 3, decimal Threshold = 90,
    int LookbackDays = 90, decimal MinimumFreshPercent = 100);
