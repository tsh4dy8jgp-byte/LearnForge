namespace LearnForge.Core;

public static class MasteryEvaluator
{
    // Callers pass only countable evidence: mock answers always, learning answers only when answered.
    public static ObjectiveMastery[] Evaluate(IEnumerable<Objective> objectives, IEnumerable<MasteryEvidence> evidence, MasteryPolicy policy, DateTime now)
    {
        var records = evidence.ToArray();
        return objectives.Select(objective =>
        {
            // The latest answer per family reflects current knowledge; drilling one item counts once.
            var window = records.Where(e => e.ObjectiveIds.Contains(objective.Id))
                .GroupBy(e => e.FamilyId)
                .Select(g => g.OrderByDescending(e => e.At).ThenByDescending(e => e.Order).First())
                .OrderByDescending(e => e.At).ThenByDescending(e => e.Order)
                .Take(policy.Window)
                .ToArray();
            var considered = window.Length;
            var correct = window.Count(e => e.FullyCorrect);
            var state = considered == 0 ? MasteryState.NotStarted
                : considered < policy.MinimumEvidence ? MasteryState.Emerging
                : correct * 100m >= policy.ProficientPercent * considered ? MasteryState.Proficient
                : MasteryState.Developing;
            DateTime? last = considered == 0 ? null : window[0].At;
            var reviewDue = state == MasteryState.Proficient && last < now.AddDays(-policy.ReviewAfterDays);
            return new ObjectiveMastery(objective.Id, state, correct, considered, window.Count(e => e.Independent), last, reviewDue);
        }).ToArray();
    }
}
