using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api.Services.Content;

// When Content:WatchSeconds is positive, polls the pack directories so a new pack file or version is published without a restart.
// A file is read only after it stays unchanged for a whole interval, which skips half-written files. Invalid files are
// logged once per change and never stop the API. Deleting a file changes nothing: published releases are immutable.
public sealed class PackWatcher(IServiceScopeFactory scopes, IConfiguration configuration, IHostEnvironment environment,
    ILogger<PackWatcher> logger) : BackgroundService
{
    private readonly record struct Stamp(long Length, DateTime Written);

    private readonly Dictionary<string, Stamp> processed = new(StringComparer.Ordinal);
    private readonly HashSet<string> missing = new(StringComparer.Ordinal);
    private Dictionary<string, Stamp> previous = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Read at run time rather than at registration, so every configuration source (including test hosts) applies.
        var seconds = configuration.GetValue("Content:WatchSeconds", 0);
        if (seconds <= 0) return;
        seconds = Math.Min(seconds, 3600);
        // Startup seeding has already read every file that exists now.
        previous = Scan();
        foreach (var (path, stamp) in previous) processed[path] = stamp;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await Tick(stoppingToken); }
            catch (Exception e) when (e is not OperationCanceledException) { logger.LogError("Pack directory scan failed: {Type}", e.GetType().Name); }
        }
    }

    private async Task Tick(CancellationToken stoppingToken)
    {
        var current = Scan();
        var ready = current.Where(f => (!processed.TryGetValue(f.Key, out var done) || done != f.Value)
            && previous.TryGetValue(f.Key, out var before) && before == f.Value).Select(f => f.Key).ToArray();
        previous = current;
        if (ready.Length == 0) return;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        try
        {
            await PackSeeder.SeedAsync(db, ready, logger, strict: false, stoppingToken);
            foreach (var path in ready) processed[path] = current[path];
        }
        catch (DbUpdateException)
        {
            // Another replica or a Studio publish stored the same release first. The next scan retries and finds it stored.
            logger.LogInformation("A pack release was published concurrently; the watcher retries on its next scan.");
        }
    }

    private Dictionary<string, Stamp> Scan()
    {
        var stamps = new Dictionary<string, Stamp>(StringComparer.Ordinal);
        foreach (var directory in PackSeeder.Directories(configuration, environment))
        {
            if (!Directory.Exists(directory))
            {
                if (missing.Add(directory)) logger.LogWarning("Watched pack directory {Directory} does not exist.", directory);
                continue;
            }
            missing.Remove(directory);
            foreach (var file in PackSeeder.Files([directory]))
                try
                {
                    var info = new FileInfo(file);
                    stamps[file] = new(info.Length, info.LastWriteTimeUtc);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* Unstable or unreadable now; seen again next scan. */ }
        }
        return stamps;
    }
}
