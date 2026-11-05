using System.Text.Json.Nodes;
using Xunit;

namespace LearnForge.Tests;

public class FlashcardTests
{
    public static Pack Sample() => ContentEngine.Compile(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", "flashcards-sample.json"))).Pack!;
    [Fact] public void Standalone_and_legacy_sources_compile()
    {
        Assert.Empty(ContentEngine.Validate(Sample()));
        Assert.True(ContentEngine.Compile(Json.Write(PackStarter.Create(ProductProfile.Flashcards))).Success);
        Assert.False(CoreTests.Demo().Features.Flashcards);
        var source = JsonNode.Parse(Json.Write(Sample()))!.AsObject(); source.Remove("goal");
        Assert.Equal(CourseGoal.Review, ContentEngine.Compile(source.ToJsonString()).Pack!.Goal);
    }
    [Fact] public void Expansion_keeps_stable_variants_and_hides_only_target_clozes()
    {
        var pack = Sample(); var units = Flashcards.Expand(pack);
        Assert.Equal(11, units.Length);
        Assert.Equal(2, units.Count(u => u.NoteId == "term"));
        var cloze = units.Single(u => u.Id.EndsWith("spacing/c1", StringComparison.Ordinal));
        Assert.Contains("[when]", cloze.Front); Assert.Contains("one long session", cloze.Front);
        Assert.DoesNotContain("{{", cloze.Back);
        var changed = pack with { Version = "1.0.1" };
        Assert.Equal(units, Flashcards.Expand(changed));
        var repeated = new FlashcardDeck("repeat", "Repeat", [new("n", FlashcardKind.Cloze, ["recall"], "Explanation", Text: "{{c2::one}} then {{c2::two}}")]);
        Assert.Equal("[...] then [...]", Assert.Single(Flashcards.Expand(repeated)).Front);
        var blankHint = new FlashcardDeck("hint", "Hint", [new("n", FlashcardKind.Cloze, ["recall"], "Explanation", Text: "Say {{c1::hello:: }}.")]);
        Assert.Equal("Say [...].", Assert.Single(Flashcards.Expand(blankHint)).Front);
    }
    [Theory]
    [InlineData("{{c0::bad}}")]
    [InlineData("{{c1::}}")]
    [InlineData("{{c1::a {{c2::nested}}}}")]
    [InlineData("{{c1::x}} {{broken}}")]
    [InlineData("No deletion")]
    [InlineData("{{c1::a::b::c}}")]
    public void Malformed_clozes_are_rejected(string text) => Assert.False(Flashcards.ValidCloze(text));
    [Fact] public void Bad_references_languages_sources_and_disabled_cards_fail()
    {
        var pack = PackStarter.Create(ProductProfile.Flashcards);
        var note = pack.Decks[0].Notes[0] with { ObjectiveIds = ["unknown"], Language = "ar", Sources = [new("Bad", "javascript:alert(1)")] };
        var invalid = pack with { FlashcardDecks = [pack.Decks[0] with { Notes = [note] }] };
        Assert.True(ContentEngine.Validate(invalid).Length >= 3);
        Assert.Contains(ContentEngine.Validate(pack with { Capabilities = new(false, false, false) }), d => d.Path == "flashcardDecks");
    }
    [Fact] public void Compact_exam_can_include_independently_authored_cards()
    {
        var source = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", "exam-sample.json")))!.AsObject();
        var objective = source["domains"]![0]!["id"]!.GetValue<string>();
        source["flashcardDecks"] = JsonNode.Parse(Json.Write(new[] { Sample().Decks[0] with { Notes = [Sample().Decks[0].Notes[0] with { ObjectiveIds = [objective] }] } }));
        var result = ContentEngine.Compile(source.ToJsonString());
        Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        Assert.True(result.Pack!.Features.Flashcards);
    }
    [Fact] public void Schedule_has_explicit_boundaries_and_resets()
    {
        var now = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new ReviewSchedule(0, now.AddDays(1)), FlashcardScheduler.Rate(-1, ReviewRating.Good, now));
        Assert.Equal(new ReviewSchedule(1, now.AddDays(3)), FlashcardScheduler.Rate(-1, ReviewRating.Easy, now));
        Assert.Equal(new ReviewSchedule(-1, now.AddDays(1)), FlashcardScheduler.Rate(-1, ReviewRating.Hard, now));
        Assert.Equal(new ReviewSchedule(-1, now.AddMinutes(10)), FlashcardScheduler.Rate(6, ReviewRating.Again, now));
        Assert.Equal(new ReviewSchedule(6, now.AddDays(120)), FlashcardScheduler.Rate(6, ReviewRating.Easy, now));
        Assert.Equal(new ReviewSchedule(2, now.AddDays(3.5)), FlashcardScheduler.Rate(2, ReviewRating.Hard, now));
    }
}
