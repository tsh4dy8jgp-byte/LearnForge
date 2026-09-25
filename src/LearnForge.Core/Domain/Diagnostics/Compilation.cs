namespace LearnForge.Core;

public sealed record Compilation(Pack? Pack, Diagnostic[] Diagnostics, string Hash)
{
    public bool Success => Pack is not null && Diagnostics.Length == 0;
}
