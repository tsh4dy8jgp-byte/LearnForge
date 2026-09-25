namespace LearnForge.Api.Contracts.Learning;

public sealed record ObjectiveMasteryDto(
    string Id,
    string Title,
    string[] Prerequisites,
    MasteryState State,
    int Correct,
    int Considered,
    int Independent,
    DateTime? LastEvidenceAt,
    bool ReviewDue,
    string[] LessonIds,
    // False when every question for the objective sits in a case study, so it cannot be practised alone.
    bool StandalonePractice);
