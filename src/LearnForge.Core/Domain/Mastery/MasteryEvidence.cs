namespace LearnForge.Core;

// Order breaks timestamp ties deterministically (the ledger's identity column).
public sealed record MasteryEvidence(long Order, string FamilyId, string[] ObjectiveIds, bool FullyCorrect, bool Independent, DateTime At);
