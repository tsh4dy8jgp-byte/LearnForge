using System.Text.RegularExpressions;

namespace LearnForge.Core;

public static partial class CharacterStudyContent
{
    public static IEnumerable<Diagnostic> Validate(Pack pack)
    {
        if (pack.CharacterStudy is not { } study) yield break;
        Diagnostic Error(string path, string message) => new("LF100", "characterStudy/" + path, message);
        var recognition = pack.Decks.FirstOrDefault(d => d.Id == study.RecognitionDeckId);
        var confusion = pack.Decks.FirstOrDefault(d => d.Id == study.ConfusionDeckId);
        if (!pack.Features.Writing || !pack.Features.Flashcards || recognition is null || confusion is null || recognition == confusion)
            yield return Error("", "Character study requires writing and separate recognition and confusion decks.");
        if (study.Contrasts.Length is < 1 or > 100 || study.Readings.Length is < 1 or > 100)
            yield return Error("", "Provide 1–100 contrasts and 1–100 short readings.");
        var characters = pack.WritingCharacters;
        var ids = characters.Select(c => c.Id).ToHashSet();
        var seen = new HashSet<string>(); var notes = new HashSet<string>();
        foreach (var pair in study.Contrasts)
        {
            if (!Identifier().IsMatch(pair.Id) || !seen.Add(pair.Id) || string.IsNullOrWhiteSpace(pair.Explanation))
                yield return Error("contrasts/" + pair.Id, "Provide distinct stable contrast IDs and an explanation.");
            if (pair.CharacterIds.Length != 2 || pair.CharacterIds.Distinct().Count() != 2 || pair.CharacterIds.Any(id => !ids.Contains(id)) ||
                pair.NoteIds.Length != 2 || pair.NoteIds.Any(id => !notes.Add(id) || confusion?.Notes.Any(n => n.Id == id && n.Kind == FlashcardKind.Basic && (n.Tags ?? []).Contains(pair.Id)) != true))
                yield return Error("contrasts/" + pair.Id, "Map two distinct characters to two distinct basic notes in the confusion deck, tagged with the contrast ID.");
            else
                for (var i = 0; i < 2; i++)
                    if (confusion!.Notes.First(n => n.Id == pair.NoteIds[i]).Back?.Trim() != characters.First(c => c.Id == pair.CharacterIds[i]).Glyph)
                        yield return Error("contrasts/" + pair.Id, "Each contrast note answer must be exactly its corresponding character glyph.");
        }
        // Recognition eligibility is explicit through the note ID; arbitrary deck names are supported.
        if (recognition is not null && characters.Any(c => !recognition.Notes.Any(n => n.Id == c.Id && n.Kind == FlashcardKind.Basic && n.Front?.Trim() == c.Glyph)))
            yield return Error("recognitionDeckId", "Each character needs a basic recognition note with its character ID and glyph front.");
        seen.Clear();
        foreach (var reading in study.Readings)
        {
            var path = "readings/" + reading.Id;
            if (!Identifier().IsMatch(reading.Id) || !seen.Add(reading.Id) || string.IsNullOrWhiteSpace(reading.Title) || string.IsNullOrWhiteSpace(reading.Note))
                yield return Error(path, "Provide distinct stable reading IDs, a title and a teaching note.");
            if (reading.RequiredCharacterIds.Length == 0 || reading.RequiredCharacterIds.Distinct().Count() != reading.RequiredCharacterIds.Length || reading.RequiredCharacterIds.Any(id => !ids.Contains(id)))
                yield return Error(path, "Reading prerequisites must be distinct character IDs in this pack.");
            if (reading.Lines.Length is < 1 or > 12 || reading.Lines.Any(l => string.IsNullOrWhiteSpace(l.Translation) || l.Words.Length is < 1 or > 60 ||
                l.Words.Any(w => string.IsNullOrWhiteSpace(w.Text) || w.Text.Length > 100 || (Hanzi().IsMatch(w.Text) && (string.IsNullOrWhiteSpace(w.Pinyin) || string.IsNullOrWhiteSpace(w.Meaning))))))
                yield return Error(path, "Provide 1–12 translated lines with short, glossed word tokens and optional punctuation tokens.");
            var text = string.Concat(reading.Lines.SelectMany(l => l.Words).Select(w => w.Text));
            if (reading.SupportWords.Any(w => string.IsNullOrWhiteSpace(w.Text) || string.IsNullOrWhiteSpace(w.Pinyin) || string.IsNullOrWhiteSpace(w.Meaning) || !text.Contains(w.Text)))
                yield return Error(path, "Support words need pronunciation and meaning and must occur in the reading.");
            var allowed = string.Concat(characters.Where(c => reading.RequiredCharacterIds.Contains(c.Id)).Select(c => c.Glyph)) + string.Concat(reading.SupportWords.Select(w => w.Text));
            if (Hanzi().Matches(text).Any(m => !allowed.Contains(m.Value)) || characters.Any(c => reading.RequiredCharacterIds.Contains(c.Id) && !text.Contains(c.Glyph)))
                yield return Error(path, "Every hanzi in a reading must be a required character or an explicitly glossed support word; every prerequisite must occur in the reading.");
        }
    }
    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]{0,99}$")] private static partial Regex Identifier();
    [GeneratedRegex(@"[\p{IsCJKUnifiedIdeographs}]")] private static partial Regex Hanzi();
}
