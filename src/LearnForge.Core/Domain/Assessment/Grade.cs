namespace LearnForge.Core;

public sealed record Grade(decimal Earned, decimal Possible, bool FullyCorrect, string[] Correct,
    Dictionary<string, string>? Matches, string Explanation);
