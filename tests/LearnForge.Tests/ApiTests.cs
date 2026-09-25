using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using LearnForge.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace LearnForge.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string database = Path.Combine(Path.GetTempPath(), "learnforge-test-" + Guid.NewGuid().ToString("N") + ".db");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Database:Provider"] = Environment.GetEnvironmentVariable("LEARNFORGE_TEST_POSTGRES") is null ? "Sqlite" : "Postgres",
            ["ConnectionStrings:Database"] = Environment.GetEnvironmentVariable("LEARNFORGE_TEST_POSTGRES") ?? "Data Source=" + database,
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        // Minimal-host configuration callbacks run after Program's provider branch.
        if (Environment.GetEnvironmentVariable("LEARNFORGE_TEST_POSTGRES") is { } postgres)
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<AppDb>();
                services.RemoveAll<DbContextOptions<AppDb>>();
                services.AddDbContext<PostgresDb>(o => o.UseNpgsql(postgres));
                services.AddScoped<AppDb>(s => s.GetRequiredService<PostgresDb>());
            });
    }
}

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Account()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Token(client);
        var registration = await client.PostAsJsonAsync("/api/auth/register", new { email = Guid.NewGuid()+"@example.test", password = "TestingPassword123", displayName = "Test Learner" });
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        await Token(client); return client;
    }
    private static async Task Token(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
    private static async Task<JsonElement> Start(HttpClient client, string mode = "mock", string? requestId = null)
    {
        var response = await client.PostAsJsonAsync("/api/me/attempts", new StartRequest("reasoning-foundations", "short", Enum.Parse<AssessmentMode>(mode, true), requestId ?? Guid.NewGuid().ToString()));
        response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Fact] public async Task Auth_csrf_ownership_and_publisher_boundaries_are_enforced()
    {
        var anonymous = factory.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/dashboard")).StatusCode);
        var alice = await Account(); var bob = await Account(); var attempt = await Start(alice);
        var id = attempt.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync("/api/me/attempts/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsJsonAsync("/api/authoring/publish", new {})).StatusCode);
        alice.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/me/attempts/" + id + "/submit", new { revision = 0 })).StatusCode);
    }
    [Fact] public async Task Attempts_resume_score_on_server_and_replay_safely()
    {
        var client = await Account(); var requestId = Guid.NewGuid().ToString();
        var a = await Start(client, requestId:requestId); var id = a.GetProperty("id").GetString();
        var replay = await Start(client, requestId:requestId);
        Assert.Equal(id, replay.GetProperty("id").GetString());
        Assert.False(a.GetProperty("questions")[0].TryGetProperty("grading", out _));
        Assert.Equal(JsonValueKind.Null, a.GetProperty("results").ValueKind);
        var pack = CoreTests.Demo(); var revision = 0;
        foreach (var item in a.GetProperty("questions").EnumerateArray())
        {
            var q = pack.Questions.Single(q => q.Id == item.GetProperty("id").GetString());
            var payload = new ResponseRequest(revision, Guid.NewGuid().ToString(), q.Id, new(q.Grading.Correct, q.Grading.Matches ?? []));
            var save = await client.PutAsJsonAsync($"/api/me/attempts/{id}/responses", payload);
            Assert.Equal(HttpStatusCode.OK, save.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/me/attempts/{id}/responses", payload)).StatusCode);
            revision++;
        }
        var resumed = await client.GetFromJsonAsync<JsonElement>($"/api/me/attempts/{id}");
        Assert.Equal(revision, resumed.GetProperty("revision").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/me/attempts/{id}/submit", new { revision = 0 })).StatusCode);
        var submit = await client.PostAsJsonAsync($"/api/me/attempts/{id}/submit", new { revision });
        var result = await submit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(100, result.GetProperty("summary").GetProperty("correctPercent").GetDecimal());
        Assert.True(result.GetProperty("summary").GetProperty("eligible").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/me/attempts/{id}/submit", new { revision })).StatusCode);
        Assert.Equal(a.GetProperty("questions").GetArrayLength(), result.GetProperty("results").EnumerateObject().Count());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me/export")).StatusCode);
    }
    [Fact] public async Task Expiry_is_server_enforced_and_learning_feedback_is_separate()
    {
        var client = await Account(); var a = await Start(client); var id = a.GetProperty("id").GetString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>(); var row = await db.Attempts.SingleAsync(a => a.Id == id);
            row.Deadline = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        }
        var expired = await client.GetFromJsonAsync<JsonElement>($"/api/me/attempts/{id}");
        Assert.Equal("completed", expired.GetProperty("status").GetString()); Assert.True(expired.GetProperty("timedOut").GetBoolean());
        var learning = await Start(client, "learn"); var learningId = learning.GetProperty("id").GetString();
        var question = learning.GetProperty("questions")[0].GetProperty("id").GetString()!;
        var answer = new ResponseRequest(0, Guid.NewGuid().ToString(), question, new([], []), true);
        var check = await client.PutAsJsonAsync($"/api/me/attempts/{learningId}/responses", answer);
        var checkedResult = await check.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(checkedResult.GetProperty("feedback").TryGetProperty(question, out _));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/me/attempts/{learningId}/responses", answer with { Revision = 1, RequestId = Guid.NewGuid().ToString() })).StatusCode);
    }
    [Fact] public async Task Accounts_can_delete_their_data_and_invalidate_their_session()
    {
        var client = await Account(); var a = await Start(client);
        var delete = new HttpRequestMessage(HttpMethod.Delete,"/api/me/account") { Content = JsonContent.Create(new { password = "TestingPassword123" }) };
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDb>().Attempts.AnyAsync(x => x.Id == a.GetProperty("id").GetString()));
    }

    [Fact] public async Task Concurrent_responses_accept_one_revision_without_lost_updates()
    {
        var client = await Account(); var a = await Start(client); var id = a.GetProperty("id").GetString();
        var qid = a.GetProperty("questions")[0].GetProperty("id").GetString()!;
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PutAsJsonAsync(
            $"/api/me/attempts/{id}/responses", new ResponseRequest(0, Guid.NewGuid().ToString(), qid, new([], [])))));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal(1, await db.Responses.CountAsync(r => r.AttemptId == id));
    }

    [Fact] public async Task Section_locks_reject_both_future_and_previous_section_writes()
    {
        var client = await Account();
        var start = await client.PostAsJsonAsync("/api/me/attempts", new StartRequest("evidence-lab", "full", AssessmentMode.Mock, Guid.NewGuid().ToString()));
        start.EnsureSuccessStatusCode(); var a = await start.Content.ReadFromJsonAsync<JsonElement>();
        var id = a.GetProperty("id").GetString(); var sections = a.GetProperty("sections");
        var questions = a.GetProperty("questions").EnumerateArray().ToArray();
        var oldQuestion = questions.First(q => q.GetProperty("scenarioId").GetString() == sections[0].GetString()).GetProperty("id").GetString()!;
        var futureQuestion = questions.First(q => q.GetProperty("scenarioId").GetString() == sections[1].GetString()).GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/me/attempts/{id}/responses", new ResponseRequest(0, Guid.NewGuid().ToString(), futureQuestion, new([], [])))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/me/attempts/{id}/section", new { revision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/me/attempts/{id}/responses", new ResponseRequest(1, Guid.NewGuid().ToString(), oldQuestion, new([], [])))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/me/attempts/{id}/responses", new ResponseRequest(1, Guid.NewGuid().ToString(), futureQuestion, new([], [])))).StatusCode);
    }

    [Fact] public async Task Publishing_is_immutable_and_does_not_rewrite_existing_attempts()
    {
        var client = await Account(); var user = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync("Publisher")) await roles.CreateAsync(new("Publisher"));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            await users.AddToRoleAsync((await users.FindByIdAsync(user.GetProperty("id").GetString()!))!, "Publisher");
        }
        await client.PostAsJsonAsync("/api/auth/logout", new {}); await Token(client);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = user.GetProperty("email").GetString(), password = "TestingPassword123" });
        login.EnsureSuccessStatusCode(); await Token(client);
        var pack = CoreTests.Demo() with { Id = "publication-" + Guid.NewGuid().ToString("N") };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/authoring/publish", pack)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/authoring/publish", pack)).StatusCode);
        var start = await client.PostAsJsonAsync("/api/me/attempts", new StartRequest(pack.Id, "short", AssessmentMode.Mock, Guid.NewGuid().ToString()));
        start.EnsureSuccessStatusCode(); var attempt = await start.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/authoring/publish", pack with { Version = "2.0.0", Title = "New title" })).StatusCode);
        var saved = await client.GetFromJsonAsync<JsonElement>("/api/me/attempts/" + attempt.GetProperty("id").GetString());
        Assert.Equal("1.0.0", saved.GetProperty("version").GetString());
        Assert.Equal(pack.Title, saved.GetProperty("title").GetString());
        var current = await client.GetFromJsonAsync<JsonElement>("/api/catalog/" + pack.Id);
        Assert.Equal("2.0.0", current.GetProperty("version").GetString()); Assert.False(current.TryGetProperty("questions", out _));
    }

    [Fact] public async Task Dashboard_readiness_tracks_five_fresh_successes_and_a_later_failure()
    {
        var client = await Account(); var pack = CoreTests.Demo();
        for (var i = 0; i < 5; i++)
        {
            var attempt = await Start(client); var id = attempt.GetProperty("id").GetString(); var revision = 0;
            foreach (var item in attempt.GetProperty("questions").EnumerateArray())
            {
                var q = pack.Questions.Single(q => q.Id == item.GetProperty("id").GetString());
                var response = await client.PutAsJsonAsync($"/api/me/attempts/{id}/responses", new ResponseRequest(revision++, Guid.NewGuid().ToString(), q.Id, new(q.Grading.Correct, q.Grading.Matches ?? [])));
                response.EnsureSuccessStatusCode();
            }
            (await client.PostAsJsonAsync($"/api/me/attempts/{id}/submit", new { revision })).EnsureSuccessStatusCode();
        }
        async Task<bool> Ready()
        {
            var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/me/dashboard");
            return dashboard.GetProperty("courses").EnumerateArray().Single(c => c.GetProperty("id").GetString() == pack.Id).GetProperty("readiness").GetProperty("ready").GetBoolean();
        }
        Assert.True(await Ready());
        var failed = await Start(client);
        (await client.PostAsJsonAsync("/api/me/attempts/" + failed.GetProperty("id").GetString() + "/submit", new { revision = 0 })).EnsureSuccessStatusCode();
        Assert.False(await Ready());
    }
}
