using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LearnForge.Tests;

public class CapabilityApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact] public async Task Publisher_drafts_are_private_revision_checked_and_preview_safe()
    {
        var publisher = await TestApi.Publisher(factory);
        var starter = await publisher.GetFromJsonAsync<JsonElement>("/api/authoring/starters/course");
        Assert.Equal("course", starter.GetProperty("profile").GetString());
        Assert.Empty(starter.GetProperty("questions").EnumerateArray());
        var source = Json.Write(PackStarter.Create(ProductProfile.Course));
        var created = await publisher.PostAsJsonAsync("/api/authoring/drafts", new { title = "Course draft", source, revision = 0 });
        created.EnsureSuccessStatusCode();
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetString()!;
        Assert.Equal(0, draft.GetProperty("revision").GetInt32());

        var preview = await publisher.PostAsJsonAsync("/api/authoring/preview", PackStarter.Create(ProductProfile.Course));
        preview.EnsureSuccessStatusCode();
        var previewJson = await preview.Content.ReadAsStringAsync();
        Assert.DoesNotContain("grading", previewJson);
        Assert.DoesNotContain("explanation", previewJson);
        Assert.Contains("course", previewJson);

        var saved = await publisher.PutAsJsonAsync("/api/authoring/drafts/" + id,
            new { title = "Updated course draft", source, revision = 0 });
        saved.EnsureSuccessStatusCode();
        Assert.Equal(1, (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await publisher.PutAsJsonAsync("/api/authoring/drafts/" + id,
            new { title = "Stale", source, revision = 0 })).StatusCode);

        var another = await TestApi.Publisher(factory);
        Assert.Equal(HttpStatusCode.NotFound, (await another.GetAsync("/api/authoring/drafts/" + id)).StatusCode);
        var site = await publisher.GetFromJsonAsync<JsonElement>("/api/site-settings");
        Assert.Equal("LearnForge", site.GetProperty("name").GetString());
    }

    [Fact] public async Task Lesson_only_course_can_be_published_completed_and_exported_without_assessment_surfaces()
    {
        var client = await TestApi.Publisher(factory);
        var pack = PackStarter.Create(ProductProfile.Course) with { Id = "course-" + Guid.NewGuid().ToString("N") };
        (await client.PostAsJsonAsync("/api/authoring/publish", pack)).EnsureSuccessStatusCode();
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/catalog/" + pack.Id);
        Assert.False(catalog.GetProperty("capabilities").GetProperty("assessments").GetBoolean());
        Assert.Empty(catalog.GetProperty("blueprints").EnumerateArray());
        Assert.False(catalog.TryGetProperty("questions", out _));
        foreach (var mode in new[] { "learn", "mock" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/me/attempts",
                new { packId = pack.Id, blueprintId = "short", mode, requestId = Guid.NewGuid().ToString() })).StatusCode);
        (await client.PutAsync($"/api/me/courses/{pack.Id}/lessons/introduction", null)).EnsureSuccessStatusCode();
        var progress = await client.GetFromJsonAsync<JsonElement>("/api/me/courses/" + pack.Id);
        Assert.True(progress.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Empty(progress.GetProperty("nextSteps").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, progress.GetProperty("goal").GetProperty("readiness").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me/export")).StatusCode);
    }

    [Theory]
    [InlineData(true, false, "learn", "mock")]
    [InlineData(false, true, "mock", "learn")]
    public async Task API_enforces_mode_capabilities_and_snapshots_the_goal(bool practice, bool assessments, string allowed, string denied)
    {
        var client = await TestApi.Publisher(factory);
        var pack = PackStarter.Create(ProductProfile.Exam) with { Id = "mode-" + Guid.NewGuid().ToString("N"),
            Capabilities = new(false, practice, assessments), Goal = CourseGoal.Mastery, Mastery = new(1, 1) };
        (await client.PostAsJsonAsync("/api/authoring/publish", pack)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/me/attempts",
            new { packId = pack.Id, blueprintId = "short", mode = denied, requestId = Guid.NewGuid().ToString() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync($"/api/me/courses/{pack.Id}/lessons/introduction", null)).StatusCode);
        var attempt = await TestApi.Start(client, allowed, pack.Id);
        Assert.DoesNotContain("grading", attempt.GetRawText());
        Assert.DoesNotContain("explanation", attempt.GetRawText());
        var completed = await TestApi.Complete(client, attempt, pack);
        Assert.Equal("mastery", completed.GetProperty("goal").GetString());
        Assert.False(completed.GetProperty("summary").GetProperty("eligible").GetBoolean());
    }
}
