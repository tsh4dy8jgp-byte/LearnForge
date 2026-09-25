namespace LearnForge.Core;

public sealed record ReadinessTrack(AssessmentSize Size, int Required, int Streak, bool Met, string[] AttemptIds);
