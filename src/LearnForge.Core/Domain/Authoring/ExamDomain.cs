namespace LearnForge.Core;

// A domain becomes a pack objective. Weight is its share of every mock (for example the exam's published percentage).
public sealed record ExamDomain(string Id, string Title, decimal? Weight = null, string[]? Prerequisites = null);
