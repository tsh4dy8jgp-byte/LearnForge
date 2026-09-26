namespace LearnForge.Core;

// Correct holds option IDs for selection kinds, invariant-culture numbers for Numeric and accepted outputs for CodeOutput.
// Tolerance is the allowed absolute difference for Numeric keys.
public sealed record Grading(ScoringPolicy Policy, string[] Correct, Dictionary<string, string>? Matches = null, decimal? Tolerance = null);
