using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using LearnForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class EvidenceLedgerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Pack pack = TestApi.LoadPack("reasoning-foundations");

    private async Task<List<EvidenceRecord>> Evidence(string attemptId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDb>().Evidence.Where(e => e.AttemptId == attemptId).ToListAsync();
    }

    [Fact] public async Task A_learning_check_records_once_and_submission_records_the_rest()
    {
        var client = await TestApi.Account(factory);
        var attempt = await TestApi.Start(client, "learn");
        var attemptId = TestApi.Id(attempt);
        var ids = TestApi.QuestionIds(attempt);
        var check = new { revision = 0, requestId = Guid.NewGuid().ToString(), questionId = ids[0], answer = TestApi.Correct(pack, ids[0]), check = true };
        (await client.PutAsJsonAsync($"/api/me/attempts/{attemptId}/responses", check)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/me/attempts/{attemptId}/responses", check)).EnsureSuccessStatusCode();
        Assert.Single(await Evidence(attemptId));

        (await client.PostAsJsonAsync($"/api/me/attempts/{attemptId}/submit", new { revision = 1 })).EnsureSuccessStatusCode();

        var rows = await Evidence(attemptId);
        Assert.Equal(ids.Length, rows.Count);
        var checkedRow = rows.Single(r => r.QuestionId == ids[0]);
        Assert.Equal(EvidenceSource.LearningCheck, checkedRow.Source);
        Assert.True(checkedRow.Answered && checkedRow.FullyCorrect);
        var question = pack.Questions.Single(q => q.Id == ids[0]);
        Assert.Equal(question.FamilyId, checkedRow.FamilyId);
        Assert.Equal(question.ObjectiveIds, checkedRow.ObjectiveIds);
        Assert.All(rows.Where(r => r.QuestionId != ids[0]), r => { Assert.Equal(EvidenceSource.LearningSubmission, r.Source); Assert.False(r.Answered); });
    }

    [Fact] public async Task A_submitted_mock_records_every_question_once()
    {
        var client = await TestApi.Account(factory);
        var attempt = await TestApi.Start(client);
        var attemptId = TestApi.Id(attempt);
        var done = await TestApi.Complete(client, attempt, pack);
        (await client.PostAsJsonAsync($"/api/me/attempts/{attemptId}/submit", new { revision = done.GetProperty("revision").GetInt32() })).EnsureSuccessStatusCode();

        var rows = await Evidence(attemptId);
        Assert.Equal(TestApi.QuestionIds(attempt).Length, rows.Count);
        Assert.All(rows, r => { Assert.Equal(EvidenceSource.MockSubmission, r.Source); Assert.True(r.FullyCorrect); Assert.Equal(r.Possible, r.Earned); });
    }

    [Fact] public async Task An_expired_mock_records_unanswered_questions_as_incorrect()
    {
        var client = await TestApi.Account(factory);
        var attempt = await TestApi.Start(client);
        var attemptId = TestApi.Id(attempt);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var row = await db.Attempts.SingleAsync(a => a.Id == attemptId);
            row.Deadline = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        var view = await client.GetFromJsonAsync<JsonElement>($"/api/me/attempts/{attemptId}");
        Assert.Equal("completed", view.GetProperty("status").GetString());

        var rows = await Evidence(attemptId);
        Assert.Equal(TestApi.QuestionIds(attempt).Length, rows.Count);
        Assert.All(rows, r => { Assert.Equal(EvidenceSource.MockSubmission, r.Source); Assert.False(r.Answered); Assert.False(r.FullyCorrect); });
    }
}
