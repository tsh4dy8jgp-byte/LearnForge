using Xunit;

namespace LearnForge.Tests;

// Lesson tables are typed content: a header row plus body rows of plain text, never HTML.
public class LessonBlockTests
{
    private static readonly string[][] Rows = [["Rule", "Member?", "Free delivery"], ["R1", "T", "X"], ["R2", "F", ""]];

    private static Diagnostic[] Validate(ContentBlock block)
    {
        var demo = CoreTests.Demo();
        var lesson = demo.Lessons[0] with { Blocks = [.. demo.Lessons[0].Blocks, block] };
        return ContentEngine.Validate(demo with { Lessons = [lesson, .. demo.Lessons.Skip(1)] });
    }

    [Fact]
    public void A_table_block_with_a_header_and_rows_is_valid()
    {
        Assert.Empty(Validate(new(ContentBlockKind.Table, "", "Decision table", Rows)));
        Assert.Empty(Validate(new(ContentBlockKind.Table, "X means the action occurs.", null, Rows)));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("header-only")]
    [InlineData("ragged")]
    [InlineData("one-column")]
    [InlineData("too-wide")]
    [InlineData("too-long")]
    [InlineData("empty-header")]
    [InlineData("huge-cell")]
    public void Malformed_tables_are_rejected(string defect)
    {
        string[][]? rows = defect switch
        {
            "missing" => null,
            "header-only" => [Rows[0]],
            "ragged" => [Rows[0], ["R1", "T"]],
            "one-column" => [["A"], ["1"]],
            "too-wide" => [Enumerable.Repeat("H", 13).ToArray(), Enumerable.Repeat("c", 13).ToArray()],
            "too-long" => [Rows[0], .. Enumerable.Repeat(Rows[1], 60)],
            "empty-header" => [["Rule", " ", "Free delivery"], Rows[1]],
            _ => [Rows[0], ["R1", new string('x', 501), "X"]],
        };
        Assert.NotEmpty(Validate(new(ContentBlockKind.Table, "", "Table", rows)));
    }

    [Fact]
    public void Rows_belong_to_table_blocks_only()
        => Assert.NotEmpty(Validate(new(ContentBlockKind.Text, "Some text.", null, Rows)));

    [Fact]
    public void Other_blocks_still_need_text_and_serialize_as_before()
    {
        Assert.NotEmpty(Validate(new(ContentBlockKind.Text, " ")));
        var json = Json.Write(CoreTests.Demo());
        Assert.DoesNotContain("\"rows\"", json);
        var table = Json.Write(new ContentBlock(ContentBlockKind.Table, "", "T", Rows));
        Assert.Contains("\"table\"", table);
        Assert.Equal(ContentBlockKind.Table, Json.Read<ContentBlock>(table).Kind);
        Assert.Equal(Rows, Json.Read<ContentBlock>(table).Rows);
    }
}
