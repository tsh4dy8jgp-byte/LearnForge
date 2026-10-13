using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class FlashcardApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<string> Publish(HttpClient publisher, Pack? content = null)
    {
        var pack = (content ?? FlashcardTests.Sample()) with { Id = "cards-" + Guid.NewGuid().ToString("N") };
        (await publisher.PostAsJsonAsync("/api/authoring/publish", pack)).EnsureSuccessStatusCode(); return pack.Id;
    }
    private static string Base(string pack) => $"/api/me/courses/{pack}/flashcards";
    private static async Task<JsonElement> Start(HttpClient client, string pack, string? requestId = null)
    {
        var response = await client.PostAsJsonAsync(Base(pack) + "/sessions", new { requestId = requestId ?? Guid.NewGuid().ToString() });
        response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<JsonElement> Action(HttpClient client, string pack, JsonElement session, string operation, string? rating = null)
    {
        var response = await client.PostAsJsonAsync(Base(pack) + $"/sessions/{TestApi.Id(session)}/{operation}", new {
            requestId = Guid.NewGuid().ToString(), revision = session.GetProperty("revision").GetInt32(), rating });
        response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Fact] public async Task Review_requires_reveal_replays_safely_and_stays_separate_from_evidence()
    {
        var client = await TestApi.Publisher(factory); var pack = await Publish(client);
        var key = Guid.NewGuid().ToString(); var session = await Start(client, pack, key);
        Assert.Equal(TestApi.Id(session), TestApi.Id(await Start(client, pack, key)));
        Assert.Equal(JsonValueKind.Null, session.GetProperty("card").GetProperty("back").ValueKind);
        var path = Base(pack) + $"/sessions/{TestApi.Id(session)}";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/rate", new { requestId = "premature", revision = 0, rating = "good" })).StatusCode);
        session = await Action(client, pack, session, "reveal");
        Assert.NotEqual(JsonValueKind.Null, session.GetProperty("card").GetProperty("back").ValueKind);
        var payload = new { requestId = Guid.NewGuid().ToString(), revision = 1, rating = "good" };
        var rated = await client.PostAsJsonAsync(path + "/rate", payload); rated.EnsureSuccessStatusCode();
        var replay = await client.PostAsJsonAsync(path + "/rate", payload); replay.EnsureSuccessStatusCode();
        Assert.Equal(await rated.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/rate", new { payload.requestId, revision = 1, rating = "easy" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/reveal", new { requestId = "stale", revision = 0 })).StatusCode);
        var progress = await client.GetFromJsonAsync<JsonElement>($"/api/me/courses/{pack}");
        Assert.Equal(1, progress.GetProperty("flashcards").GetProperty("introduced").GetInt32());
        Assert.All(progress.GetProperty("objectives").EnumerateArray(), o => Assert.Equal("notStarted", o.GetProperty("state").GetString()));
        var export = await client.GetFromJsonAsync<JsonElement>("/api/me/export");
        Assert.Empty(export.GetProperty("evidence").EnumerateArray());
        Assert.Single(export.GetProperty("flashcards").GetProperty("schedules").EnumerateArray());
    }
    [Fact] public async Task Ownership_suspension_and_deletion_are_enforced()
    {
        var alice = await TestApi.Publisher(factory); var pack = await Publish(alice);
        var bob = await TestApi.Account(factory); var session = await Start(alice, pack);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(Base(pack) + $"/sessions/{TestApi.Id(session)}")).StatusCode);
        var summary = await alice.GetFromJsonAsync<JsonElement>(Base(pack) + "/");
        var card = summary.GetProperty("cards")[0];
        (await alice.PutAsJsonAsync(Base(pack) + "/suspension", new { unitId = card.GetProperty("unitId").GetString(), suspended = true, revision = 0 })).EnsureSuccessStatusCode();
        Assert.Equal(1, (await alice.GetFromJsonAsync<JsonElement>(Base(pack) + "/")).GetProperty("suspended").GetInt32());
        session = await Action(alice, pack, session, "reveal"); await Action(alice, pack, session, "rate", "again");
        var me = await alice.GetFromJsonAsync<JsonElement>("/api/auth/me"); var userId = me.GetProperty("id").GetString()!;
        (await alice.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me/account") { Content = JsonContent.Create(new { password = TestApi.Password }) })).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.False(await db.FlashcardStates.AnyAsync(s => s.UserId == userId));
        Assert.False(await db.FlashcardSessions.AnyAsync(s => s.UserId == userId));
        Assert.False(await db.FlashcardEvents.AnyAsync(s => s.UserId == userId));
    }
    [Fact] public async Task Pinned_sessions_do_not_overwrite_revised_content_and_siblings_are_separate()
    {
        var client = await TestApi.Publisher(factory); var packId = await Publish(client);
        var session = await Start(client, packId); Assert.Equal(9, session.GetProperty("count").GetInt32());
        var currentId = session.GetProperty("card").GetProperty("id").GetString();
        var pack = FlashcardTests.Sample() with { Id = packId, Version = "1.0.1" };
        pack = pack with { FlashcardDecks = [pack.Decks[0] with { Notes = pack.Decks[0].Notes.Select(n => n with { Back = n.Kind == FlashcardKind.Cloze ? null : n.Back + " Revised." }).ToArray() }] };
        (await client.PostAsJsonAsync("/api/authoring/publish", pack)).EnsureSuccessStatusCode();
        session = await Action(client, packId, session, "reveal"); await Action(client, packId, session, "rate", "good");
        var summary = await client.GetFromJsonAsync<JsonElement>(Base(packId) + "/");
        Assert.False(summary.GetProperty("cards").EnumerateArray().Single(c => c.GetProperty("unitId").GetString() == currentId).GetProperty("introduced").GetBoolean());
    }
    [Fact] public async Task Export_rejects_invalid_filters_settings_and_unpublished_versions()
    {
        var client = await TestApi.Publisher(factory); var pack = await Publish(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/catalog/{pack}/flashcards/exports?version=99.0")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"/api/catalog/{pack}/flashcards/exports?deck=missing")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"/api/catalog/{pack}/flashcards/exports?offsetX=10")).StatusCode);
    }
    [Theory]
    [InlineData("format=apkg")]
    [InlineData("paper=a4")]
    [InlineData("mode=duplex")]
    [InlineData("binding=long")]
    [InlineData("offsetX=0&offsetY=0")]
    [InlineData("format=apkg&paper=a4&mode=duplex&binding=long&offsetX=0&offsetY=0")]
    public async Task Export_query_settings_bind_with_browser_values(string query)
    {
        var client = await TestApi.Publisher(factory); var pack = await Publish(client);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"/api/catalog/{pack}/flashcards/exports?deck=missing&{query}")).StatusCode);
    }
}
