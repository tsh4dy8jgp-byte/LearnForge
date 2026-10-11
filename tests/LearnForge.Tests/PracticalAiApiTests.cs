using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LearnForge.Tests;

public class PracticalAiApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Pack pack = TestApi.LoadPack("ai-testing-genai-practical");

    [Fact] public async Task Learner_discovers_enrolls_completes_and_practises_without_mock_or_key_leaks()
    {
        var anonymous = factory.CreateClient();
        var library = await anonymous.GetFromJsonAsync<JsonElement>("/api/catalog");
        Assert.Contains(library.EnumerateArray(), course => course.GetProperty("id").GetString() == pack.Id);
        var catalog = await anonymous.GetFromJsonAsync<JsonElement>("/api/catalog/" + pack.Id);
        Assert.Equal("course", catalog.GetProperty("profile").GetString());
        Assert.Equal("mastery", catalog.GetProperty("goal").GetString());
        Assert.False(catalog.GetProperty("capabilities").GetProperty("assessments").GetBoolean());
        Assert.Equal(16, catalog.GetProperty("lessons").GetArrayLength());
        Assert.False(catalog.TryGetProperty("questions", out _));
        Assert.DoesNotContain("grading", catalog.GetRawText());

        var client = await TestApi.Account(factory);
        (await client.PutAsJsonAsync($"/api/me/enrollments/{pack.Id}", new { status = "active" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/me/courses/{pack.Id}/lessons/orientation", null)).StatusCode);
        var before = await Progress(client);
        Assert.Equal("active", before.GetProperty("enrollment").GetString());
        Assert.Contains(before.GetProperty("completedLessons").EnumerateArray(), id => id.GetString() == "orientation");
        Assert.False(before.GetProperty("goal").GetProperty("met").GetBoolean());

        var denied = await client.PostAsJsonAsync("/api/me/attempts", new
        {
            packId = pack.Id, blueprintId = "quick-check", mode = "mock", requestId = Guid.NewGuid().ToString()
        });
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        var session = await TestApi.Start(client, "learn", pack.Id, "quick-check");
        Assert.Equal(16, TestApi.QuestionIds(session).Length);
        Assert.Equal("learn", session.GetProperty("mode").GetString());
        Assert.All(session.GetProperty("questions").EnumerateArray(), question =>
        {
            Assert.False(question.TryGetProperty("grading", out _));
            Assert.False(question.TryGetProperty("explanation", out _));
        });
        await AnswerAndCheck(client, session);
        var checkedSession = await client.GetFromJsonAsync<JsonElement>("/api/me/attempts/" + TestApi.Id(session));
        Assert.Equal(16, checkedSession.GetProperty("results").EnumerateObject().Count());
        Assert.Contains("explanation", checkedSession.GetProperty("results").GetRawText());
        var progress = await Progress(client);
        Assert.All(progress.GetProperty("objectives").EnumerateArray(), objective =>
        {
            Assert.Equal(1, objective.GetProperty("considered").GetInt32());
            Assert.Equal("emerging", objective.GetProperty("state").GetString());
        });
        Assert.Equal(JsonValueKind.Null, progress.GetProperty("goal").GetProperty("readiness").ValueKind);

        var focused = await TestApi.Start(client, "learn", pack.Id, "quick-check", "objective", "qa-metrics");
        Assert.Equal(5, TestApi.QuestionIds(focused).Length);
        await AnswerAndCheck(client, focused);
        var metrics = (await Progress(client)).GetProperty("objectives").EnumerateArray()
            .Single(o => o.GetProperty("id").GetString() == "qa-metrics");
        Assert.Equal("proficient", metrics.GetProperty("state").GetString());
        Assert.Equal(5, metrics.GetProperty("considered").GetInt32());
        Assert.Equal(5, metrics.GetProperty("correct").GetInt32());
    }

    private Task<JsonElement> Progress(HttpClient client) => client.GetFromJsonAsync<JsonElement>("/api/me/courses/" + pack.Id);

    private async Task AnswerAndCheck(HttpClient client, JsonElement session)
    {
        var revision = session.GetProperty("revision").GetInt32();
        foreach (var id in TestApi.QuestionIds(session))
        {
            var question = pack.Questions.Single(q => q.Id == id);
            var answer = question.Kind == QuestionKind.Numeric
                ? new Answer([], [], question.Grading.Correct.Single())
                : TestApi.Correct(pack, id);
            revision = await TestApi.Save(client, session, revision, id, answer, check: true);
        }
        var submitted = await client.PostAsJsonAsync($"/api/me/attempts/{TestApi.Id(session)}/submit", new { revision });
        submitted.EnsureSuccessStatusCode();
        var completed = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(100m, completed.GetProperty("summary").GetProperty("correctPercent").GetDecimal());
        Assert.False(completed.GetProperty("summary").GetProperty("eligible").GetBoolean());
    }
}
