using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class WritingTests
{
    public static Pack Pilot() => TestApi.LoadPack("chinese-writing-pilot");

    [Fact] public void Pilot_covers_thirty_characters_with_bundled_audio_and_valid_stroke_models()
    {
        var pack = Pilot();
        Assert.NotNull(pack); Assert.Empty(ContentEngine.Validate(pack));
        Assert.Equal(30, pack.WritingCharacters.Length);
        Assert.Equal("一二三四五六七八九十人大口小日月水火木山我你好中国学生吃喝茶".Order(),
            string.Concat(pack.WritingCharacters.Select(c => c.Glyph)).Order());
        Assert.Equal(7, pack.Lessons.Length);
        Assert.All(pack.WritingCharacters, c => {
            Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, c.AudioUrl.TrimStart('/'))));
            Assert.Equal(c.StrokeData.Strokes.Length, c.StrokeData.Medians.Length);
            Assert.Contains(c.Glyph, c.Sentence + string.Concat(c.Words.Select(w => w.Text)));
        });
        Assert.Equal(30, Flashcards.Expand(pack).Length);
        Assert.Equal(10, ExamComposer.Compose(pack, pack.Blueprints[0], [], "writing-pilot").Length);
        Assert.False(pack.Features.Assessments);
    }
    [Fact] public void Existing_packs_remain_compatible_and_writing_capability_requires_content()
    {
        var original = CoreTests.Demo();
        Assert.False(original.Features.Writing); Assert.Empty(original.WritingCharacters);
        Assert.NotEmpty(ContentEngine.Validate(original with { Capabilities = original.Features with { Writing = true } }));
    }
    [Fact] public void Writing_validation_rejects_bad_references_remote_audio_and_unsafe_geometry()
    {
        var p = Pilot(); var c = p.WritingCharacters[0];
        foreach (var invalid in new[] {
            c with { LessonId = "missing" }, c with { ObjectiveIds = ["unknown"] },
            c with { AudioUrl = "https://external.invalid/clip.wav" },
            c with { StrokeData = new(["<script>"], [[[0, 0], [10, 10]]]) },
            c with { StrokeData = new(c.StrokeData.Strokes, []) },
            c with { Language = "ja" }, c with { Pinyin = "" }
        }) Assert.NotEmpty(ContentEngine.Validate(p with { Characters = [invalid] }));
        Assert.NotEmpty(ContentEngine.Validate(p with { Characters = [c, c] }));
    }
}
