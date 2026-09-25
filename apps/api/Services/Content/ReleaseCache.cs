using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LearnForge.Api.Services.Content;

// Pack releases are immutable, so a deserialized release never needs invalidation. Only the
// "which release is latest" lookup reads the database, which keeps multiple replicas consistent.
public sealed class ReleaseCache(IServiceScopeFactory scopes, IConfiguration configuration) : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = configuration.GetValue("ReleaseCache:Capacity", 64) });
    private readonly Lock gate = new();

    public Task<ReleaseView> Get(string releaseId)
    {
        if (cache.TryGetValue(releaseId, out Lazy<Task<ReleaseView>>? entry)) return entry!.Value;
        lock (gate)
        {
            // One Lazy per release: concurrent misses share a single load.
            if (!cache.TryGetValue(releaseId, out entry))
            {
                entry = new Lazy<Task<ReleaseView>>(() => Load(releaseId));
                cache.Set(releaseId, entry, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = TimeSpan.FromHours(6) });
            }
        }
        return entry!.Value;
    }

    public async Task<ReleaseView?> Latest(AppDb db, string packId)
    {
        var releaseId = await db.Packs.Where(p => p.PackId == packId).OrderByDescending(p => p.PublishedAt).Select(p => p.Id).FirstOrDefaultAsync();
        return releaseId is null ? null : await Get(releaseId);
    }

    public async Task<string[]> LatestIds(AppDb db) =>
        (await db.Packs.Select(p => new { p.Id, p.PackId, p.PublishedAt }).ToListAsync())
            .OrderByDescending(p => p.PublishedAt).DistinctBy(p => p.PackId).Select(p => p.Id).ToArray();

    private async Task<ReleaseView> Load(string releaseId)
    {
        try
        {
            // Loads run outside the caller's request scope because other requests may await the same task.
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var release = await db.Packs.AsNoTracking().SingleAsync(p => p.Id == releaseId);
            var pack = Json.Read<Pack>(release.ContentJson);
            return new ReleaseView(release.Id, pack, pack.Lessons.ToDictionary(l => l.Id, l => ContentHash.Of(l)));
        }
        catch
        {
            cache.Remove(releaseId); // Never keep a failed load; the next request retries.
            throw;
        }
    }

    public void Dispose() => cache.Dispose();
}
