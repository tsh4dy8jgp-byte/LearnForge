using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LearnForge.Core;

public static partial class ContentEngine
{
    public const int MaxSourceBytes = 2_000_000;
    public static Compilation Compile(string source)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        try
        {
            if (Encoding.UTF8.GetByteCount(source) > MaxSourceBytes) throw new InvalidOperationException("Pack source exceeds 2 MB.");
            var root = JsonNode.Parse(source, documentOptions: new JsonDocumentOptions { MaxDepth = 32 }) as JsonObject
                ?? throw new InvalidOperationException("A pack must be a JSON object.");
            var templates = root["templates"] as JsonObject ?? new JsonObject();
            var node = Expand(root["pack"] ?? root, templates, new JsonObject(), 0, new ExpansionBudget());
            // A direct pack has no "format" property (unknown properties are rejected), so its presence selects a compact source.
            if (node is JsonObject compact && compact.ContainsKey("format"))
            {
                var format = compact["format"] is JsonValue value && value.TryGetValue<string>(out var name) ? name : null;
                if (format != ExamSourceAdapter.Format) throw new InvalidOperationException($"Unknown source format '{format}'. Supported: {ExamSourceAdapter.Format}.");
                var exam = compact.Deserialize<ExamSource>(Json.Options) ?? throw new JsonException("Missing exam source.");
                var conversion = new List<Diagnostic>();
                var converted = ExamSourceAdapter.ToPack(exam, conversion);
                return converted is null ? new(null, conversion.ToArray(), hash) : new(converted, Validate(converted), hash);
            }
            var pack = node!.Deserialize<Pack>(Json.Options) ?? throw new JsonException("Missing pack.");
            if (pack.Profile == ProductProfile.Course && node is JsonObject body && !body.ContainsKey("goal"))
                pack = pack with { Goal = CourseGoal.Completion };
            var errors = Validate(pack);
            return new(pack, errors, hash);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or NullReferenceException)
        {
            return new(null, [new("LF001", "$", e is JsonException j ? $"{j.Message}" : e.Message)], hash);
        }
    }

    private static JsonNode? Expand(JsonNode? node, JsonObject templates, JsonObject values, int depth, ExpansionBudget budget)
    {
        if (++budget.Nodes > 100_000) throw new InvalidOperationException("Template expansion exceeds the node budget.");
        if (depth > 24) throw new InvalidOperationException("Template expansion depth exceeded; check recursive $use references.");
        if (node is JsonObject obj)
        {
            if (obj["$use"] is JsonValue use)
            {
                var name = use.GetValue<string>();
                if (templates[name] is not { } template) throw new InvalidOperationException($"Unknown template '{name}'.");
                var args = new JsonObject();
                foreach (var pair in values) args[pair.Key] = pair.Value?.DeepClone();
                if (obj["values"] is JsonObject supplied)
                    foreach (var pair in supplied) args[pair.Key] = Expand(pair.Value, templates, values, depth + 1, budget);
                return Expand(template, templates, args, depth + 1, budget);
            }
            var result = new JsonObject();
            foreach (var pair in obj) result[pair.Key] = Expand(pair.Value, templates, values, depth + 1, budget);
            return result;
        }
        if (node is JsonArray array) return new JsonArray(array.Select(n => Expand(n, templates, values, depth + 1, budget)).ToArray());
        if (node is JsonValue scalar && scalar.TryGetValue<string>(out var text))
        {
            budget.Characters += text.Length;
            if (budget.Characters > 4_000_000) throw new InvalidOperationException("Template expansion exceeds the text budget.");
            var matches = Variable().Matches(text);
            if (matches.Count == 1 && matches[0].Value.Length == text.Length)
            {
                var key = matches[0].Groups[1].Value;
                var replacement = values[key] ?? throw new InvalidOperationException($"Missing template value '{key}'.");
                budget.Characters += replacement.ToJsonString().Length;
                if (budget.Characters > 4_000_000) throw new InvalidOperationException("Template expansion exceeds the text budget.");
                return replacement.DeepClone();
            }
            return JsonValue.Create(Variable().Replace(text, m =>
            {
                var value = values[m.Groups[1].Value]?.ToString()
                    ?? throw new InvalidOperationException($"Missing template value '{m.Groups[1].Value}'.");
                budget.Characters += value.Length;
                if (budget.Characters > 4_000_000) throw new InvalidOperationException("Template expansion exceeds the text budget.");
                return value;
            }));
        }
        return node?.DeepClone();
    }

    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}")]
    private static partial Regex Variable();
    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]{0,99}$")]
    private static partial Regex Identifier();

    public static Diagnostic[] Validate(Pack p)
    {
        var errors = new List<Diagnostic>();
        void Error(string path, string message) => errors.Add(new("LF100", path, message));
        void Ids(string path, IEnumerable<string> ids)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids)
                if (string.IsNullOrWhiteSpace(id) || !Identifier().IsMatch(id) || !seen.Add(id)) Error(path, $"Invalid or duplicate ID '{id}'.");
        }
        if (p.SchemaVersion != 1) Error("schemaVersion", "Only schema version 1 is supported.");
        Ids("id", [p.Id]);
        if (!Version.TryParse(p.Version, out _)) Error("version", "Use a numeric release version, e.g. 1.0.0.");
        if (string.IsNullOrWhiteSpace(p.Title) || string.IsNullOrWhiteSpace(p.License)) Error("title", "Title and license are required.");
        var features = p.Features;
        var questionsEnabled = features.Practice || features.Assessments;
        if (!Enum.IsDefined(p.Profile)) Error("profile", "Profile must be course, exam or hybrid.");
        if (!features.Lessons && !questionsEnabled) Error("capabilities", "Enable lessons, practice or assessments.");
        if (p.Objectives.Length is < 1 or > 200 || p.Questions.Length > 2000 || p.Lessons.Length > 500)
            Error("pack", "Require 1–200 objectives, at most 2000 questions and at most 500 lessons.");
        if (features.Lessons != (p.Lessons.Length > 0)) Error("lessons", features.Lessons
            ? "The lessons capability requires at least one lesson." : "Remove lessons or enable the lessons capability.");
        if (questionsEnabled != (p.Questions.Length > 0)) Error("questions", questionsEnabled
            ? "Practice or assessments require at least one question." : "Remove questions or enable practice or assessments.");
        if (!questionsEnabled && (p.Blueprints.Length > 0 || p.Scenarios.Length > 0))
            Error("capabilities", "Blueprints and scenarios require practice or assessments.");
        if (p.Goal == CourseGoal.Completion && !features.Lessons) Error("goal", "A completion goal requires lessons.");
        if (p.Goal == CourseGoal.Readiness && !features.Assessments) Error("goal", "A readiness goal requires assessments.");
        if (p.Goal == CourseGoal.Mastery && !questionsEnabled) Error("goal", "A mastery goal requires practice or assessments.");
        Ids("objectives", p.Objectives.Select(o => o.Id));
        Ids("lessons", p.Lessons.Select(l => l.Id));
        Ids("questions", p.Questions.Select(q => q.Id));
        Ids("scenarios", p.Scenarios.Select(s => s.Id));
        Ids("blueprints", p.Blueprints.Select(b => b.Id));
        var objectiveIds = p.Objectives.Select(o => o.Id).ToHashSet();
        void References(string path, string[] ids)
        {
            if (ids.Length == 0 || ids.Distinct().Count() != ids.Length || ids.Any(id => !objectiveIds.Contains(id))) Error(path, "Objectives must be nonempty, distinct, and resolve in this pack.");
        }
        foreach (var objective in p.Objectives)
        {
            if (objective.Prerequisites.Any(id => !objectiveIds.Contains(id))) Error(objective.Id, "Unknown prerequisite.");
            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();
            bool Cycle(string id)
            {
                if (visited.Contains(id)) return false;
                if (!visiting.Add(id)) return true;
                var found = p.Objectives.FirstOrDefault(o => o.Id == id);
                var result = found?.Prerequisites.Any(Cycle) == true;
                visiting.Remove(id); visited.Add(id); return result;
            }
            if (Cycle(objective.Id)) Error(objective.Id, "Prerequisite cycle.");
            if (features.Lessons && !p.Lessons.Any(l => l.ObjectiveIds.Contains(objective.Id))) Error(objective.Id, "Objective has no teaching lesson.");
            if (questionsEnabled && !p.Questions.Any(q => q.ObjectiveIds.Contains(objective.Id))) Error(objective.Id, "Objective has no questions.");
        }
        foreach (var l in p.Lessons)
        {
            References(l.Id, l.ObjectiveIds);
            if (string.IsNullOrWhiteSpace(l.Title) || l.Blocks.Length == 0) Error(l.Id, "A lesson requires a title and blocks.");
            if (l.Blocks.Any(b => b.Kind is not (ContentBlockKind.Text or ContentBlockKind.Callout or ContentBlockKind.Example or ContentBlockKind.Code))) Error(l.Id, "Unsupported block. Use text, callout, example or code.");
        }
        foreach (var q in p.Questions)
        {
            References(q.Id, q.ObjectiveIds);
            if (string.IsNullOrWhiteSpace(q.Prompt) || string.IsNullOrWhiteSpace(q.Explanation) || string.IsNullOrWhiteSpace(q.FamilyId)) Error(q.Id, "Prompt, explanation and family ID are required.");
            if (q.Weight <= 0 || q.Weight > 100) Error(q.Id, "Weight must be within (0, 100].");
            if (q.ScenarioId is { } sid && !p.Scenarios.Any(s => s.Id == sid)) Error(q.Id, "Unknown scenario.");
            Ids(q.Id + ".options", q.Options.Select(o => o.Id));
            Ids(q.Id + ".slots", q.Slots.Select(s => s.Id));
            var options = q.Options.Select(o => o.Id).ToHashSet();
            if (q.Grading.Policy is not (ScoringPolicy.Exact or ScoringPolicy.Partial)) Error(q.Id, "Scoring policy must be exact or partial.");
            if (q.Kind is QuestionKind.Single or QuestionKind.Multiple or QuestionKind.Sequence)
            {
                if (q.Options.Length < 2 || q.Grading.Correct.Distinct().Count() != q.Grading.Correct.Length || q.Grading.Correct.Any(id => !options.Contains(id))) Error(q.Id, "Invalid option/key contract.");
                if (q.Kind == QuestionKind.Single && (q.SelectCount != 1 || q.Grading.Correct.Length != 1)) Error(q.Id, "Single choice needs one key and one selection.");
                if (q.Kind == QuestionKind.Multiple && (q.SelectCount < 2 || q.SelectCount >= q.Options.Length || q.SelectCount != q.Grading.Correct.Length)) Error(q.Id, "Select-N requires N keys and at least one distractor.");
                if (q.Kind == QuestionKind.Sequence && !options.SetEquals(q.Grading.Correct)) Error(q.Id, "Sequence key must be a complete permutation.");
                if (q.Slots.Length > 0 || q.Grading.Matches is { Count: > 0 }) Error(q.Id, "Choice and sequence questions cannot contain slots/matches.");
            }
            else if (q.Kind is QuestionKind.Matching or QuestionKind.Dropdown)
            {
                if (q.Slots.Length == 0 || q.Grading.Correct.Length > 0 || q.Grading.Matches is null || !q.Slots.Select(s => s.Id).ToHashSet().SetEquals(q.Grading.Matches.Keys)) Error(q.Id, "Every slot requires exactly one match key.");
                foreach (var slot in q.Slots)
                {
                    Ids(q.Id + "." + slot.Id, slot.Options.Select(o => o.Id));
                    var bank = q.Kind == QuestionKind.Matching ? q.Options : slot.Options;
                    if (bank.Length < 2 || !bank.Any(o => o.Id == q.Grading.Matches?.GetValueOrDefault(slot.Id))) Error(q.Id, "A slot key does not resolve in its option bank.");
                }
                if (q.Kind == QuestionKind.Matching && !q.Reuse && q.Grading.Matches is { } keys && keys.Values.Distinct().Count() != keys.Count) Error(q.Id, "Matching key reuses a token while reuse is disabled.");
            }
            else if (q.Kind is QuestionKind.Numeric or QuestionKind.CodeOutput)
            {
                if (q.Options.Length > 0 || q.Slots.Length > 0 || q.SelectCount != 0 || q.Grading.Matches is { Count: > 0 }) Error(q.Id, "Typed-response questions cannot contain options, slots, matches or a selection count.");
                if (q.Grading.Policy != ScoringPolicy.Exact) Error(q.Id, "Typed-response questions use exact scoring.");
                if (q.Grading.Correct.Length is < 1 or > 20) Error(q.Id, "Typed-response questions need 1–20 accepted answers.");
                if (q.Kind == QuestionKind.Numeric)
                {
                    if (q.Grading.Correct.Any(key => !ResponseText.TryParseNumber(key, out _))) Error(q.Id, "Numeric keys must be invariant-culture numbers such as 4 or -2.5.");
                    if (q.Grading.Tolerance is < 0) Error(q.Id, "Numeric tolerance cannot be negative.");
                }
                else
                {
                    if (q.Grading.Correct.Any(key => key.Length > ResponseText.MaxLength || ResponseText.NormalizeOutput(key).Length == 0)) Error(q.Id, $"Accepted outputs must be nonblank and at most {ResponseText.MaxLength} characters.");
                    if (q.Code is not { } code || !Identifier().IsMatch(code.Language) || string.IsNullOrWhiteSpace(code.Source) || code.Source.Length > 20_000)
                        Error(q.Id, "Code-output questions need a program of at most 20,000 characters and a lowercase language ID.");
                }
            }
            else Error(q.Id, $"Unsupported question kind '{q.Kind}'.");
            if (q.Kind != QuestionKind.CodeOutput && q.Code is not null) Error(q.Id, "Only code-output questions carry a program.");
            if (q.Kind != QuestionKind.Numeric && q.Grading.Tolerance is not null) Error(q.Id, "Only numeric questions have a tolerance.");
            if (q.KnowledgeLevel is { } level && !Enum.IsDefined(level)) Error(q.Id, "Invalid knowledgeLevel.");
        }
        foreach (var source in p.Sources)
            if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https") Error("sources", "Reference URLs must use HTTPS.");
        foreach (var b in p.Blueprints)
        {
            References(b.Id, b.ObjectiveIds);
            if (b.Size is not (AssessmentSize.Short or AssessmentSize.Full) || b.Count < 1 || b.Count > 100 || b.Minutes is < 1 or > 600) Error(b.Id, "Invalid size/count/duration.");
            if (b.ObjectiveWeights is { } weights && (!weights.Keys.ToHashSet().SetEquals(b.ObjectiveIds) || weights.Values.Any(w => w is <= 0 or > 1000)))
                Error(b.Id, "objectiveWeights needs one positive weight (at most 1000) for each blueprint objective.");
            if (b.PassPoints is { } pass)
            {
                if (b.QuestionIds is null) Error(b.Id, "passPoints requires a fixed paper with a known possible score.");
                else
                {
                    var possible = p.Questions.Where(q => b.QuestionIds.Contains(q.Id)).Sum(q => q.Weight);
                    if (pass <= 0 || pass > possible) Error(b.Id, "passPoints must be positive and no greater than the paper's possible points.");
                }
            }
            if (errors.Count == 0)
                try { ExamComposer.Compose(p, b, [], "validation"); }
                catch (InvalidOperationException ex) { Error(b.Id, ex.Message); }
        }
        if (questionsEnabled && p.Blueprints.Length == 0) Error("blueprints", "Practice or assessments require at least one session blueprint.");
        var r = p.Readiness ?? new();
        if (r.ShortAttempts is < 1 or > 20 || r.FullAttempts is < 1 or > 20 || r.Threshold is < 0 or >= 100 || r.LookbackDays is < 1 or > 365 || r.MinimumFreshPercent is < 0 or > 100) Error("readiness", "Readiness policy is out of bounds.");
        if (!Enum.IsDefined(p.Goal)) Error("goal", "Goal must be readiness, mastery or completion.");
        var m = p.Mastery ?? new();
        if (m.MinimumEvidence < 1 || m.Window < m.MinimumEvidence || m.Window > 20 || m.ProficientPercent is < 50 or > 100 || m.ReviewAfterDays is < 1 or > 365)
            Error("mastery", "Mastery policy is out of bounds: 1 ≤ minimumEvidence ≤ window ≤ 20, proficientPercent 50–100, reviewAfterDays 1–365.");
        else if (p.Goal == CourseGoal.Mastery)
            // Like blueprint feasibility: an objective with too few families could never become proficient.
            foreach (var objective in p.Objectives)
                if (p.Questions.Where(q => q.ObjectiveIds.Contains(objective.Id)).Select(q => q.FamilyId).Distinct().Count() < m.MinimumEvidence)
                    Error(objective.Id, $"A mastery goal needs at least {m.MinimumEvidence} question families for this objective.");
        return errors.ToArray();
    }
}
