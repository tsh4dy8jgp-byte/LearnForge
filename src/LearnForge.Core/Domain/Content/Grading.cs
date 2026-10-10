using System.Text.Json.Serialization;

namespace LearnForge.Core;

// Correct holds option IDs for selection kinds, invariant-culture numbers for Numeric and accepted text for CodeOutput and ShortAnswer.
// Tolerance is the allowed absolute difference for Numeric keys. IgnoreDiacritics (ShortAnswer only) accepts "eleve" for "élève";
// it is omitted from JSON when null, so earlier releases and attempt snapshots serialize exactly as before.
public sealed record Grading(ScoringPolicy Policy, string[] Correct, Dictionary<string, string>? Matches = null, decimal? Tolerance = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IgnoreDiacritics = null);
