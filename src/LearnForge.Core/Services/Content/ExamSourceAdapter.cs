using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LearnForge.Core;

// Expands the compact exam/1 source into an ordinary exam pack, which ContentEngine then validates like any other.
internal static class ExamSourceAdapter
{
    public const string Format = "exam/1";
    private const string Code = "LF110";

    private static readonly Dictionary<QuestionKind, string[]> Fields = new()
    {
        [QuestionKind.Single] = ["answer", "distractors"],
        [QuestionKind.Multiple] = ["answers", "distractors"],
        [QuestionKind.Sequence] = ["steps"],
        [QuestionKind.Matching] = ["pairs", "extraMatches"],
        [QuestionKind.Dropdown] = ["blanks"],
        [QuestionKind.Numeric] = ["answer", "answers", "tolerance"],
        [QuestionKind.CodeOutput] = ["answer", "answers", "code"]
    };

    public static Pack? ToPack(ExamSource source, List<Diagnostic> errors)
    {
        void Error(string path, string message) => errors.Add(new(Code, path, message));
        var weighted = source.Domains.Count(d => d.Weight is not null);
        if (weighted > 0 && weighted < source.Domains.Length) Error("domains", "Give every domain a weight, or none.");
        foreach (var domain in source.Domains)
            if (domain.Weight is <= 0 or > 1000) Error(domain.Id, "A domain weight must be positive and at most 1000.");
        var questions = new List<Question>();
        for (var i = 0; i < source.Questions.Length; i++)
        {
            var q = source.Questions[i];
            var path = string.IsNullOrWhiteSpace(q.Id) ? $"questions[{i}]" : q.Id;
            var before = errors.Count;
            var converted = Convert(q, message => Error(path, message));
            if (converted is not null && errors.Count == before) questions.Add(converted);
        }
        if (errors.Count > 0) return null;

        var objectiveIds = source.Domains.Select(d => d.Id).ToArray();
        // Duplicate domain IDs are reported by Validate; DistinctBy keeps them from failing here first.
        var weights = weighted > 0 ? source.Domains.DistinctBy(d => d.Id).ToDictionary(d => d.Id, d => d.Weight!.Value) : null;
        Blueprint Mock(string id, string title, ExamMock mock, AssessmentSize size) => new(id, mock.Title ?? title, mock.Count, mock.Minutes,
            size, objectiveIds, source.Mocks.RequiredKinds ?? [], source.Mocks.LockCaseStudies, weights);
        Blueprint[] blueprints = source.Mocks.Full is { } full
            ? [Mock("short", "Short mock exam", source.Mocks.Short, AssessmentSize.Short), Mock("full", "Full mock exam", full, AssessmentSize.Full)]
            : [Mock("short", "Short mock exam", source.Mocks.Short, AssessmentSize.Short)];
        return new Pack(1, source.Id, source.Version, source.Title, source.Description, source.License,
            source.Domains.Select(d => new Objective(d.Id, d.Title, d.Prerequisites ?? [])).ToArray(), [], questions.ToArray(),
            source.CaseStudies ?? [], blueprints, source.Sources ?? [], source.Readiness, source.Goal, source.Mastery, ProductProfile.Exam);
    }

    private static Question? Convert(ExamQuestion q, Action<string> error)
    {
        var kind = JsonNamingPolicy.CamelCase.ConvertName(q.Kind.ToString());
        if (!Fields.TryGetValue(q.Kind, out var allowed)) { error($"Unsupported question kind '{kind}'."); return null; }
        var present = new (string Name, bool Set)[]
        {
            ("answer", q.Answer is not null), ("answers", q.Answers is not null), ("distractors", q.Distractors is not null),
            ("steps", q.Steps is not null), ("pairs", q.Pairs is not null), ("extraMatches", q.ExtraMatches is not null),
            ("blanks", q.Blanks is not null), ("tolerance", q.Tolerance is not null), ("code", q.Code is not null)
        };
        foreach (var field in present.Where(f => f.Set && !allowed.Contains(f.Name))) error($"'{field.Name}' does not apply to {kind} questions.");
        string[] objectives = (q.Domain, q.Domains) switch
        {
            ({ } domain, null) => [domain],
            (null, { Length: > 0 } domains) => domains,
            _ => []
        };
        if (objectives.Length == 0) error("Give either 'domain' or a nonempty 'domains' list.");
        var id = q.Id;

        Question Make(Option[] options, Slot[] slots, int selectCount, bool reuse, ScoringPolicy policy, string[] correct,
            Dictionary<string, string>? matches = null) => new(id, q.Family ?? id, q.Kind, q.Prompt, objectives, options, slots, selectCount,
            reuse, new(q.Scoring ?? policy, correct, matches, q.Tolerance), q.Explanation, q.CaseStudy, q.Weight, q.Code);

        switch (q.Kind)
        {
            case QuestionKind.Single:
            {
                var answer = Text(q.Answer, "answer", error);
                if (Require(q.Distractors, "distractors", error) is not { } distractors || answer is null) return null;
                var options = Bank(id, "", [answer, .. distractors], "the options", error);
                return Make(options, [], 1, false, ScoringPolicy.Exact, [OptionId(id, "", answer)]);
            }
            case QuestionKind.Multiple:
            {
                var answers = (q.Answers ?? []).Select((a, i) => Text(a, $"answers[{i}]", error)).ToArray();
                if (answers.Length < 2) error("Multiple-answer questions need at least two 'answers'.");
                if (Require(q.Distractors, "distractors", error) is not { } distractors || answers.Length < 2 || answers.Any(a => a is null)) return null;
                var keys = answers.Select(a => a!).ToArray();
                var options = Bank(id, "", [.. keys, .. distractors], "the options", error);
                return Make(options, [], keys.Length, false, ScoringPolicy.Partial, keys.Select(a => OptionId(id, "", a)).ToArray());
            }
            case QuestionKind.Sequence:
            {
                if (q.Steps is not { Length: >= 2 } steps) { error("Sequence questions need at least two 'steps', in the correct order."); return null; }
                var options = Bank(id, "", steps, "the steps", error);
                return Make(options, [], 0, false, ScoringPolicy.Partial, steps.Select(s => OptionId(id, "", s)).ToArray());
            }
            case QuestionKind.Matching:
            {
                if (q.Pairs is not { Length: >= 2 } pairs) { error("Matching questions need at least two 'pairs'."); return null; }
                if (pairs.Any(p => string.IsNullOrWhiteSpace(p.Item) || string.IsNullOrWhiteSpace(p.Match))) { error("Every pair needs an item and a match."); return null; }
                if (pairs.Select(p => Key(p.Item)).Distinct().Count() != pairs.Length) error("Two pairs have the same item text.");
                var tokens = pairs.Select(p => p.Match.Trim()).DistinctBy(Key).ToList();
                var extras = q.ExtraMatches ?? [];
                if (extras.Any(e => tokens.Any(t => Key(t) == Key(e)))) error("An extra match repeats one of the real matches.");
                var options = Bank(id, "", [.. tokens, .. extras], "the matches", error);
                var token = tokens.ToDictionary(Key, t => OptionId(id, "", t));
                var slots = pairs.Select((p, i) => new Slot($"p{i + 1}", p.Item.Trim(), [])).ToArray();
                var matches = pairs.Select((p, i) => (Slot: $"p{i + 1}", Option: token[Key(p.Match)])).ToDictionary(m => m.Slot, m => m.Option);
                return Make(options, slots, 0, tokens.Count < pairs.Length, ScoringPolicy.Partial, [], matches);
            }
            case QuestionKind.Dropdown:
            {
                if (q.Blanks is not { Length: >= 1 } blanks) { error("Dropdown questions need at least one blank."); return null; }
                var slots = new List<Slot>();
                var matches = new Dictionary<string, string>();
                for (var i = 0; i < blanks.Length; i++)
                {
                    var blank = blanks[i];
                    var slotId = $"b{i + 1}";
                    if (string.IsNullOrWhiteSpace(blank.Text) || string.IsNullOrWhiteSpace(blank.Answer) || blank.Distractors is not { Length: >= 1 })
                    {
                        error($"Blank {i + 1} needs text, an answer and at least one distractor.");
                        continue;
                    }
                    slots.Add(new(slotId, blank.Text.Trim(), Bank(id, slotId, [blank.Answer, .. blank.Distractors], $"blank {i + 1}", error)));
                    matches[slotId] = OptionId(id, slotId, blank.Answer);
                }
                return Make([], slots.ToArray(), 0, false, ScoringPolicy.Partial, [], matches);
            }
            default:
            {
                if ((q.Answer is null) == (q.Answers is null)) { error("Give either 'answer' or 'answers'."); return null; }
                JsonElement[] keys = q.Answer is { } single ? [single] : q.Answers!;
                var accepted = keys.Select((k, i) => Typed(k, q.Kind, i, error)).ToArray();
                if (accepted.Any(a => a is null)) return null;
                return Make([], [], 0, false, ScoringPolicy.Exact, accepted.Select(a => a!).ToArray());
            }
        }
    }

    private static string? Text(JsonElement? value, string field, Action<string> error)
    {
        if (value is { ValueKind: JsonValueKind.String } text && !string.IsNullOrWhiteSpace(text.GetString())) return text.GetString()!.Trim();
        error($"'{field}' must be nonblank text.");
        return null;
    }

    private static string[]? Require(string[]? values, string field, Action<string> error)
    {
        if (values is { Length: > 0 }) return values;
        error($"Add at least one entry to '{field}'.");
        return null;
    }

    // Numeric keys may be JSON numbers; code output keys are text, though a bare number is accepted as its literal output.
    private static string? Typed(JsonElement value, QuestionKind kind, int index, Action<string> error)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString();
        if (value.ValueKind == JsonValueKind.Number)
            return kind == QuestionKind.Numeric && value.TryGetDecimal(out var number) ? number.ToString(CultureInfo.InvariantCulture) : value.GetRawText();
        error($"Accepted answer {index + 1} must be a number or text.");
        return null;
    }

    private static Option[] Bank(string questionId, string scope, IEnumerable<string> texts, string label, Action<string> error)
    {
        var list = texts.ToArray();
        if (list.Any(string.IsNullOrWhiteSpace)) { error($"Every entry in {label} needs text."); return []; }
        if (list.Select(Key).Distinct().Count() != list.Length) { error($"Two entries in {label} have the same text."); return []; }
        var options = list.Select(t => new Option(OptionId(questionId, scope, t), t.Trim())).OrderBy(o => o.Id, StringComparer.Ordinal).ToArray();
        if (options.Select(o => o.Id).Distinct().Count() != options.Length) error($"Two entries in {label} produced the same option ID; reword one of them.");
        return options;
    }

    // Opaque, stable IDs from the question, bank and text only. Whether an option is the key must never influence its ID:
    // learners see every option's text and ID, so a role-dependent hash could be recomputed to reveal the answer.
    private static string OptionId(string questionId, string scope, string text) =>
        "o" + System.Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(questionId + "\u001f" + scope + "\u001f" + text.Trim())))[..8];

    private static string Key(string text) => text.Trim().ToLowerInvariant();
}
