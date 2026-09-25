namespace LearnForge.Core;

public sealed record Grading(ScoringPolicy Policy, string[] Correct, Dictionary<string, string>? Matches = null);
