using System.Text.RegularExpressions;

namespace LearnForge.Core;

public static partial class WritingContent
{
    public static IEnumerable<Diagnostic> Validate(Pack pack)
    {
        var characters = pack.WritingCharacters;
        Diagnostic Error(string path, string message) => new("LF100", "characters/" + path, message);
        if (characters.Length > 500) yield return Error("", "Limit a pack to 500 writing characters.");
        if (pack.Features.Writing != (characters.Length > 0)) yield return Error("", "Writing content and the writing capability must agree.");
        var ids = new HashSet<string>(); var glyphs = new HashSet<string>();
        foreach (var c in characters)
        {
            if (!Identifier().IsMatch(c.Id) || !ids.Add(c.Id)) yield return Error(c.Id, "Use distinct, stable lowercase character IDs.");
            if (c.Language != "zh-Hans" || !Hanzi().IsMatch(c.Glyph) || !glyphs.Add(c.Glyph)) yield return Error(c.Id, "The current writing engine requires distinct Simplified Chinese hanzi (zh-Hans).");
            if (!pack.Lessons.Any(l => l.Id == c.LessonId) || c.ObjectiveIds.Length == 0 || c.ObjectiveIds.Distinct().Count() != c.ObjectiveIds.Length ||
                c.ObjectiveIds.Any(id => !pack.Objectives.Any(o => o.Id == id)) || !pack.Lessons.Any(l => l.Id == c.LessonId && c.ObjectiveIds.All(l.ObjectiveIds.Contains)))
                yield return Error(c.Id, "Character lesson and objectives must resolve to the same teaching lesson.");
            if (new[] { c.Pinyin, c.Meaning, c.Components, c.Tip, c.Sentence, c.SentencePinyin, c.Translation, c.Glossary, c.AudioTranscript }.Any(string.IsNullOrWhiteSpace) ||
                c.Words.Length == 0 || c.Words.Any(w => string.IsNullOrWhiteSpace(w.Text) || string.IsNullOrWhiteSpace(w.Pinyin) || string.IsNullOrWhiteSpace(w.Meaning)))
                yield return Error(c.Id, "Characters need pronunciation, components, teaching notes, words, a translated example, glossary and audio transcript.");
            if (!AudioPath().IsMatch(c.AudioUrl)) yield return Error(c.Id, "Audio must reference a bundled /writing/audio/ asset.");
            var d = c.StrokeData;
            if (d.Strokes.Length is < 1 or > 40 || d.Strokes.Length != d.Medians.Length ||
                d.Strokes.Any(s => s.Length is < 3 or > 20000 || !StrokePath().IsMatch(s)) ||
                d.Medians.Any(m => m.Length is < 2 or > 100 || m.Any(p => p.Length != 2 || p.Any(n => !double.IsFinite(n) || n < -200 || n > 1200))))
                yield return Error(c.Id, "Provide matching, bounded stroke paths and ordered median points for every stroke.");
        }
    }
    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]{0,99}$")] private static partial Regex Identifier();
    [GeneratedRegex(@"^[\p{IsCJKUnifiedIdeographs}]$")] private static partial Regex Hanzi();
    [GeneratedRegex(@"^/writing/audio/[a-z0-9._-]+\.(mp3|wav|m4a)$")] private static partial Regex AudioPath();
    [GeneratedRegex(@"^[MmLlHhVvCcSsQqTtAaZz0-9., +\-]+$")] private static partial Regex StrokePath();
}
