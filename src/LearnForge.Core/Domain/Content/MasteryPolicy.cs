namespace LearnForge.Core;

public sealed record MasteryPolicy(int Window = 5, int MinimumEvidence = 3, decimal ProficientPercent = 80, int ReviewAfterDays = 60);
