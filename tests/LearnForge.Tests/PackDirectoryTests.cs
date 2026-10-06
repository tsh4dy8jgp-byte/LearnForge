using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using LearnForge.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LearnForge.Tests;

// A host whose database and extra pack directory are chosen by the test, so a second host can reseed the same database.
public sealed class PackDirectoryFactory(string database, string packDirectory, CapturedLogs logs, int watchSeconds = 0) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:Database"] = "Data Source=" + database,
            ["Content:PackDirectories:0"] = packDirectory,
            ["Content:WatchSeconds"] = watchSeconds.ToString(),
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        builder.ConfigureLogging(logging => logging.AddProvider(logs));
    }
}

public sealed class CapturedLogs : ILoggerProvider
{
    public ConcurrentQueue<string> Warnings { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Capture(Warnings);
    public void Dispose() { }

    private sealed class Capture(ConcurrentQueue<string> warnings) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning) warnings.Enqueue(formatter(state, exception));
        }
    }
}

public sealed class PackDirectoryTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("learnforge-packs-").FullName;
    private string Database => Path.Combine(root, "test.db");
    private string Packs => Directory.CreateDirectory(Path.Combine(root, "packs")).FullName;

    private async Task WritePack(Pack pack, string? name = null) => await File.WriteAllTextAsync(Path.Combine(Packs, (name ?? pack.Id) + ".json"), Json.Write(pack));

    private static string Exam(string id, string version = "1.0.0") => ExamSourceTests.SampleSource()
        .Replace("\"web-foundations-sample\"", "\"" + id + "\"").Replace("\"version\": \"1.0.0\"", "\"version\": \"" + version + "\"");

    // Polls until the condition holds, so watcher tests do not depend on exact timing.
    private static async Task Eventually(Func<Task<bool>> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline) Assert.Fail("Timed out waiting until " + what);
            await Task.Delay(250);
        }
    }

    [Fact] public async Task Packs_in_configured_directories_are_seeded()
    {
        await WritePack(CoreTests.Demo() with { Id = "external-pack" });
        await using var factory = new PackDirectoryFactory(Database, Packs, new());
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/catalog/external-pack")).StatusCode);
    }

    [Fact] public void A_missing_configured_directory_stops_startup()
    {
        using var factory = new PackDirectoryFactory(Database, Path.Combine(root, "does-not-exist"), new());
        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("does-not-exist", error.ToString());
    }

    [Fact] public async Task Reseeding_changed_content_under_an_existing_version_logs_a_warning()
    {
        var pack = CoreTests.Demo() with { Id = "external-pack" };
        await WritePack(pack);
        await using (var first = new PackDirectoryFactory(Database, Packs, new())) first.CreateClient();
        await WritePack(pack with { Title = "Changed without a version bump" });
        var logs = new CapturedLogs();
        await using var second = new PackDirectoryFactory(Database, Packs, logs);
        var client = second.CreateClient();
        Assert.Contains(logs.Warnings, w => w.Contains("external-pack") && w.Contains(pack.Version));
        var served = await client.GetStringAsync("/api/catalog/external-pack");
        Assert.DoesNotContain("Changed without a version bump", served);
    }

    [Fact] public async Task Exam_sources_in_configured_directories_are_seeded()
    {
        await File.WriteAllTextAsync(Path.Combine(Packs, "exam.json"), Exam("external-exam"));
        await using var factory = new PackDirectoryFactory(Database, Packs, new());
        var catalog = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/catalog/external-exam");
        Assert.Equal("exam", catalog.GetProperty("profile").GetString());
    }

    [Fact] public async Task The_same_release_in_two_files_is_seeded_once()
    {
        var pack = CoreTests.Demo() with { Id = "twice" };
        await WritePack(pack, "first");
        await WritePack(pack, "second");
        await using var factory = new PackDirectoryFactory(Database, Packs, new());
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/api/catalog/twice")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDb>().Packs.CountAsync(p => p.PackId == "twice"));
    }

    [Fact] public async Task The_watcher_publishes_new_files_and_versions_without_a_restart()
    {
        await File.WriteAllTextAsync(Path.Combine(Packs, "watched.json"), Exam("watched-exam"));
        await using var factory = new PackDirectoryFactory(Database, Packs, new(), watchSeconds: 1);
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/catalog/dropped-exam")).StatusCode);

        await File.WriteAllTextAsync(Path.Combine(Packs, "dropped.json"), Exam("dropped-exam"));
        await File.WriteAllTextAsync(Path.Combine(Packs, "watched-v2.json"), Exam("watched-exam", "1.1.0"));
        await Eventually(async () => (await client.GetAsync("/api/catalog/dropped-exam")).StatusCode == HttpStatusCode.OK, "a dropped file is published");
        await Eventually(async () => (await client.GetFromJsonAsync<JsonElement>("/api/catalog/watched-exam")).GetProperty("version").GetString() == "1.1.0",
            "a new version becomes the latest release");
    }

    [Fact] public async Task The_watcher_logs_invalid_files_and_keeps_serving()
    {
        await WritePack(CoreTests.Demo() with { Id = "steady" });
        var logs = new CapturedLogs();
        await using var factory = new PackDirectoryFactory(Database, Packs, logs, watchSeconds: 1);
        var client = factory.CreateClient();
        await File.WriteAllTextAsync(Path.Combine(Packs, "broken.json"), "{\"format\": \"exam/1\", \"id\": \"broken\"}");
        await Eventually(() => Task.FromResult(logs.Warnings.Any(w => w.Contains("broken.json"))), "the invalid file is logged");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/catalog/steady")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/catalog/broken")).StatusCode);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }
}
