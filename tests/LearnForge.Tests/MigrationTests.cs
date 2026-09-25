using LearnForge.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace LearnForge.Tests;

public class MigrationTests
{
    [Fact] public async Task Release_completions_become_pack_progress_keeping_the_first_completion()
    {
        var file = Path.Combine(Path.GetTempPath(), "learnforge-migration-" + Guid.NewGuid().ToString("N") + ".db");
        await using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlite("Data Source=" + file).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260925054146_Initial");
        // ExecuteSqlRaw formats the text, so literal braces are doubled.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "AspNetUsers" ("Id","DisplayName","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount") VALUES ('u1','Learner',0,0,0,0,0);
            INSERT INTO "Packs" ("Id","PackId","Version","ContentJson","Hash","PublishedAt") VALUES ('r1','demo','1.0.0','{{}}','h','2026-01-01 00:00:00'), ('r2','demo','2.0.0','{{}}','h','2026-02-01 00:00:00');
            INSERT INTO "Completions" ("UserId","ReleaseId","LessonId","At") VALUES ('u1','r1','intro','2026-01-02 00:00:00'), ('u1','r2','intro','2026-02-02 00:00:00'), ('u1','r2','next','2026-02-03 00:00:00');
            """);

        await migrator.MigrateAsync();

        var rows = await db.LessonProgress.OrderBy(p => p.LessonId).ToListAsync();
        Assert.Equal(new[] { "intro", "next" }, rows.Select(r => r.LessonId));
        Assert.All(rows, r => { Assert.Equal("demo", r.PackId); Assert.Null(r.ContentHash); });
        Assert.Equal(new DateTime(2026, 1, 2), rows[0].CompletedAt);
    }
}
