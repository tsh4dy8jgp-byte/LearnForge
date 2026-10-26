using Xunit;

namespace LearnForge.Tests;

public class CharacterStudyTests
{
    [Fact] public void Pilot_study_is_pack_owned_and_old_packs_still_work()
    {
        var pack = WritingTests.Pilot(); var study = pack.CharacterStudy!;
        Assert.Empty(ContentEngine.Validate(pack with { Id = "another-language-course" }));
        Assert.Equal(5, study.Contrasts.Length); Assert.Equal(6, study.Readings.Length);
        Assert.Equal(10, pack.Decks.Single(d => d.Id == study.ConfusionDeckId).Notes.Length);
        Assert.Null(CoreTests.Demo().CharacterStudy);
        Assert.Empty(ContentEngine.Validate(pack with { CharacterStudy = null }));
    }
    [Fact] public void Study_rejects_missing_decks_wrong_answers_and_unmapped_notes()
    {
        var p = WritingTests.Pilot(); var study = p.CharacterStudy!; var pair = study.Contrasts[0];
        foreach (var invalid in new[] {
            study with { RecognitionDeckId = "missing" },
            study with { ConfusionDeckId = study.RecognitionDeckId },
            study with { Contrasts = [pair with { CharacterIds = ["missing", pair.CharacterIds[1]] }] },
            study with { Contrasts = [pair with { NoteIds = pair.NoteIds.Reverse().ToArray() }] },
            study with { Contrasts = [pair with { NoteIds = [pair.NoteIds[0], pair.NoteIds[0]] }] },
        }) Assert.Contains(ContentEngine.Validate(p with { CharacterStudy = invalid }), d => d.Path.StartsWith("characterStudy/", StringComparison.Ordinal));
        var recognition = p.Decks[0] with { Notes = p.Decks[0].Notes.Skip(1).ToArray() };
        Assert.NotEmpty(CharacterStudyContent.Validate(p with { FlashcardDecks = [recognition, p.Decks[1]] }));
    }
    [Fact] public void Reading_prerequisites_cover_the_actual_text_and_extra_words_are_explicit()
    {
        var p = WritingTests.Pilot(); var study = p.CharacterStudy!; var reading = study.Readings[0];
        foreach (var invalid in new[] {
            reading with { RequiredCharacterIds = ["unknown"] },
            reading with { RequiredCharacterIds = reading.RequiredCharacterIds.Skip(1).ToArray() },
            reading with { RequiredCharacterIds = [.. reading.RequiredCharacterIds, "u8336"] },
            reading with { Lines = [new([new("是", "shì", "is")], "Is.")] },
            reading with { Lines = [new([new("一", "", "one")], "One.")] },
            reading with { SupportWords = [new("是", "shì", "is")] },
        }) Assert.NotEmpty(CharacterStudyContent.Validate(p with { CharacterStudy = study with { Readings = [invalid] } }));
        var student = study.Readings.Single(r => r.Id == "student");
        Assert.Empty(CharacterStudyContent.Validate(p with { CharacterStudy = study with { Readings = [student] } }));
        Assert.NotEmpty(CharacterStudyContent.Validate(p with { CharacterStudy = study with { Readings = [student with { SupportWords = [] }] } }));
    }
}
