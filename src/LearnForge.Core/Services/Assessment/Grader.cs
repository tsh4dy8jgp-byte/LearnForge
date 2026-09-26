namespace LearnForge.Core;

public static class Grader
{
    public static string? Validate(Question q, Answer a)
    {
        if (a.Selected is null || a.Slots is null) return "Response collections are required.";
        if (a.Selected.Length > 100 || a.Slots.Count > 100 || a.Text?.Length > ResponseText.MaxLength) return "Response exceeds size limits.";
        if (q.Kind is QuestionKind.Numeric or QuestionKind.CodeOutput)
            return a.Selected.Length != 0 || a.Slots.Count != 0 ? "This question accepts a typed response." : null;
        if (!string.IsNullOrEmpty(a.Text)) return "This question does not accept a typed response.";
        if (q.Kind is QuestionKind.Single or QuestionKind.Multiple or QuestionKind.Sequence)
        {
            if (a.Slots.Count != 0 || a.Selected.Distinct().Count() != a.Selected.Length || a.Selected.Any(id => !q.Options.Any(o => o.Id == id))) return "Unknown or duplicate option.";
            if (q.Kind != QuestionKind.Sequence && a.Selected.Length > q.SelectCount) return "Too many selections.";
        }
        else
        {
            if (a.Selected.Length != 0) return "This question accepts slot responses.";
            foreach (var (slotId, optionId) in a.Slots)
            {
                var slot = q.Slots.FirstOrDefault(s => s.Id == slotId);
                if (slot is null || !(q.Kind == QuestionKind.Matching ? q.Options : slot.Options).Any(o => o.Id == optionId)) return "Unknown slot or option.";
            }
            if (q.Kind == QuestionKind.Matching && !q.Reuse && a.Slots.Values.Distinct().Count() != a.Slots.Count) return "A token may only be used once.";
        }
        return null;
    }

    public static Grade Score(Question q, Answer? response)
    {
        var a = response ?? new Answer([], []);
        if (Validate(q, a) is { } error) throw new ArgumentException(error);
        int right, count;
        if (q.Kind is QuestionKind.Single or QuestionKind.Multiple)
        {
            count = q.Grading.Correct.Length;
            right = a.Selected.Count(q.Grading.Correct.Contains);
        }
        else if (q.Kind == QuestionKind.Sequence)
        {
            count = q.Grading.Correct.Length;
            right = q.Grading.Correct.Where((id, i) => a.Selected.ElementAtOrDefault(i) == id).Count();
        }
        else if (q.Kind == QuestionKind.Numeric)
        {
            count = 1;
            right = ResponseText.TryParseNumber(a.Text, out var value)
                && q.Grading.Correct.Any(key => ResponseText.TryParseNumber(key, out var target) && Math.Abs(value - target) <= (q.Grading.Tolerance ?? 0m)) ? 1 : 0;
        }
        else if (q.Kind == QuestionKind.CodeOutput)
        {
            count = 1;
            right = !string.IsNullOrWhiteSpace(a.Text)
                && q.Grading.Correct.Any(key => ResponseText.NormalizeOutput(key) == ResponseText.NormalizeOutput(a.Text)) ? 1 : 0;
        }
        else
        {
            count = q.Slots.Length;
            right = q.Grading.Matches!.Count(pair => a.Slots.GetValueOrDefault(pair.Key) == pair.Value);
        }
        var full = right == count;
        var fraction = q.Grading.Policy == ScoringPolicy.Exact ? (full ? 1m : 0m) : (decimal)right / count;
        return new(fraction * q.Weight, q.Weight, full, q.Grading.Correct, q.Grading.Matches, q.Explanation);
    }
}
