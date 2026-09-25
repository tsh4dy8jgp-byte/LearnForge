namespace LearnForge.Core;

public sealed record Lesson(string Id, string Title, string Summary, string[] ObjectiveIds, ContentBlock[] Blocks);
