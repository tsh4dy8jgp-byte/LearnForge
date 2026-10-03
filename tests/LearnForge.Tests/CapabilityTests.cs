using System.Text.Json.Nodes;
using Xunit;

namespace LearnForge.Tests;

public class CapabilityTests
{
    [Theory]
    [InlineData(ProductProfile.Course, true, false, false, CourseGoal.Completion)]
    [InlineData(ProductProfile.Exam, false, true, true, CourseGoal.Readiness)]
    [InlineData(ProductProfile.Hybrid, true, true, true, CourseGoal.Readiness)]
    public void Starter_profiles_compile_with_matching_content(ProductProfile profile, bool lessons, bool practice, bool assessments, CourseGoal goal)
    {
        var result = ContentEngine.Compile(Json.Write(PackStarter.Create(profile)));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(new(lessons, practice, assessments), result.Pack!.Features);
        Assert.Equal(goal, result.Pack.Goal);
        Assert.Equal(lessons, result.Pack.Lessons.Length > 0);
        Assert.Equal(practice || assessments, result.Pack.Blueprints.Length > 0);
    }

    [Fact] public void Legacy_sources_and_stored_releases_keep_all_capabilities()
    {
        var source = JsonNode.Parse(Json.Write(CoreTests.Demo()))!.AsObject();
        source.Remove("profile"); source.Remove("capabilities");
        var compiled = ContentEngine.Compile(source.ToJsonString());
        Assert.True(compiled.Success);
        Assert.Equal(ProductProfile.Hybrid, compiled.Pack!.Profile);
        Assert.Equal(new(true, true, true), Json.Read<Pack>(source.ToJsonString()).Features);
    }

    [Fact] public void Course_profile_defaults_to_completion_when_goal_is_omitted()
    {
        var source = JsonNode.Parse(Json.Write(PackStarter.Create(ProductProfile.Course)))!.AsObject();
        source.Remove("goal"); source.Remove("capabilities");
        var compiled = ContentEngine.Compile(source.ToJsonString());
        Assert.True(compiled.Success);
        Assert.Equal(CourseGoal.Completion, compiled.Pack!.Goal);
    }

    [Theory]
    [InlineData(false, false, false, CourseGoal.Completion)]
    [InlineData(false, true, false, CourseGoal.Completion)]
    [InlineData(true, false, false, CourseGoal.Readiness)]
    [InlineData(true, false, false, CourseGoal.Mastery)]
    public void Incompatible_capabilities_and_goals_are_rejected(bool lessons, bool practice, bool assessments, CourseGoal goal)
    {
        var pack = PackStarter.Create(ProductProfile.Course) with { Capabilities = new(lessons, practice, assessments), Goal = goal };
        Assert.Contains(ContentEngine.Validate(pack), d => d.Path is "goal" or "capabilities");
    }

    [Fact] public void Content_cannot_hide_behind_disabled_capabilities()
    {
        var pack = PackStarter.Create(ProductProfile.Hybrid) with { Capabilities = new(false, false, false) };
        var errors = ContentEngine.Validate(pack);
        Assert.Contains(errors, d => d.Path == "lessons");
        Assert.Contains(errors, d => d.Path == "questions");
    }

    [Fact] public void Explicit_capabilities_override_profile_defaults()
    {
        var pack = CoreTests.Demo() with { Profile = ProductProfile.Course, Capabilities = new(true, true, false), Goal = CourseGoal.Mastery };
        Assert.Empty(ContentEngine.Validate(pack));
        Assert.False(ContentEngine.Compile(Json.Write(pack)).Pack!.Features.Assessments);
    }

    [Fact] public void Completed_lesson_only_courses_never_recommend_practice()
    {
        var pack = PackStarter.Create(ProductProfile.Course);
        var mastery = MasteryEvaluator.Evaluate(pack.Objectives, [], new(), DateTime.UtcNow);
        var first = NextStepPlanner.Plan(pack, mastery, new HashSet<string>(), null);
        Assert.Equal(NextStepKind.ReadLesson, Assert.Single(first).Kind);
        Assert.Empty(NextStepPlanner.Plan(pack, mastery, pack.Lessons.Select(l => l.Id).ToHashSet(), null));
    }

    [Fact] public void Assessment_only_packs_suggest_a_supported_first_session()
    {
        var pack = PackStarter.Create(ProductProfile.Exam) with { Capabilities = new(false, false, true) };
        var mastery = MasteryEvaluator.Evaluate(pack.Objectives, [], new(), DateTime.UtcNow);
        var step = Assert.Single(NextStepPlanner.Plan(pack, mastery, new HashSet<string>(), null));
        Assert.Equal(NextStepKind.TakeMock, step.Kind);
        Assert.Equal(NextStepReason.AssessmentAvailable, step.Reason);
    }
}
