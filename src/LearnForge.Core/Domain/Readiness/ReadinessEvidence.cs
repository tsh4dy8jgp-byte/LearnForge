namespace LearnForge.Core;

public sealed record ReadinessEvidence(string Id, AssessmentSize Size, DateTime CompletedAt, decimal CorrectPercent, bool Eligible);
