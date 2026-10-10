using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LearnForge.Core;

// Shared rules for typed responses, used by both the compiler (keys) and the grader (learner text).
public static partial class ResponseText
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

    // Letter case, spacing (including no-break spaces), apostrophe and hyphen style, the œ/æ ligatures and closing punctuation
    // never decide correctness. Diacritics do, unless the question ignores them.
    public static string NormalizeShortAnswer(string text, bool ignoreDiacritics = false)
    {
        var value = text.Normalize(NormalizationForm.FormC);
        value = Apostrophe().Replace(value, "'");
        value = Hyphen().Replace(value, "-");
        value = Whitespace().Replace(value, " ");
        value = SpacedApostrophe().Replace(value, "'");
        value = value.Trim().ToLowerInvariant().Replace("œ", "oe").Replace("æ", "ae");
        value = ClosingPunctuation().Replace(value, "");
        if (!ignoreDiacritics) return value;
        var stripped = value.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return string.Concat(stripped).Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex("[‘’ʼ´`]")]
    private static partial Regex Apostrophe();
    [GeneratedRegex("[‐‑]")]
    private static partial Regex Hyphen();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
    [GeneratedRegex(" ?' ?")]
    private static partial Regex SpacedApostrophe();
    [GeneratedRegex(@"[\s.!?…]+$")]
    private static partial Regex ClosingPunctuation();
}
