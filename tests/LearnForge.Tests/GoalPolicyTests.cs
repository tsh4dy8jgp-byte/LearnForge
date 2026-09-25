using System.Text.Json.Nodes;
using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class GoalPolicyTests
{
    private static string Without(Pack pack, params string[] properties)
    {
        var node = JsonNode.Parse(Json.Write(pack))!.AsObject();
        foreach (var name in properties) node.Remove(name);
        return node.ToJsonString();
    }

    [Fact] public void Packs_that_omit_goal_readiness_and_mastery_compile_as_readiness_packs()
    {
        var compiled = ContentEngine.Compile(Without(CoreTests.Demo(), "goal", "readiness", "mastery"));
        Assert.True(compiled.Success, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Equal(CourseGoal.Readiness, compiled.Pack!.Goal);
        Assert.Null(compiled.Pack.Readiness);
        Assert.Null(compiled.Pack.Mastery);
    }

    [Fact] public void Goal_and_mastery_are_read_from_camel_case_json()
    {
        var node = JsonNode.Parse(Without(CoreTests.Demo(), "readiness"))!.AsObject();
        node["goal"] = "mastery";
        node["mastery"] = new JsonObject { ["window"] = 6, ["minimumEvidence"] = 4 };
        var compiled = ContentEngine.Compile(node.ToJsonString());
        Assert.True(compiled.Success, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Equal(CourseGoal.Mastery, compiled.Pack!.Goal);
        Assert.Equal(new MasteryPolicy(6, 4), compiled.Pack.Mastery);
    }

    [Theory]
    [InlineData(5, 0, 80, 60)]
    [InlineData(2, 3, 80, 60)]
    [InlineData(21, 3, 80, 60)]
    [InlineData(5, 3, 49, 60)]
    [InlineData(5, 3, 101, 60)]
    [InlineData(5, 3, 80, 0)]
    [InlineData(5, 3, 80, 366)]
    public void Mastery_policy_bounds_are_enforced(int window, int minimum, int percent, int reviewDays)
    {
        var pack = CoreTests.Demo() with { Mastery = new(window, minimum, percent, reviewDays) };
        Assert.Contains(ContentEngine.Validate(pack), d => d.Path == "mastery");
    }

    [Fact] public void Mastery_goal_requires_enough_question_families_per_objective()
    {
        // The demo pack has 16 families for sets and logic but only 8 for ordering.
        var diagnostics = ContentEngine.Validate(CoreTests.Demo() with { Goal = CourseGoal.Mastery, Mastery = new(10, 10) });
        Assert.Contains(diagnostics, d => d.Path == "ordering");
        Assert.DoesNotContain(diagnostics, d => d.Path is "sets" or "logic");
        Assert.Empty(ContentEngine.Validate(CoreTests.Demo() with { Goal = CourseGoal.Mastery }));
    }

    [Fact] public void Undefined_goal_values_are_rejected() =>
        Assert.Contains(ContentEngine.Validate(CoreTests.Demo() with { Goal = (CourseGoal)7 }), d => d.Path == "goal");

    [Fact] public void Content_hash_is_stable_and_changes_with_content()
    {
        var lesson = CoreTests.Demo().Lessons[0];
        Assert.Matches("^[0-9a-f]{64}$", ContentHash.Of(lesson));
        Assert.Equal(ContentHash.Of(lesson), ContentHash.Of(lesson with { }));
        Assert.NotEqual(ContentHash.Of(lesson), ContentHash.Of(lesson with { Title = lesson.Title + " (revised)" }));
    }
}
