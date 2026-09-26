namespace LearnForge.Core;

// Explicit allowlist for learner-facing delivery. Private grading data is never serialized here.
public sealed record DeliveryQuestion(string Id, QuestionKind Kind, string Prompt, string[] ObjectiveIds,
    Option[] Options, Slot[] Slots, int SelectCount, bool Reuse, string? ScenarioId, decimal Weight, CodeSample? Code = null)
{
    public static DeliveryQuestion From(Question q) => new(q.Id, q.Kind, q.Prompt, q.ObjectiveIds,
        q.Options, q.Slots, q.SelectCount, q.Reuse, q.ScenarioId, q.Weight, q.Code);
}
