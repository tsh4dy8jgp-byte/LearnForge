namespace LearnForge.Core;

public sealed record ContentBlock(ContentBlockKind Kind, string Text, string? Title = null);
