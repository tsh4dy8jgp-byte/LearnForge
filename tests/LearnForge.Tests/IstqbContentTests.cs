using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class IstqbContentTests
{
    private static Pack Pack() => TestApi.LoadPack("istqb-ctfl-4");
    private static readonly string[] Papers = ["a", "b", "c", "d", "e", "f", "g"];

    // Independently transcribed groups from the CTFL table, not inferred from the authored bank.
    private static readonly (string Objectives, int Count, KnowledgeLevel Level)[] Groups =
    [
        ("1.1.1 1.2.2", 1, KnowledgeLevel.K1), ("1.5.2", 1, KnowledgeLevel.K1),
        ("1.1.2 1.2.1 1.2.3", 1, KnowledgeLevel.K2), ("1.3.1", 1, KnowledgeLevel.K2),
        ("1.4.1 1.4.2 1.4.3 1.4.4 1.4.5", 3, KnowledgeLevel.K2), ("1.5.1 1.5.3", 1, KnowledgeLevel.K2),
        ("2.1.2", 1, KnowledgeLevel.K1), ("2.1.3", 1, KnowledgeLevel.K1),
        ("2.2.1 2.2.2", 1, KnowledgeLevel.K2), ("2.2.3 2.3.1", 1, KnowledgeLevel.K2),
        ("2.1.1 2.1.6", 1, KnowledgeLevel.K2), ("2.1.4 2.1.5", 1, KnowledgeLevel.K2),
        ("3.1.1 3.2.1 3.2.3 3.2.5", 2, KnowledgeLevel.K1),
        ("3.1.2 3.1.3", 1, KnowledgeLevel.K2), ("3.2.2 3.2.4", 1, KnowledgeLevel.K2),
        ("4.1.1", 1, KnowledgeLevel.K2), ("4.3.1 4.3.2 4.3.3", 2, KnowledgeLevel.K2),
        ("4.4.1 4.4.2 4.4.3", 2, KnowledgeLevel.K2), ("4.5.1 4.5.2", 1, KnowledgeLevel.K2),
        ("4.2.1 4.2.2 4.2.3 4.2.4 4.5.3", 5, KnowledgeLevel.K3),
        ("5.1.2 5.1.6 5.2.1 5.3.1", 1, KnowledgeLevel.K1),
        ("5.1.1 5.1.3", 1, KnowledgeLevel.K2), ("5.1.7", 1, KnowledgeLevel.K2),
        ("5.2.2 5.2.3 5.2.4", 1, KnowledgeLevel.K2), ("5.3.2 5.3.3", 1, KnowledgeLevel.K2),
        ("5.4.1", 1, KnowledgeLevel.K2), ("5.1.4 5.1.5 5.5.1", 3, KnowledgeLevel.K3),
        ("6.1.1", 1, KnowledgeLevel.K2), ("6.2.1", 1, KnowledgeLevel.K1)
    ];

    [Fact]
    public void The_pack_covers_every_syllabus_objective_with_teaching_and_practice()
    {
        var pack = Pack();
        Assert.Empty(ContentEngine.Validate(pack));
        Assert.Equal(24, pack.Lessons.Length);
        Assert.Equal(366, pack.Questions.Length);
        Assert.Equal(15, pack.Blueprints.Length);
        // Practice variants share the family of the one paper question they paraphrase, so freshness is not overstated.
        var paperIds = pack.Blueprints.Where(b => b.QuestionIds is not null).SelectMany(b => b.QuestionIds!).ToHashSet();
        var shared = pack.Questions.GroupBy(q => q.FamilyId).Where(g => g.Count() > 1).ToArray();
        Assert.Equal(46, shared.Sum(g => g.Count() - 1));
        Assert.All(shared, g =>
        {
            Assert.Single(g, q => paperIds.Contains(q.Id));
            Assert.Single(g.Select(q => string.Join(" ", q.ObjectiveIds)).Distinct());
        });
        var levels = Groups.SelectMany(g => g.Objectives.Split(' ').Select(id => (Id: "fl-" + id, g.Level))).ToDictionary(x => x.Id, x => x.Level);
        var officialObjectives = Groups.SelectMany(g => g.Objectives.Split(' ')).Select(id => "fl-" + id).Order().ToArray();
        Assert.Equal(64, officialObjectives.Length);
        Assert.Equal(officialObjectives, pack.Objectives.Where(o => o.Id.StartsWith("fl-")).Select(o => o.Id).Order());
        Assert.All(pack.Objectives, o =>
        {
            Assert.Contains(pack.Lessons, l => l.ObjectiveIds.Contains(o.Id));
            Assert.Contains(pack.Questions, q => q.ObjectiveIds.Contains(o.Id));
        });
        Assert.All(pack.Questions, q =>
        {
            Assert.Contains(q.Kind, new[] { QuestionKind.Single, QuestionKind.Multiple });
            Assert.Equal(ScoringPolicy.Exact, q.Grading.Policy);
            Assert.Equal(1, q.Weight);
            Assert.Equal(2, q.ObjectiveIds.Length);
            Assert.Equal(levels[q.ObjectiveIds[1]], q.KnowledgeLevel);
            // Exam format: four options for single choice, five for Select TWO.
            Assert.Equal(q.Kind == QuestionKind.Multiple ? 5 : 4, q.Options.Length);
            if (q.Kind == QuestionKind.Multiple) Assert.Equal(2, q.SelectCount);
            Assert.Equal(1, Grader.Score(q, new(q.Grading.Correct, [])).Earned);
            Assert.Equal(0, Grader.Score(q, null).Earned);
            if (q.Kind == QuestionKind.Multiple)
                Assert.Equal(0, Grader.Score(q, new([q.Grading.Correct[0]], [])).Earned);
        });
        // Multiple-choice fidelity intentionally triggers the generic format-diversity warning.
        Assert.All(ContentLinter.Lint(pack), d => Assert.Equal("LF220", d.Code));
    }

    [Fact]
    public void Each_paper_matches_official_chapter_level_and_objective_group_rules()
    {
        var pack = Pack();
        var families = new HashSet<string>();
        foreach (var form in Papers)
        {
            var blueprint = pack.Blueprints.Single(b => b.Id == "paper-" + form);
            var paper = ExamComposer.Compose(pack, blueprint, [], "first");
            Assert.Equal(40, paper.Length);
            Assert.Equal(60, blueprint.Minutes);
            Assert.Equal(26, blueprint.PassPoints);
            Assert.Equal(new[] { 8, 6, 4, 11, 9, 2 }, Enumerable.Range(1, 6).Select(i => paper.Count(q => q.ObjectiveIds[0] == $"ch-{i}")));
            Assert.Equal(new[] { 8, 24, 8 }, new[] { KnowledgeLevel.K1, KnowledgeLevel.K2, KnowledgeLevel.K3 }.Select(level => paper.Count(q => q.KnowledgeLevel == level)));
            foreach (var (objectives, count, level) in Groups)
            {
                var ids = objectives.Split(' ').Select(id => "fl-" + id).ToArray();
                var items = paper.Where(q => ids.Contains(q.ObjectiveIds[1])).ToArray();
                Assert.Equal(count, items.Length);
                Assert.All(items, q => Assert.Equal(level, q.KnowledgeLevel));
                if (count <= ids.Length) Assert.Equal(count, items.Select(q => q.ObjectiveIds[1]).Distinct().Count());
                if (count >= ids.Length) Assert.All(ids, id => Assert.Contains(items, q => q.ObjectiveIds[1] == id));
            }
            Assert.All(paper, q => Assert.True(families.Add(q.FamilyId), "Shared family: " + q.FamilyId));
            Assert.Equal(paper.Select(q => q.Id), ExamComposer.Compose(pack, blueprint, paper.Select(q => q.FamilyId).ToHashSet(), "repeat").Select(q => q.Id));
            var extended = pack.Blueprints.Single(b => b.Id == "paper-" + form + "-extended");
            Assert.Equal(75, extended.Minutes);
            Assert.Equal(blueprint.QuestionIds, extended.QuestionIds);
        }
    }

    [Fact]
    public void Short_practice_composes_with_existing_weighted_selection()
    {
        var pack = Pack();
        var blueprint = pack.Blueprints.Single(b => b.Id == "short");
        Assert.Null(blueprint.PassPoints);
        for (var seed = 0; seed < 20; seed++)
        {
            var paper = ExamComposer.Compose(pack, blueprint, [], seed.ToString());
            Assert.Equal(20, paper.Length);
            Assert.All(blueprint.ObjectiveIds, id => Assert.Contains(paper, q => q.ObjectiveIds.Contains(id)));
        }
    }
}
