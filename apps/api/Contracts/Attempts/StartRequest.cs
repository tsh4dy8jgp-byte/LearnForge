namespace LearnForge.Api.Contracts.Attempts;

public sealed record StartRequest(string PackId, string BlueprintId, AssessmentMode Mode, string RequestId, PracticeFocus? Focus = null);
