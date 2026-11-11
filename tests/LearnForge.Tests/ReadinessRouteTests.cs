using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LearnForge.Tests;

// A pack can switch the short readiness route off with readiness.shortAttempts 0, leaving full mocks as the only evidence.
public class ReadinessRouteTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private static ReadinessEvidence[] Mocks(AssessmentSize size, int count, decimal percent) =>
        Enumerable.Range(0, count).Select(i => new ReadinessEvidence(i.ToString(), size, Now.AddDays(-i), percent, true)).ToArray();

    [Fact]
    public void A_zero_required_track_is_never_met()
    {
        var r = ReadinessEvaluator.Evaluate([], new(ShortAttempts: 0), Now);
        Assert.False(r.Ready); Assert.False(r.Short.Met); Assert.Equal(0, r.Short.Required);
    }

    [Fact]
    public void Short_mocks_cannot_declare_readiness_when_the_route_is_off()
        => Assert.False(ReadinessEvaluator.Evaluate(Mocks(AssessmentSize.Short, 20, 100m), new(ShortAttempts: 0), Now).Ready);

    [Theory, InlineData(31, true), InlineData(30, false)]
    public void Three_full_mocks_use_the_strict_bar(int correct, bool ready)
        => Assert.Equal(ready, ReadinessEvaluator.Evaluate(Mocks(AssessmentSize.Full, 3, correct * 100m / 40), new(ShortAttempts: 0, Threshold: 75), Now).Ready);

    [Fact]
    public void An_ineligible_full_mock_breaks_the_streak()
    {
        var mocks = Mocks(AssessmentSize.Full, 3, 100m);
        mocks[0] = mocks[0] with { Eligible = false };
        Assert.False(ReadinessEvaluator.Evaluate(mocks, new(ShortAttempts: 0), Now).Ready);
    }

    [Fact]
    public void Short_route_off_needs_a_full_blueprint()
    {
        var demo = CoreTests.Demo();
        var off = demo with { Readiness = new(ShortAttempts: 0) };
        Assert.DoesNotContain(ContentEngine.Validate(off), d => d.Path == "readiness");
        Assert.Contains(ContentEngine.Validate(off with { Blueprints = demo.Blueprints.Where(b => b.Size == AssessmentSize.Short).ToArray() }), d => d.Path == "readiness");
        Assert.Contains(ContentEngine.Validate(demo with { Readiness = new(ShortAttempts: -1) }), d => d.Path == "readiness");
    }

    [Fact]
    public void Lint_ignores_the_short_route_when_it_is_off()
    {
        var demo = CoreTests.Demo();
        Assert.DoesNotContain(ContentLinter.Lint(demo with { Readiness = new(ShortAttempts: 0) }), d => d.Code == "LF222");
        // An unreachable full route is still reported: the switched-off short route must not mask it.
        Assert.Contains(ContentLinter.Lint(demo with { Readiness = new(ShortAttempts: 0, FullAttempts: 5) }), d => d.Code == "LF222");
    }

    [Fact]
    public async Task A_short_mock_is_not_eligible_when_the_route_is_off()
    {
        var pack = TestApi.WithFixedPaper(CoreTests.Demo()) with { Id = "route-off", Readiness = new(ShortAttempts: 0) };
        var client = await TestApi.Publisher(factory);
        (await client.PostAsJsonAsync("/api/authoring/publish", pack)).EnsureSuccessStatusCode();
        var done = await TestApi.Complete(client, await TestApi.Start(client, packId: pack.Id), pack);
        var summary = done.GetProperty("summary");
        Assert.False(summary.GetProperty("eligible").GetBoolean());
        Assert.Equal(100m, summary.GetProperty("freshPercent").GetDecimal());
    }
}
