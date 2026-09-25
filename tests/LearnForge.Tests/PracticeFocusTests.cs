using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class PracticeFocusTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Pack pack = TestApi.LoadPack("reasoning-foundations");

    private static async Task<HttpResponseMessage> Start(HttpClient client, object body) => await client.PostAsJsonAsync("/api/me/attempts", body);

    [Fact] public async Task Objective_practice_serves_only_that_objective_and_prefers_unseen_families()
    {
        var client = await TestApi.Account(factory);
        var first = await TestApi.Start(client, "learn", focus: "objective", objectiveId: "ordering");
        var firstIds = TestApi.QuestionIds(first);
        Assert.Equal(5, firstIds.Length);
        Assert.All(firstIds, id => Assert.Contains("ordering", pack.Questions.Single(q => q.Id == id).ObjectiveIds));
        await TestApi.Complete(client, first, pack, correct: false);

        var second = TestApi.QuestionIds(await TestApi.Start(client, "learn", focus: "objective", objectiveId: "ordering"));
        // The demo pack has 8 ordering families, so 3 unseen ones lead the second session.
        Assert.Equal(3, second.Count(id => !firstIds.Contains(id)));
    }

    [Fact] public async Task Objective_practice_validates_its_inputs_and_replays()
    {
        var client = await TestApi.Account(factory);
        var basics = new { packId = pack.Id, blueprintId = "short" };
        Assert.Equal(HttpStatusCode.BadRequest, (await Start(client, new { basics.packId, basics.blueprintId, mode = "learn", requestId = Guid.NewGuid().ToString(), focus = "objective" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Start(client, new { basics.packId, basics.blueprintId, mode = "learn", requestId = Guid.NewGuid().ToString(), objectiveId = "sets" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Start(client, new { basics.packId, basics.blueprintId, mode = "learn", requestId = Guid.NewGuid().ToString(), focus = "objective", objectiveId = "unknown" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Start(client, new { basics.packId, basics.blueprintId, mode = "mock", requestId = Guid.NewGuid().ToString(), focus = "objective", objectiveId = "sets" })).StatusCode);

        var requestId = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await Start(client, new { basics.packId, basics.blueprintId, mode = "learn", requestId, focus = "objective", objectiveId = "sets" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Start(client, new { basics.packId, basics.blueprintId, mode = "learn", requestId, focus = "objective", objectiveId = "sets" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Start(client, new { basics.packId, basics.blueprintId, mode = "learn", requestId, focus = "objective", objectiveId = "logic" })).StatusCode);
    }

    [Fact] public async Task Objective_practice_for_case_study_only_objectives_explains_itself()
    {
        var client = await TestApi.Account(factory);
        var response = await Start(client, new { packId = "evidence-lab", blueprintId = "short", mode = "learn", requestId = Guid.NewGuid().ToString(), focus = "objective", objectiveId = "evaluate" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("no standalone practice", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Fact] public async Task Mistake_practice_uses_the_latest_countable_answer()
    {
        var client = await TestApi.Account(factory);
        var attempt = await TestApi.Start(client);
        var ids = TestApi.QuestionIds(attempt);
        var revision = await TestApi.Save(client, attempt, 0, ids[0], TestApi.Correct(pack, ids[0]));
        (await client.PostAsJsonAsync($"/api/me/attempts/{TestApi.Id(attempt)}/submit", new { revision })).EnsureSuccessStatusCode();

        var mistakes = TestApi.QuestionIds(await TestApi.Start(client, "learn", focus: "mistakes"));
        Assert.DoesNotContain(ids[0], mistakes);
        Assert.Equal(ids.Skip(1).Order(), mistakes.Order());
    }

    [Fact] public async Task Weak_practice_needs_evidence_first()
    {
        var client = await TestApi.Account(factory);
        Assert.Equal(HttpStatusCode.Conflict, (await Start(client, new { packId = pack.Id, blueprintId = "short", mode = "learn", requestId = Guid.NewGuid().ToString(), focus = "weak" })).StatusCode);
        await TestApi.Complete(client, await TestApi.Start(client, blueprintId: "full"), pack, correct: false);
        Assert.NotEmpty(TestApi.QuestionIds(await TestApi.Start(client, "learn", focus: "weak")));
    }
}
