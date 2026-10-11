using System.Globalization;
using Xunit;

namespace LearnForge.Tests;

public class PracticalAiContentTests
{
    private readonly Pack pack = TestApi.LoadPack("ai-testing-genai-practical");

    [Fact] public void Course_supports_practical_mastery_with_independent_objective_coverage()
    {
        Assert.Equal(ProductProfile.Course, pack.Profile);
        Assert.Equal(CourseGoal.Mastery, pack.Goal);
        Assert.Equal(new(true, true, false), pack.Features);
        Assert.Empty(ContentEngine.Validate(pack));
        Assert.Empty(ContentLinter.Lint(pack));
        Assert.Equal(16, pack.Objectives.Length);
        Assert.Equal(16, pack.Lessons.Length);
        Assert.Equal(80, pack.Questions.Length);
        Assert.Equal(5, pack.Questions.Select(q => q.Kind).Distinct().Count());
        foreach (var objective in pack.Objectives)
        {
            Assert.Single(pack.Lessons, l => l.ObjectiveIds.Contains(objective.Id));
            var questions = pack.Questions.Where(q => q.ObjectiveIds.Contains(objective.Id)).ToArray();
            Assert.Equal(5, questions.Length);
            Assert.Equal(5, questions.Select(q => q.FamilyId).Distinct().Count());
        }
    }

    [Fact] public void Course_wide_sessions_cover_each_objective_once_across_selection_seeds()
    {
        var blueprint = Assert.Single(pack.Blueprints);
        Assert.Equal(16, blueprint.Count);
        for (var seed = 0; seed < 20; seed++)
        {
            var selected = ExamComposer.Compose(pack, blueprint, [], "course-check-" + seed);
            Assert.Equal(16, selected.Length);
            Assert.Equal(16, selected.Select(q => q.FamilyId).Distinct().Count());
            Assert.Equal(pack.Objectives.Select(o => o.Id).Order(), selected.SelectMany(q => q.ObjectiveIds).Order());
        }
    }

    [Fact] public void Every_question_key_grades_and_can_supply_proficient_mastery_evidence()
    {
        var now = DateTime.UtcNow;
        var evidence = pack.Questions.Select((q, i) =>
        {
            var answer = q.Kind == QuestionKind.Numeric
                ? new Answer([], [], q.Grading.Correct.Single())
                : new Answer(q.Grading.Correct, q.Grading.Matches ?? []);
            Assert.Null(Grader.Validate(q, answer));
            Assert.True(Grader.Score(q, answer).FullyCorrect);
            Assert.False(Grader.Score(q, null).FullyCorrect);
            return new MasteryEvidence(i, q.FamilyId, q.ObjectiveIds, true, false, now);
        }).ToArray();
        Assert.All(MasteryEvaluator.Evaluate(pack.Objectives, evidence, pack.Mastery ?? new(), now), mastery =>
        {
            Assert.Equal(MasteryState.Proficient, mastery.State);
            Assert.Equal(5, mastery.Considered);
        });
    }

    [Theory]
    [InlineData("precision", 32, 8, 18)]
    [InlineData("recall", 21, 9, 9)]
    [InlineData("f1", 30, 10, 20)]
    public void Classification_keys_use_the_correct_denominators(string metric, decimal tp, decimal fp, decimal fn)
    {
        var expected = 100m * (metric switch
        {
            "precision" => tp / (tp + fp),
            "recall" => tp / (tp + fn),
            _ => 2 * tp / (2 * tp + fp + fn)
        });
        AssertNumeric("qa-metrics-" + metric, expected);
    }

    [Fact] public void Workflow_and_capstone_keys_account_for_review_effort_and_the_full_denominator()
    {
        AssertNumeric("qa-regression-run-rate", 100m * (18 + 17 + 19) / (20 * 3));
        AssertNumeric("qa-reporting-net-saving", 100m * (100 - 15 - 35 - 20) / 100);
        AssertNumeric("qa-reporting-acceptance-rate", 100m * 18 / 30);
        AssertNumeric("qa-capstone-pass-rate", 100m * 3 / 5);
    }

    private void AssertNumeric(string id, decimal expected)
    {
        var question = pack.Questions.Single(q => q.Id == id);
        Assert.Equal(QuestionKind.Numeric, question.Kind);
        Assert.True(Grader.Score(question, new([], [], expected.ToString(CultureInfo.InvariantCulture))).FullyCorrect);
        Assert.False(Grader.Score(question, new([], [], (expected + 1).ToString(CultureInfo.InvariantCulture))).FullyCorrect);
    }
}
