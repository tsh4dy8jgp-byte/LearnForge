using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class FixedPaperTests
{
    // The demo pack with one fixed paper over five "sets" questions; every demo question is its own family.
    private static Pack Pack()
    {
        var demo = CoreTests.Demo();
        var ids = new[] { "sets-single-1", "sets-single-2", "sets-single-3", "sets-multi-1", "sets-multi-2" };
        var paper = new Blueprint("paper-a", "Paper A", ids.Length, 10, AssessmentSize.Full, ["sets"], [], QuestionIds: ids, PassPoints: 4);
        return demo with { Blueprints = [paper] };
    }

    [Fact] public void A_fixed_paper_validates_and_composes_in_its_listed_order()
    {
        var pack = Pack();
        Assert.Empty(ContentEngine.Validate(pack));
        Assert.Equal(pack.Blueprints[0].QuestionIds, ExamComposer.Compose(pack, pack.Blueprints[0], [], "any").Select(q => q.Id));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("count")]
    [InlineData("family")]
    [InlineData("weights")]
    [InlineData("coverage")]
    [InlineData("kind")]
    [InlineData("scenario")]
    [InlineData("threshold")]
    [InlineData("negative-threshold")]
    public void Malformed_fixed_papers_are_rejected(string defect)
    {
        var pack = Pack();
        var b = pack.Blueprints.Single(b => b.Id == "paper-a");
        var ids = b.QuestionIds!.ToArray();
        switch (defect)
        {
            case "missing": ids[0] = "unknown"; b = b with { QuestionIds = ids }; break;
            case "duplicate": ids[0] = ids[1]; b = b with { QuestionIds = ids }; break;
            case "count": b = b with { QuestionIds = ids[..^1] }; break;
            case "family": pack = pack with { Questions = pack.Questions.Select(q => q.Id == ids[0] ? q with { FamilyId = ids[1] } : q).ToArray() }; break;
            case "weights": b = b with { ObjectiveWeights = b.ObjectiveIds.ToDictionary(id => id, _ => 1m) }; break;
            case "coverage": b = b with { ObjectiveIds = b.ObjectiveIds.Append("logic").ToArray() }; break;
            case "kind": b = b with { RequiredKinds = [QuestionKind.Numeric] }; break;
            case "scenario":
                pack = pack with { Scenarios = [new("shared", "Shared", "Context")], Questions = pack.Questions.Select(q => q.Id is "sets-single-1" or "logic-dropdown-1" ? q with { ScenarioId = "shared" } : q).ToArray() };
                break;
            case "threshold": b = b with { PassPoints = 6 }; break;
            case "negative-threshold": b = b with { PassPoints = 0 }; break;
        }
        pack = pack with { Blueprints = [b] };
        Assert.NotEmpty(ContentEngine.Validate(pack));
    }

    [Fact]
    public void Complete_scenario_groups_can_be_selected_in_a_fixed_paper()
    {
        var pack = Pack();
        pack = pack with { Scenarios = [new("shared", "Shared", "Context")], Questions = pack.Questions.Select(q => q.Id is "sets-single-1" or "sets-single-2" ? q with { ScenarioId = "shared" } : q).ToArray() };
        Assert.Empty(ContentEngine.Validate(pack));
    }

    [Fact]
    public void Legacy_json_omits_new_fields_and_round_trips()
    {
        var pack = CoreTests.Demo();
        var json = Json.Write(pack);
        Assert.DoesNotContain("questionIds", json);
        Assert.DoesNotContain("passPoints", json);
        Assert.DoesNotContain("knowledgeLevel", json);
        Assert.Empty(ContentEngine.Compile(json).Diagnostics);
    }
}
