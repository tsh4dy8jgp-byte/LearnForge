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
    string[] LessonIds);
