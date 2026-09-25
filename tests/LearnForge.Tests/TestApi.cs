using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using LearnForge.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LearnForge.Tests;

// Shared HTTP helpers for integration tests. Each test class owns its ApiFactory, so auth rate limits stay per class.
public static class TestApi
{
    public const string Password = "TestingPassword123";

    public static Pack LoadPack(string packId) =>
        ContentEngine.Compile(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "packs", packId + ".json"))).Pack!;

    public static async Task<HttpClient> Account(ApiFactory factory)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Token(client);
        var registration = await client.PostAsJsonAsync("/api/auth/register", new { email = Guid.NewGuid() + "@example.test", password = Password, displayName = "Test Learner" });
        registration.EnsureSuccessStatusCode();
        await Token(client);
        return client;
    }

    public static async Task Token(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    public static async Task<JsonElement> Start(HttpClient client, string mode = "mock", string packId = "reasoning-foundations",
        string blueprintId = "short", string? focus = null, string? objectiveId = null)
    {
        var response = await client.PostAsJsonAsync("/api/me/attempts", new { packId, blueprintId, mode, requestId = Guid.NewGuid().ToString(), focus, objectiveId });
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Start failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static string Id(JsonElement attempt) => attempt.GetProperty("id").GetString()!;

    public static string[] QuestionIds(JsonElement attempt) =>
        attempt.GetProperty("questions").EnumerateArray().Select(q => q.GetProperty("id").GetString()!).ToArray();

    public static Answer Correct(Pack pack, string questionId)
    {
        var q = pack.Questions.Single(q => q.Id == questionId);
        return new(q.Grading.Correct, q.Grading.Matches ?? []);
    }

    // Saves one response and returns the next expected revision.
    public static async Task<int> Save(HttpClient client, JsonElement attempt, int revision, string questionId, Answer answer, bool check = false)
    {
        var response = await client.PutAsJsonAsync($"/api/me/attempts/{Id(attempt)}/responses", new { revision, requestId = Guid.NewGuid().ToString(), questionId, answer, check });
        response.EnsureSuccessStatusCode();
        return revision + 1;
    }

    // Answers every question correctly (or none when correct is false), submits, and returns the completed view.
    public static async Task<JsonElement> Complete(HttpClient client, JsonElement attempt, Pack pack, bool correct = true)
    {
        var revision = attempt.GetProperty("revision").GetInt32();
        if (correct) foreach (var id in QuestionIds(attempt)) revision = await Save(client, attempt, revision, id, Correct(pack, id));
        var submit = await client.PostAsJsonAsync($"/api/me/attempts/{Id(attempt)}/submit", new { revision });
        submit.EnsureSuccessStatusCode();
        return await submit.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<HttpClient> Publisher(ApiFactory factory)
    {
        var client = await Account(factory);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync("Publisher")) await roles.CreateAsync(new("Publisher"));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            await users.AddToRoleAsync((await users.FindByIdAsync(me.GetProperty("id").GetString()!))!, "Publisher");
        }
        await client.PostAsJsonAsync("/api/auth/logout", new { });
        await Token(client);
        (await client.PostAsJsonAsync("/api/auth/login", new { email = me.GetProperty("email").GetString(), password = Password })).EnsureSuccessStatusCode();
        await Token(client);
        return client;
    }
}
