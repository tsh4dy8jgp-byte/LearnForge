using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api.Services.Content;

// Publishes pack files from the bundled packs folder and Content:PackDirectories. Startup seeding is strict: one invalid
// file stops the API. The watcher is lenient: it logs an invalid file and skips it. Stored releases are never replaced.
public static class PackSeeder
{
    // The bundled folder is listed only when it exists; configured directories are listed even when missing so callers decide.
    public static string[] Directories(IConfiguration configuration, IHostEnvironment environment)
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "packs");
        var configured = (configuration.GetSection("Content:PackDirectories").Get<string[]>() ?? [])
            .Select(directory => Path.GetFullPath(directory, environment.ContentRootPath));
        return (Directory.Exists(bundled) ? configured.Prepend(bundled) : configured).ToArray();
    }

    public static string[] Files(IEnumerable<string> directories) => directories.Where(Directory.Exists)
        .SelectMany(d => Directory.GetFiles(d, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal)).ToArray();

    public static async Task<int> SeedAsync(AppDb db, IEnumerable<string> files, ILogger logger, bool strict, CancellationToken cancellationToken = default)
    {
        // The same release can appear twice in one pass (for example the bundled copy and a watched source folder); add it once.
        var pass = new Dictionary<(string Id, string Version), (string Hash, string Path)>();
        var added = new List<(Version Version, PackRelease Release)>();
        foreach (var path in files)
        {
            Compilation compiled;
            try
            {
                // A file larger than the compiler's limit (plus a byte-order mark) cannot compile, so do not read it into memory.
                compiled = new FileInfo(path).Length > ContentEngine.MaxSourceBytes + 3
                    ? new(null, [new("LF001", "$", "Pack source exceeds 2 MB.")], "")
                    : ContentEngine.Compile(await File.ReadAllTextAsync(path, cancellationToken));
            }
            catch (Exception e) when (!strict && e is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Could not read pack file {Path}: {Type}", path, e.GetType().Name);
                continue;
            }
            if (!compiled.Success)
            {
                if (strict) throw new InvalidOperationException($"Invalid seed pack {Path.GetFileName(path)}: {Json.Write(compiled.Diagnostics)}");
                logger.LogWarning("Skipped invalid pack file {Path}: {Diagnostics}", path,
                    string.Join("; ", compiled.Diagnostics.Take(10).Select(d => $"{d.Code} {d.Path}: {d.Message}")));
                continue;
            }
            var p = compiled.Pack!;
            if (pass.TryGetValue((p.Id, p.Version), out var first))
            {
                if (first.Hash != compiled.Hash)
                    logger.LogWarning("Pack {PackId}@{Version} in {Path} differs from {First}, which was read first. Bump its version to publish the change.", p.Id, p.Version, path, first.Path);
                continue;
            }
            pass[(p.Id, p.Version)] = (compiled.Hash, path);
            var stored = await db.Packs.Where(x => x.PackId == p.Id && x.Version == p.Version).Select(x => x.Hash).FirstOrDefaultAsync(cancellationToken);
            if (stored is null)
            {
                added.Add((Version.Parse(p.Version), new() { PackId = p.Id, Version = p.Version, ContentJson = Json.Write(p), Hash = compiled.Hash }));
                logger.LogInformation("Publishing {PackId}@{Version} from {Path}.", p.Id, p.Version, path);
            }
            // Releases are immutable, so changed content under a stored version is ignored until the version is bumped.
            else if (stored != compiled.Hash)
                logger.LogWarning("Seed pack {PackId}@{Version} in {Path} differs from the stored release and was not applied. Bump its version to publish the change.", p.Id, p.Version, path);
        }
        // "Latest" is the newest PublishedAt, so several new versions of one pack from the same pass are stamped in version order.
        var now = DateTime.UtcNow;
        var ordered = added.OrderBy(a => a.Release.PackId, StringComparer.Ordinal).ThenBy(a => a.Version).Select(a => a.Release).ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            ordered[i].PublishedAt = now.AddMilliseconds(i);
            db.Packs.Add(ordered[i]);
        }
        await db.SaveChangesAsync(cancellationToken);
        return ordered.Length;
    }
}
