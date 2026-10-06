using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class ExamComposerTests
{
    // One objective with a large bank and many objectives with a single question each: a paper must still find the rare ones.
    private static Pack Skewed(int bulk, int rare)
    {
        var demo = CoreTests.Demo();
        var objectives = Enumerable.Range(0, rare + 1).Select(i => new Objective($"o{i}", $"Objective {i}", [])).ToArray();
        Question Item(string id, string objective) => new(id, id, QuestionKind.Single, "Pick a.", [objective],
            [new("a", "A"), new("b", "B")], [], 1, false, new(ScoringPolicy.Exact, ["a"]), "A is right.");
        var questions = Enumerable.Range(0, bulk).Select(i => Item($"bulk-{i}", "o0"))
            .Concat(Enumerable.Range(1, rare).Select(i => Item($"rare-{i}", $"o{i}"))).ToArray();
        var lesson = demo.Lessons[0] with { Id = "all", ObjectiveIds = objectives.Select(o => o.Id).ToArray() };
        return demo with { Objectives = objectives, Lessons = [lesson], Questions = questions, Scenarios = [], Blueprints = [] };
    }

    [Fact]
    public void A_paper_covering_every_objective_is_found_when_most_items_share_one_objective()
    {
        var pack = Skewed(bulk: 150, rare: 20);
        var paper = new Blueprint("paper", "Paper", 25, 30, AssessmentSize.Full, pack.Objectives.Select(o => o.Id).ToArray(), []);

        var selection = ExamComposer.Compose(pack, paper, [], "validation");

        Assert.Equal(25, selection.Length);
        Assert.All(pack.Objectives, o => Assert.Contains(selection, q => q.ObjectiveIds.Contains(o.Id)));
    }

    [Fact]
    public void An_uncoverable_paper_still_fails_explicitly()
    {
        var pack = Skewed(bulk: 30, rare: 20);
        var tooSmall = new Blueprint("paper", "Paper", 10, 30, AssessmentSize.Short, pack.Objectives.Select(o => o.Id).ToArray(), []);

        Assert.Throws<InvalidOperationException>(() => ExamComposer.Compose(pack, tooSmall, [], "validation"));
    }

    // The composer before weighted blueprints, kept verbatim as an oracle: new pruning must never change an unweighted selection.
    private static Question[] PreviousCompose(Pack pack, Blueprint blueprint, HashSet<string> seenFamilies, string seed)
    {
        var groups = pack.Questions.GroupBy(q => q.ScenarioId is { } id ? "case:" + id : "item:" + q.Id)
            .Select(g => g.ToArray()).Where(g => g.All(q => q.ObjectiveIds.Any(blueprint.ObjectiveIds.Contains)))
            .Where(g => g.Select(q => q.FamilyId).Distinct().Count() == g.Length)
            .OrderBy(g => g.Count(q => seenFamilies.Contains(q.FamilyId)))
            .ThenBy(g => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed + g[0].Id))))
            .ToArray();
        var chosen = new List<Question>();
        var steps = 0;
        var coverPerSlot = Math.Max(1, groups.SelectMany(g => g).Select(q => q.ObjectiveIds.Count(blueprint.ObjectiveIds.Contains)).DefaultIfEmpty(1).Max());
        bool Search(int start)
        {
            if (++steps > 100_000) return false;
            var uncovered = blueprint.ObjectiveIds.Count(id => !chosen.Any(q => q.ObjectiveIds.Contains(id)));
            if (uncovered > (blueprint.Count - chosen.Count) * coverPerSlot) return false;
            if (chosen.Count == blueprint.Count) return uncovered == 0
                && blueprint.RequiredKinds.All(kind => chosen.Any(q => q.Kind == kind));
            if (chosen.Count > blueprint.Count || groups.Skip(start).Sum(g => g.Length) + chosen.Count < blueprint.Count) return false;
            for (var i = start; i < groups.Length; i++)
            {
                var group = groups[i];
                if (chosen.Count + group.Length > blueprint.Count || group.Any(q => chosen.Any(c => c.FamilyId == q.FamilyId))) continue;
                chosen.AddRange(group);
                if (Search(i + 1)) return true;
                chosen.RemoveRange(chosen.Count - group.Length, group.Length);
            }
            return false;
        }
        if (!Search(0)) throw new InvalidOperationException("Cannot compose.");
        return chosen.ToArray();
    }

    private static string[]? Ids(Func<Question[]> compose)
    {
        try { return compose().Select(q => q.Id).ToArray(); }
        catch (InvalidOperationException) { return null; }
    }

    private static HashSet<string> RandomSeen(Pack pack, int seed, double share)
    {
        var random = new Random(seed);
        return pack.Questions.Select(q => q.FamilyId).Distinct().Where(_ => random.NextDouble() < share).ToHashSet();
    }

    [Fact]
    public void Unweighted_selection_matches_the_previous_composer()
    {
        var evidence = ContentEngine.Compile(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "packs", "evidence-lab.json"))).Pack!;
        var skewed = Skewed(bulk: 60, rare: 8);
        var cases = new List<(Pack Pack, Blueprint Blueprint)>();
        foreach (var pack in new[] { CoreTests.Demo(), evidence }) cases.AddRange(pack.Blueprints.Select(b => (pack, b)));
        cases.Add((skewed, new Blueprint("paper", "Paper", 12, 30, AssessmentSize.Full, skewed.Objectives.Select(o => o.Id).ToArray(), [])));
        cases.Add((WeightedBank(), WeightedBank().Blueprints[0] with { ObjectiveWeights = null, RequiredKinds = [QuestionKind.Single, QuestionKind.Multiple] }));
        foreach (var (pack, blueprint) in cases)
            for (var seed = 0; seed < 25; seed++)
            {
                var seen = RandomSeen(pack, seed, seed % 2 == 0 ? 0 : 0.4);
                var expected = Ids(() => PreviousCompose(pack, blueprint, seen, "seed-" + seed));
                Assert.NotNull(expected);
                Assert.Equal(expected, Ids(() => ExamComposer.Compose(pack, blueprint, seen, "seed-" + seed)));
            }
    }

    // 120 questions over four weighted domains: 105 standalone items and three five-question case studies.
    private static Pack WeightedBank()
    {
        var demo = CoreTests.Demo();
        string[] domains = ["d1", "d2", "d3", "d4"];
        Question Item(string id, string domain, int n, string? scenario = null) => n % 7 == 3
            ? new(id, id, QuestionKind.Multiple, "Pick two.", [domain], [new("a", "A"), new("b", "B"), new("c", "C")], [], 2, false,
                new(ScoringPolicy.Partial, ["a", "b"]), "A and B are right.", scenario)
            : new(id, id, QuestionKind.Single, "Pick a.", [domain], [new("a", "A"), new("b", "B")], [], 1, false,
                new(ScoringPolicy.Exact, ["a"]), "A is right.", scenario);
        var standalone = new[] { ("d1", 32), ("d2", 27), ("d3", 25), ("d4", 21) }
            .SelectMany(d => Enumerable.Range(0, d.Item2).Select(i => Item($"{d.Item1}-{i}", d.Item1, i)));
        var cases = new (string Id, string[] Primaries)[]
        {
            ("case-a", ["d1", "d2", "d3", "d4", "d1"]), ("case-b", ["d2", "d2", "d3", "d1", "d4"]), ("case-c", ["d3", "d3", "d3", "d3", "d3"])
        };
        var caseItems = cases.SelectMany(c => c.Primaries.Select((d, i) => Item($"{c.Id}-{i}", d, i, c.Id)));
        var weights = new Dictionary<string, decimal> { ["d1"] = 30, ["d2"] = 26, ["d3"] = 24, ["d4"] = 20 };
        var lesson = demo.Lessons[0] with { Id = "all", ObjectiveIds = domains };
        return demo with
        {
            Objectives = domains.Select(d => new Objective(d, d, [])).ToArray(), Lessons = [lesson],
            Questions = standalone.Concat(caseItems).ToArray(), Scenarios = cases.Select(c => new Scenario(c.Id, c.Id, "Background")).ToArray(),
            Blueprints =
            [
                new("full", "Full", 60, 120, AssessmentSize.Full, domains, [QuestionKind.Single, QuestionKind.Multiple], ObjectiveWeights: weights),
                new("short", "Short", 20, 40, AssessmentSize.Short, domains, [QuestionKind.Single, QuestionKind.Multiple], ObjectiveWeights: weights)
            ]
        };
    }

    [Theory]
    [InlineData("full", new[] { 18, 16, 14, 12 })]
    [InlineData("short", new[] { 6, 5, 5, 4 })]
    public void Weighted_blueprint_keeps_each_domain_within_one_of_its_share(string blueprintId, int[] quotas)
    {
        var pack = WeightedBank();
        Assert.Empty(ContentEngine.Validate(pack));
        var blueprint = pack.Blueprints.Single(b => b.Id == blueprintId);
        var withCaseStudy = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            var selection = ExamComposer.Compose(pack, blueprint, RandomSeen(pack, seed, 0.3), "seed-" + seed);
            if (selection.Any(q => q.ScenarioId is not null)) withCaseStudy++;
            Assert.Equal(blueprint.Count, selection.Length);
            Assert.Equal(selection.Length, selection.Select(q => q.FamilyId).Distinct().Count());
            Assert.All(blueprint.RequiredKinds, kind => Assert.Contains(selection, q => q.Kind == kind));
            foreach (var scenario in selection.Select(q => q.ScenarioId).OfType<string>().Distinct())
                Assert.Equal(pack.Questions.Count(q => q.ScenarioId == scenario), selection.Count(q => q.ScenarioId == scenario));
            for (var d = 0; d < quotas.Length; d++)
                Assert.InRange(selection.Count(q => q.ObjectiveIds[0] == blueprint.ObjectiveIds[d]), Math.Max(1, quotas[d] - 1), quotas[d] + 1);
        }
        Assert.InRange(withCaseStudy, 1, 199); // case studies are drawn, but not forced into every paper
    }

    [Fact]
    public void Weighted_selection_is_deterministic_per_seed()
    {
        var pack = WeightedBank();
        var seen = RandomSeen(pack, 7, 0.3);
        Assert.Equal(Ids(() => ExamComposer.Compose(pack, pack.Blueprints[0], seen, "same")),
            Ids(() => ExamComposer.Compose(pack, pack.Blueprints[0], seen, "same")));
    }

    [Fact]
    public void A_weighted_blueprint_without_enough_items_names_the_domain()
    {
        var pack = WeightedBank();
        pack = pack with { Questions = pack.Questions.Where(q => q.ScenarioId is null && (q.ObjectiveIds[0] != "d4" || q.Id is "d4-0" or "d4-1")).ToArray() };
        var error = Assert.Throws<InvalidOperationException>(() => ExamComposer.Compose(pack, pack.Blueprints[0], [], "validation"));
        Assert.Contains("'d4'", error.Message);
        Assert.Contains(ContentEngine.Validate(pack), d => d.Path == "full" && d.Message.Contains("'d4'"));
    }

    [Fact]
    public void Objective_weights_must_cover_exactly_the_blueprint_objectives()
    {
        var pack = WeightedBank();
        var full = pack.Blueprints[0];
        Pack With(Dictionary<string, decimal> weights) => pack with { Blueprints = [full with { ObjectiveWeights = weights }] };
        Assert.Contains(ContentEngine.Validate(With(new() { ["d1"] = 1, ["d2"] = 1, ["d3"] = 1 })), d => d.Message.Contains("objectiveWeights"));
        Assert.Contains(ContentEngine.Validate(With(new() { ["d1"] = 1, ["d2"] = 1, ["d3"] = 1, ["d4"] = 0 })), d => d.Message.Contains("objectiveWeights"));
        Assert.Contains(ContentEngine.Validate(With(new() { ["d1"] = 1, ["d2"] = 1, ["d3"] = 1, ["d4"] = 1, ["d5"] = 1 })), d => d.Message.Contains("objectiveWeights"));
        Assert.Empty(ContentEngine.Validate(With(new() { ["d1"] = 1, ["d2"] = 1, ["d3"] = 1, ["d4"] = 1 })));
    }

    [Fact]
    public void Unweighted_blueprints_serialize_without_weights()
    {
        var blueprint = CoreTests.Demo().Blueprints[0];
        Assert.DoesNotContain("objectiveWeights", Json.Write(blueprint));
        Assert.Contains("\"objectiveWeights\"", Json.Write(WeightedBank().Blueprints[0]));
    }
}
