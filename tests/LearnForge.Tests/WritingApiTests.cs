using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using LearnForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class WritingApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string Base(string pack) => $"/api/me/courses/{pack}/writing";
    private async Task<(HttpClient Client, Pack Pack)> Course()
    {
        var client = await TestApi.Publisher(factory);
        var pack = WritingTests.Pilot() with { Id = "writing-" + Guid.NewGuid().ToString("N") };
        (await client.PostAsJsonAsync("/api/authoring/publish", pack)).EnsureSuccessStatusCode();
        return (client, pack);
    }
    private static async Task<JsonElement> Start(HttpClient client, Pack pack, string mode = "recall", string? key = null)
    {
        var response = await client.PostAsJsonAsync(Base(pack.Id) + "/sessions", new {
            characterId = pack.WritingCharacters[0].Id, mode, requestId = key ?? Guid.NewGuid().ToString(), version = pack.Version });
        response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<JsonElement> Summary(HttpClient client, Pack pack) =>
        await client.GetFromJsonAsync<JsonElement>(Base(pack.Id) + "/");
    private static async Task Finish(HttpClient client, Pack pack, JsonElement session, int hints = 0)
    {
        var response = await client.PostAsJsonAsync(Base(pack.Id) + $"/sessions/{TestApi.Id(session)}/result", new {
            requestId = Guid.NewGuid().ToString(), revision = 0, completed = true, mistakes = 0, hints });
        response.EnsureSuccessStatusCode();
    }
    [Fact] public async Task Results_are_idempotent_owned_and_never_assessment_evidence()
    {
        var (client, pack) = await Course(); var key = Guid.NewGuid().ToString();
        var session = await Start(client, pack, key: key);
        Assert.Equal(TestApi.Id(session), TestApi.Id(await Start(client, pack, key: key)));
        var bob = await TestApi.Account(factory);
        var path = Base(pack.Id) + $"/sessions/{TestApi.Id(session)}";
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(path)).StatusCode);
        var body = new { requestId = "same-result", revision = 0, completed = true, mistakes = 0, hints = 0 };
        var result = await client.PostAsJsonAsync(path + "/result", body); result.EnsureSuccessStatusCode();
        var replay = await client.PostAsJsonAsync(path + "/result", body); replay.EnsureSuccessStatusCode();
        Assert.Equal(await result.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/result", body with { hints = 1 })).StatusCode);
        var summary = await Summary(client, pack); var state = summary.GetProperty("characters")[0];
        Assert.Equal(1, state.GetProperty("independentCompletions").GetInt32());
        Assert.Equal(0, state.GetProperty("stage").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, state.GetProperty("dueAt").ValueKind);
        var export = await client.GetFromJsonAsync<JsonElement>("/api/me/export");
        Assert.Single(export.GetProperty("writing").GetProperty("sessions").EnumerateArray());
        Assert.Empty(export.GetProperty("evidence").EnumerateArray());
        Assert.Empty(export.GetProperty("flashcards").GetProperty("schedules").EnumerateArray());
    }
    [Fact] public async Task Guided_modes_and_hints_do_not_advance_independent_writing()
    {
        var (client, pack) = await Course();
        foreach (var mode in new[] { "trace", "copy", "sequence", "watch" }) await Finish(client, pack, await Start(client, pack, mode));
        var progress = (await Summary(client, pack)).GetProperty("characters")[0];
        Assert.Equal(2, progress.GetProperty("assistedCompletions").GetInt32());
        Assert.Equal(1, progress.GetProperty("sequenceCompletions").GetInt32());
        Assert.Equal(0, progress.GetProperty("independentCompletions").GetInt32());
        Assert.Equal(JsonValueKind.Null, progress.GetProperty("dueAt").ValueKind);
        await Finish(client, pack, await Start(client, pack), hints: 1);
        progress = (await Summary(client, pack)).GetProperty("characters")[0];
        Assert.Equal(-1, progress.GetProperty("stage").GetInt32());
        Assert.Equal(0, progress.GetProperty("independentCompletions").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, progress.GetProperty("dueAt").ValueKind);
    }
    [Fact] public async Task Sessions_resume_old_releases_without_advancing_revised_characters()
    {
        var (client, pack) = await Course(); var session = await Start(client, pack);
        Assert.Equal(TestApi.Id(session), (await Summary(client, pack)).GetProperty("activeSessionId").GetString());
        var revised = pack with { Version = "1.0.1", Characters = pack.WritingCharacters.Select(c => c with { Tip = c.Tip + " Updated." }).ToArray() };
        (await client.PostAsJsonAsync("/api/authoring/publish", revised)).EnsureSuccessStatusCode();
        var resumed = await client.GetFromJsonAsync<JsonElement>(Base(pack.Id) + $"/sessions/{TestApi.Id(session)}");
        Assert.Equal("1.0.0", resumed.GetProperty("version").GetString());
        await Finish(client, pack, session);
        Assert.Equal(0, (await Summary(client, revised)).GetProperty("characters")[0].GetProperty("independentCompletions").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Base(pack.Id) + "/sessions", new {
            characterId = pack.WritingCharacters[0].Id, mode = "recall", requestId = "stale-version", version = "1.0.0" })).StatusCode);
    }
    [Fact] public async Task Invalid_results_active_session_conflicts_and_account_deletion_are_handled()
    {
        var (client, pack) = await Course(); var session = await Start(client, pack);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Base(pack.Id) + "/sessions", new {
            characterId = pack.WritingCharacters[1].Id, mode = "trace", requestId = "other", version = pack.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Base(pack.Id) + $"/sessions/{TestApi.Id(session)}/result", new {
            requestId = "bad", revision = 0, completed = true, mistakes = -1, hints = 0 })).StatusCode);
        await Finish(client, pack, session);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me"); var id = me.GetProperty("id").GetString()!;
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me/account") { Content = JsonContent.Create(new { password = TestApi.Password }) })).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.False(await db.WritingSessions.AnyAsync(s => s.UserId == id));
        Assert.False(await db.WritingStates.AnyAsync(s => s.UserId == id));
    }
    [Fact] public async Task Publishing_rejects_missing_audio_assets()
    {
        var (client, pack) = await Course();
        var invalid = pack with { Version = "1.0.1", Characters = [pack.WritingCharacters[0] with { AudioUrl = "/writing/audio/missing.wav" }] };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/authoring/publish", invalid)).StatusCode);
    }
}
