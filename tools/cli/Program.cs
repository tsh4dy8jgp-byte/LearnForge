using System.Security.Cryptography;
using System.Text;
using LearnForge.Core;

if (args.Length == 0 || args[0] is "help" or "--help")
{
    Console.WriteLine("LearnForge content compiler\n  check <source.json> [--watch] [--json]\n  lint <source.json> [--json] [--strict]\n  build <source.json> --out <directory>\n  diff <before.json> <after.json>\n  init <source.json> [--profile course|exam|hybrid]\nSources are packs, {templates, pack} wrappers or compact \"format\": \"exam/1\" exams; see docs/authoring.md.");
    return 0;
}
try
{
    if (args.Length < 2) throw new ArgumentException("Supply a source file.");
    var command = args[0]; var path = Path.GetFullPath(args[1]);
    if (command == "init")
    {
        if (File.Exists(path)) throw new ArgumentException("Destination already exists.");
        var profileIndex = Array.IndexOf(args, "--profile");
        var profile = ProductProfile.Hybrid;
        if (profileIndex >= 0 && (profileIndex + 1 >= args.Length ||
            !Enum.TryParse(args[profileIndex + 1], true, out profile) || !Enum.IsDefined(profile)))
            throw new ArgumentException("Use --profile course, exam or hybrid.");
        var pack = PackStarter.Create(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, Json.Write(pack));
        Console.WriteLine($"Created {path}"); return 0;
    }
    Compilation Compile(string file) => ContentEngine.Compile(File.ReadAllText(file));
    void Report(Compilation c)
    {
        if (args.Contains("--json")) Console.WriteLine(Json.Write(new { c.Success, c.Hash, c.Diagnostics }));
        else if (c.Success) Console.WriteLine($"PASS {c.Pack!.Id}@{c.Pack.Version}: {c.Pack.Lessons.Length} lessons, {c.Pack.Questions.Length} questions, {c.Pack.Objectives.Length} objectives.");
        else foreach (var d in c.Diagnostics) Console.Error.WriteLine($"{d.Code} {d.Path}: {d.Message}");
    }
    if (command == "check" && args.Contains("--watch"))
    {
        Console.WriteLine("Watching this source file. Ctrl+C to stop.");
        var last = "";
        while (true)
        {
            var source = await File.ReadAllTextAsync(path);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
            if (hash != last) { Report(ContentEngine.Compile(source)); last = hash; }
            await Task.Delay(400);
        }
    }
    if (command == "lint")
    {
        // Quality warnings flag likely giveaways; they never fail a compile, so only --strict turns them into a failing exit code.
        var linted = Compile(path);
        var warnings = linted.Success ? ContentLinter.Lint(linted.Pack!) : [];
        if (args.Contains("--json")) Console.WriteLine(Json.Write(new { linted.Success, linted.Hash, linted.Diagnostics, Warnings = warnings }));
        else
        {
            Report(linted);
            foreach (var w in warnings) Console.WriteLine($"{w.Code} {w.Path}: {w.Message}");
            if (linted.Success) Console.WriteLine(warnings.Length == 1 ? "1 quality warning." : $"{warnings.Length} quality warnings.");
        }
        return !linted.Success || (args.Contains("--strict") && warnings.Length > 0) ? 1 : 0;
    }
    var compilation = Compile(path); Report(compilation);
    if (!compilation.Success) return 1;
    var p = compilation.Pack!;
    if (command == "check") return 0;
    if (command == "diff")
    {
        if (args.Length < 3) throw new ArgumentException("Supply the second source file.");
        var other = Compile(args[2]); Report(other); if (!other.Success) return 1;
        var oldItems = p.Questions.ToDictionary(q => q.Id);
        var newItems = other.Pack!.Questions.ToDictionary(q => q.Id);
        Console.WriteLine(Json.Write(new { Added = newItems.Keys.Except(oldItems.Keys), Removed = oldItems.Keys.Except(newItems.Keys),
            Changed = newItems.Keys.Intersect(oldItems.Keys).Where(id => Json.Write(newItems[id]) != Json.Write(oldItems[id])) }));
        return 0;
    }
    if (command != "build") throw new ArgumentException("Unknown command. Use --help.");
    var outIndex = Array.IndexOf(args, "--out");
    if (outIndex < 0 || outIndex + 1 >= args.Length) throw new ArgumentException("build requires --out <new-directory>.");
    var destination = Path.GetFullPath(args[outIndex + 1]);
    if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any()) throw new ArgumentException("Build directory must be empty. Use a fresh release directory.");
    Directory.CreateDirectory(destination);
    var artifacts = new Dictionary<string, string>
    {
        ["delivery.json"] = Json.Write(new { p.SchemaVersion, p.Id, p.Version, p.Title, p.Description, p.License, p.Objectives, p.Lessons,
            p.Scenarios, p.Blueprints, p.Sources, p.Profile, Capabilities = p.Features, p.Goal, Questions = p.Questions.Select(DeliveryQuestion.From) }),
        ["grading.private.json"] = Json.Write(p.Questions.ToDictionary(q => q.Id, q => new { q.Grading, q.Explanation })),
        ["pack.private.json"] = Json.Write(p),
        ["validation-report.json"] = Json.Write(new { compilation.Success, compilation.Diagnostics }),
        ["search.json"] = Json.Write(p.Lessons.Select(l => new { l.Id, l.Title, Text = string.Join("\n", l.Blocks.Select(b => b.Text)), l.ObjectiveIds }))
    };
    foreach (var (name, contents) in artifacts) await File.WriteAllTextAsync(Path.Combine(destination, name), contents);
    await File.WriteAllTextAsync(Path.Combine(destination, "manifest.json"), Json.Write(new
    {
        Engine = "LearnForge.Content/1", p.Id, p.Version, SourceHash = compilation.Hash,
        Outputs = artifacts.ToDictionary(k => k.Key, v => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(v.Value))))
    }));
    Console.WriteLine($"Built {destination}. Keep *.private.json outside any web root.");
    return 0;
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine(e.Message); return 1;
}
