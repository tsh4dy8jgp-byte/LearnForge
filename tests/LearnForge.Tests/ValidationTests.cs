using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LearnForge.Tests;

public class ValidationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string[] Fields(JsonElement problem) => problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray();

    [Fact] public async Task Invalid_registration_returns_field_errors()
    {
        var client = factory.CreateClient(new() { HandleCookies = true });
        await TestApi.Token(client);
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "learner@example.test", password = "short", displayName = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = Fields(await response.Content.ReadFromJsonAsync<JsonElement>());
        Assert.Contains(fields, f => f.Equals("Password", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fields, f => f.Equals("DisplayName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] public async Task Oversized_start_requests_are_rejected_before_the_handler()
    {
        var client = await TestApi.Account(factory);
        var response = await client.PostAsJsonAsync("/api/me/attempts", new { packId = new string('x', 101), blueprintId = "short", mode = "mock", requestId = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(Fields(await response.Content.ReadFromJsonAsync<JsonElement>()), f => f.Equals("PackId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] public async Task Undefined_enrollment_status_is_rejected()
    {
        var client = await TestApi.Account(factory);
        var response = await client.PutAsJsonAsync("/api/me/enrollments/reasoning-foundations", new { status = 7 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
