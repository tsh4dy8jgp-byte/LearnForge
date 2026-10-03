namespace LearnForge.Api.Contracts.Content;

public sealed record DraftSummaryDto(string Id, string Title, int Revision, DateTime UpdatedAt);
