namespace LearnForge.Api.Contracts.Export;

public sealed record EvidenceExportDto(string PackId, string ReleaseId, string AttemptId, string QuestionId, string FamilyId,
    string[] ObjectiveIds, EvidenceSource Source, bool Answered, bool FullyCorrect, decimal Earned, decimal Possible, DateTime At);
