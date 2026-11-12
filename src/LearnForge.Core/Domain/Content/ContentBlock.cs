using System.Text.Json.Serialization;

namespace LearnForge.Core;

// A table block carries Rows: the first row is the header, the rest are body rows of plain text. Text is then an optional
// note shown under the table. Rows is omitted from JSON when null, so earlier releases serialize exactly as before.
public sealed record ContentBlock(
    ContentBlockKind Kind,
    string Text,
    string? Title = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[][]? Rows = null);
