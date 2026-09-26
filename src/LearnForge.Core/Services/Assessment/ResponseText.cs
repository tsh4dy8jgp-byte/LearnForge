using System.Globalization;

namespace LearnForge.Core;

// Shared rules for typed responses, used by both the compiler (keys) and the grader (learner text).
public static class ResponseText
{
    public const int MaxLength = 2000;
    private const NumberStyles NumberStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    // Invariant culture only: "4.5" is a number everywhere, "4,5" nowhere.
    public static bool TryParseNumber(string? text, out decimal value)
    {
        value = 0;
        return text is not null && decimal.TryParse(text.Trim(), NumberStyle, CultureInfo.InvariantCulture, out value);
    }

    // Line endings, surrounding whitespace and runs of spaces or tabs never decide correctness.
    public static string NormalizeOutput(string text) =>
        string.Join('\n', text.Replace("\r\n", "\n").Trim().Split('\n')
            .Select(line => string.Join(' ', line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))));
}
