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
            await EvidenceBackfill.RunAsync(db);
        }
        await SeedPacksAsync(db);
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

    private static async Task SeedPacksAsync(AppDb db)
    {
        var packDirectory = Path.Combine(AppContext.BaseDirectory, "packs");
        if (Directory.Exists(packDirectory))
            foreach (var path in Directory.GetFiles(packDirectory, "*.json", SearchOption.AllDirectories))
            {
                var compiled = ContentEngine.Compile(await File.ReadAllTextAsync(path));
                if (!compiled.Success) throw new InvalidOperationException($"Invalid seed pack {Path.GetFileName(path)}: {Json.Write(compiled.Diagnostics)}");
                var p = compiled.Pack!;
                if (!await db.Packs.AnyAsync(x => x.PackId == p.Id && x.Version == p.Version)) db.Packs.Add(new() { PackId = p.Id, Version = p.Version, ContentJson = Json.Write(p), Hash = compiled.Hash });
            }
        await db.SaveChangesAsync();
    }
}
