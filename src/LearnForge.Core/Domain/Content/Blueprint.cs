using System.Text.Json.Serialization;

namespace LearnForge.Core;

// ObjectiveWeights (optional) asks the composer to follow these shares of Count, e.g. an exam's published domain weights.
// It is omitted from JSON when null, so unweighted releases and attempt snapshots serialize exactly as before.
public sealed record Blueprint(string Id, string Title, int Count, int Minutes, AssessmentSize Size,
    string[] ObjectiveIds, QuestionKind[] RequiredKinds, bool LockSections = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, decimal>? ObjectiveWeights = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? QuestionIds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? PassPoints = null);
