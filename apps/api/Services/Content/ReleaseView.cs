namespace LearnForge.Api.Services.Content;

public sealed record ReleaseView(string ReleaseId, Pack Pack, IReadOnlyDictionary<string, string> LessonHashes);
