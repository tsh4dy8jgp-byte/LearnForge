using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api.Startup;

public static class DatabaseInitializer
{
    // Returns false when a maintenance command (--migrate, --grant-publisher) finished and the process should exit.
    public static async Task<bool> RunAsync(WebApplication app, string[] args)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        // Separate migrations preserve each provider's native types and identity columns.
        if (app.Configuration.GetValue("Database:AutoMigrate", true) || args.Contains("--migrate"))
        {
            await db.Database.MigrateAsync();
            var backfilled = await EvidenceBackfill.RunAsync(db);
            if (backfilled > 0) app.Logger.LogInformation("Evidence backfill wrote ledger rows for {Attempts} attempts.", backfilled);
        }
        else if (!(await db.Database.GetPendingMigrationsAsync()).Any() && await EvidenceBackfill.PendingAsync(db) is var pending and > 0)
            // Schema applied another way (for example an EF SQL script): mastery and question freshness stay incomplete until backfilled.
            app.Logger.LogWarning("{Attempts} completed attempts have no evidence ledger rows. Run the migration step (--migrate) to backfill them.", pending);
        await SeedPacksAsync(db, PackDirectories(app), app.Logger);
        if (args.Contains("--migrate")) return false;
        var grantIndex = Array.IndexOf(args, "--grant-publisher");
        if (grantIndex < 0) return true;
        if (args.Length <= grantIndex + 1) throw new InvalidOperationException("Supply an existing account email.");
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await manager.FindByEmailAsync(args[grantIndex + 1]) ?? throw new InvalidOperationException("Account must be registered first.");
        if (!await roles.RoleExistsAsync("Publisher")) await roles.CreateAsync(new("Publisher"));
        var result = await manager.AddToRoleAsync(user, "Publisher");
        if (!result.Succeeded && !await manager.IsInRoleAsync(user, "Publisher")) throw new InvalidOperationException("Role grant failed.");
        app.Logger.LogInformation("Publisher role granted. Sign out and back in to refresh the session.");
        return false;
    }

    // The bundled packs folder is optional; directories named in Content:PackDirectories (e.g. a content repository) must exist.
    private static IEnumerable<string> PackDirectories(WebApplication app)
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "packs");
        if (Directory.Exists(bundled)) yield return bundled;
        foreach (var configured in app.Configuration.GetSection("Content:PackDirectories").Get<string[]>() ?? [])
        {
            var directory = Path.GetFullPath(configured, app.Environment.ContentRootPath);
            if (!Directory.Exists(directory)) throw new InvalidOperationException($"Configured pack directory '{directory}' does not exist.");
            yield return directory;
        }
    }

    private static async Task SeedPacksAsync(AppDb db, IEnumerable<string> directories, ILogger logger)
    {
        foreach (var directory in directories)
            foreach (var path in Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories))
            {
                var compiled = ContentEngine.Compile(await File.ReadAllTextAsync(path));
                if (!compiled.Success) throw new InvalidOperationException($"Invalid seed pack {Path.GetFileName(path)}: {Json.Write(compiled.Diagnostics)}");
                var p = compiled.Pack!;
                var stored = await db.Packs.Where(x => x.PackId == p.Id && x.Version == p.Version).Select(x => x.Hash).FirstOrDefaultAsync();
                if (stored is null) db.Packs.Add(new() { PackId = p.Id, Version = p.Version, ContentJson = Json.Write(p), Hash = compiled.Hash });
                // Releases are immutable, so changed content under a stored version is ignored until the version is bumped.
                else if (stored != compiled.Hash)
                    logger.LogWarning("Seed pack {PackId}@{Version} in {Path} differs from the stored release and was not applied. Bump its version to publish the change.", p.Id, p.Version, path);
            }
        await db.SaveChangesAsync();
    }
}
