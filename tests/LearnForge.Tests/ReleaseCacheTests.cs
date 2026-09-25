using LearnForge.Api;
using LearnForge.Api.Services.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class ReleaseCacheTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact] public async Task The_latest_release_is_deserialized_once_and_reused()
    {
        var cache = factory.Services.GetRequiredService<ReleaseCache>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var first = await cache.Latest(db, "reasoning-foundations");
        var second = await cache.Latest(db, "reasoning-foundations");
        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(first!.Pack.Lessons.Select(l => l.Id).Order(), first.LessonHashes.Keys.Order());
        Assert.Null(await cache.Latest(db, "missing-pack"));
        Assert.Contains(first.ReleaseId, await cache.LatestIds(db));
    }

    [Fact] public async Task Concurrent_misses_share_one_load()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var id = await db.Packs.Where(p => p.PackId == "evidence-lab").Select(p => p.Id).FirstAsync();
        using var cache = new ReleaseCache(factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Services.GetRequiredService<IConfiguration>());
        var views = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => cache.Get(id)));
        Assert.All(views, v => Assert.Same(views[0], v));
    }
}
