using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class IstqbContentTests
{
    [Theory]
    [InlineData("istqb-ctfl-4")]
    [InlineData("istqb-ct-ai-2")]
    [InlineData("istqb-ct-genai-1")]
    public void The_pack_covers_every_syllabus_objective_with_teaching_and_practice(string packId)
    {
        var spec = IstqbExamSpec.For(packId);
        var pack = TestApi.LoadPack(packId);
        Assert.Empty(ContentEngine.Validate(pack));
        Assert.Equal(spec.Lessons, pack.Lessons.Length);
        Assert.Equal(spec.Questions, pack.Questions.Length);
        Assert.Equal(1 + 2 * spec.Papers.Length, pack.Blueprints.Length);
        // Practice variants share the family of the one paper question they paraphrase, so freshness is not overstated.
        var paperIds = pack.Blueprints.Where(b => b.QuestionIds is not null).SelectMany(b => b.QuestionIds!).ToHashSet();
        var shared = pack.Questions.GroupBy(q => q.FamilyId).Where(g => g.Count() > 1).ToArray();
        Assert.Equal(spec.SharedFamilyExtras, shared.Sum(g => g.Count() - 1));
        Assert.All(shared, g =>
        {
            Assert.Single(g, q => paperIds.Contains(q.Id));
            Assert.Single(g.Select(q => string.Join(" ", q.ObjectiveIds)).Distinct());
        });
        var levels = spec.Groups.SelectMany(g => spec.ObjectiveIds(g.Objectives).Select(id => (Id: id, g.Level))).ToDictionary(x => x.Id, x => x.Level);
        var officialObjectives = levels.Keys.Order().ToArray();
        Assert.Equal(spec.SyllabusObjectives, officialObjectives.Length);
        Assert.Equal(officialObjectives, pack.Objectives.Where(o => o.Id.StartsWith(spec.Prefix)).Select(o => o.Id).Order());
        Assert.All(pack.Objectives, o =>
        {
            Assert.Contains(pack.Lessons, l => l.ObjectiveIds.Contains(o.Id));
            Assert.Contains(pack.Questions, q => q.ObjectiveIds.Contains(o.Id));
        });
        // Every syllabus objective is examined in at least one fixed paper, not only in practice.
        Assert.All(officialObjectives, id => Assert.Contains(pack.Questions, q => paperIds.Contains(q.Id) && q.ObjectiveIds[1] == id));
        Assert.All(pack.Questions, q =>
        {
            Assert.Contains(q.Kind, new[] { QuestionKind.Single, QuestionKind.Multiple });
            Assert.Equal(ScoringPolicy.Exact, q.Grading.Policy);
            Assert.Equal(2, q.ObjectiveIds.Length);
            Assert.Equal(levels[q.ObjectiveIds[1]], q.KnowledgeLevel);
            // Official point values: one point per question, or the specialist exams' two points for K3.
            Assert.Equal(spec.Points(q.KnowledgeLevel), q.Weight);
            // Exam format: four options for single choice, five for Select TWO.
            Assert.Equal(q.Kind == QuestionKind.Multiple ? 5 : 4, q.Options.Length);
            if (q.Kind == QuestionKind.Multiple) Assert.Equal(2, q.SelectCount);
            Assert.Equal(q.Weight, Grader.Score(q, new(q.Grading.Correct, [])).Earned);
            Assert.Equal(0, Grader.Score(q, null).Earned);
            if (q.Kind == QuestionKind.Multiple)
                Assert.Equal(0, Grader.Score(q, new([q.Grading.Correct[0]], [])).Earned);
        });
        // Multiple-choice fidelity intentionally triggers the generic format-diversity warning.
        Assert.All(ContentLinter.Lint(pack), d => Assert.Equal("LF220", d.Code));
    }

    [Theory]
    [InlineData("istqb-ctfl-4")]
    [InlineData("istqb-ct-ai-2")]
    [InlineData("istqb-ct-genai-1")]
    public void Each_paper_matches_official_chapter_level_and_objective_group_rules(string packId)
    {
        var spec = IstqbExamSpec.For(packId);
        var pack = TestApi.LoadPack(packId);
        var families = new HashSet<string>();
        var chapters = Enumerable.Range(1, spec.ChapterCounts.Length).Select(i => $"ch-{i}").ToArray();
        foreach (var form in spec.Papers)
        {
            var blueprint = pack.Blueprints.Single(b => b.Id == "paper-" + form);
            var paper = ExamComposer.Compose(pack, blueprint, [], "first");
            Assert.Equal(40, paper.Length);
            Assert.Equal(60, blueprint.Minutes);
            Assert.Equal(spec.PassPoints, blueprint.PassPoints);
            Assert.Equal(spec.TotalPoints, paper.Sum(q => q.Weight));
            Assert.Equal(spec.ChapterCounts, chapters.Select(ch => paper.Count(q => q.ObjectiveIds[0] == ch)));
            Assert.Equal(spec.ChapterPoints, chapters.Select(ch => (int)paper.Where(q => q.ObjectiveIds[0] == ch).Sum(q => q.Weight)));
            Assert.Equal(spec.LevelCounts, new[] { KnowledgeLevel.K1, KnowledgeLevel.K2, KnowledgeLevel.K3 }.Select(level => paper.Count(q => q.KnowledgeLevel == level)));
            foreach (var (objectives, count, level) in spec.Groups)
            {
                var ids = spec.ObjectiveIds(objectives);
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
            Assert.Equal(blueprint.PassPoints, extended.PassPoints);
        }
    }

    [Theory]
    [InlineData("istqb-ctfl-4")]
    [InlineData("istqb-ct-ai-2")]
    [InlineData("istqb-ct-genai-1")]
    public void Short_practice_composes_with_existing_weighted_selection(string packId)
    {
        var pack = TestApi.LoadPack(packId);
        var blueprint = pack.Blueprints.Single(b => b.Id == "short");
        Assert.Null(blueprint.PassPoints);
        Assert.Equal(IstqbExamSpec.For(packId).ChapterCounts.Length, blueprint.ObjectiveIds.Length);
        for (var seed = 0; seed < 20; seed++)
        {
            var paper = ExamComposer.Compose(pack, blueprint, [], seed.ToString());
            Assert.Equal(20, paper.Length);
            Assert.All(blueprint.ObjectiveIds, id => Assert.Contains(paper, q => q.ObjectiveIds.Contains(id)));
        }
    }
}
