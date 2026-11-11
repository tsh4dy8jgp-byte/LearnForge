using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LearnForge.Tests;

// reserveFixedPapers keeps questions from fixed papers out of practice until the learner has met them in a paper.
public class PaperReservationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static Pack Reserved(string id = "reserved") =>
        TestApi.WithFixedPaper(CoreTests.Demo()) with { Id = id, ReserveFixedPapers = true };

    private static HashSet<string> PaperFamilies(Pack pack) =>
        pack.Blueprints.Single(b => b.Id == "paper-x").QuestionIds!.Select(id => pack.Questions.Single(q => q.Id == id).FamilyId).ToHashSet();

    private async Task<HttpClient> Published(Pack pack)
    {
        var client = await TestApi.Publisher(factory);
        (await client.PostAsJsonAsync("/api/authoring/publish", pack)).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public void Composed_sessions_exclude_unseen_paper_families()
    {
        var pack = Reserved();
        var paper = PaperFamilies(pack);
        var blueprint = pack.Blueprints.Single(b => b.Id == "short");
        for (var seed = 0; seed < 20; seed++)
            Assert.DoesNotContain(ExamComposer.Compose(pack, blueprint, [], "seed-" + seed), q => paper.Contains(q.FamilyId));
    }

    [Fact]
    public void A_seen_paper_family_returns_to_practice()
    {
        var pack = Reserved();
        var paper = PaperFamilies(pack);
        Assert.Equal(paper, PaperReservation.HiddenFamilies(pack, new HashSet<string>()));
        var seen = paper.First();
        Assert.DoesNotContain(seen, PaperReservation.HiddenFamilies(pack, new HashSet<string> { seen }));
        Assert.Empty(PaperReservation.HiddenFamilies(pack with { ReserveFixedPapers = false }, new HashSet<string>()));
    }

    [Fact]
    public void The_flag_is_omitted_from_json_when_off()
    {
        var demo = CoreTests.Demo();
        var json = Json.Write(demo);
        Assert.DoesNotContain("reserveFixedPapers", json);
        Assert.False(Json.Read<Pack>(json).ReserveFixedPapers);
        Assert.Empty(ContentEngine.Compile(json).Diagnostics);
        Assert.True(Json.Read<Pack>(Json.Write(Reserved())).ReserveFixedPapers);
    }

    [Fact]
    public void A_fixed_paper_still_starts()
    {
        var pack = Reserved();
        var paper = pack.Blueprints.Single(b => b.Id == "paper-x");
        Assert.Empty(ContentEngine.Validate(pack));
        Assert.Equal(paper.QuestionIds, ExamComposer.Compose(pack, paper, [], "seed").Select(q => q.Id));
    }

    [Fact]
    public void An_undersized_practice_bank_fails_validation()
        => Assert.NotEmpty(ContentEngine.Validate(TestApi.LoadPack("istqb-ct-genai-1") with { ReserveFixedPapers = true }));

    [Fact]
    public async Task Practice_leaves_the_paper_fresh_and_eligible()
    {
        var pack = Reserved("reserved-practice");
        var client = await Published(pack);
        var objective = pack.Blueprints.Single(b => b.Id == "paper-x").ObjectiveIds[0];
        var paper = pack.Blueprints.Single(b => b.Id == "paper-x").QuestionIds!;
        var learn = await TestApi.Start(client, "learn", pack.Id);
        Assert.DoesNotContain(TestApi.QuestionIds(learn), paper.Contains);
        await TestApi.Complete(client, learn, pack);
        var focused = await TestApi.Start(client, "learn", pack.Id, focus: "objective", objectiveId: objective);
        Assert.DoesNotContain(TestApi.QuestionIds(focused), paper.Contains);
        await TestApi.Complete(client, focused, pack);
        var summary = (await TestApi.Complete(client, await TestApi.Start(client, packId: pack.Id, blueprintId: "paper-x"), pack)).GetProperty("summary");
        Assert.Equal(100m, summary.GetProperty("freshPercent").GetDecimal());
        Assert.True(summary.GetProperty("eligible").GetBoolean());
    }

    [Fact]
    public async Task Mock_mistakes_reach_mistake_practice()
    {
        var pack = Reserved("reserved-mistakes");
        var client = await Published(pack);
        await TestApi.Complete(client, await TestApi.Start(client, packId: pack.Id, blueprintId: "paper-x"), pack, correct: false);
        var mistakes = TestApi.QuestionIds(await TestApi.Start(client, "learn", pack.Id, focus: "mistakes"));
        Assert.Contains(mistakes, pack.Blueprints.Single(b => b.Id == "paper-x").QuestionIds!.Contains);
    }

    [Fact]
    public async Task A_new_release_restarts_the_streak()
    {
        var pack = Reserved("reserved-release");
        var client = await Published(pack);
        var first = (await TestApi.Complete(client, await TestApi.Start(client, packId: pack.Id, blueprintId: "paper-x"), pack)).GetProperty("summary");
        Assert.True(first.GetProperty("eligible").GetBoolean());
        Assert.Equal(1, await FullStreak(client, pack.Id));

        (await client.PostAsJsonAsync("/api/authoring/publish", pack with { Version = pack.Version + ".1" })).EnsureSuccessStatusCode();
        Assert.Equal(0, await FullStreak(client, pack.Id));
        var again = (await TestApi.Complete(client, await TestApi.Start(client, packId: pack.Id, blueprintId: "paper-x"), pack)).GetProperty("summary");
        Assert.Equal(0m, again.GetProperty("freshPercent").GetDecimal());
        Assert.False(again.GetProperty("eligible").GetBoolean());
    }

    private static async Task<int> FullStreak(HttpClient client, string packId)
    {
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/me/dashboard");
        var course = dashboard.GetProperty("courses").EnumerateArray().Single(c => c.GetProperty("packId").GetString() == packId);
        return course.GetProperty("goal").GetProperty("readiness").GetProperty("full").GetProperty("streak").GetInt32();
    }
}
