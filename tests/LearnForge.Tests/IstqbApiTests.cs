using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using LearnForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class IstqbApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(25, false)]
    [InlineData(26, true)]
    public async Task Full_mock_results_use_the_inclusive_pass_boundary(int correct, bool passed)
    {
        var client = await TestApi.Account(factory);
        var pack = TestApi.LoadPack("istqb-ctfl-4");
        var a = await TestApi.Start(client, packId: pack.Id, blueprintId: "paper-a");
        Assert.Equal(JsonValueKind.Null, a.GetProperty("summary").ValueKind);
        Assert.All(a.GetProperty("questions").EnumerateArray(), q => Assert.False(q.TryGetProperty("grading", out _)));
        var reloaded = await client.GetFromJsonAsync<JsonElement>($"/api/me/attempts/{TestApi.Id(a)}");
        Assert.EndsWith("Z", reloaded.GetProperty("startedAt").GetString());
        Assert.EndsWith("Z", reloaded.GetProperty("deadline").GetString());
        Assert.Equal(TimeSpan.FromMinutes(60), reloaded.GetProperty("deadline").GetDateTime() - reloaded.GetProperty("startedAt").GetDateTime());
        var revision = 0;
        foreach (var id in TestApi.QuestionIds(a).Take(correct))
            revision = await TestApi.Save(client, a, revision, id, TestApi.Correct(pack, id));
        var result = await client.PostAsJsonAsync($"/api/me/attempts/{TestApi.Id(a)}/submit", new { revision });
        result.EnsureSuccessStatusCode();
        var view = await result.Content.ReadFromJsonAsync<JsonElement>();
        var summary = view.GetProperty("summary");
        Assert.EndsWith("Z", view.GetProperty("completedAt").GetString());
        Assert.Equal(correct, summary.GetProperty("earned").GetDecimal());
        Assert.Equal(40, summary.GetProperty("possible").GetDecimal());
        Assert.Equal(26, summary.GetProperty("passPoints").GetDecimal());
        Assert.Equal(passed, summary.GetProperty("passed").GetBoolean());
        var repeat = await TestApi.Start(client, packId: pack.Id, blueprintId: "paper-a-extended");
        Assert.Equal(TestApi.QuestionIds(a), TestApi.QuestionIds(repeat));
        Assert.Equal(JsonValueKind.Null, repeat.GetProperty("summary").ValueKind);
        Assert.Equal(TimeSpan.FromMinutes(75), repeat.GetProperty("deadline").GetDateTime() - repeat.GetProperty("startedAt").GetDateTime());
        var repeatedResult = await TestApi.Complete(client, repeat, pack, correct: false);
        Assert.Equal(0, repeatedResult.GetProperty("summary").GetProperty("freshPercent").GetDecimal());
    }

    [Theory]
    [InlineData("istqb-ct-ai-2", false)]
    [InlineData("istqb-ct-ai-2", true)]
    [InlineData("istqb-ct-genai-1", false)]
    [InlineData("istqb-ct-genai-1", true)]
    public async Task Specialist_mocks_score_weighted_points_against_the_official_pass_mark(string packId, bool passed)
    {
        var spec = IstqbExamSpec.For(packId);
        var client = await TestApi.Account(factory);
        var pack = TestApi.LoadPack(packId);
        var a = await TestApi.Start(client, packId: pack.Id, blueprintId: "paper-a");
        Assert.All(a.GetProperty("questions").EnumerateArray(), q => Assert.False(q.TryGetProperty("grading", out _)));
        Assert.Equal(TimeSpan.FromMinutes(60), a.GetProperty("deadline").GetDateTime() - a.GetProperty("startedAt").GetDateTime());
        // K3 questions are worth two points, so answer until the earned points sit exactly on or just below the pass mark.
        var target = spec.PassPoints - (passed ? 0 : 1);
        decimal earned = 0;
        var revision = 0;
        foreach (var id in TestApi.QuestionIds(a))
        {
            var weight = pack.Questions.Single(q => q.Id == id).Weight;
            if (earned + weight > target) continue;
            revision = await TestApi.Save(client, a, revision, id, TestApi.Correct(pack, id));
            earned += weight;
        }
        Assert.Equal(target, earned);
        var result = await client.PostAsJsonAsync($"/api/me/attempts/{TestApi.Id(a)}/submit", new { revision });
        result.EnsureSuccessStatusCode();
        var summary = (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("summary");
        Assert.Equal(target, summary.GetProperty("earned").GetDecimal());
        Assert.Equal(spec.TotalPoints, summary.GetProperty("possible").GetDecimal());
        Assert.Equal(spec.PassPoints, summary.GetProperty("passPoints").GetDecimal());
        Assert.Equal(passed, summary.GetProperty("passed").GetBoolean());
        var repeat = await TestApi.Start(client, packId: pack.Id, blueprintId: "paper-a-extended");
        Assert.Equal(TestApi.QuestionIds(a), TestApi.QuestionIds(repeat));
        Assert.Equal(TimeSpan.FromMinutes(75), repeat.GetProperty("deadline").GetDateTime() - repeat.GetProperty("startedAt").GetDateTime());
    }

    [Fact]
    public async Task Expiry_scores_saved_answers_and_learning_does_not_show_exam_pass_fail()
    {
        var client = await TestApi.Account(factory);
        var pack = TestApi.LoadPack("istqb-ctfl-4");
        var a = await TestApi.Start(client, packId: pack.Id, blueprintId: "paper-b");
        var revision = 0;
        foreach (var id in TestApi.QuestionIds(a).Take(26))
            revision = await TestApi.Save(client, a, revision, id, TestApi.Correct(pack, id));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var row = await db.Attempts.SingleAsync(x => x.Id == TestApi.Id(a));
            row.Deadline = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        var expired = await client.GetFromJsonAsync<JsonElement>($"/api/me/attempts/{TestApi.Id(a)}");
        Assert.True(expired.GetProperty("timedOut").GetBoolean());
        Assert.True(expired.GetProperty("summary").GetProperty("passed").GetBoolean());
        Assert.False(expired.GetProperty("summary").GetProperty("eligible").GetBoolean());
        var learn = await TestApi.Start(client, "learn", pack.Id, "paper-b", "objective", "fl-4.2.1");
        Assert.All(learn.GetProperty("questions").EnumerateArray(), q => Assert.Contains("fl-4.2.1", q.GetProperty("objectiveIds").EnumerateArray().Select(o => o.GetString())));
        var learned = await TestApi.Complete(client, learn, pack);
        Assert.Equal(JsonValueKind.Null, learned.GetProperty("summary").GetProperty("passed").ValueKind);
        Assert.Equal(JsonValueKind.Null, learned.GetProperty("summary").GetProperty("passPoints").ValueKind);
    }

    [Fact]
    public async Task A_new_release_cannot_change_an_existing_attempts_pass_threshold()
    {
        var publisher = await TestApi.Publisher(factory);
        var original = TestApi.LoadPack("istqb-ctfl-4") with { Id = "snapshot-exam" };
        (await publisher.PostAsJsonAsync("/api/authoring/publish", original)).EnsureSuccessStatusCode();
        var client = await TestApi.Account(factory);
        var a = await TestApi.Start(client, packId: original.Id, blueprintId: "paper-c");
        var changed = original with { Version = original.Version + ".1", Blueprints = original.Blueprints.Select(b => b.PassPoints is null ? b : b with { PassPoints = 40 }).ToArray() };
        (await publisher.PostAsJsonAsync("/api/authoring/publish", changed)).EnsureSuccessStatusCode();
        var result = await TestApi.Complete(client, a, original, correct: false);
        Assert.Equal(original.Version, result.GetProperty("version").GetString());
        Assert.Equal(26, result.GetProperty("summary").GetProperty("passPoints").GetDecimal());
    }
}
