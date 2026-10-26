using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class FlashcardStudyApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<string> Publish(HttpClient publisher, Pack? content = null)
    {
        var pack = (content ?? FlashcardTests.Sample()) with { Id = "cards-" + Guid.NewGuid().ToString("N") };
        (await publisher.PostAsJsonAsync("/api/authoring/publish", pack)).EnsureSuccessStatusCode(); return pack.Id;
    }
    private static string Base(string pack) => $"/api/me/courses/{pack}/flashcards";
    private static async Task<JsonElement> Action(HttpClient client, string pack, JsonElement session, string operation, string? rating = null)
    {
        var response = await client.PostAsJsonAsync(Base(pack) + $"/sessions/{TestApi.Id(session)}/{operation}", new {
            requestId = Guid.NewGuid().ToString(), revision = session.GetProperty("revision").GetInt32(), rating });
        response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static Pack Bulk(FlashcardKind kind, int notes) => FlashcardTests.Sample() with
    {
        FlashcardDecks = [new("bulk", "Bulk", Enumerable.Range(1, notes).Select(i =>
            new FlashcardNote($"note-{i}", kind, ["recall"], "Explanation.", $"Prompt {i}?", $"Answer {i}.")).ToArray())]
    };
    [Fact] public async Task Short_study_sessions_are_bounded_idempotent_and_take_due_cards_first()
    {
        var client = await TestApi.Publisher(factory); var pack = await Publish(client, Bulk(FlashcardKind.Basic, 12));
        var key = Guid.NewGuid().ToString();
        var body = new { requestId = key, deck = "bulk", limit = 2 };
        var response = await client.PostAsJsonAsync(Base(pack) + "/sessions", body); response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, session.GetProperty("count").GetInt32());
        var replay = await client.PostAsJsonAsync(Base(pack) + "/sessions", body);
        Assert.Equal(await response.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Base(pack) + "/sessions", body with { limit = 3 })).StatusCode);
        for (var i = 0; i < 2; i++) session = await Action(client, pack, await Action(client, pack, session, "reveal"), "rate", "good");
        Assert.True(session.GetProperty("completed").GetBoolean());
        var user = (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString()!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var states = await db.FlashcardStates.Where(s => s.UserId == user && s.PackId == pack).ToArrayAsync();
            Assert.Equal(2, states.Length);
            foreach (var state in states) state.DueAt = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }
        var due = await client.PostAsJsonAsync(Base(pack) + "/sessions", new { requestId = Guid.NewGuid().ToString(), deck = "bulk", limit = 1 });
        due.EnsureSuccessStatusCode(); session = await due.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, session.GetProperty("count").GetInt32());
        Assert.Contains(session.GetProperty("card").GetProperty("noteId").GetString(), new[] { "note-1", "note-2" });
    }
    [Theory]
    [InlineData(0)] [InlineData(21)]
    public async Task Short_study_limit_is_validated(int limit)
    {
        var client = await TestApi.Publisher(factory); var pack = await Publish(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Base(pack) + "/sessions", new { requestId = "invalid-limit", limit })).StatusCode);
    }
}
