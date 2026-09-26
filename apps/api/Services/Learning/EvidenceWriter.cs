namespace LearnForge.Api.Services.Learning;

public static class EvidenceWriter
{
    public static bool IsAnswered(Answer? answer) =>
        answer is not null && (answer.Selected.Length > 0 || answer.Slots.Count > 0 || !string.IsNullOrWhiteSpace(answer.Text));

    // Mock answers always count (unanswered is incorrect); learning answers count only when the learner answered.
    public static bool Countable(EvidenceSource source, bool answered) => source == EvidenceSource.MockSubmission || answered;

    public static EvidenceRecord Record(Attempt attempt, Question question, Answer? answer, Grade grade, EvidenceSource source, DateTime at) => new()
    {
        UserId = attempt.UserId, PackId = attempt.PackId, ReleaseId = attempt.PackReleaseId, AttemptId = attempt.Id,
        QuestionId = question.Id, FamilyId = question.FamilyId, ObjectiveIds = question.ObjectiveIds, Source = source,
        Answered = IsAnswered(answer), FullyCorrect = grade.FullyCorrect, Earned = grade.Earned, Possible = grade.Possible, At = at
    };
}
