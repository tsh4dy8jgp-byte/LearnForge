using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class LearningRecordTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Pack pack = TestApi.LoadPack("reasoning-foundations");

    private static Task<JsonElement> Progress(HttpClient client, string packId) =>
        client.GetFromJsonAsync<JsonElement>($"/api/me/courses/{packId}");

    private static JsonElement Objective(JsonElement progress, string id) =>
        progress.GetProperty("objectives").EnumerateArray().Single(o => o.GetProperty("id").GetString() == id);

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    private async Task<HttpClient> Publish(Pack release, HttpClient? publisher = null)
    {
        publisher ??= await TestApi.Publisher(factory);
        (await publisher.PostAsJsonAsync("/api/authoring/publish", release)).EnsureSuccessStatusCode();
        return publisher;
    }

    private static string NewId(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..12];

    [Fact] public async Task Learning_mode_answers_build_mastery_evidence()
    {
        var client = await TestApi.Account(factory);
        Assert.Equal("notStarted", Objective(await Progress(client, pack.Id), "sets").GetProperty("state").GetString());
        var attempt = await TestApi.Start(client, "learn", blueprintId: "full");
        var revision = 0;
        foreach (var id in TestApi.QuestionIds(attempt)) revision = await TestApi.Save(client, attempt, revision, id, TestApi.Correct(pack, id), check: true);

        var progress = await Progress(client, pack.Id);
        var touched = progress.GetProperty("objectives").EnumerateArray().Where(o => o.GetProperty("considered").GetInt32() > 0).ToArray();
        Assert.NotEmpty(touched);
        Assert.All(touched, o => Assert.Equal(o.GetProperty("considered").GetInt32(), o.GetProperty("correct").GetInt32()));
        Assert.Equal("active", progress.GetProperty("enrollment").GetString());
    }

    [Fact] public async Task Skipped_learning_questions_do_not_change_mastery()
    {
        var client = await TestApi.Account(factory);
        var attempt = await TestApi.Start(client, "learn");
        await TestApi.Complete(client, attempt, pack, correct: false);
        var progress = await Progress(client, pack.Id);
        Assert.All(progress.GetProperty("objectives").EnumerateArray(), o => Assert.Equal("notStarted", o.GetProperty("state").GetString()));
    }

    [Fact] public async Task Lesson_progress_survives_new_releases_and_flags_revised_lessons()
    {
        var v1 = pack with { Id = NewId("carry") };
        var publisher = await Publish(v1);
        foreach (var lesson in new[] { "sets-intro", "logic-intro" })
            Assert.Equal(HttpStatusCode.NoContent, (await publisher.PutAsync($"/api/me/courses/{v1.Id}/lessons/{lesson}", null)).StatusCode);
        var v2 = v1 with { Version = "1.1.0", Lessons = v1.Lessons.Select(l => l.Id == "logic-intro" ? l with { Summary = l.Summary + " Updated." } : l).ToArray() };
        await Publish(v2, publisher);

        var progress = await Progress(publisher, v1.Id);
        Assert.Equal("1.1.0", progress.GetProperty("version").GetString());
        Assert.Equal(new[] { "logic-intro", "sets-intro" }, Strings(progress.GetProperty("completedLessons")).Order());
        Assert.Equal(new[] { "logic-intro" }, Strings(progress.GetProperty("revisedLessons")));

        await publisher.PutAsync($"/api/me/courses/{v1.Id}/lessons/logic-intro", null);
        Assert.Empty(Strings((await Progress(publisher, v1.Id)).GetProperty("revisedLessons")));
    }

    [Fact] public async Task Removed_lessons_leave_progress_without_errors()
    {
        var extra = pack.Lessons[0] with { Id = "sets-extra", Title = "More sets" };
        var v1 = pack with { Id = NewId("removed"), Lessons = [.. pack.Lessons, extra] };
        var publisher = await Publish(v1);
        await publisher.PutAsync($"/api/me/courses/{v1.Id}/lessons/sets-extra", null);
        await publisher.PutAsync($"/api/me/courses/{v1.Id}/lessons/sets-intro", null);
        await Publish(v1 with { Version = "1.1.0", Lessons = pack.Lessons }, publisher);

        var progress = await Progress(publisher, v1.Id);
        Assert.Equal(new[] { "sets-intro" }, Strings(progress.GetProperty("completedLessons")));
        Assert.Equal(3, progress.GetProperty("lessonCount").GetInt32());
        Assert.Equal(1, (await publisher.GetFromJsonAsync<JsonElement>("/api/me/dashboard")).GetProperty("completedLessons").GetInt32());
    }

    [Fact] public async Task Enrollment_is_automatic_and_archiving_hides_but_keeps_the_course()
    {
        var client = await TestApi.Account(factory);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/me/dashboard")).GetProperty("courses").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, (await Progress(client, pack.Id)).GetProperty("enrollment").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/me/courses/{pack.Id}/lessons/sets-intro", null)).StatusCode);
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/me/dashboard");
        Assert.Equal(pack.Id, dashboard.GetProperty("courses").EnumerateArray().Single().GetProperty("packId").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/me/enrollments/{pack.Id}", new { status = "archived" })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/me/dashboard")).GetProperty("courses").EnumerateArray());
        var archived = await Progress(client, pack.Id);
        Assert.Equal("archived", archived.GetProperty("enrollment").GetString());
        Assert.Single(archived.GetProperty("completedLessons").EnumerateArray());

        await TestApi.Start(client, "learn");
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/me/dashboard")).GetProperty("courses").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/me/enrollments/missing-pack", new { status = "active" })).StatusCode);
    }

    [Fact] public async Task Course_progress_offers_a_next_step_and_readiness_goal_status()
    {
        var client = await TestApi.Account(factory);
        var progress = await Progress(client, pack.Id);
        var step = progress.GetProperty("nextSteps").EnumerateArray().First();
        Assert.Equal("readLesson", step.GetProperty("kind").GetString());
        Assert.Equal("startObjective", step.GetProperty("reason").GetString());
        Assert.Equal("sets-intro", step.GetProperty("lessonId").GetString());
        Assert.Equal("Think in sets", step.GetProperty("lessonTitle").GetString());
        var goal = progress.GetProperty("goal");
        Assert.Equal("readiness", goal.GetProperty("goal").GetString());
        Assert.False(goal.GetProperty("met").GetBoolean());
        Assert.Equal(JsonValueKind.Object, goal.GetProperty("readiness").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/me/courses/missing-pack")).StatusCode);
    }

    [Fact] public async Task Mastery_goal_packs_report_objectives_instead_of_readiness()
    {
        var mastery = pack with { Id = NewId("mastery"), Goal = CourseGoal.Mastery, Readiness = null };
        var publisher = await Publish(mastery);
        var goal = (await Progress(publisher, mastery.Id)).GetProperty("goal");
        Assert.Equal("mastery", goal.GetProperty("goal").GetString());
        Assert.Equal(JsonValueKind.Null, goal.GetProperty("readiness").ValueKind);
        Assert.Equal(3, goal.GetProperty("objectiveCount").GetInt32());
    }

    [Fact] public async Task Completion_goal_is_met_when_every_lesson_is_read()
    {
        var completion = pack with { Id = NewId("complete"), Goal = CourseGoal.Completion, Readiness = null };
        var publisher = await Publish(completion);
        foreach (var lesson in completion.Lessons.Take(2)) await publisher.PutAsync($"/api/me/courses/{completion.Id}/lessons/{lesson.Id}", null);
        Assert.False((await Progress(publisher, completion.Id)).GetProperty("goal").GetProperty("met").GetBoolean());
        await publisher.PutAsync($"/api/me/courses/{completion.Id}/lessons/{completion.Lessons[2].Id}", null);
        Assert.True((await Progress(publisher, completion.Id)).GetProperty("goal").GetProperty("met").GetBoolean());
    }

    [Fact] public async Task The_public_catalog_contains_no_personal_progress()
    {
        var client = await TestApi.Account(factory);
        await client.PutAsync($"/api/me/courses/{pack.Id}/lessons/sets-intro", null);
        var course = await client.GetFromJsonAsync<JsonElement>($"/api/catalog/{pack.Id}");
        Assert.False(course.TryGetProperty("completedLessons", out _));
        Assert.Equal("readiness", course.GetProperty("goal").GetString());
    }

    [Fact] public async Task Case_study_only_objectives_are_flagged_and_practised_through_a_blueprint()
    {
        var client = await TestApi.Account(factory);
        await client.PutAsync("/api/me/courses/evidence-lab/lessons/evidence-first", null);
        var lab = await Progress(client, "evidence-lab");
        Assert.False(Objective(lab, "evaluate").GetProperty("standalonePractice").GetBoolean());
        var step = lab.GetProperty("nextSteps").EnumerateArray().First();
        Assert.Equal("practise", step.GetProperty("kind").GetString());
        Assert.Equal("short", step.GetProperty("blueprintId").GetString());
        Assert.True(Objective(await Progress(client, pack.Id), "sets").GetProperty("standalonePractice").GetBoolean());
    }
}
