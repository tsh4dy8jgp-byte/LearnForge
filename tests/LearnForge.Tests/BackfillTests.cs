using LearnForge.Api;
using LearnForge.Api.Services.Learning;
using LearnForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class BackfillTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Pack pack = TestApi.LoadPack("reasoning-foundations");

    // Completes a mock, then removes its ledger rows and the enrollment, as if it predated the ledger.
    private async Task<(string AttemptId, string UserId, int Questions)> LegacyAttempt()
    {
        var client = await TestApi.Account(factory);
        var attempt = await TestApi.Start(client);
        await TestApi.Complete(client, attempt, pack);
        var attemptId = TestApi.Id(attempt);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var userId = await db.Attempts.Where(a => a.Id == attemptId).Select(a => a.UserId).SingleAsync();
        await db.Evidence.Where(e => e.AttemptId == attemptId).ExecuteDeleteAsync();
        await db.Enrollments.Where(e => e.UserId == userId).ExecuteDeleteAsync();
        return (attemptId, userId, TestApi.QuestionIds(attempt).Length);
    }

    [Fact] public async Task Backfill_rebuilds_missing_evidence_and_enrollments_idempotently()
    {
        var (attemptId, userId, questions) = await LegacyAttempt();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        Assert.Equal(1, await EvidenceBackfill.RunAsync(db));
        Assert.Equal(0, await EvidenceBackfill.RunAsync(db));

        var rows = await db.Evidence.Where(e => e.AttemptId == attemptId).ToListAsync();
        Assert.Equal(questions, rows.Count);
        Assert.All(rows, r => { Assert.Equal(EvidenceSource.MockSubmission, r.Source); Assert.True(r.FullyCorrect); });
        Assert.True(await db.Enrollments.AnyAsync(e => e.UserId == userId && e.PackId == pack.Id));
    }

    [Fact] public async Task Concurrent_backfills_do_not_fail()
    {
        var (attemptId, _, questions) = await LegacyAttempt();
        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            await EvidenceBackfill.RunAsync(scope.ServiceProvider.GetRequiredService<AppDb>());
        }));
        using var check = factory.Services.CreateScope();
        Assert.Equal(questions, await check.ServiceProvider.GetRequiredService<AppDb>().Evidence.CountAsync(e => e.AttemptId == attemptId));
    }

    [Fact] public async Task A_pending_backfill_is_detectable_until_it_runs()
    {
        await LegacyAttempt();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal(1, await EvidenceBackfill.PendingAsync(db));
        await EvidenceBackfill.RunAsync(db);
        Assert.Equal(0, await EvidenceBackfill.PendingAsync(db));
    }
}
