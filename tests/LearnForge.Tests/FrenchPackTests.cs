using Xunit;

namespace LearnForge.Tests;

// The French grammar pack is generated outside the repository, so these checks guard its production-first shape.
public class FrenchPackTests
{
    private static Compilation Compile() =>
        ContentEngine.Compile(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "packs", "french-b2-grammar.json")));

    [Fact]
    public void French_pack_compiles_without_errors_or_quality_warnings()
    {
        var compiled = Compile();
        Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}")));
        Assert.Equal(CourseGoal.Mastery, compiled.Pack!.Goal);
        Assert.Empty(ContentLinter.Lint(compiled.Pack));
    }

    [Fact]
    public void Every_objective_has_enough_independent_and_mostly_typed_practice()
    {
        var pack = Compile().Pack!;
        Assert.All(pack.Objectives, objective =>
        {
            var questions = pack.Questions.Where(q => q.ObjectiveIds.Contains(objective.Id)).ToArray();
            Assert.True(questions.Select(q => q.FamilyId).Distinct().Count() >= 6, $"{objective.Id} needs at least six families.");
            Assert.True(questions.Count(q => q.Kind == QuestionKind.ShortAnswer) * 10 >= questions.Length * 4,
                $"{objective.Id} should ask for typed production in at least 40% of its items.");
        });
    }

    [Fact]
    public void Every_typed_answer_is_accepted_by_the_grader()
    {
        foreach (var q in Compile().Pack!.Questions.Where(q => q.Kind == QuestionKind.ShortAnswer))
            foreach (var key in q.Grading.Correct)
                Assert.True(Grader.Score(q, new([], [], key)).FullyCorrect, $"{q.Id} rejects its own key '{key}'.");
    }
}
