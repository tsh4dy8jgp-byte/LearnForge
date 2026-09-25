namespace LearnForge.Core;

public sealed record ObjectiveMastery(string ObjectiveId, MasteryState State, int Correct, int Considered, int Independent,
    DateTime? LastEvidenceAt, bool ReviewDue);
