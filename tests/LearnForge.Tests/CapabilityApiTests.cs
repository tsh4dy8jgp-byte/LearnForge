using System.Net;
using System.Net.Http.Json;
using System.Text;
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

    private static StringContent Source(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact] public async Task Exam_sources_preview_with_quality_warnings_and_without_keys()
    {
        var publisher = await TestApi.Publisher(factory);
        var source = ExamSourceTests.SampleSource();
        var preview = await publisher.PostAsync("/api/authoring/preview", Source(source));
        preview.EnsureSuccessStatusCode();
        var body = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Empty(body.GetProperty("warnings").EnumerateArray());
        var questions = body.GetProperty("questions").GetRawText();
        Assert.DoesNotContain("grading", questions);
        Assert.DoesNotContain("explanation", questions);
        Assert.DoesNotContain("miss miss hit", questions);

        // A giveaway that still compiles: the key grows much longer than its distractors.
        var giveaway = source.Replace("\"answer\": \"301 Moved Permanently\"", "\"answer\": \"301 Moved Permanently, so clients and crawlers update the links they store\"");
        var warned = await (await publisher.PostAsync("/api/authoring/preview", Source(giveaway))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(warned.GetProperty("success").GetBoolean());
        Assert.Contains(warned.GetProperty("warnings").EnumerateArray(),
            w => w.GetProperty("code").GetString() == "LF201" && w.GetProperty("path").GetString() == "http-redirect-status");
        var validated = await (await publisher.PostAsync("/api/authoring/validate", Source(giveaway))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(validated.GetProperty("success").GetBoolean());
        Assert.NotEmpty(validated.GetProperty("warnings").EnumerateArray());
        var broken = await (await publisher.PostAsync("/api/authoring/validate", Source(source.Replace("\"exam/1\"", "\"exam/9\"")))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(broken.GetProperty("success").GetBoolean());
        Assert.Empty(broken.GetProperty("warnings").EnumerateArray());
    }

    [Fact] public async Task A_published_exam_source_runs_weighted_mocks_with_opaque_option_ids()
    {
        var publisher = await TestApi.Publisher(factory);
        var id = "exam-" + Guid.NewGuid().ToString("N")[..12];
        var source = ExamSourceTests.SampleSource().Replace("\"web-foundations-sample\"", "\"" + id + "\"");
        (await publisher.PostAsync("/api/authoring/publish", Source(source))).EnsureSuccessStatusCode();
        var catalog = await publisher.GetFromJsonAsync<JsonElement>("/api/catalog/" + id);
        Assert.Equal("exam", catalog.GetProperty("profile").GetString());
        Assert.Equal(40, catalog.GetProperty("blueprints")[1].GetProperty("objectiveWeights").GetProperty("http").GetDecimal());

        var attempt = await TestApi.Start(publisher, "mock", id, "full");
        var delivered = attempt.GetProperty("questions").EnumerateArray().ToArray();
        Assert.Equal(10, delivered.Length);
        var options = delivered.SelectMany(q => q.GetProperty("options").EnumerateArray()
            .Concat(q.GetProperty("slots").EnumerateArray().SelectMany(s => s.GetProperty("options").EnumerateArray())));
        Assert.All(options, o => Assert.Matches("^o[0-9a-f]{8}$", o.GetProperty("id").GetString()));

        // Case studies are locked sections here, so answer the standalone section and submit.
        var pack = ContentEngine.Compile(source).Pack!;
        var revision = attempt.GetProperty("revision").GetInt32();
        var standalone = delivered.Where(q => q.GetProperty("scenarioId").ValueKind == JsonValueKind.Null).Select(q => q.GetProperty("id").GetString()!).ToArray();
        foreach (var question in standalone)
        {
            var key = pack.Questions.Single(q => q.Id == question);
            var answer = key.Kind is QuestionKind.Numeric or QuestionKind.CodeOutput ? new Answer([], [], key.Grading.Correct[0]) : TestApi.Correct(pack, question);
            revision = await TestApi.Save(publisher, attempt, revision, question, answer);
        }
        var submit = await publisher.PostAsJsonAsync($"/api/me/attempts/{TestApi.Id(attempt)}/submit", new { revision });
        submit.EnsureSuccessStatusCode();
        var results = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("results");
        Assert.All(standalone, q => Assert.True(results.GetProperty(q).GetProperty("fullyCorrect").GetBoolean(), q));
    }
}
