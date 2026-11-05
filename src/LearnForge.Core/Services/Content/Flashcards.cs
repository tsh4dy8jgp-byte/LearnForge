using System.Globalization;
using System.Text.RegularExpressions;

namespace LearnForge.Core;

public static partial class Flashcards
{
    public static readonly string[] Languages = ["en", "ru", "kk", "uk", "de", "fr", "es", "it", "pt", "tr", "nl", "pl"];
    // Intentionally excludes nested deletions and braces in answers/hints.
    [GeneratedRegex(@"\{\{c([1-9][0-9]?)::([^{}]+?)\}\}", RegexOptions.Singleline)]
    private static partial Regex Deletion();

    public static bool ValidCloze(string text)
    {
        var matches = Deletion().Matches(text);
        return matches.Count > 0 && matches.All(m => !string.IsNullOrWhiteSpace(m.Groups[2].Value.Split("::")[0]))
            && !Deletion().Replace(text, "").Contains("{{", StringComparison.Ordinal)
            && !Deletion().Replace(text, "").Contains("}}", StringComparison.Ordinal)
            && matches.All(m => m.Groups[2].Value.Split("::").Length <= 2);
    }

    public static FlashcardUnit[] Expand(Pack pack) => pack.Decks.SelectMany(Expand).ToArray();
    public static FlashcardUnit[] Expand(FlashcardDeck deck) => deck.Notes.SelectMany(note =>
    {
        FlashcardUnit Unit(string variant, string front, string back)
        {
            var unit = new FlashcardUnit($"{deck.Id}/{note.Id}/{variant}", deck.Id, note.Id, front, back,
                note.Explanation, note.ObjectiveIds, note.Tags ?? [], note.Language, note.Sources ?? [], "");
            return unit with { ContentHash = ContentHash.Of(unit) };
        }
        if (note.Kind == FlashcardKind.Cloze)
            return Deletion().Matches(note.Text!).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct().Order()
                .Select(number => Unit("c" + number, Deletion().Replace(note.Text!, m =>
                {
                    var parts = m.Groups[2].Value.Split("::");
                    // A blank hint shows the default placeholder, as Anki does.
                    return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) == number
                        ? "[" + (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : "...") + "]" : parts[0];
                }), Deletion().Replace(note.Text!, m => m.Groups[2].Value.Split("::")[0]))).ToArray();
        return note.Kind == FlashcardKind.Reversible
            ? new[] { Unit("forward", note.Front!, note.Back!), Unit("reverse", note.Back!, note.Front!) }
            : new[] { Unit("forward", note.Front!, note.Back!) };
    }).ToArray();

    public static Diagnostic[] Lint(Pack pack)
    {
        var warnings = new List<Diagnostic>();
        var units = Expand(pack);
        foreach (var group in units.GroupBy(u => u.Front.Trim().ToLowerInvariant()).Where(g => g.Count() > 1))
            warnings.Add(new("LF230", group.First().Id, "Several flashcards have the same recall prompt."));
        foreach (var unit in units)
        {
            if (unit.Front.Length > 400 || unit.Back.Length > 600)
                warnings.Add(new("LF231", unit.Id, "Long flashcard text may not fit a physical card. Split the recall target and verify PDF export."));
            if (unit.Front.Contains(unit.Back, StringComparison.OrdinalIgnoreCase))
                warnings.Add(new("LF232", unit.Id, "The recall prompt contains the complete answer."));
        }
        foreach (var objective in pack.Objectives.Where(o => !units.Any(u => u.ObjectiveIds.Contains(o.Id))))
            warnings.Add(new("LF233", objective.Id, "This objective has no flashcard recall coverage."));
        return warnings.ToArray();
    }
}
