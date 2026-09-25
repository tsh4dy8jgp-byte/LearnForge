namespace LearnForge.Core;

public sealed record ReadinessResult(bool Ready, string Message, ReadinessTrack Short, ReadinessTrack Full, decimal Threshold);
