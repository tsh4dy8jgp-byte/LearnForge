using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class PrivacyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact] public async Task Export_includes_learning_records_and_deletion_removes_them()
    {
        var pack = TestApi.LoadPack("reasoning-foundations");
        var client = await TestApi.Account(factory);
        await client.PutAsync($"/api/me/courses/{pack.Id}/lessons/sets-intro", null);
        var attempt = await TestApi.Start(client);
        await TestApi.Complete(client, attempt, pack);

        var export = await client.GetFromJsonAsync<JsonElement>("/api/me/export");
        Assert.Single(export.GetProperty("enrollments").EnumerateArray());
        Assert.Single(export.GetProperty("lessonProgress").EnumerateArray());
        Assert.Equal(TestApi.QuestionIds(attempt).Length, export.GetProperty("evidence").GetArrayLength());

        var userId = (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString()!;
        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/me/account") { Content = JsonContent.Create(new { password = TestApi.Password }) };
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.False(await db.Enrollments.AnyAsync(e => e.UserId == userId));
        Assert.False(await db.LessonProgress.AnyAsync(e => e.UserId == userId));
        Assert.False(await db.Evidence.AnyAsync(e => e.UserId == userId));
    }
}
