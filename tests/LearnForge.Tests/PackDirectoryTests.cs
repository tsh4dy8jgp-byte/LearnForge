using System.Collections.Concurrent;
using System.Net;
using LearnForge.Api;
using LearnForge.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LearnForge.Tests;

// A host whose database and extra pack directory are chosen by the test, so a second host can reseed the same database.
public sealed class PackDirectoryFactory(string database, string packDirectory, CapturedLogs logs) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:Database"] = "Data Source=" + database,
            ["Content:PackDirectories:0"] = packDirectory,
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

    private async Task WritePack(Pack pack) => await File.WriteAllTextAsync(Path.Combine(Packs, pack.Id + ".json"), Json.Write(pack));

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

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }
}
