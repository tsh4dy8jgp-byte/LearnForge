using System.Text.RegularExpressions;

namespace LearnForge.Core;

// Heuristic item-quality checks for a compiled pack. Warnings never block publishing; they flag the cues that let learners
// guess an answer without knowing it. Messages describe lengths, counts and prompt words, never a key's text.
public static partial class ContentLinter
{
    private static readonly HashSet<string> Absolutes =
    [
        "always", "never", "every", "everyone", "everything", "none", "nothing", "only", "guaranteed", "guarantees", "guarantee",
        "completely", "entirely", "totally", "impossible", "certainly", "certain", "absolutely", "proves", "proven"
    ];
    private static readonly HashSet<string> Hedges =
        ["usually", "often", "typically", "generally", "may", "might", "sometimes", "likely", "tends", "frequently", "probably"];
    private static readonly HashSet<string> Stopwords =
    [
        "which", "these", "those", "there", "their", "about", "would", "should", "could", "following", "option", "options", "answer",
        "answers", "statement", "statements", "select", "choose", "using", "after", "before", "other", "where", "while", "being",
        "between", "under", "within", "without", "first", "best", "most", "what", "when", "whose", "whom"
    ];
    private static readonly HashSet<string> RevealingIdTokens = ["correct", "right", "answer", "key", "wrong", "incorrect", "distractor", "decoy"];

    public static Diagnostic[] Lint(Pack pack)
    {
        var warnings = new List<Diagnostic>();
        void Warn(string code, string path, string message) => warnings.Add(new(code, path, message));
        foreach (var q in pack.Questions) Question(q, Warn);
        Bank(pack, Warn);
        return warnings.ToArray();
    }

    private static void Question(Question q, Action<string, string, string> warn)
    {
        var keys = q.Options.Where(o => q.Grading.Correct.Contains(o.Id)).ToArray();
        var distractors = q.Options.Where(o => !q.Grading.Correct.Contains(o.Id)).ToArray();
        var choice = q.Kind is QuestionKind.Single or QuestionKind.Multiple;
        if (choice && keys.Length > 0 && distractors.Length > 0)
        {
            KeyLength(q.Id, q.Kind, keys, distractors, warn);
            Qualifiers(q.Id, keys, distractors, warn);
            if (distractors.Length >= 2) StemEcho(q.Id, q.Prompt, keys, distractors, warn);
            KeyInStem(q.Id, q.Prompt, keys, distractors, warn);
            ArticleCue(q.Id, q.Prompt, keys, distractors, warn);
        }
        if (q.Kind == QuestionKind.Dropdown && q.Grading.Matches is { } blanks)
            foreach (var slot in q.Slots)
            {
                var key = slot.Options.Where(o => o.Id == blanks.GetValueOrDefault(slot.Id)).ToArray();
                var others = slot.Options.Where(o => o.Id != blanks.GetValueOrDefault(slot.Id)).ToArray();
                if (key.Length == 0 || others.Length == 0) continue;
                var path = q.Id + "." + slot.Id;
                KeyLength(path, QuestionKind.Single, key, others, warn);
                KeyInStem(path, slot.Text + " " + q.Prompt, key, others, warn);
                ArticleCue(path, slot.Text, key, others, warn);
            }

        var banks = new[] { q.Options }.Concat(q.Slots.Select(s => s.Options)).Where(b => b.Length > 0).ToArray();
        var texts = banks.SelectMany(b => b).Select(o => o.Text).Append(q.Prompt).Append(q.Explanation);
        if (banks.SelectMany(b => b).Any(o => CatchAll().IsMatch(o.Text) || PairedLetters().IsMatch(o.Text)) || texts.Any(t => LetterReference().IsMatch(t)))
            warn("LF207", q.Id, "Avoid “all/none of the above”, “both A and B” and references to option letters: options are shuffled and unlabeled.");
        if (banks.Any(b => b.Select(o => Normalize(o.Text)).Distinct().Count() != b.Length))
            warn("LF208", q.Id, "Two options read the same once case and punctuation are ignored.");

        switch (q.Kind)
        {
            case QuestionKind.Single when q.Options.Length < 4:
                warn("LF209", q.Id, $"Offer at least four options; with {q.Options.Length}, a guess is right {100 / Math.Max(1, q.Options.Length)}% of the time.");
                break;
            case QuestionKind.Multiple when q.Options.Length - q.SelectCount < 2:
                warn("LF209", q.Id, "Offer at least two distractors so the last choice is not a free pick.");
                break;
            case QuestionKind.Dropdown:
                foreach (var slot in q.Slots.Where(s => s.Options.Length < 3))
                    warn("LF209", q.Id + "." + slot.Id, $"Give each blank at least three choices; with {slot.Options.Length}, a guess is right {100 / Math.Max(1, slot.Options.Length)}% of the time.");
                break;
            case QuestionKind.Matching when !q.Reuse && q.Options.Length <= q.Slots.Length:
                warn("LF209", q.Id, "Add an extra match so the last item cannot be solved by elimination.");
                break;
            case QuestionKind.Sequence when q.Options.Length is < 3 or > 8:
                warn("LF209", q.Id, $"Use three to eight steps; this sequence has {q.Options.Length}.");
                break;
        }

        if (banks.SelectMany(b => b).Any(o => IdTokens().Split(o.Id).Any(RevealingIdTokens.Contains)))
            warn("LF210", q.Id, "Option IDs reach the browser; an ID that names its role reveals the key. Use neutral IDs or the exam/1 format.");
        else if (q.Kind == QuestionKind.Sequence && OrderedIds(q.Grading.Correct))
            warn("LF210", q.Id, "These step IDs ascend in the correct order, which the browser can see. Use neutral IDs or the exam/1 format.");

        var lastSentence = Sentences().Split(q.Prompt.Trim()).LastOrDefault(s => s.Trim().Length > 0) ?? "";
        if (Negative().IsMatch(lastSentence))
            warn("LF211", q.Id, "Emphasize the negative as NOT, EXCEPT or LEAST, or rephrase the question positively.");
        if (q.Explanation.Trim().Length < 60)
            warn("LF212", q.Id, "Explain why the key is right and why the most tempting distractor is wrong (at least 60 characters).");
    }

    private static void KeyLength(string path, QuestionKind kind, Option[] keys, Option[] distractors, Action<string, string, string> warn)
    {
        var keyLengths = keys.Select(o => o.Text.Trim().Length).ToArray();
        var lengths = distractors.Select(o => o.Text.Trim().Length).ToArray();
        double mean = lengths.Average(), keyMean = keyLengths.Average();
        var cue = kind == QuestionKind.Multiple
            ? keyLengths.Min() > lengths.Max() && keyMean >= 1.3 * mean && keyMean - mean >= 10
            : keyLengths[0] > lengths.Max() && keyLengths[0] >= 1.3 * mean && keyLengths[0] - mean >= 10;
        if (cue)
            warn("LF201", path, $"The key is {keyMean:0} characters; the distractors average {mean:0}. Keep options within about 15% of each other in length.");
    }

    private static void Qualifiers(string path, Option[] keys, Option[] distractors, Action<string, string, string> warn)
    {
        var absoluteDistractors = distractors.Count(o => Words(o.Text).Any(Absolutes.Contains));
        var absoluteKey = keys.Any(o => Words(o.Text).Any(Absolutes.Contains));
        var hedgedKey = keys.Any(o => Words(o.Text).Any(Hedges.Contains));
        var hedgedDistractor = distractors.Any(o => Words(o.Text).Any(Hedges.Contains));
        if ((absoluteDistractors >= 2 && !absoluteKey) || (hedgedKey && !hedgedDistractor && absoluteDistractors >= 1))
            warn("LF203", path, "Absolute words (always, never, only, …) appear only in distractors or hedges (usually, may, …) only in the key; test-wise learners eliminate on that alone.");
    }

    private static void StemEcho(string path, string prompt, Option[] keys, Option[] distractors, Action<string, string, string> warn)
    {
        static string Root(string word) => word.Length > 6 ? word[..6] : word;
        HashSet<string> Roots(IEnumerable<Option> options) => options.SelectMany(o => Words(o.Text)).Where(w => w.Length >= 5).Select(Root).ToHashSet();
        var keyRoots = Roots(keys);
        var distractorRoots = Roots(distractors);
        var echoed = Words(prompt).Where(w => w.Length >= 5 && !Stopwords.Contains(w)).DistinctBy(Root)
            .Where(w => keyRoots.Contains(Root(w)) && !distractorRoots.Contains(Root(w))).ToArray();
        if (echoed.Length > 0)
            warn("LF204", path, $"The prompt word(s) {string.Join(", ", echoed.Select(w => "“" + w + "”"))} reappear in the key but in no distractor. Reword the key or reuse the term in a distractor.");
    }

    private static void KeyInStem(string path, string stem, Option[] keys, Option[] distractors, Action<string, string, string> warn)
    {
        var text = " " + Normalize(stem) + " ";
        bool Inside(Option o) => Normalize(o.Text) is { Length: >= 4 } n && text.Contains(" " + n + " ");
        if (keys.Any(Inside) && !distractors.Any(Inside))
            warn("LF205", path, "The key's wording appears in the stem while no distractor's does.");
    }

    private static void ArticleCue(string path, string stem, Option[] keys, Option[] distractors, Action<string, string, string> warn)
    {
        var article = Words(stem.TrimEnd(' ', ':', '…', '_', '.')).LastOrDefault();
        if (article is not ("a" or "an")) return;
        bool Agrees(Option o) => (article == "an") == "aeiou".Contains(char.ToLowerInvariant(o.Text.TrimStart().FirstOrDefault()));
        if (keys.All(Agrees) && distractors.Any(o => !Agrees(o)))
            warn("LF206", path, $"The stem ends with “{article}”, which only some options fit grammatically. End the stem before the article or make every option agree.");
    }

    // Single letters or integers that ascend in key order, after removing their shared prefix (a–d, s1–s4, step-1–step-4).
    private static bool OrderedIds(string[] ids)
    {
        if (ids.Length < 2) return false;
        var prefix = ids.Aggregate((a, b) => new string(a.Zip(b).TakeWhile(p => p.First == p.Second).Select(p => p.First).ToArray()));
        var rest = ids.Select(id => id[prefix.Length..]).ToArray();
        if (rest.All(r => r.Length == 1 && char.IsAsciiLetterLower(r[0])))
            return rest.Zip(rest.Skip(1)).All(p => string.CompareOrdinal(p.First, p.Second) < 0);
        if (rest.All(r => r.Length > 0 && r.All(char.IsAsciiDigit)))
            return rest.Select(r => long.Parse(r)).Zip(rest.Skip(1).Select(r => long.Parse(r))).All(p => p.First < p.Second);
        return false;
    }

    private static void Bank(Pack pack, Action<string, string, string> warn)
    {
        var singles = pack.Questions.Where(q => q.Kind == QuestionKind.Single && q.Options.Length >= 3).ToArray();
        if (singles.Length >= 20)
        {
            int Extreme(Func<int, int, bool> beats) => singles.Count(q =>
            {
                var key = q.Options.First(o => o.Id == q.Grading.Correct[0]).Text.Trim().Length;
                return q.Options.Where(o => o.Id != q.Grading.Correct[0]).All(o => beats(key, o.Text.Trim().Length));
            });
            var longest = Extreme((key, other) => key > other);
            var shortest = Extreme((key, other) => key < other);
            if (longest * 10 > singles.Length * 4)
                warn("LF202", "questions", $"The key is the longest option in {longest} of {singles.Length} single-choice questions; aim for about one in {singles.Average(q => q.Options.Length):0}.");
            if (shortest * 10 > singles.Length * 4)
                warn("LF202", "questions", $"The key is the shortest option in {shortest} of {singles.Length} single-choice questions; vary which option is longest.");
        }
        var singleKeys = pack.Questions.Where(q => q.Kind == QuestionKind.Single && q.Grading.Correct.Length == 1).Select(q => q.Grading.Correct[0]).ToArray();
        if (singleKeys.Length >= 5 && singleKeys.GroupBy(k => k).Max(g => g.Count()) * 2 >= singleKeys.Length)
            warn("LF210", "questions", $"One option ID is the key in at least half of the {singleKeys.Length} single-choice questions, and IDs reach the browser. Use neutral IDs or the exam/1 format.");

        // Whole content, not just the prompt: generic instructions ("Arrange these numbers…") legitimately repeat across items.
        static string Content(Question q) => string.Join("|", new[] { q.ScenarioId ?? "", Normalize(q.Prompt), q.Code?.Source ?? "" }
            .Concat(q.Options.Select(o => Normalize(o.Text)).Order())
            .Concat(q.Slots.Select(s => Normalize(s.Text) + ":" + string.Join(",", s.Options.Select(o => Normalize(o.Text)).Order()))));
        foreach (var group in pack.Questions.GroupBy(Content).Where(g => g.Count() > 1))
            foreach (var q in group.Skip(1).Where(q => q.FamilyId != group.First().FamilyId))
                warn("LF213", q.Id, $"Same content as {group.First().Id} in a different family, so one mock can show both. Give true variants one family or remove the copy.");

        if (pack.Questions.Length >= 20)
        {
            var top = pack.Questions.GroupBy(q => q.Kind).MaxBy(g => g.Count())!;
            if (top.Count() * 10 > pack.Questions.Length * 7)
                warn("LF220", "questions", $"{top.Count()} of {pack.Questions.Length} questions are {Camel(top.Key)}; mix in other formats the exam uses.");
            else if (pack.Questions.Select(q => q.Kind).Distinct().Count() < 3)
                warn("LF220", "questions", "The bank uses fewer than three question formats.");
        }

        var reported = new HashSet<string>();
        foreach (var b in pack.Blueprints.Where(b => b.ObjectiveWeights is not null))
        {
            var primary = pack.Questions.Select(q => q.ObjectiveIds.FirstOrDefault(b.ObjectiveIds.Contains)).OfType<string>().ToArray();
            var total = b.ObjectiveWeights!.Values.Sum();
            foreach (var (objective, weight) in b.ObjectiveWeights)
            {
                var actual = primary.Count(p => p == objective);
                var expected = (double)(primary.Length * weight / total);
                if ((actual < 0.75 * expected || actual > 1.5 * expected) && Math.Abs(actual - expected) >= 3 && reported.Add(objective))
                    warn("LF221", objective, $"{actual} questions have this as their first objective; its weight suggests about {expected:0}.");
            }
        }

        if (pack.Goal == CourseGoal.Readiness && pack.Features.Assessments && pack.Blueprints.Length > 0)
        {
            var policy = pack.Readiness ?? new();
            var plans = pack.Blueprints.Select(b =>
            {
                var families = pack.Questions.Where(q => q.ObjectiveIds.Any(b.ObjectiveIds.Contains)).Select(q => q.FamilyId).Distinct().Count();
                var attempts = b.Size == AssessmentSize.Short ? policy.ShortAttempts : policy.FullAttempts;
                var fresh = (int)Math.Ceiling(policy.MinimumFreshPercent * b.Count / 100m);
                var need = policy.MinimumFreshPercent == 0 ? b.Count : (attempts - 1) * b.Count + fresh;
                return (Blueprint: b, Attempts: attempts, Families: families, Need: need);
            }).ToArray();
            if (plans.All(p => p.Need > p.Families))
            {
                var closest = plans.MinBy(p => p.Need - p.Families);
                warn("LF222", "readiness", $"Readiness needs {closest.Need} distinct question families for {closest.Attempts} {Camel(closest.Blueprint.Size)} mocks of " +
                    $"{closest.Blueprint.Count} at {policy.MinimumFreshPercent}% fresh questions, but the bank has {closest.Families}. Add families or lower " +
                    "minimumFreshPercent or the attempt counts; learning-mode practice also uses up fresh families.");
            }
        }

        foreach (var scenario in pack.Scenarios)
            if (pack.Questions.Count(q => q.ScenarioId == scenario.Id) < 2)
                warn("LF223", scenario.Id, "A case study needs at least two questions; otherwise fold its background into the question.");
    }

    private static string[] Words(string text) => Word().Matches(text).Select(m => m.Value.ToLowerInvariant()).ToArray();
    private static string Normalize(string text) => string.Join(' ', Words(text));
    private static string Camel<T>(T value) where T : Enum => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();
    [GeneratedRegex(@"\b(all|none|both|neither|any)\s+of\s+the\s+(above|below|previous|following|options|answers|choices)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CatchAll();
    [GeneratedRegex(@"^\s*(both|neither)\s+[a-f]\s+(and|nor)\s+[a-f]\b", RegexOptions.IgnoreCase)]
    private static partial Regex PairedLetters();
    [GeneratedRegex(@"\b(?i:option|answer|choice)\s+\(?[A-F]\)?(?=[\s,.;:)!?]|$)")]
    private static partial Regex LetterReference();
    [GeneratedRegex(@"[-_.]")]
    private static partial Regex IdTokens();
    [GeneratedRegex(@"(?<=[.?!])\s+")]
    private static partial Regex Sentences();
    [GeneratedRegex(@"\b(not|except)\b|(?<!\bat )\bleast\b")]
    private static partial Regex Negative();
}
