namespace LearnForge.Api.Services.Content;

public static class WritingAssetValidator
{
    public static Compilation Check(Compilation compilation)
    {
        if (!compilation.Success) return compilation;
        var diagnostics = compilation.Pack!.WritingCharacters
            .Where(c => !File.Exists(Path.Combine(AppContext.BaseDirectory, c.AudioUrl.TrimStart('/'))))
            .Select(c => new Diagnostic("LF100", "characters/" + c.Id, "Pronunciation asset is not bundled on this deployment."));
        return compilation with { Diagnostics = compilation.Diagnostics.Concat(diagnostics).ToArray() };
    }
}
