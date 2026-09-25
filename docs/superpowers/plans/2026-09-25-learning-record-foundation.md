# Learning Record Foundation (SP1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give learners enrollment ("My courses"), progress that survives new pack releases, transparent per-objective mastery built from mock and learning answers, prerequisite-aware next steps, per-pack goals (readiness, mastery or completion) and objective-focused practice, all on top of an append-only evidence ledger.

**Architecture:** Pure, deterministic rules live in `LearnForge.Core` (`MasteryEvaluator`, `NextStepPlanner`, `ContentHash`, goal and mastery policy validation). The API writes one `EvidenceRecord` per question when feedback is released and derives everything else on read through `LearningRecordService`. A `ReleaseCache` keeps each immutable pack release deserialized once. Endpoints move from `Program.cs` into feature modules written with C# 14 extension members. The Angular app gains generated API types, an `HttpClient`-based API service, `httpResource` pages, Signal Forms, `@angular/aria` tabs and Vitest unit tests.

**Tech Stack:** .NET 10 (C# 14), ASP.NET Core minimal APIs, `Microsoft.Extensions.Validation`, EF Core 10.0.12 (SQLite and PostgreSQL), xUnit 2.9; Angular 22.2 (zoneless, standalone), `@angular/aria` 22.2.0, Signal Forms, `httpResource`, Vitest 5 through `@angular/build:unit-test`, `@hey-api/openapi-ts` 0.99, Playwright.

**Spec:** `docs/superpowers/specs/2026-09-25-learning-record-foundation-design.md` (context: `docs/superpowers/specs/2026-09-25-learner-roadmap.md`).

## Global Constraints

- `Directory.Build.props` sets `TreatWarningsAsErrors`, nullable and implicit usings. Any compiler warning fails the build, including nullable warnings in tests.
- One public type per file, in the existing folders (`Core/Domain/*`, `Contracts/<Area>/*`, `Infrastructure/Entities/*`, `Services/<Area>/*`).
- Wire enums are C# enums serialized camelCase through `JsonStringEnumConverter`. API responses are named record DTOs, never anonymous or `object` shapes.
- Mastery policy defaults: `Window = 5`, `MinimumEvidence = 3`, `ProficientPercent = 80`, `ReviewAfterDays = 60`. Bounds: `1 ≤ MinimumEvidence ≤ Window ≤ 20`, `50 ≤ ProficientPercent ≤ 100`, `1 ≤ ReviewAfterDays ≤ 365`.
- Proficient means `k × 100 ≥ ProficientPercent × n` over the latest record per family, limited to the latest `Window` families, with `n ≥ MinimumEvidence`.
- Evidence rules: mock records always count (unanswered counts as incorrect); learning records count only when answered. Readiness stays **release-scoped and mock-only**, and `ReadinessEvaluator` is unchanged.
- Answer keys and explanations never appear in any new DTO.
- The evidence ledger is append-only with a unique `(AttemptId, QuestionId)` index.
- Every schema change needs migrations for both `AppDb` (`Migrations/Sqlite`) and `PostgresDb` (`Migrations/Postgres`). PostgreSQL migration class names end in `Postgres`.
- Pinned versions for new dependencies: `Microsoft.Extensions.ApiDescription.Server` 10.0.12, `@angular/aria` 22.2.0, `vitest` 5.0.2, `jsdom` 30.1.1, `@hey-api/openapi-ts` 0.99.0.
- Content is rendered as text (no `innerHTML`). Every interaction keeps a keyboard path, and no state is conveyed by colour alone.
- Commit messages end with a blank line and `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Each integration test class owns its `ApiFactory`, because the `auth` rate limit allows 30 sign-ins or registrations per minute per factory. Keep each new class under 20 of those calls.

## Review Focus

Most likely to bite, not covered by the obvious tests. Each line names the owning task that adds the pinning test.

1. A new release **removes a lesson** the learner had completed: progress must drop it quietly instead of throwing or counting it (Task 8, `Removed_lessons_leave_progress_without_errors`).
2. A learner **submits a learning session without answering**: mastery must not move, because skipped learning items are ignored (Task 8, `Skipped_learning_questions_do_not_change_mastery`).
3. **Goal met** must follow the pack's goal kind. A completion pack is met when every lesson is read, not when readiness is met (Task 8, `Completion_goal_is_met_when_every_lesson_is_read`).
4. Objective practice for an objective whose questions are **all inside case studies** must return a 409 with a helpful message, not an empty session or a 500 (Task 9, `Objective_practice_for_case_study_only_objectives_explains_itself`).
5. **Two instances run the migration step at once:** the backfill must not crash on duplicate keys (Task 10, `Concurrent_backfills_do_not_fail`).

---

## File map

| Area | Create | Modify / delete |
| --- | --- | --- |
| Core enums | `Domain/Enums/{CourseGoal,MasteryState,NextStepKind,NextStepReason,EvidenceSource,EnrollmentStatus}.cs` | `Domain/Enums/PracticeFocus.cs` |
| Core contract | `Domain/Content/MasteryPolicy.cs`, `Services/Content/ContentHash.cs` | `Domain/Content/Pack.cs`, `Services/Content/ContentEngine.cs` |
| Core rules | `Domain/Mastery/{MasteryEvidence,ObjectiveMastery,MasteryEvaluator}.cs`, `Domain/Learning/{NextStep,NextStepPlanner}.cs` | — |
| API endpoints | `Endpoints/{Auth,Catalog,Learner,Attempt,Authoring}Endpoints.cs`, `Startup/DatabaseInitializer.cs` | `Program.cs` |
| API services | `Services/Content/{ReleaseCache,ReleaseView}.cs`, `Services/Learning/{EvidenceWriter,LearningRecordService,EvidenceBackfill}.cs` | `Services/Attempts/{AttemptService,ExpiryWorker}.cs`, `Services/Analytics/Analytics.cs`, `GlobalUsings.cs` |
| Persistence | `Infrastructure/Entities/{Enrollment,EvidenceRecord,LessonProgress}.cs`, 2 migrations | `Infrastructure/Entities/Attempt.cs`, `Infrastructure/Persistence/AppDb.cs`; delete `Infrastructure/Entities/Completion.cs` |
| Contracts | `Contracts/Learning/{CourseProgressDto,ObjectiveMasteryDto,NextStepDto,CourseGoalStatusDto,EnrollmentRequest}.cs`, `Contracts/Export/{EnrollmentExportDto,LessonProgressExportDto,EvidenceExportDto}.cs` | `Contracts/Analytics/DashboardDto.cs`, `Contracts/Content/CourseCatalogDto.cs`, `Contracts/Attempts/{StartRequest,ResponseRequest}.cs`, `Contracts/Auth/*Request.cs`, `Contracts/Export/LearnerExportDto.cs`; delete `Contracts/Analytics/{CourseDashboardDto,ObjectiveDashboardDto,RecommendationDto,GradedEvidence,ObjectiveGradeEvidence}.cs` |
| Tooling | `apps/web/openapi-ts.config.ts`, `apps/web/openapi/learnforge.json` (generated), `apps/web/src/app/generated/*` (generated) | `apps/api/LearnForge.Api.csproj`, `Makefile`, `tools/cli/Program.cs` |
| Web | `src/app/{mastery-badge,next-steps}.ts` plus `*.spec.ts`, `src/app/pages/dashboard.spec.ts` | `src/app/{app.config,api,models}.ts`, `src/app/pages/{dashboard,course}.ts`, `src/styles.scss`, `angular.json`, `package.json`, `e2e/learning.spec.ts` |
| Tests | `tests/LearnForge.Tests/{GoalPolicyTests,MasteryEvaluatorTests,NextStepPlannerTests,ReleaseCacheTests,MigrationTests,TestApi,EvidenceLedgerTests,LearningRecordTests,PracticeFocusTests,BackfillTests,PrivacyTests,ValidationTests}.cs` | `tests/LearnForge.Tests/ApiTests.cs` (one readiness assertion) |
| Docs | — | `docs/{architecture,assessment,user-guide,authoring,api,testing}.md`, `CLAUDE.md` |

Web note: Tasks 8–12 change API response shapes that the dashboard and course pages consume. Those two pages are expected to be wrong at runtime on this branch until Tasks 15–16. `npm run build` stays green throughout, because the legacy TypeScript interfaces remain until each page is migrated.

---

### Task 0: Branch from the baseline

The repository already has a baseline commit on `main`. This task records the planning documents and starts a feature branch.

**Files:** `docs/superpowers/specs/2026-09-25-learning-record-foundation-design.md` (updated during planning), `docs/superpowers/plans/2026-09-25-learning-record-foundation.md` (new).

- [ ] **Step 1: Confirm a clean, green baseline**

```bash
cd /Users/bakhtiyarkussainov/eduframe
git status --short
dotnet test LearnForge.slnx
```
Expected: only the two planning documents are listed as changed or untracked; `Passed!  - Failed: 0, Passed: 22`.

- [ ] **Step 2: Commit the planning documents and branch**

```bash
git add docs/superpowers
git commit -m "docs: plan the learning record foundation (SP1)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git switch -c feat/learning-record-foundation
```

---

### Task 1: Pack goal and mastery policy contract

**Files:**
- Create: `src/LearnForge.Core/Domain/Enums/CourseGoal.cs`, `src/LearnForge.Core/Domain/Content/MasteryPolicy.cs`, `src/LearnForge.Core/Services/Content/ContentHash.cs`
- Modify: `src/LearnForge.Core/Domain/Content/Pack.cs`, `src/LearnForge.Core/Services/Content/ContentEngine.cs:174-176`, `apps/api/Services/Attempts/AttemptService.cs:70`, `apps/api/Services/Analytics/Analytics.cs:55`, `apps/api/Program.cs:149`, `tools/cli/Program.cs:19-21`
- Test: `tests/LearnForge.Tests/GoalPolicyTests.cs`

**Interfaces:**
- Produces: `enum CourseGoal { Readiness, Mastery, Completion }`; `record MasteryPolicy(int Window = 5, int MinimumEvidence = 3, decimal ProficientPercent = 80, int ReviewAfterDays = 60)`; `Pack(..., Blueprint[] Blueprints, SourceReference[] Sources, ReadinessPolicy? Readiness = null, CourseGoal Goal = CourseGoal.Readiness, MasteryPolicy? Mastery = null)`; `static string ContentHash.Of<T>(T value)` (64 lowercase hex characters).

- [ ] **Step 1: Write the failing tests**

Create `tests/LearnForge.Tests/GoalPolicyTests.cs`:

```csharp
using System.Text.Json.Nodes;
using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class GoalPolicyTests
{
    private static string Without(Pack pack, params string[] properties)
    {
        var node = JsonNode.Parse(Json.Write(pack))!.AsObject();
        foreach (var name in properties) node.Remove(name);
        return node.ToJsonString();
    }

    [Fact] public void Packs_that_omit_goal_readiness_and_mastery_compile_as_readiness_packs()
    {
        var compiled = ContentEngine.Compile(Without(CoreTests.Demo(), "goal", "readiness", "mastery"));
        Assert.True(compiled.Success, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Equal(CourseGoal.Readiness, compiled.Pack!.Goal);
        Assert.Null(compiled.Pack.Readiness);
        Assert.Null(compiled.Pack.Mastery);
    }

    [Fact] public void Goal_and_mastery_are_read_from_camel_case_json()
    {
        var node = JsonNode.Parse(Without(CoreTests.Demo(), "readiness"))!.AsObject();
        node["goal"] = "mastery";
        node["mastery"] = new JsonObject { ["window"] = 6, ["minimumEvidence"] = 4 };
        var compiled = ContentEngine.Compile(node.ToJsonString());
        Assert.True(compiled.Success, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Equal(CourseGoal.Mastery, compiled.Pack!.Goal);
        Assert.Equal(new MasteryPolicy(6, 4), compiled.Pack.Mastery);
    }

    [Theory]
    [InlineData(5, 0, 80, 60)]
    [InlineData(2, 3, 80, 60)]
    [InlineData(21, 3, 80, 60)]
    [InlineData(5, 3, 49, 60)]
    [InlineData(5, 3, 101, 60)]
    [InlineData(5, 3, 80, 0)]
    [InlineData(5, 3, 80, 366)]
    public void Mastery_policy_bounds_are_enforced(int window, int minimum, int percent, int reviewDays)
    {
        var pack = CoreTests.Demo() with { Mastery = new(window, minimum, percent, reviewDays) };
        Assert.Contains(ContentEngine.Validate(pack), d => d.Path == "mastery");
    }

    [Fact] public void Mastery_goal_requires_enough_question_families_per_objective()
    {
        // The demo pack has 16 families for sets and logic but only 8 for ordering.
        var diagnostics = ContentEngine.Validate(CoreTests.Demo() with { Goal = CourseGoal.Mastery, Mastery = new(10, 10) });
        Assert.Contains(diagnostics, d => d.Path == "ordering");
        Assert.DoesNotContain(diagnostics, d => d.Path is "sets" or "logic");
        Assert.Empty(ContentEngine.Validate(CoreTests.Demo() with { Goal = CourseGoal.Mastery }));
    }

    [Fact] public void Undefined_goal_values_are_rejected() =>
        Assert.Contains(ContentEngine.Validate(CoreTests.Demo() with { Goal = (CourseGoal)7 }), d => d.Path == "goal");

    [Fact] public void Content_hash_is_stable_and_changes_with_content()
    {
        var lesson = CoreTests.Demo().Lessons[0];
        Assert.Matches("^[0-9a-f]{64}$", ContentHash.Of(lesson));
        Assert.Equal(ContentHash.Of(lesson), ContentHash.Of(lesson with { }));
        Assert.NotEqual(ContentHash.Of(lesson), ContentHash.Of(lesson with { Title = lesson.Title + " (revised)" }));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~GoalPolicyTests"`
Expected: build FAILS with `CS0246: The type or namespace name 'CourseGoal' could not be found`.

- [ ] **Step 3: Add the enum, policy and hash**

`src/LearnForge.Core/Domain/Enums/CourseGoal.cs`:
```csharp
namespace LearnForge.Core;

public enum CourseGoal { Readiness, Mastery, Completion }
```

`src/LearnForge.Core/Domain/Content/MasteryPolicy.cs`:
```csharp
namespace LearnForge.Core;

public sealed record MasteryPolicy(int Window = 5, int MinimumEvidence = 3, decimal ProficientPercent = 80, int ReviewAfterDays = 60);
```

`src/LearnForge.Core/Services/Content/ContentHash.cs`:
```csharp
using System.Security.Cryptography;
using System.Text;

namespace LearnForge.Core;

public static class ContentHash
{
    // Uses the canonical serializer, so the hash changes only when content changes.
    public static string Of<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json.Write(value))));
}
```

Replace `src/LearnForge.Core/Domain/Content/Pack.cs`:
```csharp
namespace LearnForge.Core;

// Readiness, goal and mastery are optional in source JSON; packs that omit them keep the exam-readiness goal.
public sealed record Pack(int SchemaVersion, string Id, string Version, string Title, string Description,
    string License, Objective[] Objectives, Lesson[] Lessons, Question[] Questions, Scenario[] Scenarios,
    Blueprint[] Blueprints, SourceReference[] Sources, ReadinessPolicy? Readiness = null,
    CourseGoal Goal = CourseGoal.Readiness, MasteryPolicy? Mastery = null);
```

- [ ] **Step 4: Validate goal and mastery in `ContentEngine.Validate`**

Replace the last three lines of `Validate` (from `var r = p.Readiness;` to `return errors.ToArray();`) with:
```csharp
        var r = p.Readiness ?? new();
        if (r.ShortAttempts is < 1 or > 20 || r.FullAttempts is < 1 or > 20 || r.Threshold is < 0 or >= 100 || r.LookbackDays is < 1 or > 365 || r.MinimumFreshPercent is < 0 or > 100) Error("readiness", "Readiness policy is out of bounds.");
        if (!Enum.IsDefined(p.Goal)) Error("goal", "Goal must be readiness, mastery or completion.");
        var m = p.Mastery ?? new();
        if (m.MinimumEvidence < 1 || m.Window < m.MinimumEvidence || m.Window > 20 || m.ProficientPercent is < 50 or > 100 || m.ReviewAfterDays is < 1 or > 365)
            Error("mastery", "Mastery policy is out of bounds: 1 ≤ minimumEvidence ≤ window ≤ 20, proficientPercent 50–100, reviewAfterDays 1–365.");
        else if (p.Goal == CourseGoal.Mastery)
            // Like blueprint feasibility: an objective with too few families could never become proficient.
            foreach (var objective in p.Objectives)
                if (p.Questions.Where(q => q.ObjectiveIds.Contains(objective.Id)).Select(q => q.FamilyId).Distinct().Count() < m.MinimumEvidence)
                    Error(objective.Id, $"A mastery goal needs at least {m.MinimumEvidence} question families for this objective.");
        return errors.ToArray();
```

- [ ] **Step 5: Update the call sites that read `Readiness`**

- `apps/api/Services/Attempts/AttemptService.cs:70`: replace `pack.Readiness);` with `pack.Readiness ?? new());`.
- `apps/api/Services/Analytics/Analytics.cs:55`: replace `pack.Readiness, now)` with `pack.Readiness ?? new(), now)`.
- `apps/api/Program.cs:149`: replace `p.Sources, p.Readiness, completed` with `p.Sources, p.Readiness ?? new(), completed`.
- `tools/cli/Program.cs`: replace the `new Pack(...)` expression (lines 19–21) with:
```csharp
        var pack = new Pack(1, "my-course", "1.0.0", "My first course", "Replace this sample with your subject.", "Private",
            [new("parity", "Recognize even integers", [])],
            [new("introduction", "Even integers", "A short introduction.", ["parity"], [new(ContentBlockKind.Text, "An integer is even when it is divisible by two.")])],
            [q], [], [new("short", "Quick check", 1, 5, AssessmentSize.Short, ["parity"], [QuestionKind.Single])], [], Readiness: new());
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx`
Expected: PASS, all tests (22 existing + 12 new).

Run: `make check-content`
Expected: two `PASS` lines.

- [ ] **Step 7: Commit**

```bash
git add src tools apps tests
git commit -m "feat(core): add course goals, mastery policy and content hashes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Mastery evaluator

**Files:**
- Create: `src/LearnForge.Core/Domain/Enums/MasteryState.cs`, `src/LearnForge.Core/Domain/Mastery/MasteryEvidence.cs`, `src/LearnForge.Core/Domain/Mastery/ObjectiveMastery.cs`, `src/LearnForge.Core/Domain/Mastery/MasteryEvaluator.cs`
- Test: `tests/LearnForge.Tests/MasteryEvaluatorTests.cs`

**Interfaces:**
- Consumes: `MasteryPolicy`, `Objective` (Task 1).
- Produces: `enum MasteryState { NotStarted, Emerging, Developing, Proficient }`; `record MasteryEvidence(long Order, string FamilyId, string[] ObjectiveIds, bool FullyCorrect, bool Independent, DateTime At)`; `record ObjectiveMastery(string ObjectiveId, MasteryState State, int Correct, int Considered, int Independent, DateTime? LastEvidenceAt, bool ReviewDue)`; `static ObjectiveMastery[] MasteryEvaluator.Evaluate(IEnumerable<Objective> objectives, IEnumerable<MasteryEvidence> evidence, MasteryPolicy policy, DateTime now)`, returned in objective order.

- [ ] **Step 1: Write the failing tests**

Create `tests/LearnForge.Tests/MasteryEvaluatorTests.cs`:

```csharp
using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class MasteryEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Objective[] Objectives = [new("sets", "Sets", []), new("logic", "Logic", ["sets"])];
    private long order;

    private MasteryEvidence E(string family, bool correct, double daysAgo = 1, string objective = "sets", bool independent = false) =>
        new(++order, family, [objective], correct, independent, Now.AddDays(-daysAgo));

    private static ObjectiveMastery Sets(params MasteryEvidence[] evidence) =>
        MasteryEvaluator.Evaluate(Objectives, evidence, new MasteryPolicy(), Now).Single(m => m.ObjectiveId == "sets");

    [Fact] public void No_evidence_is_not_started()
    {
        var m = Sets();
        Assert.Equal(MasteryState.NotStarted, m.State);
        Assert.Equal((0, 0), (m.Correct, m.Considered));
        Assert.Null(m.LastEvidenceAt);
    }

    [Fact] public void Fewer_than_the_minimum_families_is_emerging_even_when_correct() =>
        Assert.Equal(MasteryState.Emerging, Sets(E("a", true), E("b", true)).State);

    [Fact] public void Four_of_the_last_five_families_is_proficient_and_three_is_developing()
    {
        Assert.Equal(MasteryState.Proficient, Sets(E("a", true), E("b", true), E("c", true), E("d", true), E("e", false)).State);
        Assert.Equal(MasteryState.Developing, Sets(E("a", true), E("b", true), E("c", true), E("d", false), E("e", false)).State);
    }

    [Fact] public void Only_the_latest_answer_per_family_counts()
    {
        var m = Sets(E("a", false, daysAgo: 5), E("a", true, daysAgo: 1), E("b", true), E("c", true));
        Assert.Equal((3, 3), (m.Correct, m.Considered));
        Assert.Equal(MasteryState.Proficient, m.State);
    }

    [Fact] public void Only_the_latest_window_of_families_counts()
    {
        var m = Sets(E("old1", false, 30), E("old2", false, 29), E("a", true, 5), E("b", true, 4), E("c", true, 3), E("d", true, 2), E("e", true, 1));
        Assert.Equal((5, 5), (m.Correct, m.Considered));
        Assert.Equal(MasteryState.Proficient, m.State);
    }

    [Fact] public void Ties_on_timestamp_are_broken_by_order()
    {
        var m = Sets(E("a", false, 2), E("a", true, 2), E("b", true), E("c", true));
        Assert.Equal(3, m.Correct);
    }

    [Fact] public void Proficiency_becomes_review_due_after_the_review_interval()
    {
        Assert.True(Sets(E("a", true, 70), E("b", true, 65), E("c", true, 61)).ReviewDue);
        Assert.False(Sets(E("a", true, 10), E("b", true, 9), E("c", true, 8)).ReviewDue);
        Assert.False(Sets(E("a", false, 70), E("b", false, 65), E("c", false, 61)).ReviewDue);
    }

    [Fact] public void Evidence_only_affects_its_own_objectives_and_counts_independent_answers()
    {
        var all = MasteryEvaluator.Evaluate(Objectives, [E("a", true, objective: "logic", independent: true), E("b", true, objective: "logic")], new MasteryPolicy(), Now);
        Assert.Equal(new[] { "sets", "logic" }, all.Select(m => m.ObjectiveId));
        Assert.Equal(MasteryState.NotStarted, all[0].State);
        Assert.Equal((2, 1), (all[1].Considered, all[1].Independent));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~MasteryEvaluatorTests"`
Expected: build FAILS with `CS0246 ... 'MasteryEvidence' could not be found`.

- [ ] **Step 3: Implement**

`src/LearnForge.Core/Domain/Enums/MasteryState.cs`:
```csharp
namespace LearnForge.Core;

public enum MasteryState { NotStarted, Emerging, Developing, Proficient }
```

`src/LearnForge.Core/Domain/Mastery/MasteryEvidence.cs`:
```csharp
namespace LearnForge.Core;

// Order breaks timestamp ties deterministically (the ledger's identity column).
public sealed record MasteryEvidence(long Order, string FamilyId, string[] ObjectiveIds, bool FullyCorrect, bool Independent, DateTime At);
```

`src/LearnForge.Core/Domain/Mastery/ObjectiveMastery.cs`:
```csharp
namespace LearnForge.Core;

public sealed record ObjectiveMastery(string ObjectiveId, MasteryState State, int Correct, int Considered, int Independent,
    DateTime? LastEvidenceAt, bool ReviewDue);
```

`src/LearnForge.Core/Domain/Mastery/MasteryEvaluator.cs`:
```csharp
namespace LearnForge.Core;

public static class MasteryEvaluator
{
    // Callers pass only countable evidence: mock answers always, learning answers only when answered.
    public static ObjectiveMastery[] Evaluate(IEnumerable<Objective> objectives, IEnumerable<MasteryEvidence> evidence, MasteryPolicy policy, DateTime now)
    {
        var records = evidence.ToArray();
        return objectives.Select(objective =>
        {
            // The latest answer per family reflects current knowledge; drilling one item counts once.
            var window = records.Where(e => e.ObjectiveIds.Contains(objective.Id))
                .GroupBy(e => e.FamilyId)
                .Select(g => g.OrderByDescending(e => e.At).ThenByDescending(e => e.Order).First())
                .OrderByDescending(e => e.At).ThenByDescending(e => e.Order)
                .Take(policy.Window)
                .ToArray();
            var considered = window.Length;
            var correct = window.Count(e => e.FullyCorrect);
            var state = considered == 0 ? MasteryState.NotStarted
                : considered < policy.MinimumEvidence ? MasteryState.Emerging
                : correct * 100m >= policy.ProficientPercent * considered ? MasteryState.Proficient
                : MasteryState.Developing;
            DateTime? last = considered == 0 ? null : window[0].At;
            var reviewDue = state == MasteryState.Proficient && last < now.AddDays(-policy.ReviewAfterDays);
            return new ObjectiveMastery(objective.Id, state, correct, considered, window.Count(e => e.Independent), last, reviewDue);
        }).ToArray();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~MasteryEvaluatorTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat(core): evaluate per-objective mastery from latest family evidence" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Next-step planner

**Files:**
- Create: `src/LearnForge.Core/Domain/Enums/NextStepKind.cs`, `src/LearnForge.Core/Domain/Enums/NextStepReason.cs`, `src/LearnForge.Core/Domain/Learning/NextStep.cs`, `src/LearnForge.Core/Domain/Learning/NextStepPlanner.cs`
- Test: `tests/LearnForge.Tests/NextStepPlannerTests.cs`

**Interfaces:**
- Consumes: `ObjectiveMastery`, `MasteryState` (Task 2); `CourseGoal` (Task 1); the existing `ReadinessResult`.
- Produces: `enum NextStepKind { ReadLesson, Practise, Review, TakeMock }`; `enum NextStepReason { StartObjective, ContinueReading, NeedsEvidence, BelowProficient, ReviewDue, ReadyForMock }`; `record NextStep(NextStepKind Kind, NextStepReason Reason, string? ObjectiveId = null, string? LessonId = null, string? BlueprintId = null)`; `static NextStep[] NextStepPlanner.Plan(Pack pack, IReadOnlyCollection<ObjectiveMastery> mastery, IReadOnlySet<string> completedLessons, ReadinessResult? readiness, int limit = 3)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/LearnForge.Tests/NextStepPlannerTests.cs` (demo pack: `sets` → `logic` → `ordering`, lessons `sets-intro`, `logic-intro`, `ordering-intro`, blueprints `short` and `full`):

```csharp
using LearnForge.Core;
using Xunit;
using static LearnForge.Core.MasteryState;

namespace LearnForge.Tests;

public class NextStepPlannerTests
{
    private static readonly Pack Demo = CoreTests.Demo();
    private static readonly ReadinessResult NotReady = new(false, "", new(AssessmentSize.Short, 5, 0, false, []), new(AssessmentSize.Full, 3, 0, false, []), 90);

    private static ObjectiveMastery M(string id, MasteryState state, bool reviewDue = false) => new(id, state, 0, 0, 0, null, reviewDue);
    private static ObjectiveMastery[] States(MasteryState sets, MasteryState logic, MasteryState ordering) => [M("sets", sets), M("logic", logic), M("ordering", ordering)];

    [Fact] public void A_new_learner_starts_with_the_first_lesson_of_the_root_objective()
    {
        var steps = NextStepPlanner.Plan(Demo, States(NotStarted, NotStarted, NotStarted), new HashSet<string>(), NotReady);
        Assert.Equal(new[] { new NextStep(NextStepKind.ReadLesson, NextStepReason.StartObjective, "sets", "sets-intro") }, steps);
    }

    [Fact] public void After_reading_the_learner_practises_until_proficient()
    {
        var read = new HashSet<string> { "sets-intro" };
        Assert.Equal(new NextStep(NextStepKind.Practise, NextStepReason.NeedsEvidence, "sets"), NextStepPlanner.Plan(Demo, States(Emerging, NotStarted, NotStarted), read, NotReady).Single());
        Assert.Equal(new NextStep(NextStepKind.Practise, NextStepReason.BelowProficient, "sets"), NextStepPlanner.Plan(Demo, States(Developing, NotStarted, NotStarted), read, NotReady).Single());
    }

    [Fact] public void Prerequisites_unlock_later_objectives_in_depth_order()
    {
        var steps = NextStepPlanner.Plan(Demo, States(Proficient, Developing, NotStarted), new HashSet<string> { "sets-intro" }, NotReady);
        Assert.Equal(new[] { new NextStep(NextStepKind.ReadLesson, NextStepReason.ContinueReading, "logic", "logic-intro") }, steps);
    }

    [Fact] public void Review_due_objectives_follow_frontier_work()
    {
        ObjectiveMastery[] mastery = [M("sets", Proficient, reviewDue: true), M("logic", NotStarted), M("ordering", NotStarted)];
        var steps = NextStepPlanner.Plan(Demo, mastery, new HashSet<string> { "sets-intro" }, NotReady);
        Assert.Equal(new[]
        {
            new NextStep(NextStepKind.ReadLesson, NextStepReason.StartObjective, "logic", "logic-intro"),
            new NextStep(NextStepKind.Review, NextStepReason.ReviewDue, "sets")
        }, steps);
    }

    [Fact] public void A_mock_is_suggested_only_for_readiness_goals_once_everything_is_proficient()
    {
        var all = States(Proficient, Proficient, Proficient);
        var lessons = Demo.Lessons.Select(l => l.Id).ToHashSet();
        Assert.Equal(new[] { new NextStep(NextStepKind.TakeMock, NextStepReason.ReadyForMock, BlueprintId: "short") }, NextStepPlanner.Plan(Demo, all, lessons, NotReady));
        Assert.Empty(NextStepPlanner.Plan(Demo, all, lessons, NotReady with { Ready = true }));
        Assert.Empty(NextStepPlanner.Plan(Demo with { Goal = CourseGoal.Mastery }, all, lessons, NotReady));
        Assert.Empty(NextStepPlanner.Plan(Demo, all, lessons, readiness: null));
    }

    [Fact] public void Output_is_limited_and_deterministic()
    {
        var flat = Demo with { Objectives = Demo.Objectives.Select(o => o with { Prerequisites = [] }).ToArray() };
        var first = NextStepPlanner.Plan(flat, States(NotStarted, NotStarted, NotStarted), new HashSet<string>(), NotReady, limit: 2);
        Assert.Equal(new[] { "sets", "logic" }, first.Select(s => s.ObjectiveId));
        Assert.Equal(first, NextStepPlanner.Plan(flat, States(NotStarted, NotStarted, NotStarted), new HashSet<string>(), NotReady, limit: 2));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~NextStepPlannerTests"`
Expected: build FAILS with `CS0103: The name 'NextStepPlanner' does not exist`.

- [ ] **Step 3: Implement**

`src/LearnForge.Core/Domain/Enums/NextStepKind.cs`:
```csharp
namespace LearnForge.Core;

public enum NextStepKind { ReadLesson, Practise, Review, TakeMock }
```

`src/LearnForge.Core/Domain/Enums/NextStepReason.cs`:
```csharp
namespace LearnForge.Core;

public enum NextStepReason { StartObjective, ContinueReading, NeedsEvidence, BelowProficient, ReviewDue, ReadyForMock }
```

`src/LearnForge.Core/Domain/Learning/NextStep.cs`:
```csharp
namespace LearnForge.Core;

public sealed record NextStep(NextStepKind Kind, NextStepReason Reason, string? ObjectiveId = null, string? LessonId = null, string? BlueprintId = null);
```

`src/LearnForge.Core/Domain/Learning/NextStepPlanner.cs`:
```csharp
namespace LearnForge.Core;

public static class NextStepPlanner
{
    // Suggests work on the "frontier": objectives that are not yet proficient but whose prerequisites are.
    public static NextStep[] Plan(Pack pack, IReadOnlyCollection<ObjectiveMastery> mastery, IReadOnlySet<string> completedLessons,
        ReadinessResult? readiness, int limit = 3)
    {
        var states = mastery.ToDictionary(m => m.ObjectiveId);
        bool Proficient(string id) => states.TryGetValue(id, out var m) && m.State == MasteryState.Proficient;
        var byId = pack.Objectives.ToDictionary(o => o.Id);
        var depths = new Dictionary<string, int>();
        // Prerequisites are validated to be acyclic, so this recursion terminates.
        int Depth(string id) => depths.TryGetValue(id, out var d) ? d
            : depths[id] = byId[id].Prerequisites.Length == 0 ? 0 : byId[id].Prerequisites.Max(Depth) + 1;

        var steps = new List<NextStep>();
        var frontier = pack.Objectives.Select((objective, index) => (Objective: objective, Index: index))
            .Where(x => !Proficient(x.Objective.Id) && x.Objective.Prerequisites.All(Proficient))
            .OrderBy(x => Depth(x.Objective.Id)).ThenBy(x => x.Index);
        foreach (var (objective, _) in frontier)
        {
            var state = states.TryGetValue(objective.Id, out var m) ? m.State : MasteryState.NotStarted;
            var lesson = pack.Lessons.FirstOrDefault(l => l.ObjectiveIds.Contains(objective.Id) && !completedLessons.Contains(l.Id));
            steps.Add(lesson is not null
                ? new(NextStepKind.ReadLesson, state == MasteryState.NotStarted ? NextStepReason.StartObjective : NextStepReason.ContinueReading, objective.Id, lesson.Id)
                : new(NextStepKind.Practise, state == MasteryState.Developing ? NextStepReason.BelowProficient : NextStepReason.NeedsEvidence, objective.Id));
        }
        steps.AddRange(pack.Objectives.Where(o => states.TryGetValue(o.Id, out var m) && m.ReviewDue)
            .Select(o => new NextStep(NextStepKind.Review, NextStepReason.ReviewDue, o.Id)));
        var mock = pack.Blueprints.FirstOrDefault(b => b.Size == AssessmentSize.Short) ?? pack.Blueprints.FirstOrDefault();
        if (pack.Goal == CourseGoal.Readiness && readiness is { Ready: false } && mock is not null && pack.Objectives.All(o => Proficient(o.Id)))
            steps.Add(new(NextStepKind.TakeMock, NextStepReason.ReadyForMock, BlueprintId: mock.Id));
        return steps.Take(limit).ToArray();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~NextStepPlannerTests"`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat(core): plan prerequisite-aware next steps" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Move endpoints into feature modules (no behaviour change)

This is a pure refactor, so the existing test suite is the safety net. Endpoint bodies move verbatim.

**Files:**
- Create: `apps/api/Endpoints/AuthEndpoints.cs`, `CatalogEndpoints.cs`, `LearnerEndpoints.cs`, `AttemptEndpoints.cs`, `AuthoringEndpoints.cs`; `apps/api/Startup/DatabaseInitializer.cs`
- Modify: `apps/api/Program.cs` (replace lines 104–243)

**Interfaces:**
- Produces: extension methods on `IEndpointRouteBuilder`: `MapAuth()`, `MapCatalog()`, `MapLearner()`, `MapAttempts()`, `MapAuthoring()`; `static Task<bool> DatabaseInitializer.RunAsync(WebApplication app, string[] args)`, which returns `false` when a maintenance command (`--migrate`, `--grant-publisher`) finished and the process should exit.

- [ ] **Step 1: Create `apps/api/Endpoints/AuthEndpoints.cs`**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace LearnForge.Api.Endpoints;

public static class AuthEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapAuth()
        {
            var auth = app.MapGroup("/api/auth");
            auth.MapGet("/csrf", (HttpContext ctx, IAntiforgery anti) => new CsrfResponse(anti.GetAndStoreTokens(ctx).RequestToken!));
            auth.MapPost("/register", async (RegisterRequest request, UserManager<User> users, SignInManager<User> signIn, IConfiguration configuration) =>
            {
                if (!configuration.GetValue("Auth:AllowRegistration", true)) return Results.Problem("Registration is closed.", statusCode: 403);
                if (request.Email.Length > 254 || request.Password.Length > 128 || request.DisplayName.Length is < 1 or > 80 || !System.Net.Mail.MailAddress.TryCreate(request.Email, out var address) || address.Address != request.Email) return Results.BadRequest(new ApiErrorResponse("Enter a valid email, a display name, and a 12–128 character password."));
                var user = new User { UserName = request.Email.Trim(), Email = request.Email.Trim(), DisplayName = request.DisplayName.Trim() };
                var result = await users.CreateAsync(user, request.Password);
                if (!result.Succeeded) return Results.BadRequest(new ApiErrorResponse("Registration failed. Use a unique email and a 12+ character password with upper/lowercase letters and a number."));
                await signIn.SignInAsync(user, false);
                return Results.Ok(new RegisterResponse(user.DisplayName, user.Email!));
            }).RequireRateLimiting("auth");
            auth.MapPost("/login", async (LoginRequest request, SignInManager<User> signIn) =>
            {
                if (request.Email.Length > 254 || request.Password.Length > 128) return Results.Unauthorized();
                var result = await signIn.PasswordSignInAsync(request.Email.Trim(), request.Password, false, true);
                return result.Succeeded ? Results.Ok() : Results.Json(new ApiErrorResponse("Sign-in failed. Check your details or try again later."), statusCode: 401);
            }).RequireRateLimiting("auth");
            auth.MapGet("/me", async (ClaimsPrincipal principal, UserManager<User> users) =>
            {
                var user = await users.GetUserAsync(principal);
                return user is null ? Results.Unauthorized() : Results.Ok(new CurrentUserResponse(user.Id, user.DisplayName, user.Email!, await users.IsInRoleAsync(user, "Publisher")));
            }).RequireAuthorization();
            auth.MapPost("/logout", async (SignInManager<User> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
            auth.MapPost("/password", async (ChangePasswordRequest request, ClaimsPrincipal principal, UserManager<User> users, SignInManager<User> signIn) =>
            {
                var user = (await users.GetUserAsync(principal))!;
                if (request.NewPassword.Length > 128 || request.CurrentPassword.Length > 128) return Results.BadRequest();
                var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded) return Results.BadRequest(new ApiErrorResponse("Password change failed. Check the current password and the new password requirements."));
                await users.UpdateSecurityStampAsync(user); await signIn.RefreshSignInAsync(user); return Results.NoContent();
            }).RequireAuthorization().RequireRateLimiting("auth");
        }
    }
}
```

- [ ] **Step 2: Create `apps/api/Endpoints/CatalogEndpoints.cs`**

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Identity.AccountIdentity;

namespace LearnForge.Api.Endpoints;

public static class CatalogEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapCatalog()
        {
            app.MapGet("/api/catalog", async (AppDb db) => (await db.Packs.OrderByDescending(p => p.PublishedAt).ToListAsync()).DistinctBy(p => p.PackId).Select(release =>
            {
                var p = Json.Read<Pack>(release.ContentJson);
                return new CatalogSummaryDto(p.Id, p.Title, p.Description, p.Version, p.Lessons.Length, p.Questions.Length, p.Objectives.Length);
            }).ToArray());
            app.MapGet("/api/catalog/{id}", async (string id, AppDb db, ClaimsPrincipal user) =>
            {
                var release = await db.Packs.Where(p => p.PackId == id).OrderByDescending(p => p.PublishedAt).FirstOrDefaultAsync();
                if (release is null) return Results.NotFound();
                var p = Json.Read<Pack>(release.ContentJson);
                var completed = user.Identity?.IsAuthenticated == true ? await db.Completions.Where(c => c.UserId == UserId(user) && c.ReleaseId == release.Id).Select(c => c.LessonId).ToArrayAsync() : [];
                return Results.Ok(new CourseCatalogDto(p.Id, p.Title, p.Description, p.Version, p.License, p.Objectives,
                    p.Lessons, p.Blueprints, p.Sources, p.Readiness ?? new(), completed, p.Questions.Length));
            });
        }
    }
}
```

- [ ] **Step 3: Create `apps/api/Endpoints/LearnerEndpoints.cs`**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Identity.AccountIdentity;

namespace LearnForge.Api.Endpoints;

public static class LearnerEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapLearner()
        {
            var me = app.MapGroup("/api/me").RequireAuthorization();
            me.MapPut("/courses/{id}/lessons/{lessonId}", async (string id, string lessonId, ClaimsPrincipal user, AppDb db) =>
            {
                var release = await db.Packs.Where(p => p.PackId == id).OrderByDescending(p => p.PublishedAt).FirstOrDefaultAsync();
                if (release is null || !Json.Read<Pack>(release.ContentJson).Lessons.Any(l => l.Id == lessonId)) return Results.NotFound();
                if (await db.Completions.FindAsync(UserId(user), release.Id, lessonId) is null) { db.Completions.Add(new() { UserId = UserId(user), ReleaseId = release.Id, LessonId = lessonId }); await db.SaveChangesAsync(); }
                return Results.NoContent();
            });
            me.MapGet("/dashboard", async (ClaimsPrincipal user, AppDb db, AttemptService service) =>
            {
                foreach (var a in await db.Attempts.Where(a => a.UserId == UserId(user) && a.Status == AttemptStatus.InProgress).ToListAsync()) await service.Expire(a);
                return await Analytics.Dashboard(db, UserId(user), service.Now);
            });
            me.MapGet("/export", async (ClaimsPrincipal user, AppDb db, AttemptService service) =>
            {
                var attempts = await db.Attempts.Where(a => a.UserId == UserId(user)).ToListAsync();
                var export = new LearnerExportDto(service.Now, await Analytics.Dashboard(db, UserId(user), service.Now), attempts.Select(service.View).ToArray());
                return Results.File(System.Text.Encoding.UTF8.GetBytes(Json.Write(export)), "application/json", "learnforge-history.json");
            });
            me.MapDelete("/account", async ([FromBody] DeleteAccountRequest request, ClaimsPrincipal principal, UserManager<User> users, SignInManager<User> signIn, AppDb db) =>
            {
                var user = (await users.GetUserAsync(principal))!;
                if (request.Password.Length > 128 || !await users.CheckPasswordAsync(user, request.Password)) return Results.Unauthorized();
                var audit = await db.Audit.Where(a => a.ActorId == user.Id).ToListAsync(); db.Audit.RemoveRange(audit);
                await db.SaveChangesAsync();
                var deleted = await users.DeleteAsync(user);
                if (!deleted.Succeeded) return Results.Problem("Account deletion failed.");
                await signIn.SignOutAsync(); return Results.NoContent();
            });
        }
    }
}
```

- [ ] **Step 4: Create `apps/api/Endpoints/AttemptEndpoints.cs`**

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Identity.AccountIdentity;

namespace LearnForge.Api.Endpoints;

public static class AttemptEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapAttempts()
        {
            var attempts = app.MapGroup("/api/me/attempts").RequireAuthorization();
            attempts.MapGet("", async (ClaimsPrincipal user, AppDb db) => (await db.Attempts.Where(a => a.UserId == UserId(user)).OrderByDescending(a => a.StartedAt).ToListAsync()).Select(Analytics.Summary));
            attempts.MapPost("", async (StartRequest request, ClaimsPrincipal user, AttemptService service) => service.View(await service.Start(UserId(user), request)));
            attempts.MapGet("/{id}", async (string id, ClaimsPrincipal user, AttemptService service) => service.View(await service.Find(UserId(user), id)));
            attempts.MapPut("/{id}/responses", async (string id, ResponseRequest request, ClaimsPrincipal user, AttemptService service) =>
            {
                var a = await service.Find(UserId(user), id); await service.Save(a, request); return service.View(a);
            });
            attempts.MapPost("/{id}/submit", async (string id, TransitionRequest request, ClaimsPrincipal user, AttemptService service) =>
            {
                var a = await service.Find(UserId(user), id); await service.Submit(a, request.Revision); return service.View(a);
            });
            attempts.MapPost("/{id}/section", async (string id, TransitionRequest request, ClaimsPrincipal user, AttemptService service) =>
            {
                var a = await service.Find(UserId(user), id); await service.NextSection(a, request.Revision); return service.View(a);
            });
        }
    }
}
```

- [ ] **Step 5: Create `apps/api/Endpoints/AuthoringEndpoints.cs`**

```csharp
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Identity.AccountIdentity;

namespace LearnForge.Api.Endpoints;

public static class AuthoringEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapAuthoring()
        {
            var authoring = app.MapGroup("/api/authoring").RequireAuthorization(policy => policy.RequireRole("Publisher"));
            authoring.MapPost("/validate", (JsonElement source) =>
            {
                var result = ContentEngine.Compile(source.GetRawText());
                return new ValidationResponseDto(result.Success, result.Hash, result.Diagnostics, result.Pack?.Questions.Length, result.Pack?.Lessons.Length);
            });
            authoring.MapPost("/publish", async (JsonElement source, AppDb db, ClaimsPrincipal user) =>
            {
                var result = ContentEngine.Compile(source.GetRawText());
                if (!result.Success) return Results.BadRequest(new ApiErrorResponse("Content validation failed."));
                var p = result.Pack!;
                if (await db.Packs.AnyAsync(r => r.PackId == p.Id && r.Version == p.Version)) return Results.Conflict(new ApiErrorResponse("This release already exists. Publish a new version."));
                var release = new PackRelease { PackId = p.Id, Version = p.Version, ContentJson = Json.Write(p), Hash = result.Hash };
                db.Packs.Add(release); db.Audit.Add(new() { ActorId = UserId(user), Action = "pack.published", ResourceId = release.Id }); await db.SaveChangesAsync();
                return Results.Ok(new PublishResponseDto(p.Id, p.Version));
            });
        }
    }
}
```

- [ ] **Step 6: Create `apps/api/Startup/DatabaseInitializer.cs`**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api.Startup;

public static class DatabaseInitializer
{
    // Returns false when a maintenance command (--migrate, --grant-publisher) finished and the process should exit.
    public static async Task<bool> RunAsync(WebApplication app, string[] args)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        // Separate migrations preserve each provider's native types and identity columns.
        if (app.Configuration.GetValue("Database:AutoMigrate", true) || args.Contains("--migrate"))
            await db.Database.MigrateAsync();
        await SeedPacksAsync(db);
        if (args.Contains("--migrate")) return false;
        var grantIndex = Array.IndexOf(args, "--grant-publisher");
        if (grantIndex < 0) return true;
        if (args.Length <= grantIndex + 1) throw new InvalidOperationException("Supply an existing account email.");
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await manager.FindByEmailAsync(args[grantIndex + 1]) ?? throw new InvalidOperationException("Account must be registered first.");
        if (!await roles.RoleExistsAsync("Publisher")) await roles.CreateAsync(new("Publisher"));
        var result = await manager.AddToRoleAsync(user, "Publisher");
        if (!result.Succeeded && !await manager.IsInRoleAsync(user, "Publisher")) throw new InvalidOperationException("Role grant failed.");
        app.Logger.LogInformation("Publisher role granted. Sign out and back in to refresh the session.");
        return false;
    }

    private static async Task SeedPacksAsync(AppDb db)
    {
        var packDirectory = Path.Combine(AppContext.BaseDirectory, "packs");
        if (Directory.Exists(packDirectory))
            foreach (var path in Directory.GetFiles(packDirectory, "*.json", SearchOption.AllDirectories))
            {
                var compiled = ContentEngine.Compile(await File.ReadAllTextAsync(path));
                if (!compiled.Success) throw new InvalidOperationException($"Invalid seed pack {Path.GetFileName(path)}: {Json.Write(compiled.Diagnostics)}");
                var p = compiled.Pack!;
                if (!await db.Packs.AnyAsync(x => x.PackId == p.Id && x.Version == p.Version)) db.Packs.Add(new() { PackId = p.Id, Version = p.Version, ContentJson = Json.Write(p), Hash = compiled.Hash });
            }
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 7: Slim `Program.cs`**

Delete everything from line 104 (`var auth = app.MapGroup("/api/auth");`) to the end of the file (`app.Run();`, line 243), and append:
```csharp
app.MapAuth();
app.MapCatalog();
app.MapLearner();
app.MapAttempts();
app.MapAuthoring();

if (!await DatabaseInitializer.RunAsync(app, args)) return;
app.Run();
```
At the top of `Program.cs`, add `using LearnForge.Api.Endpoints;` and `using LearnForge.Api.Startup;`, and delete `using static LearnForge.Api.Services.Identity.AccountIdentity;`.

- [ ] **Step 8: Verify nothing changed**

Run: `dotnet build LearnForge.slnx -c Release && dotnet test LearnForge.slnx`
Expected: build succeeds with 0 warnings; all tests PASS (22 original plus those from Tasks 1–3).

- [ ] **Step 9: Commit**

```bash
git add apps/api
git commit -m "refactor(api): move endpoints into C# 14 extension-member modules" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Release cache

**Files:**
- Create: `apps/api/Services/Content/ReleaseView.cs`, `apps/api/Services/Content/ReleaseCache.cs`
- Modify: `apps/api/GlobalUsings.cs`, `apps/api/Program.cs` (service registration), `apps/api/Endpoints/CatalogEndpoints.cs`, `apps/api/Endpoints/LearnerEndpoints.cs` (lesson PUT), `apps/api/Services/Attempts/AttemptService.cs` (`Start`)
- Test: `tests/LearnForge.Tests/ReleaseCacheTests.cs`

**Interfaces:**
- Consumes: `ContentHash.Of` (Task 1).
- Produces: `record ReleaseView(string ReleaseId, Pack Pack, IReadOnlyDictionary<string, string> LessonHashes)`; singleton `ReleaseCache` with `Task<ReleaseView> Get(string releaseId)`, `Task<ReleaseView?> Latest(AppDb db, string packId)` and `Task<string[]> LatestIds(AppDb db)` (latest release per pack, newest first). `AttemptService` gains a `ReleaseCache releases` constructor parameter.

- [ ] **Step 1: Write the failing tests**

Create `tests/LearnForge.Tests/ReleaseCacheTests.cs`:

```csharp
using LearnForge.Api;
using LearnForge.Api.Services.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class ReleaseCacheTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact] public async Task The_latest_release_is_deserialized_once_and_reused()
    {
        var cache = factory.Services.GetRequiredService<ReleaseCache>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var first = await cache.Latest(db, "reasoning-foundations");
        var second = await cache.Latest(db, "reasoning-foundations");
        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(first!.Pack.Lessons.Select(l => l.Id).Order(), first.LessonHashes.Keys.Order());
        Assert.Null(await cache.Latest(db, "missing-pack"));
        Assert.Contains(first.ReleaseId, await cache.LatestIds(db));
    }

    [Fact] public async Task Concurrent_misses_share_one_load()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var id = await db.Packs.Where(p => p.PackId == "evidence-lab").Select(p => p.Id).FirstAsync();
        using var cache = new ReleaseCache(factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Services.GetRequiredService<IConfiguration>());
        var views = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => cache.Get(id)));
        Assert.All(views, v => Assert.Same(views[0], v));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~ReleaseCacheTests"`
Expected: build FAILS with `CS0234: ... 'Content' does not exist in the namespace 'LearnForge.Api.Services'`.

- [ ] **Step 3: Implement the cache**

`apps/api/Services/Content/ReleaseView.cs`:
```csharp
namespace LearnForge.Api.Services.Content;

public sealed record ReleaseView(string ReleaseId, Pack Pack, IReadOnlyDictionary<string, string> LessonHashes);
```

`apps/api/Services/Content/ReleaseCache.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LearnForge.Api.Services.Content;

// Pack releases are immutable, so a deserialized release never needs invalidation. Only the
// "which release is latest" lookup reads the database, which keeps multiple replicas consistent.
public sealed class ReleaseCache(IServiceScopeFactory scopes, IConfiguration configuration) : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = configuration.GetValue("ReleaseCache:Capacity", 64) });
    private readonly Lock gate = new();

    public Task<ReleaseView> Get(string releaseId)
    {
        if (cache.TryGetValue(releaseId, out Lazy<Task<ReleaseView>>? entry)) return entry!.Value;
        lock (gate)
        {
            // One Lazy per release: concurrent misses share a single load.
            if (!cache.TryGetValue(releaseId, out entry))
            {
                entry = new Lazy<Task<ReleaseView>>(() => Load(releaseId));
                cache.Set(releaseId, entry, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = TimeSpan.FromHours(6) });
            }
        }
        return entry!.Value;
    }

    public async Task<ReleaseView?> Latest(AppDb db, string packId)
    {
        var releaseId = await db.Packs.Where(p => p.PackId == packId).OrderByDescending(p => p.PublishedAt).Select(p => p.Id).FirstOrDefaultAsync();
        return releaseId is null ? null : await Get(releaseId);
    }

    public async Task<string[]> LatestIds(AppDb db) =>
        (await db.Packs.Select(p => new { p.Id, p.PackId, p.PublishedAt }).ToListAsync())
            .OrderByDescending(p => p.PublishedAt).DistinctBy(p => p.PackId).Select(p => p.Id).ToArray();

    private async Task<ReleaseView> Load(string releaseId)
    {
        try
        {
            // Loads run outside the caller's request scope because other requests may await the same task.
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var release = await db.Packs.AsNoTracking().SingleAsync(p => p.Id == releaseId);
            var pack = Json.Read<Pack>(release.ContentJson);
            return new ReleaseView(release.Id, pack, pack.Lessons.ToDictionary(l => l.Id, l => ContentHash.Of(l)));
        }
        catch
        {
            cache.Remove(releaseId); // Never keep a failed load; the next request retries.
            throw;
        }
    }

    public void Dispose() => cache.Dispose();
}
```

Add `global using LearnForge.Api.Services.Content;` to `apps/api/GlobalUsings.cs`. In `Program.cs`, after `builder.Services.AddScoped<AttemptService>();`, add:
```csharp
builder.Services.AddSingleton<ReleaseCache>();
```

- [ ] **Step 4: Serve the catalog from the cache**

Replace the body of `MapCatalog()` in `CatalogEndpoints.cs`:
```csharp
            app.MapGet("/api/catalog", async (AppDb db, ReleaseCache releases) =>
            {
                var courses = new List<CatalogSummaryDto>();
                foreach (var releaseId in await releases.LatestIds(db))
                {
                    var p = (await releases.Get(releaseId)).Pack;
                    courses.Add(new(p.Id, p.Title, p.Description, p.Version, p.Lessons.Length, p.Questions.Length, p.Objectives.Length));
                }
                return courses.ToArray();
            });
            app.MapGet("/api/catalog/{id}", async (string id, AppDb db, ClaimsPrincipal user, ReleaseCache releases) =>
            {
                var release = await releases.Latest(db, id);
                if (release is null) return Results.NotFound();
                var p = release.Pack;
                var completed = user.Identity?.IsAuthenticated == true ? await db.Completions.Where(c => c.UserId == UserId(user) && c.ReleaseId == release.ReleaseId).Select(c => c.LessonId).ToArrayAsync() : [];
                return Results.Ok(new CourseCatalogDto(p.Id, p.Title, p.Description, p.Version, p.License, p.Objectives,
                    p.Lessons, p.Blueprints, p.Sources, p.Readiness ?? new(), completed, p.Questions.Length));
            });
```

In `LearnerEndpoints.cs`, replace the lesson PUT handler:
```csharp
            me.MapPut("/courses/{id}/lessons/{lessonId}", async (string id, string lessonId, ClaimsPrincipal user, AppDb db, ReleaseCache releases) =>
            {
                var release = await releases.Latest(db, id);
                if (release is null || !release.LessonHashes.ContainsKey(lessonId)) return Results.NotFound();
                if (await db.Completions.FindAsync(UserId(user), release.ReleaseId, lessonId) is null) { db.Completions.Add(new() { UserId = UserId(user), ReleaseId = release.ReleaseId, LessonId = lessonId }); await db.SaveChangesAsync(); }
                return Results.NoContent();
            });
```

- [ ] **Step 5: Use the cache when starting attempts**

In `AttemptService.cs`, change the class declaration to:
```csharp
public sealed class AttemptService(AppDb db, TimeProvider clock, ReleaseCache releases)
```
In `Start`, replace the two lines
```csharp
        var release = await db.Packs.Where(p => p.PackId == request.PackId).OrderByDescending(p => p.PublishedAt).FirstOrDefaultAsync()
            ?? throw new DomainError(404, "Course not found.");
```
with
```csharp
        var release = await releases.Latest(db, request.PackId) ?? throw new DomainError(404, "Course not found.");
```
Replace `var pack = Json.Read<Pack>(release.ContentJson);` with `var pack = release.Pack;`, and replace `PackReleaseId = release.Id` with `PackReleaseId = release.ReleaseId`.

- [ ] **Step 6: Run the tests**

Run: `dotnet test LearnForge.slnx`
Expected: all tests PASS, including the 2 new cache tests.

- [ ] **Step 7: Commit**

```bash
git add apps/api tests
git commit -m "feat(api): cache immutable pack releases" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Persistence model and migrations

**Files:**
- Create: `src/LearnForge.Core/Domain/Enums/EvidenceSource.cs`, `src/LearnForge.Core/Domain/Enums/EnrollmentStatus.cs`, `apps/api/Infrastructure/Entities/Enrollment.cs`, `apps/api/Infrastructure/Entities/EvidenceRecord.cs`, `apps/api/Infrastructure/Entities/LessonProgress.cs`, two generated migrations
- Delete: `apps/api/Infrastructure/Entities/Completion.cs`
- Modify: `apps/api/Infrastructure/Entities/Attempt.cs`, `apps/api/Infrastructure/Persistence/AppDb.cs`, `apps/api/Endpoints/CatalogEndpoints.cs`, `apps/api/Endpoints/LearnerEndpoints.cs`, `apps/api/Services/Analytics/Analytics.cs`
- Test: `tests/LearnForge.Tests/MigrationTests.cs`

**Interfaces:**
- Produces: `enum EvidenceSource { MockSubmission, LearningCheck, LearningSubmission }`; `enum EnrollmentStatus { Active, Archived }`; entities `Enrollment { UserId, PackId, Status, EnrolledAt, LastActivityAt }`, `EvidenceRecord { long Id, UserId, PackId, ReleaseId, AttemptId, QuestionId, FamilyId, string[] ObjectiveIds, Source, Answered, FullyCorrect, decimal Earned, decimal Possible, At }`, `LessonProgress { UserId, PackId, LessonId, CompletedAt, string? ContentHash }`; `Attempt.FocusObjectiveId` (`string?`); `AppDb` sets `LessonProgress`, `Enrollments`, `Evidence` (tables `LessonProgress`, `Enrollments`, `Evidence`).

- [ ] **Step 1: Write the failing migration test**

Create `tests/LearnForge.Tests/MigrationTests.cs`:

```csharp
using LearnForge.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace LearnForge.Tests;

public class MigrationTests
{
    [Fact] public async Task Release_completions_become_pack_progress_keeping_the_first_completion()
    {
        var file = Path.Combine(Path.GetTempPath(), "learnforge-migration-" + Guid.NewGuid().ToString("N") + ".db");
        await using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlite("Data Source=" + file).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260925054146_Initial");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "AspNetUsers" ("Id","DisplayName","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount") VALUES ('u1','Learner',0,0,0,0,0);
            INSERT INTO "Packs" ("Id","PackId","Version","ContentJson","Hash","PublishedAt") VALUES ('r1','demo','1.0.0','{}','h','2026-01-01 00:00:00'), ('r2','demo','2.0.0','{}','h','2026-02-01 00:00:00');
            INSERT INTO "Completions" ("UserId","ReleaseId","LessonId","At") VALUES ('u1','r1','intro','2026-01-02 00:00:00'), ('u1','r2','intro','2026-02-02 00:00:00'), ('u1','r2','next','2026-02-03 00:00:00');
            """);

        await migrator.MigrateAsync();

        var rows = await db.LessonProgress.OrderBy(p => p.LessonId).ToListAsync();
        Assert.Equal(new[] { "intro", "next" }, rows.Select(r => r.LessonId));
        Assert.All(rows, r => { Assert.Equal("demo", r.PackId); Assert.Null(r.ContentHash); });
        Assert.Equal(new DateTime(2026, 1, 2), rows[0].CompletedAt);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~MigrationTests"`
Expected: build FAILS with `CS1061: 'AppDb' does not contain a definition for 'LessonProgress'`.

- [ ] **Step 3: Add the enums and entities**

`src/LearnForge.Core/Domain/Enums/EvidenceSource.cs`:
```csharp
namespace LearnForge.Core;

public enum EvidenceSource { MockSubmission, LearningCheck, LearningSubmission }
```

`src/LearnForge.Core/Domain/Enums/EnrollmentStatus.cs`:
```csharp
namespace LearnForge.Core;

public enum EnrollmentStatus { Active, Archived }
```

`apps/api/Infrastructure/Entities/Enrollment.cs`:
```csharp
using LearnForge.Core;

namespace LearnForge.Api;

public sealed class Enrollment
{
    public string UserId { get; set; } = "";
    public string PackId { get; set; } = "";
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}
```

`apps/api/Infrastructure/Entities/EvidenceRecord.cs`:
```csharp
using LearnForge.Core;

namespace LearnForge.Api;

// Append-only: one row per question per attempt, written when its feedback is released.
public sealed class EvidenceRecord
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public string PackId { get; set; } = "";
    public string ReleaseId { get; set; } = "";
    public string AttemptId { get; set; } = "";
    public string QuestionId { get; set; } = "";
    public string FamilyId { get; set; } = "";
    public string[] ObjectiveIds { get; set; } = [];
    public EvidenceSource Source { get; set; }
    public bool Answered { get; set; }
    public bool FullyCorrect { get; set; }
    public decimal Earned { get; set; }
    public decimal Possible { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
```

`apps/api/Infrastructure/Entities/LessonProgress.cs`:
```csharp
namespace LearnForge.Api;

// Keyed by stable pack and lesson IDs so progress survives new releases.
public sealed class LessonProgress
{
    public string UserId { get; set; } = "";
    public string PackId { get; set; } = "";
    public string LessonId { get; set; } = "";
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public string? ContentHash { get; set; }
}
```

Delete `apps/api/Infrastructure/Entities/Completion.cs`. In `Attempt.cs`, add after the `Focus` property:
```csharp
    public string? FocusObjectiveId { get; set; }
```

- [ ] **Step 4: Map the entities in `AppDb`**

Replace the `DbSet` block and the two `Completion` lines in `AppDb.cs` so that the class reads:
```csharp
public class AppDb(DbContextOptions options) : IdentityDbContext<User>(options)
{
    public DbSet<PackRelease> Packs => Set<PackRelease>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<ResponseEvent> Responses => Set<ResponseEvent>();
    public DbSet<LessonProgress> LessonProgress => Set<LessonProgress>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<EvidenceRecord> Evidence => Set<EvidenceRecord>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<PackRelease>().HasIndex(x => new { x.PackId, x.Version }).IsUnique();
        builder.Entity<Attempt>().HasIndex(x => new { x.UserId, x.StartKey }).IsUnique();
        builder.Entity<Attempt>().HasIndex(x => x.ActiveKey).IsUnique();
        builder.Entity<Attempt>().HasIndex(x => new { x.UserId, x.PackId, x.StartedAt });
        builder.Entity<Attempt>().Property(x => x.Mode).HasConversion<string>();
        builder.Entity<Attempt>().Property(x => x.Size).HasConversion<string>();
        builder.Entity<Attempt>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<Attempt>().Property(x => x.Focus).HasConversion<string>();
        builder.Entity<Attempt>().Property(x => x.Revision).IsConcurrencyToken();
        builder.Entity<Attempt>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Attempt>().HasOne<PackRelease>().WithMany().HasForeignKey(x => x.PackReleaseId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ResponseEvent>().HasIndex(x => new { x.AttemptId, x.RequestId }).IsUnique();
        builder.Entity<ResponseEvent>().HasOne<Attempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<LessonProgress>().HasKey(x => new { x.UserId, x.PackId, x.LessonId });
        builder.Entity<LessonProgress>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Enrollment>().HasKey(x => new { x.UserId, x.PackId });
        builder.Entity<Enrollment>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<Enrollment>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // The unique index makes ledger writes idempotent per attempt and question.
        builder.Entity<EvidenceRecord>().HasIndex(x => new { x.AttemptId, x.QuestionId }).IsUnique();
        builder.Entity<EvidenceRecord>().HasIndex(x => new { x.UserId, x.PackId, x.At });
        builder.Entity<EvidenceRecord>().Property(x => x.Source).HasConversion<string>();
        builder.Entity<EvidenceRecord>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EvidenceRecord>().HasOne<Attempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 5: Switch existing readers and writers from `Completions` to `LessonProgress`**

In `CatalogEndpoints.cs`, replace the `var completed = ...` line in the detail endpoint with:
```csharp
                var completed = user.Identity?.IsAuthenticated == true
                    ? (await db.LessonProgress.Where(c => c.UserId == UserId(user) && c.PackId == id).Select(c => c.LessonId).ToArrayAsync()).Where(release.LessonHashes.ContainsKey).ToArray()
                    : [];
```

In `LearnerEndpoints.cs`, replace the lesson PUT handler body with:
```csharp
            me.MapPut("/courses/{id}/lessons/{lessonId}", async (string id, string lessonId, ClaimsPrincipal user, AppDb db, ReleaseCache releases) =>
            {
                var release = await releases.Latest(db, id);
                if (release is null || !release.LessonHashes.TryGetValue(lessonId, out var hash)) return Results.NotFound();
                var progress = await db.LessonProgress.FindAsync(UserId(user), id, lessonId);
                if (progress is null) db.LessonProgress.Add(new() { UserId = UserId(user), PackId = id, LessonId = lessonId, ContentHash = hash });
                else progress.ContentHash = hash;
                await db.SaveChangesAsync();
                return Results.NoContent();
            });
```

In `Analytics.cs`:
- change `var completions = await db.Completions.Where(c => c.UserId == user).ToListAsync();` to `var completions = await db.LessonProgress.Where(c => c.UserId == user).ToListAsync();`;
- change the `BuildCourse` parameter type `IReadOnlyCollection<Completion> completions` to `IReadOnlyCollection<LessonProgress> completions`;
- change `completions.Count(c => c.ReleaseId == release.Id)` to `completions.Count(c => c.PackId == release.PackId)`.

Run: `dotnet build LearnForge.slnx`
Expected: build succeeds.

- [ ] **Step 6: Generate both migrations**

```bash
dotnet ef migrations add LearningRecordFoundation --project apps/api --context AppDb --output-dir Migrations/Sqlite
dotnet ef migrations add LearningRecordFoundationPostgres --project apps/api --context PostgresDb --output-dir Migrations/Postgres
```
Expected: each generates `<timestamp>_<Name>.cs`, `<timestamp>_<Name>.Designer.cs`, and an updated model snapshot.

- [ ] **Step 7: Copy completions inside each migration**

Apply the same edit to **both** generated `<timestamp>_LearningRecordFoundation.cs` and `<timestamp>_LearningRecordFoundationPostgres.cs`. The SQL uses quoted identifiers and is valid in SQLite and PostgreSQL.

Add these constants inside the migration class:
```csharp
        // Completion becomes pack-scoped: the first completion of each lesson across all releases wins.
        private const string CopyCompletions = """
            INSERT INTO "LessonProgress" ("UserId", "PackId", "LessonId", "CompletedAt", "ContentHash")
            SELECT c."UserId", p."PackId", c."LessonId", MIN(c."At"), NULL
            FROM "Completions" c JOIN "Packs" p ON p."Id" = c."ReleaseId"
            GROUP BY c."UserId", p."PackId", c."LessonId";
            """;

        // Rollback maps each lesson back onto the pack's latest release.
        private const string RestoreCompletions = """
            INSERT INTO "Completions" ("UserId", "ReleaseId", "LessonId", "At")
            SELECT lp."UserId", (SELECT p."Id" FROM "Packs" p WHERE p."PackId" = lp."PackId" ORDER BY p."PublishedAt" DESC LIMIT 1), lp."LessonId", lp."CompletedAt"
            FROM "LessonProgress" lp
            WHERE EXISTS (SELECT 1 FROM "Packs" p WHERE p."PackId" = lp."PackId");
            """;
```
In `Up`, move the generated `migrationBuilder.DropTable(name: "Completions");` to the very end of the method and put `migrationBuilder.Sql(CopyCompletions);` immediately before it. The copy must run after `CreateTable(name: "LessonProgress", ...)` and before the drop.

In `Down`, reorder so the method ends with: the generated `CreateTable(name: "Completions", ...)` block, then `migrationBuilder.Sql(RestoreCompletions);`, then `migrationBuilder.DropTable(name: "LessonProgress");`. The drops of `Enrollments` and `Evidence` and the `DropColumn` for `FocusObjectiveId` can stay where EF placed them.

- [ ] **Step 8: Run the tests**

Run: `dotnet test LearnForge.slnx`
Expected: all tests PASS, including `MigrationTests`. The API tests also apply the new migration to a fresh SQLite database.

If Docker is available, also run the PostgreSQL suite:
```bash
docker compose up -d db
LEARNFORGE_TEST_POSTGRES='Host=127.0.0.1;Port=55432;Database=learnforge_test;Username=learnforge;Password=learnforge-local-only' dotnet test LearnForge.slnx
```
Expected: PASS. `MigrationTests` always uses SQLite; the PostgreSQL run applies `LearningRecordFoundationPostgres`.

- [ ] **Step 9: Commit**

```bash
git add src apps/api tests
git commit -m "feat(api): add enrollment, evidence ledger and pack-scoped lesson progress tables" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Write evidence when feedback is released

**Files:**
- Create: `apps/api/Services/Learning/EvidenceWriter.cs`, `tests/LearnForge.Tests/TestApi.cs`, `tests/LearnForge.Tests/EvidenceLedgerTests.cs`
- Modify: `apps/api/Services/Attempts/AttemptService.cs` (`Expire`, `Save`, `Submit`, `Finish`), `apps/api/Services/Attempts/ExpiryWorker.cs`, `apps/api/GlobalUsings.cs`

**Interfaces:**
- Consumes: `EvidenceRecord`, `EvidenceSource`, `AppDb.Evidence` (Task 6).
- Produces: `static class EvidenceWriter` with `bool IsAnswered(Answer? answer)`, `bool Countable(EvidenceSource source, bool answered)`, and `EvidenceRecord Record(Attempt attempt, Question question, Answer? answer, Grade grade, EvidenceSource source, DateTime at)`. `AttemptService.Finish` becomes `Task Finish(Attempt a, bool timedOut)`. Test helpers in `TestApi`: `LoadPack`, `Account`, `Token`, `Start`, `Id`, `QuestionIds`, `Correct`, `Save`, `Complete`, `Publisher`.

- [ ] **Step 1: Create the shared test helpers**

Create `tests/LearnForge.Tests/TestApi.cs`:
```csharp
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using LearnForge.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LearnForge.Tests;

// Shared HTTP helpers for integration tests. Each test class owns its ApiFactory, so auth rate limits stay per class.
public static class TestApi
{
    public const string Password = "TestingPassword123";

    public static Pack LoadPack(string packId) =>
        ContentEngine.Compile(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "packs", packId + ".json"))).Pack!;

    public static async Task<HttpClient> Account(ApiFactory factory)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Token(client);
        var registration = await client.PostAsJsonAsync("/api/auth/register", new { email = Guid.NewGuid() + "@example.test", password = Password, displayName = "Test Learner" });
        registration.EnsureSuccessStatusCode();
        await Token(client);
        return client;
    }

    public static async Task Token(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    public static async Task<JsonElement> Start(HttpClient client, string mode = "mock", string packId = "reasoning-foundations",
        string blueprintId = "short", string? focus = null, string? objectiveId = null)
    {
        var response = await client.PostAsJsonAsync("/api/me/attempts", new { packId, blueprintId, mode, requestId = Guid.NewGuid().ToString(), focus, objectiveId });
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Start failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static string Id(JsonElement attempt) => attempt.GetProperty("id").GetString()!;

    public static string[] QuestionIds(JsonElement attempt) =>
        attempt.GetProperty("questions").EnumerateArray().Select(q => q.GetProperty("id").GetString()!).ToArray();

    public static Answer Correct(Pack pack, string questionId)
    {
        var q = pack.Questions.Single(q => q.Id == questionId);
        return new(q.Grading.Correct, q.Grading.Matches ?? []);
    }

    // Saves one response and returns the next expected revision.
    public static async Task<int> Save(HttpClient client, JsonElement attempt, int revision, string questionId, Answer answer, bool check = false)
    {
        var response = await client.PutAsJsonAsync($"/api/me/attempts/{Id(attempt)}/responses", new { revision, requestId = Guid.NewGuid().ToString(), questionId, answer, check });
        response.EnsureSuccessStatusCode();
        return revision + 1;
    }

    // Answers every question correctly (or none when correct is false), submits, and returns the completed view.
    public static async Task<JsonElement> Complete(HttpClient client, JsonElement attempt, Pack pack, bool correct = true)
    {
        var revision = attempt.GetProperty("revision").GetInt32();
        if (correct) foreach (var id in QuestionIds(attempt)) revision = await Save(client, attempt, revision, id, Correct(pack, id));
        var submit = await client.PostAsJsonAsync($"/api/me/attempts/{Id(attempt)}/submit", new { revision });
        submit.EnsureSuccessStatusCode();
        return await submit.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<HttpClient> Publisher(ApiFactory factory)
    {
        var client = await Account(factory);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync("Publisher")) await roles.CreateAsync(new("Publisher"));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            await users.AddToRoleAsync((await users.FindByIdAsync(me.GetProperty("id").GetString()!))!, "Publisher");
        }
        await client.PostAsJsonAsync("/api/auth/logout", new { });
        await Token(client);
        (await client.PostAsJsonAsync("/api/auth/login", new { email = me.GetProperty("email").GetString(), password = Password })).EnsureSuccessStatusCode();
        await Token(client);
        return client;
    }
}
```

- [ ] **Step 2: Write the failing ledger tests**

Create `tests/LearnForge.Tests/EvidenceLedgerTests.cs`:
```csharp
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using LearnForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class EvidenceLedgerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Pack pack = TestApi.LoadPack("reasoning-foundations");

    private async Task<List<EvidenceRecord>> Evidence(string attemptId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDb>().Evidence.Where(e => e.AttemptId == attemptId).ToListAsync();
    }

    [Fact] public async Task A_learning_check_records_once_and_submission_records_the_rest()
    {
        var client = await TestApi.Account(factory);
        var attempt = await TestApi.Start(client, "learn");
        var attemptId = TestApi.Id(attempt);
        var ids = TestApi.QuestionIds(attempt);
        var check = new { revision = 0, requestId = Guid.NewGuid().ToString(), questionId = ids[0], answer = TestApi.Correct(pack, ids[0]), check = true };
        (await client.PutAsJsonAsync($"/api/me/attempts/{attemptId}/responses", check)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/me/attempts/{attemptId}/responses", check)).EnsureSuccessStatusCode();
        Assert.Single(await Evidence(attemptId));

        (await client.PostAsJsonAsync($"/api/me/attempts/{attemptId}/submit", new { revision = 1 })).EnsureSuccessStatusCode();

        var rows = await Evidence(attemptId);
        Assert.Equal(ids.Length, rows.Count);
        var checkedRow = rows.Single(r => r.QuestionId == ids[0]);
        Assert.Equal(EvidenceSource.LearningCheck, checkedRow.Source);
        Assert.True(checkedRow.Answered && checkedRow.FullyCorrect);
        var question = pack.Questions.Single(q => q.Id == ids[0]);
        Assert.Equal(question.FamilyId, checkedRow.FamilyId);
        Assert.Equal(question.ObjectiveIds, checkedRow.ObjectiveIds);
        Assert.All(rows.Where(r => r.QuestionId != ids[0]), r => { Assert.Equal(EvidenceSource.LearningSubmission, r.Source); Assert.False(r.Answered); });
    }

    [Fact] public async Task A_submitted_mock_records_every_question_once()
    {
        var client = await TestApi.Account(factory);
        var attempt = await TestApi.Start(client);
        var attemptId = TestApi.Id(attempt);
        var done = await TestApi.Complete(client, attempt, pack);
        (await client.PostAsJsonAsync($"/api/me/attempts/{attemptId}/submit", new { revision = done.GetProperty("revision").GetInt32() })).EnsureSuccessStatusCode();

        var rows = await Evidence(attemptId);
        Assert.Equal(TestApi.QuestionIds(attempt).Length, rows.Count);
        Assert.All(rows, r => { Assert.Equal(EvidenceSource.MockSubmission, r.Source); Assert.True(r.FullyCorrect); Assert.Equal(r.Possible, r.Earned); });
    }

    [Fact] public async Task An_expired_mock_records_unanswered_questions_as_incorrect()
    {
        var client = await TestApi.Account(factory);
        var attempt = await TestApi.Start(client);
        var attemptId = TestApi.Id(attempt);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var row = await db.Attempts.SingleAsync(a => a.Id == attemptId);
            row.Deadline = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        var view = await client.GetFromJsonAsync<JsonElement>($"/api/me/attempts/{attemptId}");
        Assert.Equal("completed", view.GetProperty("status").GetString());

        var rows = await Evidence(attemptId);
        Assert.Equal(TestApi.QuestionIds(attempt).Length, rows.Count);
        Assert.All(rows, r => { Assert.Equal(EvidenceSource.MockSubmission, r.Source); Assert.False(r.Answered); Assert.False(r.FullyCorrect); });
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~EvidenceLedgerTests"`
Expected: FAIL. `Assert.Single() Failure: The collection was empty` in the first test; the other two find 0 rows.

- [ ] **Step 4: Implement `EvidenceWriter`**

`apps/api/Services/Learning/EvidenceWriter.cs`:
```csharp
namespace LearnForge.Api.Services.Learning;

public static class EvidenceWriter
{
    public static bool IsAnswered(Answer? answer) => answer is not null && (answer.Selected.Length > 0 || answer.Slots.Count > 0);

    // Mock answers always count (unanswered is incorrect); learning answers count only when the learner answered.
    public static bool Countable(EvidenceSource source, bool answered) => source == EvidenceSource.MockSubmission || answered;

    public static EvidenceRecord Record(Attempt attempt, Question question, Answer? answer, Grade grade, EvidenceSource source, DateTime at) => new()
    {
        UserId = attempt.UserId, PackId = attempt.PackId, ReleaseId = attempt.PackReleaseId, AttemptId = attempt.Id,
        QuestionId = question.Id, FamilyId = question.FamilyId, ObjectiveIds = question.ObjectiveIds, Source = source,
        Answered = IsAnswered(answer), FullyCorrect = grade.FullyCorrect, Earned = grade.Earned, Possible = grade.Possible, At = at
    };
}
```
Add `global using LearnForge.Api.Services.Learning;` to `apps/api/GlobalUsings.cs`.

- [ ] **Step 5: Write ledger rows from `AttemptService` and `ExpiryWorker`**

In `AttemptService.cs`:

1. In `Expire`, replace `Finish(a, true);` with `await Finish(a, true);`.
2. In `Save`, replace
```csharp
        if (request.Check) { feedback[q.Id] = Grader.Score(q, request.Answer); a.FeedbackJson = Json.Write(feedback); }
```
with
```csharp
        if (request.Check)
        {
            var grade = Grader.Score(q, request.Answer);
            feedback[q.Id] = grade;
            a.FeedbackJson = Json.Write(feedback);
            db.Evidence.Add(EvidenceWriter.Record(a, q, request.Answer, grade, EvidenceSource.LearningCheck, Now));
        }
```
3. In `Submit`, replace `Finish(a, false);` with `await Finish(a, false);`.
4. Replace the whole `Finish` method with:
```csharp
    public async Task Finish(Attempt a, bool timedOut)
    {
        var s = Snapshot(a); var answers = Answers(a);
        var grades = s.Questions.Select(q => Grader.Score(q, answers.GetValueOrDefault(q.Id))).ToArray();
        var completedAt = Now;
        a.Status = AttemptStatus.Completed; a.ActiveKey = null; a.CompletedAt = completedAt; a.TimedOut = timedOut;
        a.Earned = grades.Sum(g => g.Earned); a.Possible = grades.Sum(g => g.Possible);
        a.CorrectPercent = grades.Count(g => g.FullyCorrect) * 100m / grades.Length;
        a.Eligible = a.Mode == AssessmentMode.Mock && !timedOut && a.FreshPercent >= s.Readiness.MinimumFreshPercent;
        a.Revision++;
        db.Audit.Add(new() { ActorId = a.UserId, Action = timedOut ? "attempt.expired" : "attempt.submitted", ResourceId = a.Id });
        // Rows are saved with the transition, so the Revision concurrency token covers both.
        // Any question without a row gets one, including checks made before the ledger existed.
        var feedback = Json.Read<Dictionary<string, Grade>>(a.FeedbackJson);
        var recorded = (await db.Evidence.Where(e => e.AttemptId == a.Id).Select(e => e.QuestionId).ToListAsync()).ToHashSet();
        foreach (var (question, grade) in s.Questions.Zip(grades))
        {
            if (recorded.Contains(question.Id)) continue;
            var source = feedback.ContainsKey(question.Id) ? EvidenceSource.LearningCheck
                : a.Mode == AssessmentMode.Mock ? EvidenceSource.MockSubmission : EvidenceSource.LearningSubmission;
            db.Evidence.Add(EvidenceWriter.Record(a, question, answers.GetValueOrDefault(question.Id), grade, source, completedAt));
        }
    }
```

In `ExpiryWorker.cs`, replace `foreach (var attempt in expired) service.Finish(attempt, true);` with:
```csharp
                foreach (var attempt in expired) await service.Finish(attempt, true);
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx`
Expected: all tests PASS, including the 3 ledger tests.

- [ ] **Step 7: Commit**

```bash
git add apps/api tests
git commit -m "feat(api): record evidence when feedback is released" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Learning records: enrollment, course progress, dashboard

**Files:**
- Create: `apps/api/Services/Learning/LearningRecordService.cs`, `apps/api/Contracts/Learning/{CourseProgressDto,ObjectiveMasteryDto,NextStepDto,CourseGoalStatusDto,EnrollmentRequest}.cs`, `tests/LearnForge.Tests/LearningRecordTests.cs`
- Modify: `apps/api/Contracts/Analytics/DashboardDto.cs`, `apps/api/Contracts/Content/CourseCatalogDto.cs`, `apps/api/Services/Analytics/Analytics.cs`, `apps/api/Endpoints/{CatalogEndpoints,LearnerEndpoints}.cs`, `apps/api/Services/Attempts/AttemptService.cs` (constructor and `Start`), `apps/api/Program.cs`, `apps/api/GlobalUsings.cs`, `tests/LearnForge.Tests/ApiTests.cs:202`
- Delete: `apps/api/Contracts/Analytics/{CourseDashboardDto,ObjectiveDashboardDto,RecommendationDto,GradedEvidence}.cs`

**Interfaces:**
- Consumes: `MasteryEvaluator` (Task 2), `NextStepPlanner` (Task 3), `ReleaseCache` (Task 5), entities (Task 6), `EvidenceWriter.Countable` (Task 7).
- Produces:
  - `LearningRecordService` (scoped) with `Task<ReleaseView> Release(string packId)`, `Task Touch(string user, string packId)` (the caller saves), `Task SetEnrollment(string user, string packId, EnrollmentStatus status)`, `Task CompleteLesson(string user, string packId, string lessonId)`, `Task<CourseProgressDto> CourseProgress(string user, string packId)`, `Task<DashboardDto> Dashboard(string user)` and `Task<MasteryEvidence[]> Evidence(string user, string packId)`.
  - DTOs `CourseProgressDto(string PackId, string Title, string Version, EnrollmentStatus? Enrollment, int LessonCount, string[] CompletedLessons, string[] RevisedLessons, ObjectiveMasteryDto[] Objectives, NextStepDto[] NextSteps, CourseGoalStatusDto Goal, DateTime? LastActivityAt)`, `ObjectiveMasteryDto(string Id, string Title, string[] Prerequisites, MasteryState State, int Correct, int Considered, int Independent, DateTime? LastEvidenceAt, bool ReviewDue, string[] LessonIds)`, `NextStepDto(NextStepKind Kind, NextStepReason Reason, string? ObjectiveId, string? ObjectiveTitle, string? LessonId, string? LessonTitle, string? BlueprintId)`, `CourseGoalStatusDto(CourseGoal Goal, bool Met, int ProficientObjectives, int ObjectiveCount, int CompletedLessons, int LessonCount, ReadinessResult? Readiness)`, `EnrollmentRequest(EnrollmentStatus Status)`.
  - `DashboardDto(CourseProgressDto[] Courses, AttemptSummaryDto[] RecentAttempts, int CompletedAttempts, int ActiveAttempts, int CompletedLessons)`.
  - `CourseCatalogDto(string Id, string Title, string Description, string Version, string License, Objective[] Objectives, Lesson[] Lessons, Blueprint[] Blueprints, SourceReference[] Sources, ReadinessPolicy Readiness, CourseGoal Goal, int QuestionCount)`.
  - Endpoints `GET /api/me/courses/{id}` and `PUT /api/me/enrollments/{id}`.

- [ ] **Step 1: Write the failing tests**

Create `tests/LearnForge.Tests/LearningRecordTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Core;
using Xunit;

namespace LearnForge.Tests;

public class LearningRecordTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Pack pack = TestApi.LoadPack("reasoning-foundations");

    private static Task<JsonElement> Progress(HttpClient client, string packId) =>
        client.GetFromJsonAsync<JsonElement>($"/api/me/courses/{packId}");

    private static JsonElement Objective(JsonElement progress, string id) =>
        progress.GetProperty("objectives").EnumerateArray().Single(o => o.GetProperty("id").GetString() == id);

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    private async Task<HttpClient> Publish(Pack release, HttpClient? publisher = null)
    {
        publisher ??= await TestApi.Publisher(factory);
        (await publisher.PostAsJsonAsync("/api/authoring/publish", release)).EnsureSuccessStatusCode();
        return publisher;
    }

    private static string NewId(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..12];

    [Fact] public async Task Learning_mode_answers_build_mastery_evidence()
    {
        var client = await TestApi.Account(factory);
        Assert.Equal("notStarted", Objective(await Progress(client, pack.Id), "sets").GetProperty("state").GetString());
        var attempt = await TestApi.Start(client, "learn", blueprintId: "full");
        var revision = 0;
        foreach (var id in TestApi.QuestionIds(attempt)) revision = await TestApi.Save(client, attempt, revision, id, TestApi.Correct(pack, id), check: true);

        var progress = await Progress(client, pack.Id);
        var touched = progress.GetProperty("objectives").EnumerateArray().Where(o => o.GetProperty("considered").GetInt32() > 0).ToArray();
        Assert.NotEmpty(touched);
        Assert.All(touched, o => Assert.Equal(o.GetProperty("considered").GetInt32(), o.GetProperty("correct").GetInt32()));
        Assert.Equal("active", progress.GetProperty("enrollment").GetString());
    }

    [Fact] public async Task Skipped_learning_questions_do_not_change_mastery()
    {
        var client = await TestApi.Account(factory);
        var attempt = await TestApi.Start(client, "learn");
        await TestApi.Complete(client, attempt, pack, correct: false);
        var progress = await Progress(client, pack.Id);
        Assert.All(progress.GetProperty("objectives").EnumerateArray(), o => Assert.Equal("notStarted", o.GetProperty("state").GetString()));
    }

    [Fact] public async Task Lesson_progress_survives_new_releases_and_flags_revised_lessons()
    {
        var v1 = pack with { Id = NewId("carry") };
        var publisher = await Publish(v1);
        foreach (var lesson in new[] { "sets-intro", "logic-intro" })
            Assert.Equal(HttpStatusCode.NoContent, (await publisher.PutAsync($"/api/me/courses/{v1.Id}/lessons/{lesson}", null)).StatusCode);
        var v2 = v1 with { Version = "1.1.0", Lessons = v1.Lessons.Select(l => l.Id == "logic-intro" ? l with { Summary = l.Summary + " Updated." } : l).ToArray() };
        await Publish(v2, publisher);

        var progress = await Progress(publisher, v1.Id);
        Assert.Equal("1.1.0", progress.GetProperty("version").GetString());
        Assert.Equal(new[] { "logic-intro", "sets-intro" }, Strings(progress.GetProperty("completedLessons")).Order());
        Assert.Equal(new[] { "logic-intro" }, Strings(progress.GetProperty("revisedLessons")));

        await publisher.PutAsync($"/api/me/courses/{v1.Id}/lessons/logic-intro", null);
        Assert.Empty(Strings((await Progress(publisher, v1.Id)).GetProperty("revisedLessons")));
    }

    [Fact] public async Task Removed_lessons_leave_progress_without_errors()
    {
        var extra = pack.Lessons[0] with { Id = "sets-extra", Title = "More sets" };
        var v1 = pack with { Id = NewId("removed"), Lessons = [.. pack.Lessons, extra] };
        var publisher = await Publish(v1);
        await publisher.PutAsync($"/api/me/courses/{v1.Id}/lessons/sets-extra", null);
        await publisher.PutAsync($"/api/me/courses/{v1.Id}/lessons/sets-intro", null);
        await Publish(v1 with { Version = "1.1.0", Lessons = pack.Lessons }, publisher);

        var progress = await Progress(publisher, v1.Id);
        Assert.Equal(new[] { "sets-intro" }, Strings(progress.GetProperty("completedLessons")));
        Assert.Equal(3, progress.GetProperty("lessonCount").GetInt32());
    }

    [Fact] public async Task Enrollment_is_automatic_and_archiving_hides_but_keeps_the_course()
    {
        var client = await TestApi.Account(factory);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/me/dashboard")).GetProperty("courses").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, (await Progress(client, pack.Id)).GetProperty("enrollment").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/me/courses/{pack.Id}/lessons/sets-intro", null)).StatusCode);
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/me/dashboard");
        Assert.Equal(pack.Id, dashboard.GetProperty("courses").EnumerateArray().Single().GetProperty("packId").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/me/enrollments/{pack.Id}", new { status = "archived" })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/me/dashboard")).GetProperty("courses").EnumerateArray());
        var archived = await Progress(client, pack.Id);
        Assert.Equal("archived", archived.GetProperty("enrollment").GetString());
        Assert.Single(archived.GetProperty("completedLessons").EnumerateArray());

        await TestApi.Start(client, "learn");
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/me/dashboard")).GetProperty("courses").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/me/enrollments/missing-pack", new { status = "active" })).StatusCode);
    }

    [Fact] public async Task Course_progress_offers_a_next_step_and_readiness_goal_status()
    {
        var client = await TestApi.Account(factory);
        var progress = await Progress(client, pack.Id);
        var step = progress.GetProperty("nextSteps").EnumerateArray().First();
        Assert.Equal("readLesson", step.GetProperty("kind").GetString());
        Assert.Equal("startObjective", step.GetProperty("reason").GetString());
        Assert.Equal("sets-intro", step.GetProperty("lessonId").GetString());
        Assert.Equal("Think in sets", step.GetProperty("lessonTitle").GetString());
        var goal = progress.GetProperty("goal");
        Assert.Equal("readiness", goal.GetProperty("goal").GetString());
        Assert.False(goal.GetProperty("met").GetBoolean());
        Assert.Equal(JsonValueKind.Object, goal.GetProperty("readiness").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/me/courses/missing-pack")).StatusCode);
    }

    [Fact] public async Task Mastery_goal_packs_report_objectives_instead_of_readiness()
    {
        var mastery = pack with { Id = NewId("mastery"), Goal = CourseGoal.Mastery, Readiness = null };
        var publisher = await Publish(mastery);
        var goal = (await Progress(publisher, mastery.Id)).GetProperty("goal");
        Assert.Equal("mastery", goal.GetProperty("goal").GetString());
        Assert.Equal(JsonValueKind.Null, goal.GetProperty("readiness").ValueKind);
        Assert.Equal(3, goal.GetProperty("objectiveCount").GetInt32());
    }

    [Fact] public async Task Completion_goal_is_met_when_every_lesson_is_read()
    {
        var completion = pack with { Id = NewId("complete"), Goal = CourseGoal.Completion, Readiness = null };
        var publisher = await Publish(completion);
        foreach (var lesson in completion.Lessons.Take(2)) await publisher.PutAsync($"/api/me/courses/{completion.Id}/lessons/{lesson.Id}", null);
        Assert.False((await Progress(publisher, completion.Id)).GetProperty("goal").GetProperty("met").GetBoolean());
        await publisher.PutAsync($"/api/me/courses/{completion.Id}/lessons/{completion.Lessons[2].Id}", null);
        Assert.True((await Progress(publisher, completion.Id)).GetProperty("goal").GetProperty("met").GetBoolean());
    }

    [Fact] public async Task The_public_catalog_contains_no_personal_progress()
    {
        var client = await TestApi.Account(factory);
        await client.PutAsync($"/api/me/courses/{pack.Id}/lessons/sets-intro", null);
        var course = await client.GetFromJsonAsync<JsonElement>($"/api/catalog/{pack.Id}");
        Assert.False(course.TryGetProperty("completedLessons", out _));
        Assert.Equal("readiness", course.GetProperty("goal").GetString());
    }
}
```

In `tests/LearnForge.Tests/ApiTests.cs`, replace the `return` line inside the local function `Ready()` (line 202) with:
```csharp
            return dashboard.GetProperty("courses").EnumerateArray().Single(c => c.GetProperty("packId").GetString() == pack.Id).GetProperty("goal").GetProperty("readiness").GetProperty("ready").GetBoolean();
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearningRecordTests|FullyQualifiedName~ApiTests"`
Expected: FAIL. `/api/me/courses/{id}` returns 404 (not mapped), the dashboard has no `packId`, and the catalog still returns `completedLessons`.

- [ ] **Step 3: Add the contracts**

`apps/api/Contracts/Learning/CourseProgressDto.cs`:
```csharp
namespace LearnForge.Api.Contracts.Learning;

public sealed record CourseProgressDto(
    string PackId,
    string Title,
    string Version,
    EnrollmentStatus? Enrollment,
    int LessonCount,
    string[] CompletedLessons,
    string[] RevisedLessons,
    ObjectiveMasteryDto[] Objectives,
    NextStepDto[] NextSteps,
    CourseGoalStatusDto Goal,
    DateTime? LastActivityAt);
```

`apps/api/Contracts/Learning/ObjectiveMasteryDto.cs`:
```csharp
namespace LearnForge.Api.Contracts.Learning;

public sealed record ObjectiveMasteryDto(
    string Id,
    string Title,
    string[] Prerequisites,
    MasteryState State,
    int Correct,
    int Considered,
    int Independent,
    DateTime? LastEvidenceAt,
    bool ReviewDue,
    string[] LessonIds);
```

`apps/api/Contracts/Learning/NextStepDto.cs`:
```csharp
namespace LearnForge.Api.Contracts.Learning;

// Reasons travel as enums so the client can phrase (and later localize) them.
public sealed record NextStepDto(
    NextStepKind Kind,
    NextStepReason Reason,
    string? ObjectiveId,
    string? ObjectiveTitle,
    string? LessonId,
    string? LessonTitle,
    string? BlueprintId);
```

`apps/api/Contracts/Learning/CourseGoalStatusDto.cs`:
```csharp
namespace LearnForge.Api.Contracts.Learning;

public sealed record CourseGoalStatusDto(
    CourseGoal Goal,
    bool Met,
    int ProficientObjectives,
    int ObjectiveCount,
    int CompletedLessons,
    int LessonCount,
    ReadinessResult? Readiness);
```

`apps/api/Contracts/Learning/EnrollmentRequest.cs`:
```csharp
namespace LearnForge.Api.Contracts.Learning;

public sealed record EnrollmentRequest(EnrollmentStatus Status);
```

Replace `apps/api/Contracts/Analytics/DashboardDto.cs`:
```csharp
namespace LearnForge.Api.Contracts.Analytics;

public sealed record DashboardDto(
    CourseProgressDto[] Courses,
    AttemptSummaryDto[] RecentAttempts,
    int CompletedAttempts,
    int ActiveAttempts,
    int CompletedLessons);
```

Replace `apps/api/Contracts/Content/CourseCatalogDto.cs`:
```csharp
namespace LearnForge.Api.Contracts.Content;

// Public course data only; personal progress lives under /api/me/courses/{id}.
public sealed record CourseCatalogDto(string Id, string Title, string Description, string Version, string License,
    Objective[] Objectives, Lesson[] Lessons, Blueprint[] Blueprints, SourceReference[] Sources,
    ReadinessPolicy Readiness, CourseGoal Goal, int QuestionCount);
```

Delete `apps/api/Contracts/Analytics/CourseDashboardDto.cs`, `ObjectiveDashboardDto.cs`, `RecommendationDto.cs` and `GradedEvidence.cs`. Add `global using LearnForge.Api.Contracts.Learning;` to `apps/api/GlobalUsings.cs`.

- [ ] **Step 4: Implement `LearningRecordService`**

`apps/api/Services/Learning/LearningRecordService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Analytics.Analytics;

namespace LearnForge.Api.Services.Learning;

// Derives learner progress on read from the evidence ledger, lesson progress and enrollments.
public sealed class LearningRecordService(AppDb db, ReleaseCache releases, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<ReleaseView> Release(string packId) =>
        await releases.Latest(db, packId) ?? throw new DomainError(404, "Course not found.");

    // Records learning activity and reactivates an archived course. The caller saves.
    public async Task Touch(string user, string packId)
    {
        var enrollment = await db.Enrollments.FindAsync(user, packId);
        if (enrollment is null) db.Enrollments.Add(new() { UserId = user, PackId = packId, EnrolledAt = Now, LastActivityAt = Now });
        else { enrollment.Status = EnrollmentStatus.Active; enrollment.LastActivityAt = Now; }
    }

    public async Task SetEnrollment(string user, string packId, EnrollmentStatus status)
    {
        await Release(packId);
        var enrollment = await db.Enrollments.FindAsync(user, packId);
        if (enrollment is null) db.Enrollments.Add(new() { UserId = user, PackId = packId, Status = status, EnrolledAt = Now, LastActivityAt = Now });
        else enrollment.Status = status;
        await db.SaveChangesAsync();
    }

    public async Task CompleteLesson(string user, string packId, string lessonId)
    {
        var release = await Release(packId);
        if (!release.LessonHashes.TryGetValue(lessonId, out var hash)) throw new DomainError(404, "Lesson not found.");
        var progress = await db.LessonProgress.FindAsync(user, packId, lessonId);
        if (progress is null) db.LessonProgress.Add(new() { UserId = user, PackId = packId, LessonId = lessonId, CompletedAt = Now, ContentHash = hash });
        else progress.ContentHash = hash; // Marking a revised lesson again clears the "updated" flag.
        await Touch(user, packId);
        await db.SaveChangesAsync();
    }

    public async Task<CourseProgressDto> CourseProgress(string user, string packId) =>
        await Build(user, await Release(packId), await db.Enrollments.AsNoTracking().SingleOrDefaultAsync(e => e.UserId == user && e.PackId == packId));

    public async Task<DashboardDto> Dashboard(string user)
    {
        var enrollments = await db.Enrollments.AsNoTracking().Where(e => e.UserId == user && e.Status == EnrollmentStatus.Active)
            .OrderByDescending(e => e.LastActivityAt).ToListAsync();
        var courses = new List<CourseProgressDto>();
        foreach (var enrollment in enrollments)
            if (await releases.Latest(db, enrollment.PackId) is { } release) courses.Add(await Build(user, release, enrollment));
        var recent = await db.Attempts.AsNoTracking().Where(a => a.UserId == user).OrderByDescending(a => a.StartedAt).Take(5).ToListAsync();
        return new DashboardDto(courses.ToArray(), recent.Select(Summary).ToArray(),
            await db.Attempts.CountAsync(a => a.UserId == user && a.Status == AttemptStatus.Completed),
            await db.Attempts.CountAsync(a => a.UserId == user && a.Status == AttemptStatus.InProgress),
            await db.LessonProgress.CountAsync(p => p.UserId == user));
    }

    public async Task<MasteryEvidence[]> Evidence(string user, string packId) =>
        (await db.Evidence.AsNoTracking().Where(e => e.UserId == user && e.PackId == packId)
            .Select(e => new { e.Id, e.FamilyId, e.ObjectiveIds, e.Source, e.Answered, e.FullyCorrect, e.At }).ToListAsync())
        .Where(e => EvidenceWriter.Countable(e.Source, e.Answered))
        .Select(e => new MasteryEvidence(e.Id, e.FamilyId, e.ObjectiveIds, e.FullyCorrect, e.Source == EvidenceSource.MockSubmission, e.At))
        .ToArray();

    private async Task<CourseProgressDto> Build(string user, ReleaseView release, Enrollment? enrollment)
    {
        var pack = release.Pack;
        // Progress is keyed by stable lesson IDs; lessons removed from the current release are ignored.
        var progress = (await db.LessonProgress.AsNoTracking().Where(p => p.UserId == user && p.PackId == pack.Id).ToListAsync())
            .Where(p => release.LessonHashes.ContainsKey(p.LessonId)).ToArray();
        var completed = progress.Select(p => p.LessonId).ToHashSet();
        var revised = progress.Where(p => p.ContentHash is not null && p.ContentHash != release.LessonHashes[p.LessonId]).Select(p => p.LessonId).ToArray();
        var mastery = MasteryEvaluator.Evaluate(pack.Objectives, await Evidence(user, pack.Id), pack.Mastery ?? new(), Now);
        ReadinessResult? readiness = null;
        if (pack.Goal == CourseGoal.Readiness)
        {
            // Readiness stays release-scoped and mock-only.
            var mocks = await db.Attempts.AsNoTracking()
                .Where(a => a.UserId == user && a.PackReleaseId == release.ReleaseId && a.Mode == AssessmentMode.Mock && a.Status == AttemptStatus.Completed)
                .Select(a => new { a.Id, a.Size, a.CompletedAt, a.CorrectPercent, a.Eligible }).ToListAsync();
            readiness = ReadinessEvaluator.Evaluate(mocks.Select(a => new ReadinessEvidence(a.Id, a.Size, a.CompletedAt!.Value, a.CorrectPercent, a.Eligible)), pack.Readiness ?? new(), Now);
        }
        var proficient = mastery.Count(m => m.State == MasteryState.Proficient);
        var met = pack.Goal switch
        {
            CourseGoal.Mastery => proficient == pack.Objectives.Length,
            CourseGoal.Completion => completed.Count == pack.Lessons.Length,
            _ => readiness?.Ready == true,
        };
        var objectives = pack.Objectives.ToDictionary(o => o.Id);
        var lessons = pack.Lessons.ToDictionary(l => l.Id);
        return new CourseProgressDto(pack.Id, pack.Title, pack.Version, enrollment?.Status, pack.Lessons.Length,
            completed.ToArray(), revised,
            mastery.Select(m => new ObjectiveMasteryDto(m.ObjectiveId, objectives[m.ObjectiveId].Title, objectives[m.ObjectiveId].Prerequisites,
                m.State, m.Correct, m.Considered, m.Independent, m.LastEvidenceAt, m.ReviewDue,
                pack.Lessons.Where(l => l.ObjectiveIds.Contains(m.ObjectiveId)).Select(l => l.Id).ToArray())).ToArray(),
            NextStepPlanner.Plan(pack, mastery, completed, readiness).Select(s => new NextStepDto(s.Kind, s.Reason,
                s.ObjectiveId, s.ObjectiveId is null ? null : objectives[s.ObjectiveId].Title,
                s.LessonId, s.LessonId is null ? null : lessons[s.LessonId].Title, s.BlueprintId)).ToArray(),
            new CourseGoalStatusDto(pack.Goal, met, proficient, pack.Objectives.Length, completed.Count, pack.Lessons.Length, readiness),
            enrollment?.LastActivityAt);
    }
}
```
The `using static` line avoids a name clash: from `LearnForge.Api.Services.Learning`, the bare name `Analytics` resolves to the sibling namespace, not the class.

Replace `apps/api/Services/Analytics/Analytics.cs` with only the summary mapper:
```csharp
namespace LearnForge.Api.Services.Analytics;

public static class Analytics
{
    public static AttemptSummaryDto Summary(Attempt a)
    {
        var snapshot = AttemptService.Snapshot(a);
        return new(a.Id, a.PackId, snapshot.Title, snapshot.Version, a.Size, a.Mode, a.Status,
            a.StartedAt, a.CompletedAt, a.Deadline, a.CorrectPercent, a.Eligible, a.TimedOut,
            a.FreshPercent, a.Focus, a.Possible == 0 ? 0 : a.Earned * 100 / a.Possible, snapshot.Questions.Length);
    }
}
```

In `Program.cs`, after `builder.Services.AddSingleton<ReleaseCache>();`, add:
```csharp
builder.Services.AddScoped<LearningRecordService>();
```

- [ ] **Step 5: Wire the endpoints and auto-enrollment**

In `CatalogEndpoints.cs`, replace the detail endpoint:
```csharp
            app.MapGet("/api/catalog/{id}", async (string id, AppDb db, ReleaseCache releases) =>
            {
                var release = await releases.Latest(db, id);
                if (release is null) return Results.NotFound();
                var p = release.Pack;
                return Results.Ok(new CourseCatalogDto(p.Id, p.Title, p.Description, p.Version, p.License, p.Objectives,
                    p.Lessons, p.Blueprints, p.Sources, p.Readiness ?? new(), p.Goal, p.Questions.Length));
            });
```
Then remove `using System.Security.Claims;` and the `using static ...AccountIdentity;` line from that file.

In `LearnerEndpoints.cs`, replace the lesson PUT, dashboard and export handlers, and add the two new endpoints:
```csharp
            me.MapPut("/courses/{id}/lessons/{lessonId}", async (string id, string lessonId, ClaimsPrincipal user, LearningRecordService learning) =>
            {
                await learning.CompleteLesson(UserId(user), id, lessonId);
                return Results.NoContent();
            });
            me.MapGet("/courses/{id}", (string id, ClaimsPrincipal user, LearningRecordService learning) => learning.CourseProgress(UserId(user), id));
            me.MapPut("/enrollments/{id}", async (string id, EnrollmentRequest request, ClaimsPrincipal user, LearningRecordService learning) =>
            {
                await learning.SetEnrollment(UserId(user), id, request.Status);
                return Results.NoContent();
            });
            me.MapGet("/dashboard", async (ClaimsPrincipal user, AppDb db, AttemptService service, LearningRecordService learning) =>
            {
                foreach (var a in await db.Attempts.Where(a => a.UserId == UserId(user) && a.Status == AttemptStatus.InProgress).ToListAsync()) await service.Expire(a);
                return await learning.Dashboard(UserId(user));
            });
            me.MapGet("/export", async (ClaimsPrincipal user, AppDb db, AttemptService service, LearningRecordService learning) =>
            {
                var attempts = await db.Attempts.Where(a => a.UserId == UserId(user)).ToListAsync();
                var export = new LearnerExportDto(service.Now, await learning.Dashboard(UserId(user)), attempts.Select(service.View).ToArray());
                return Results.File(System.Text.Encoding.UTF8.GetBytes(Json.Write(export)), "application/json", "learnforge-history.json");
            });
```

In `AttemptService.cs`, change the declaration to
```csharp
public sealed class AttemptService(AppDb db, TimeProvider clock, ReleaseCache releases, LearningRecordService learning)
```
and in `Start`, insert `await learning.Touch(user, pack.Id);` directly before the final `await db.SaveChangesAsync();`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx`
Expected: all tests PASS, including 9 `LearningRecordTests` and the updated readiness test.

- [ ] **Step 7: Commit**

```bash
git add apps/api tests
git commit -m "feat(api): enrollment, cross-release lesson progress, mastery and next steps" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Session selection from the ledger and objective practice

**Files:**
- Modify: `src/LearnForge.Core/Domain/Enums/PracticeFocus.cs`, `apps/api/Contracts/Attempts/StartRequest.cs`, `apps/api/Services/Attempts/AttemptService.cs` (`Start` and new private members)
- Delete: `apps/api/Contracts/Analytics/ObjectiveGradeEvidence.cs`
- Test: `tests/LearnForge.Tests/PracticeFocusTests.cs`

**Interfaces:**
- Consumes: `AppDb.Evidence`, `Attempt.FocusObjectiveId` (Task 6); `EvidenceWriter.Countable` (Task 7); `MasteryEvaluator` (Task 2).
- Produces: `enum PracticeFocus { Mistakes, Weak, Objective }`; `StartRequest(string PackId, string BlueprintId, AssessmentMode Mode, string RequestId, PracticeFocus? Focus = null, string? ObjectiveId = null)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/LearnForge.Tests/PracticeFocusTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~PracticeFocusTests"`
Expected: FAIL. `focus = "objective"` is not a `PracticeFocus` value, so those requests return 400 and `TestApi.Start` throws `Start failed with 400`.

- [ ] **Step 3: Extend the contract**

`src/LearnForge.Core/Domain/Enums/PracticeFocus.cs`:
```csharp
namespace LearnForge.Core;

public enum PracticeFocus { Mistakes, Weak, Objective }
```

`apps/api/Contracts/Attempts/StartRequest.cs`:
```csharp
namespace LearnForge.Api.Contracts.Attempts;

public sealed record StartRequest(string PackId, string BlueprintId, AssessmentMode Mode, string RequestId,
    PracticeFocus? Focus = null, string? ObjectiveId = null);
```

Delete `apps/api/Contracts/Analytics/ObjectiveGradeEvidence.cs`.

- [ ] **Step 4: Select sessions from the ledger**

In `AttemptService.cs`, add `using System.Security.Cryptography;` and `using System.Text;` at the top, then replace the whole `Start` method with:
```csharp
    public async Task<Attempt> Start(string user, StartRequest request)
    {
        if (!Guid.TryParse(request.RequestId, out _)) throw new DomainError(400, "Invalid session settings.");
        if (request.Mode == AssessmentMode.Mock && request.Focus is not null) throw new DomainError(400, "Focused practice uses learning mode.");
        if ((request.Focus == PracticeFocus.Objective) != (request.ObjectiveId is not null)) throw new DomainError(400, "Objective practice needs exactly one objective.");
        var replay = await db.Attempts.SingleOrDefaultAsync(a => a.UserId == user && a.StartKey == request.RequestId);
        if (replay is not null)
        {
            if (replay.PackId != request.PackId || replay.Mode != request.Mode || replay.Focus != request.Focus
                || replay.FocusObjectiveId != request.ObjectiveId || Snapshot(replay).Blueprint.Id != request.BlueprintId)
                throw new DomainError(409, "This request ID already belongs to another session.");
            await Expire(replay);
            return replay;
        }
        var release = await releases.Latest(db, request.PackId) ?? throw new DomainError(404, "Course not found.");
        var active = await db.Attempts.SingleOrDefaultAsync(a => a.ActiveKey == user + ":" + request.PackId);
        if (active is not null)
        {
            await Expire(active);
            if (active.Status == AttemptStatus.InProgress) throw new DomainError(409, $"Resume or finish your existing session: {active.Id}");
        }
        var pack = release.Pack;
        var blueprint = pack.Blueprints.FirstOrDefault(b => b.Id == request.BlueprintId) ?? throw new DomainError(400, "Unknown blueprint.");
        // The ledger holds every question of every finished attempt, so it is also the exposure history.
        var history = await db.Evidence.AsNoTracking().Where(e => e.UserId == user && e.PackId == request.PackId)
            .Select(e => new EvidenceRow(e.Id, e.QuestionId, e.FamilyId, e.ObjectiveIds, e.Source, e.Answered, e.FullyCorrect, e.At)).ToListAsync();
        var seen = history.Select(e => e.FamilyId).ToHashSet();
        var chosen = request.Focus is { } focus
            ? Focused(pack, blueprint, request, focus, history, seen)
            : ExamComposer.Compose(pack, blueprint, seen, request.RequestId);
        chosen = chosen.OrderBy(q => q.ScenarioId is null ? 0 : 1).ThenBy(q => q.ScenarioId).Select(Shuffle).ToArray();
        var snapshot = new AttemptSnapshot(pack.Title, pack.Version, pack.Objectives, pack.Scenarios, blueprint, chosen, pack.Readiness ?? new());
        var attempt = new Attempt
        {
            UserId = user, PackReleaseId = release.ReleaseId, PackId = pack.Id, StartKey = request.RequestId,
            ActiveKey = user + ":" + pack.Id, Mode = request.Mode, Size = blueprint.Size, Focus = request.Focus,
            FocusObjectiveId = request.ObjectiveId, StartedAt = Now, Deadline = Now.AddMinutes(blueprint.Minutes),
            SnapshotJson = Json.Write(snapshot), FreshPercent = chosen.Count(q => !seen.Contains(q.FamilyId)) * 100m / chosen.Length
        };
        db.Attempts.Add(attempt);
        db.Audit.Add(new() { ActorId = user, Action = "attempt.started", ResourceId = attempt.Id });
        await learning.Touch(user, pack.Id);
        await db.SaveChangesAsync();
        return attempt;
    }

    private sealed record EvidenceRow(long Id, string QuestionId, string FamilyId, string[] ObjectiveIds,
        EvidenceSource Source, bool Answered, bool FullyCorrect, DateTime At);

    private Question[] Focused(Pack pack, Blueprint blueprint, StartRequest request, PracticeFocus focus, List<EvidenceRow> history, HashSet<string> seen)
    {
        var countable = history.Where(e => EvidenceWriter.Countable(e.Source, e.Answered)).ToArray();
        // A question counts as missed when its most recent countable answer was not fully correct.
        var missed = countable.GroupBy(e => e.QuestionId)
            .Where(g => !g.OrderByDescending(e => e.At).ThenByDescending(e => e.Id).First().FullyCorrect)
            .Select(g => g.Key).ToHashSet();
        var standalone = pack.Questions.Where(q => q.ScenarioId == null);
        IEnumerable<Question> pool;
        Func<Question, int> tier;
        switch (focus)
        {
            case PracticeFocus.Mistakes:
                pool = standalone.Where(q => missed.Contains(q.Id));
                tier = _ => 0;
                break;
            case PracticeFocus.Weak:
                var mastery = MasteryEvaluator.Evaluate(pack.Objectives,
                    countable.Select(e => new MasteryEvidence(e.Id, e.FamilyId, e.ObjectiveIds, e.FullyCorrect, e.Source == EvidenceSource.MockSubmission, e.At)),
                    pack.Mastery ?? new(), Now);
                var weak = mastery.Where(m => m.Considered > 0).OrderBy(m => (decimal)m.Correct / m.Considered)
                    .ThenBy(m => m.ObjectiveId, StringComparer.Ordinal).Take(2).Select(m => m.ObjectiveId).ToHashSet();
                pool = standalone.Where(q => q.ObjectiveIds.Any(weak.Contains));
                tier = q => seen.Contains(q.FamilyId) ? 1 : 0;
                break;
            default:
                if (!pack.Objectives.Any(o => o.Id == request.ObjectiveId)) throw new DomainError(400, "Unknown objective.");
                pool = standalone.Where(q => q.ObjectiveIds.Contains(request.ObjectiveId!));
                // Unseen families first, then earlier mistakes, then everything else.
                tier = q => !seen.Contains(q.FamilyId) ? 0 : missed.Contains(q.Id) ? 1 : 2;
                break;
        }
        var chosen = pool.OrderBy(tier).ThenBy(q => SeededOrder(request.RequestId, q.Id), StringComparer.Ordinal).Take(blueprint.Count).ToArray();
        if (chosen.Length == 0) throw new DomainError(409, focus == PracticeFocus.Objective
            ? "This objective has no standalone practice questions yet; its questions are part of case studies. Try a balanced session."
            : "Complete an assessment first to build targeted practice evidence.");
        return chosen;
    }

    // Deterministic per request, like ExamComposer, so a replayed start selects the same questions.
    private static string SeededOrder(string seed, string id) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed + id)));
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx`
Expected: all tests PASS, including 5 `PracticeFocusTests`. The existing readiness test still passes: 5 consecutive short mocks with 100% fresh questions, because the ledger now supplies seen families.

- [ ] **Step 6: Commit**

```bash
git add src apps/api tests
git commit -m "feat(api): select focused practice from the evidence ledger and add objective practice" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Backfill the ledger and enrollments during migration

**Files:**
- Create: `apps/api/Services/Learning/EvidenceBackfill.cs`, `tests/LearnForge.Tests/BackfillTests.cs`
- Modify: `apps/api/Startup/DatabaseInitializer.cs`

**Interfaces:**
- Consumes: `EvidenceWriter.Record` (Task 7), `AttemptService.Snapshot` and `Answers` (existing).
- Produces: `static Task<int> EvidenceBackfill.RunAsync(AppDb db, CancellationToken cancellationToken = default)`, which returns the number of attempts backfilled.

- [ ] **Step 1: Write the failing tests**

Create `tests/LearnForge.Tests/BackfillTests.cs`:
```csharp
using LearnForge.Api;
using LearnForge.Api.Services.Learning;
using LearnForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class BackfillTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Pack pack = TestApi.LoadPack("reasoning-foundations");

    // Completes a mock, then removes its ledger rows and the enrollment, as if it predated the ledger.
    private async Task<(string AttemptId, string UserId, int Questions)> LegacyAttempt()
    {
        var client = await TestApi.Account(factory);
        var attempt = await TestApi.Start(client);
        await TestApi.Complete(client, attempt, pack);
        var attemptId = TestApi.Id(attempt);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var userId = await db.Attempts.Where(a => a.Id == attemptId).Select(a => a.UserId).SingleAsync();
        await db.Evidence.Where(e => e.AttemptId == attemptId).ExecuteDeleteAsync();
        await db.Enrollments.Where(e => e.UserId == userId).ExecuteDeleteAsync();
        return (attemptId, userId, TestApi.QuestionIds(attempt).Length);
    }

    [Fact] public async Task Backfill_rebuilds_missing_evidence_and_enrollments_idempotently()
    {
        var (attemptId, userId, questions) = await LegacyAttempt();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        Assert.Equal(1, await EvidenceBackfill.RunAsync(db));
        Assert.Equal(0, await EvidenceBackfill.RunAsync(db));

        var rows = await db.Evidence.Where(e => e.AttemptId == attemptId).ToListAsync();
        Assert.Equal(questions, rows.Count);
        Assert.All(rows, r => { Assert.Equal(EvidenceSource.MockSubmission, r.Source); Assert.True(r.FullyCorrect); });
        Assert.True(await db.Enrollments.AnyAsync(e => e.UserId == userId && e.PackId == pack.Id));
    }

    [Fact] public async Task Concurrent_backfills_do_not_fail()
    {
        var (attemptId, _, questions) = await LegacyAttempt();
        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            await EvidenceBackfill.RunAsync(scope.ServiceProvider.GetRequiredService<AppDb>());
        }));
        using var check = factory.Services.CreateScope();
        Assert.Equal(questions, await check.ServiceProvider.GetRequiredService<AppDb>().Evidence.CountAsync(e => e.AttemptId == attemptId));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~BackfillTests"`
Expected: build FAILS with `CS0103: The name 'EvidenceBackfill' does not exist`.

- [ ] **Step 3: Implement the backfill**

`apps/api/Services/Learning/EvidenceBackfill.cs`:
```csharp
using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api.Services.Learning;

// Rebuilds ledger rows and enrollments for data created before the ledger existed. It runs in the
// migration step and is safe to repeat or to run on several instances at once. In-progress attempts
// need nothing: AttemptService.Finish records any question without a row when the attempt completes.
public static class EvidenceBackfill
{
    public static async Task<int> RunAsync(AppDb db, CancellationToken cancellationToken = default)
    {
        var pending = await db.Attempts.Where(a => a.Status == AttemptStatus.Completed && !db.Evidence.Any(e => e.AttemptId == a.Id))
            .Select(a => a.Id).ToListAsync(cancellationToken);
        var written = 0;
        foreach (var id in pending)
        {
            db.ChangeTracker.Clear();
            var attempt = await db.Attempts.AsNoTracking().SingleAsync(a => a.Id == id, cancellationToken);
            var snapshot = AttemptService.Snapshot(attempt);
            var answers = AttemptService.Answers(attempt);
            var feedback = Json.Read<Dictionary<string, Grade>>(attempt.FeedbackJson);
            foreach (var question in snapshot.Questions)
            {
                var answer = answers.GetValueOrDefault(question.Id);
                var source = feedback.ContainsKey(question.Id) ? EvidenceSource.LearningCheck
                    : attempt.Mode == AssessmentMode.Mock ? EvidenceSource.MockSubmission : EvidenceSource.LearningSubmission;
                db.Evidence.Add(EvidenceWriter.Record(attempt, question, answer, Grader.Score(question, answer), source, attempt.CompletedAt ?? attempt.StartedAt));
            }
            try { await db.SaveChangesAsync(cancellationToken); written++; }
            catch (DbUpdateException) { /* Another instance backfilled this attempt first. */ }
        }
        db.ChangeTracker.Clear();
        var active = (await db.Attempts.Select(a => new { a.UserId, a.PackId }).Distinct().ToListAsync(cancellationToken))
            .Concat(await db.LessonProgress.Select(p => new { p.UserId, p.PackId }).Distinct().ToListAsync(cancellationToken))
            .Distinct();
        var enrolled = (await db.Enrollments.Select(e => new { e.UserId, e.PackId }).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var pair in active.Where(p => !enrolled.Contains(p)))
        {
            db.Enrollments.Add(new() { UserId = pair.UserId, PackId = pair.PackId });
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException) { /* Enrolled concurrently. */ }
            db.ChangeTracker.Clear();
        }
        return written;
    }
}
```

In `DatabaseInitializer.RunAsync`, replace
```csharp
        if (app.Configuration.GetValue("Database:AutoMigrate", true) || args.Contains("--migrate"))
            await db.Database.MigrateAsync();
```
with
```csharp
        if (app.Configuration.GetValue("Database:AutoMigrate", true) || args.Contains("--migrate"))
        {
            await db.Database.MigrateAsync();
            await EvidenceBackfill.RunAsync(db);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx`
Expected: all tests PASS, including 2 `BackfillTests`.

- [ ] **Step 5: Commit**

```bash
git add apps/api tests
git commit -m "feat(api): backfill the evidence ledger and enrollments during migration" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Export and deletion cover the new records

**Files:**
- Create: `apps/api/Contracts/Export/{EnrollmentExportDto,LessonProgressExportDto,EvidenceExportDto}.cs`, `tests/LearnForge.Tests/PrivacyTests.cs`
- Modify: `apps/api/Contracts/Export/LearnerExportDto.cs`, `apps/api/Endpoints/LearnerEndpoints.cs` (export)

**Interfaces:**
- Produces: `LearnerExportDto(DateTime ExportedAt, DashboardDto Dashboard, AttemptView[] Attempts, EnrollmentExportDto[] Enrollments, LessonProgressExportDto[] LessonProgress, EvidenceExportDto[] Evidence)`.

- [ ] **Step 1: Write the failing test**

Create `tests/LearnForge.Tests/PrivacyTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class PrivacyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact] public async Task Export_includes_learning_records_and_deletion_removes_them()
    {
        var pack = TestApi.LoadPack("reasoning-foundations");
        var client = await TestApi.Account(factory);
        await client.PutAsync($"/api/me/courses/{pack.Id}/lessons/sets-intro", null);
        var attempt = await TestApi.Start(client);
        await TestApi.Complete(client, attempt, pack);

        var export = await client.GetFromJsonAsync<JsonElement>("/api/me/export");
        Assert.Single(export.GetProperty("enrollments").EnumerateArray());
        Assert.Single(export.GetProperty("lessonProgress").EnumerateArray());
        Assert.Equal(TestApi.QuestionIds(attempt).Length, export.GetProperty("evidence").GetArrayLength());

        var userId = (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString()!;
        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/me/account") { Content = JsonContent.Create(new { password = TestApi.Password }) };
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.False(await db.Enrollments.AnyAsync(e => e.UserId == userId));
        Assert.False(await db.LessonProgress.AnyAsync(e => e.UserId == userId));
        Assert.False(await db.Evidence.AnyAsync(e => e.UserId == userId));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~PrivacyTests"`
Expected: FAIL with `KeyNotFoundException` for `enrollments`.

- [ ] **Step 3: Implement**

`apps/api/Contracts/Export/EnrollmentExportDto.cs`:
```csharp
namespace LearnForge.Api.Contracts.Export;

public sealed record EnrollmentExportDto(string PackId, EnrollmentStatus Status, DateTime EnrolledAt, DateTime LastActivityAt);
```

`apps/api/Contracts/Export/LessonProgressExportDto.cs`:
```csharp
namespace LearnForge.Api.Contracts.Export;

public sealed record LessonProgressExportDto(string PackId, string LessonId, DateTime CompletedAt, string? ContentHash);
```

`apps/api/Contracts/Export/EvidenceExportDto.cs`:
```csharp
namespace LearnForge.Api.Contracts.Export;

public sealed record EvidenceExportDto(string PackId, string ReleaseId, string AttemptId, string QuestionId, string FamilyId,
    string[] ObjectiveIds, EvidenceSource Source, bool Answered, bool FullyCorrect, decimal Earned, decimal Possible, DateTime At);
```

Replace `apps/api/Contracts/Export/LearnerExportDto.cs`:
```csharp
namespace LearnForge.Api.Contracts.Export;

public sealed record LearnerExportDto(DateTime ExportedAt, DashboardDto Dashboard, AttemptView[] Attempts,
    EnrollmentExportDto[] Enrollments, LessonProgressExportDto[] LessonProgress, EvidenceExportDto[] Evidence);
```

Replace the export handler in `LearnerEndpoints.cs`:
```csharp
            me.MapGet("/export", async (ClaimsPrincipal user, AppDb db, AttemptService service, LearningRecordService learning) =>
            {
                var id = UserId(user);
                var attempts = await db.Attempts.Where(a => a.UserId == id).ToListAsync();
                var enrollments = await db.Enrollments.Where(e => e.UserId == id)
                    .Select(e => new EnrollmentExportDto(e.PackId, e.Status, e.EnrolledAt, e.LastActivityAt)).ToArrayAsync();
                var lessons = await db.LessonProgress.Where(p => p.UserId == id)
                    .Select(p => new LessonProgressExportDto(p.PackId, p.LessonId, p.CompletedAt, p.ContentHash)).ToArrayAsync();
                var evidence = await db.Evidence.Where(e => e.UserId == id).OrderBy(e => e.Id)
                    .Select(e => new EvidenceExportDto(e.PackId, e.ReleaseId, e.AttemptId, e.QuestionId, e.FamilyId, e.ObjectiveIds,
                        e.Source, e.Answered, e.FullyCorrect, e.Earned, e.Possible, e.At)).ToArrayAsync();
                var export = new LearnerExportDto(service.Now, await learning.Dashboard(id), attempts.Select(service.View).ToArray(), enrollments, lessons, evidence);
                return Results.File(System.Text.Encoding.UTF8.GetBytes(Json.Write(export)), "application/json", "learnforge-history.json");
            });
```
Deletion needs no code, because all three tables cascade from the user (Task 6).

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx`
Expected: all tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api tests
git commit -m "feat(api): export enrollments, lesson progress and evidence" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Built-in request validation (.NET 10 `AddValidation`)

**Files:**
- Modify: `apps/api/Program.cs`, `apps/api/Contracts/Auth/{RegisterRequest,LoginRequest,ChangePasswordRequest,DeleteAccountRequest}.cs`, `apps/api/Contracts/Attempts/{StartRequest,ResponseRequest}.cs`, `apps/api/Contracts/Learning/EnrollmentRequest.cs`, `apps/api/Endpoints/{AuthEndpoints,LearnerEndpoints}.cs`
- Test: `tests/LearnForge.Tests/ValidationTests.cs`

**Interfaces:**
- Produces: invalid bodies now return `400` `application/problem+json` with an `errors` object keyed by field. Later tasks rely on that shape (the web error mapper in Task 13).

- [ ] **Step 1: Write the failing tests**

Create `tests/LearnForge.Tests/ValidationTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LearnForge.Tests;

public class ValidationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string[] Fields(JsonElement problem) => problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray();

    [Fact] public async Task Invalid_registration_returns_field_errors()
    {
        var client = factory.CreateClient(new() { HandleCookies = true });
        await TestApi.Token(client);
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "learner@example.test", password = "short", displayName = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = Fields(await response.Content.ReadFromJsonAsync<JsonElement>());
        Assert.Contains(fields, f => f.Equals("Password", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fields, f => f.Equals("DisplayName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] public async Task Oversized_start_requests_are_rejected_before_the_handler()
    {
        var client = await TestApi.Account(factory);
        var response = await client.PostAsJsonAsync("/api/me/attempts", new { packId = new string('x', 101), blueprintId = "short", mode = "mock", requestId = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(Fields(await response.Content.ReadFromJsonAsync<JsonElement>()), f => f.Equals("PackId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] public async Task Undefined_enrollment_status_is_rejected()
    {
        var client = await TestApi.Account(factory);
        var response = await client.PutAsJsonAsync("/api/me/enrollments/reasoning-foundations", new { status = 7 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~ValidationTests"`
Expected: FAIL.
- Registration returns 400 but with no `errors` property, so `KeyNotFoundException` is thrown.
- The oversized start returns 404.
- The enrollment status 7 returns 204.

- [ ] **Step 3: Annotate the request records**

`apps/api/Contracts/Auth/RegisterRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Auth;

public sealed record RegisterRequest(
    [property: Required, EmailAddress, MaxLength(254)] string Email,
    [property: Required, StringLength(128, MinimumLength = 12)] string Password,
    [property: Required, StringLength(80, MinimumLength = 1)] string DisplayName);
```

`apps/api/Contracts/Auth/LoginRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Auth;

public sealed record LoginRequest([property: Required, MaxLength(254)] string Email, [property: Required, MaxLength(128)] string Password);
```

`apps/api/Contracts/Auth/ChangePasswordRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Auth;

public sealed record ChangePasswordRequest([property: Required, MaxLength(128)] string CurrentPassword, [property: Required, MaxLength(128)] string NewPassword);
```

`apps/api/Contracts/Auth/DeleteAccountRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Auth;

public sealed record DeleteAccountRequest([property: Required, MaxLength(128)] string Password);
```

`apps/api/Contracts/Attempts/StartRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Attempts;

public sealed record StartRequest(
    [property: Required, MaxLength(100)] string PackId,
    [property: Required, MaxLength(100)] string BlueprintId,
    [property: EnumDataType(typeof(AssessmentMode))] AssessmentMode Mode,
    [property: Required, MaxLength(64)] string RequestId,
    [property: EnumDataType(typeof(PracticeFocus))] PracticeFocus? Focus = null,
    [property: MaxLength(100)] string? ObjectiveId = null);
```

`apps/api/Contracts/Attempts/ResponseRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Attempts;

public sealed record ResponseRequest(int Revision, [property: Required, MaxLength(64)] string RequestId,
    [property: Required, MaxLength(100)] string QuestionId, Answer Answer, bool Check = false);
```

`apps/api/Contracts/Learning/EnrollmentRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Learning;

public sealed record EnrollmentRequest([property: EnumDataType(typeof(EnrollmentStatus))] EnrollmentStatus Status);
```

- [ ] **Step 4: Enable validation and remove the manual length checks**

In `Program.cs`, after `builder.Services.AddOpenApi();`, add:
```csharp
// .NET 10 minimal API validation: DataAnnotations on request records are enforced before handlers run.
builder.Services.AddValidation();
```

In `AuthEndpoints.cs`:
- In `/register`, replace the length/email `if` with the stricter address check only: `if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out var address) || address.Address != request.Email) return Results.BadRequest(new ApiErrorResponse("Enter a valid email address."));`
- In `/login`, delete `if (request.Email.Length > 254 || request.Password.Length > 128) return Results.Unauthorized();`.
- In `/password`, delete `if (request.NewPassword.Length > 128 || request.CurrentPassword.Length > 128) return Results.BadRequest();`.

In `LearnerEndpoints.cs` (`/account`), change `if (request.Password.Length > 128 || !await users.CheckPasswordAsync(user, request.Password))` to `if (!await users.CheckPasswordAsync(user, request.Password))`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build LearnForge.slnx -c Release && dotnet test LearnForge.slnx`
Expected: 0 warnings; all tests PASS.

If `errors` is still missing, check three things: `AddValidation()` is called in the API project (the source generator only covers that assembly), the records live in `.cs` files, and the attributes use the `property:` target.

- [ ] **Step 6: Commit**

```bash
git add apps/api tests
git commit -m "feat(api): validate request records with .NET 10 minimal API validation" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: OpenAPI document, generated TypeScript types and an `HttpClient` API service

**Files:**
- Create: `apps/web/openapi-ts.config.ts`, generated `apps/web/openapi/learnforge.json`, generated `apps/web/src/app/generated/*`
- Modify: `apps/api/LearnForge.Api.csproj`, `apps/api/Program.cs`, `apps/api/Endpoints/{CatalogEndpoints,AuthEndpoints}.cs` (typed results), `Makefile`, `apps/web/package.json`, `apps/web/src/app/api.ts`, `apps/web/src/app/app.config.ts`, `apps/web/src/app/models.ts`

**Interfaces:**
- Produces:
  - `make api-types` regenerates the OpenAPI document and TypeScript types; `make check-api-types` fails on drift.
  - `models.ts` additionally re-exports the generated types `CourseCatalogDto`, `CourseGoal`, `CourseGoalStatusDto`, `CourseProgressDto`, `DashboardDto`, `EnrollmentStatus`, `MasteryState`, `NextStepDto`, `NextStepKind`, `NextStepReason` and `ObjectiveMasteryDto`.
  - `api.ts` exports `Api` (same promise methods, now over `HttpClient`, with a public `csrfToken`), `ApiError`, `toApiError(error: unknown): ApiError`, `csrfInterceptor: HttpInterceptorFn` and `message(e: unknown): string`, which now also understands `HttpErrorResponse` and validation `errors`.

- [ ] **Step 1: Enable on-demand OpenAPI generation**

In `apps/api/LearnForge.Api.csproj`, add to the first `<ItemGroup>`:
```xml
    <PackageReference Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.12" PrivateAssets="all" IncludeAssets="runtime; build; native; contentfiles; analyzers; buildtransitive" />
```
and add a property group:
```xml
  <PropertyGroup>
    <!-- Generated on demand by `make api-types`; ordinary and Docker builds skip it. -->
    <OpenApiGenerateDocumentsOnBuild>false</OpenApiGenerateDocumentsOnBuild>
    <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/../web/openapi</OpenApiDocumentsDirectory>
    <OpenApiGenerateDocumentsOptions>--file-name learnforge</OpenApiGenerateDocumentsOptions>
  </PropertyGroup>
```

In `Program.cs`, inside `ConfigureHttpJsonOptions`, add before the converter line:
```csharp
    // Strict numbers keep the OpenAPI schema (and the generated TypeScript) typed as number, not number | string.
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
```
Replace the final two lines of `Program.cs` with:
```csharp
// Build-time OpenAPI generation runs this entry point with a mock server; it must not touch a database.
var generatingOpenApiDocument = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (!generatingOpenApiDocument && !await DatabaseInitializer.RunAsync(app, args)) return;
app.Run();
```

- [ ] **Step 2: Use typed results where the schema needs a response type**

Endpoints that return an untyped `IResult` publish no response schema. Two of them carry types the web needs.

In `CatalogEndpoints.cs`, add `using Microsoft.AspNetCore.Http.HttpResults;` and replace the detail endpoint with:
```csharp
            app.MapGet("/api/catalog/{id}", async Task<Results<Ok<CourseCatalogDto>, NotFound>> (string id, AppDb db, ReleaseCache releases) =>
            {
                var release = await releases.Latest(db, id);
                if (release is null) return TypedResults.NotFound();
                var p = release.Pack;
                return TypedResults.Ok(new CourseCatalogDto(p.Id, p.Title, p.Description, p.Version, p.License, p.Objectives,
                    p.Lessons, p.Blueprints, p.Sources, p.Readiness ?? new(), p.Goal, p.Questions.Length));
            });
```
In `AuthEndpoints.cs`, add `using Microsoft.AspNetCore.Http.HttpResults;` and replace `/me` with:
```csharp
            auth.MapGet("/me", async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> (ClaimsPrincipal principal, UserManager<User> users) =>
            {
                var user = await users.GetUserAsync(principal);
                return user is null ? TypedResults.Unauthorized()
                    : TypedResults.Ok(new CurrentUserResponse(user.Id, user.DisplayName, user.Email!, await users.IsInRoleAsync(user, "Publisher")));
            }).RequireAuthorization();
```

Run: `dotnet test LearnForge.slnx`
Expected: all tests PASS (behaviour is unchanged).

- [ ] **Step 3: Add the TypeScript generator**

```bash
npm install --prefix apps/web --save-dev --save-exact @hey-api/openapi-ts@0.99.0
```
Create `apps/web/openapi-ts.config.ts`:
```ts
import { defineConfig } from '@hey-api/openapi-ts';

// Types only: the app keeps its own HTTP layer (the Api service and httpResource).
export default defineConfig({
  input: './openapi/learnforge.json',
  output: './src/app/generated',
  plugins: ['@hey-api/typescript'],
});
```
In `apps/web/package.json`, add to `scripts`: `"api:types": "openapi-ts"`.

In the root `Makefile`, extend the `.PHONY` line with `api-types check-api-types` and add:
```make
api-types:
	dotnet build apps/api/LearnForge.Api.csproj -p:OpenApiGenerateDocumentsOnBuild=true
	npm run api:types --prefix apps/web
check-api-types: api-types
	git diff --exit-code -- apps/web/openapi apps/web/src/app/generated
```

- [ ] **Step 4: Generate and inspect**

Run: `make api-types`
Then check:
```bash
test -f apps/web/openapi/learnforge.json && echo "document OK"
grep -c '"readLesson"' apps/web/openapi/learnforge.json
grep -nE "export type (CourseProgressDto|DashboardDto|NextStepReason|MasteryState|CourseCatalogDto|CurrentUserResponse) " apps/web/src/app/generated/types.gen.ts
grep -n "number | string" apps/web/src/app/generated/types.gen.ts || echo "numbers are strict"
```
Expected:
- `document OK`;
- a count of at least 1, which shows enums are camelCase strings;
- six `export type` lines;
- `numbers are strict`.

If a type name differs (for example the generator adds a suffix), use the generated name in the `models.ts` re-exports below and in later tasks.

- [ ] **Step 5: Move `Api` onto `HttpClient`**

Replace `apps/web/src/app/api.ts`:
```ts
import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { User } from './models';

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

// Turns HTTP failures, including .NET validation problems, into one readable message.
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;
  if (!(error instanceof HttpErrorResponse)) return new ApiError('The request could not be completed.', 0);
  const data = (error.error ?? {}) as { detail?: string; errors?: Record<string, string[]> };
  const fieldErrors = data.errors ? Object.values(data.errors).flat().join(' ') : '';
  return new ApiError(
    data.detail ||
      fieldErrors ||
      (error.status === 401 ? 'Sign in to continue.' : 'The request could not be completed.'),
    error.status,
  );
}

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);
  readonly user = signal<User | null>(null);
  readonly unavailable = signal(false);
  csrfToken = '';
  async initialize() {
    try {
      await this.refreshCsrf();
      this.user.set(await this.get<User>('/auth/me'));
    } catch (e) {
      if (!(e instanceof ApiError && e.status === 401)) this.unavailable.set(true);
    }
  }
  async refreshCsrf() {
    this.csrfToken = (await this.get<{ token: string }>('/auth/csrf')).token;
  }
  async refreshSession() {
    this.user.set(await this.get<User>('/auth/me'));
    await this.refreshCsrf();
    this.unavailable.set(false);
  }
  get<T>(path: string): Promise<T> {
    return this.request<T>('GET', path);
  }
  post<T>(path: string, body: unknown = {}): Promise<T> {
    return this.request<T>('POST', path, body);
  }
  put<T>(path: string, body: unknown = {}): Promise<T> {
    return this.request<T>('PUT', path, body);
  }
  delete<T>(path: string, body: unknown): Promise<T> {
    return this.request<T>('DELETE', path, body);
  }
  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    try {
      return (await firstValueFrom(this.http.request<T>(method, '/api' + path, { body }))) as T;
    } catch (e) {
      throw toApiError(e);
    }
  }
}

// Adds the antiforgery token to unsafe API requests, including those made by httpResource.
export const csrfInterceptor: HttpInterceptorFn = (request, next) =>
  request.method === 'GET' || request.method === 'HEAD' || !request.url.startsWith('/api')
    ? next(request)
    : next(request.clone({ setHeaders: { 'X-CSRF-TOKEN': inject(Api).csrfToken } }));

export const message = (e: unknown) =>
  e instanceof HttpErrorResponse || e instanceof ApiError
    ? toApiError(e).message
    : e instanceof Error
      ? e.message
      : 'Something went wrong. Please try again.';
```

Replace `apps/web/src/app/app.config.ts`:
```ts
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { Api, csrfInterceptor } from './api';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch(), withInterceptors([csrfInterceptor])),
    provideRouter(routes),
    provideAppInitializer(() => inject(Api).initialize()),
  ],
};
```

Append to `apps/web/src/app/models.ts`:
```ts
// Generated from the API's OpenAPI document by `make api-types`. New code uses these;
// the hand-written interfaces above are replaced page by page.
export type {
  CourseCatalogDto,
  CourseGoal,
  CourseGoalStatusDto,
  CourseProgressDto,
  DashboardDto,
  EnrollmentStatus,
  MasteryState,
  NextStepDto,
  NextStepKind,
  NextStepReason,
  ObjectiveMasteryDto,
} from './generated/types.gen';
```

- [ ] **Step 6: Verify**

Run: `npm run build --prefix apps/web`
Expected: build succeeds.

`make check-api-types` compares regenerated output against committed files (`git diff` ignores untracked files), so run it after the commit in Step 7.

Smoke test: `make dev`, open http://127.0.0.1:4300, register, sign out and sign in (CSRF-protected POSTs now go through the interceptor). Stop with Ctrl+C.

- [ ] **Step 7: Commit**

```bash
git add apps/api apps/web Makefile
git commit -m "feat: generate TypeScript types from OpenAPI and move the web API client onto HttpClient" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
make check-api-types
```
Expected: `make check-api-types` exits 0.

---

### Task 14: Vitest unit tests and shared learning components

**Files:**
- Create: `apps/web/src/app/mastery-badge.ts`, `apps/web/src/app/mastery-badge.spec.ts`, `apps/web/src/app/next-steps.ts`, `apps/web/src/app/next-steps.spec.ts`
- Modify: `apps/web/angular.json`, `apps/web/package.json`, `apps/web/src/styles.scss`

**Interfaces:**
- Consumes: `MasteryState` and `NextStepDto` from `models.ts` (Task 13).
- Produces:
  - `<lf-mastery-badge [state] [correct] [considered] [reviewDue]>` and `masteryLabel(state: MasteryState): string`.
  - `<lf-next-steps [packId] [steps]>`, `describeStep(step: NextStepDto): string` and `stepLink(packId: string, step: NextStepDto): { path: string[]; query: Record<string, string> }`. Links use query params `tab` (`learn` | `practice`), `lesson`, `objective`, `blueprint` and `mode`, which the course page reads in Task 16.

- [ ] **Step 1: Install and configure Vitest**

```bash
npm install --prefix apps/web --save-dev --save-exact vitest@5.0.2 jsdom@30.1.1
```
In `apps/web/angular.json`, add a sibling of `"serve"` under `projects.web.architect`:
```json
        "test": {
          "builder": "@angular/build:unit-test",
          "options": {
            "buildTarget": "web:build:development",
            "tsConfig": "tsconfig.spec.json"
          }
        }
```
In `apps/web/package.json`, set `"test": "ng test --watch=false"` and keep `"test:e2e": "playwright test"`.

- [ ] **Step 2: Write the failing specs**

`apps/web/src/app/mastery-badge.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { MasteryBadge, masteryLabel } from './mastery-badge';
import { MasteryState } from './models';

describe('MasteryBadge', () => {
  it('names every state in words', () => {
    const states: MasteryState[] = ['notStarted', 'emerging', 'developing', 'proficient'];
    expect(states.map(masteryLabel)).toEqual(['Not started', 'Getting started', 'Developing', 'Proficient']);
  });

  it('shows the evidence behind the state, not only a colour', async () => {
    const fixture = TestBed.createComponent(MasteryBadge);
    fixture.componentRef.setInput('state', 'developing');
    fixture.componentRef.setInput('correct', 3);
    fixture.componentRef.setInput('considered', 5);
    await fixture.whenStable();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Developing');
    expect(text).toContain('3 of last 5 correct');
  });

  it('says when there is no evidence and when a review is due', async () => {
    const fixture = TestBed.createComponent(MasteryBadge);
    fixture.componentRef.setInput('state', 'proficient');
    fixture.componentRef.setInput('reviewDue', true);
    await fixture.whenStable();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Proficient · review due');
    expect(text).toContain('No evidence yet');
  });
});
```

`apps/web/src/app/next-steps.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { NextStepDto } from './models';
import { describeStep, NextSteps, stepLink } from './next-steps';

const step = (overrides: Partial<NextStepDto>): NextStepDto => ({
  kind: 'practise',
  reason: 'needsEvidence',
  objectiveId: 'sets',
  objectiveTitle: 'Reason about sets',
  lessonId: null,
  lessonTitle: null,
  blueprintId: null,
  ...overrides,
});

describe('next steps', () => {
  it('phrases every reason for a learner', () => {
    expect(describeStep(step({ kind: 'readLesson', reason: 'startObjective', lessonId: 'sets-intro', lessonTitle: 'Think in sets' })))
      .toBe('Begin "Reason about sets" by reading "Think in sets".');
    expect(describeStep(step({ reason: 'belowProficient' }))).toContain('until it becomes proficient');
    expect(describeStep(step({ kind: 'review', reason: 'reviewDue' }))).toContain('Review "Reason about sets"');
    expect(describeStep(step({ kind: 'takeMock', reason: 'readyForMock', objectiveId: null, objectiveTitle: null, blueprintId: 'short' })))
      .toContain('Take a timed mock');
  });

  it('links each kind of step to the right course view', () => {
    expect(stepLink('demo', step({ kind: 'readLesson', lessonId: 'sets-intro' }))).toEqual({ path: ['/courses', 'demo'], query: { tab: 'learn', lesson: 'sets-intro' } });
    expect(stepLink('demo', step({ kind: 'review' }))).toEqual({ path: ['/courses', 'demo'], query: { tab: 'practice', objective: 'sets' } });
    expect(stepLink('demo', step({ kind: 'takeMock', blueprintId: 'short' }))).toEqual({ path: ['/courses', 'demo'], query: { tab: 'practice', mode: 'mock', blueprint: 'short' } });
  });

  it('renders an action link per step and a message when nothing is left', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(NextSteps);
    fixture.componentRef.setInput('packId', 'demo');
    fixture.componentRef.setInput('steps', [step({})]);
    await fixture.whenStable();
    const link = (fixture.nativeElement as HTMLElement).querySelector('a')!;
    expect(link.textContent).toContain('Practise');
    expect(link.getAttribute('href')).toBe('/courses/demo?tab=practice&objective=sets');
    fixture.componentRef.setInput('steps', []);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('You have completed every suggested step');
  });
});
```

Run: `npm test --prefix apps/web`
Expected: FAIL with `Failed to resolve import "./mastery-badge"`.

- [ ] **Step 3: Implement the components**

`apps/web/src/app/mastery-badge.ts`:
```ts
import { Component, computed, input } from '@angular/core';
import { MasteryState } from './models';

const labels: Record<MasteryState, { text: string; icon: string }> = {
  notStarted: { text: 'Not started', icon: '○' },
  emerging: { text: 'Getting started', icon: '◔' },
  developing: { text: 'Developing', icon: '◑' },
  proficient: { text: 'Proficient', icon: '●' },
};

export const masteryLabel = (state: MasteryState) => labels[state].text;

// State is always spelled out with its evidence; the icon and colour only reinforce it.
@Component({
  selector: 'lf-mastery-badge',
  template: `<span class="pill mastery" [class.success]="state() === 'proficient'"
      ><span aria-hidden="true">{{ icon() }}</span> {{ label() }}</span
    ><small class="muted">{{ evidence() }}</small>`,
})
export class MasteryBadge {
  readonly state = input.required<MasteryState>();
  readonly correct = input(0);
  readonly considered = input(0);
  readonly reviewDue = input(false);
  readonly label = computed(() => masteryLabel(this.state()) + (this.reviewDue() ? ' · review due' : ''));
  readonly icon = computed(() => labels[this.state()].icon);
  readonly evidence = computed(() =>
    this.considered() === 0
      ? 'No evidence yet'
      : `${this.correct()} of last ${this.considered()} correct`,
  );
}
```

`apps/web/src/app/next-steps.ts`:
```ts
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NextStepDto } from './models';

// The API sends reason codes; wording lives here so it can be localized later.
export function describeStep(step: NextStepDto): string {
  const objective = step.objectiveTitle ?? 'this objective';
  switch (step.reason) {
    case 'startObjective':
      return `Begin "${objective}" by reading "${step.lessonTitle}".`;
    case 'continueReading':
      return `Finish reading "${step.lessonTitle}" for "${objective}".`;
    case 'needsEvidence':
      return `Practise "${objective}" so your progress can be measured.`;
    case 'belowProficient':
      return `Keep practising "${objective}" until it becomes proficient.`;
    case 'reviewDue':
      return `Review "${objective}": it has been a while since you practised it.`;
    case 'readyForMock':
      return 'Every objective is proficient. Take a timed mock to build readiness evidence.';
  }
}

export function stepLink(packId: string, step: NextStepDto): { path: string[]; query: Record<string, string> } {
  const path = ['/courses', packId];
  switch (step.kind) {
    case 'readLesson':
      return { path, query: { tab: 'learn', lesson: step.lessonId ?? '' } };
    case 'takeMock':
      return { path, query: { tab: 'practice', mode: 'mock', blueprint: step.blueprintId ?? '' } };
    default:
      return { path, query: { tab: 'practice', objective: step.objectiveId ?? '' } };
  }
}

const actions: Record<NextStepDto['kind'], string> = {
  readLesson: 'Open lesson',
  practise: 'Practise',
  review: 'Review',
  takeMock: 'Start a mock',
};

@Component({
  selector: 'lf-next-steps',
  imports: [RouterLink],
  template: `<ol class="next-steps">
    @for (step of steps(); track $index) {
      <li>
        <span>{{ describe(step) }}</span>
        <a class="text-link" [routerLink]="link(step).path" [queryParams]="link(step).query"
          >{{ action(step) }} →</a
        >
      </li>
    } @empty {
      <li class="muted">You have completed every suggested step for this course.</li>
    }
  </ol>`,
})
export class NextSteps {
  readonly packId = input.required<string>();
  readonly steps = input.required<NextStepDto[]>();
  readonly describe = describeStep;
  link(step: NextStepDto) {
    return stepLink(this.packId(), step);
  }
  action(step: NextStepDto) {
    return actions[step.kind];
  }
}
```

Append to `apps/web/src/styles.scss`:
```scss
.next-steps {
  margin: 0 0 18px;
  padding-left: 20px;
  display: grid;
  gap: 8px;
}
.next-steps li {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 14px;
  align-items: baseline;
}
lf-mastery-badge {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 4px 8px;
  align-items: center;
}
```

- [ ] **Step 4: Run the specs to verify they pass**

Run: `npm test --prefix apps/web`
Expected: PASS (6 tests).

Run: `npm run build --prefix apps/web`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add apps/web
git commit -m "feat(web): add Vitest and shared mastery and next-step components" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: Dashboard shows my courses, mastery, next steps and goals

**Files:**
- Modify: `apps/web/src/app/pages/dashboard.ts` (full rewrite), `apps/web/src/app/models.ts` (remove the legacy `Readiness`, `CourseProgress` and `Dashboard` interfaces)
- Test: `apps/web/src/app/pages/dashboard.spec.ts`

**Interfaces:**
- Consumes: `GET /api/me/dashboard` → `DashboardDto` (Task 8); `MasteryBadge`, `NextSteps` (Task 14).

- [ ] **Step 1: Write the failing spec**

`apps/web/src/app/pages/dashboard.spec.ts`:
```ts
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { DashboardDto } from '../models';
import { DashboardPage } from './dashboard';

const sample: DashboardDto = {
  courses: [
    {
      packId: 'reasoning-foundations',
      title: 'Reasoning foundations',
      version: '1.0.0',
      enrollment: 'active',
      lessonCount: 3,
      completedLessons: ['sets-intro'],
      revisedLessons: [],
      objectives: [
        { id: 'sets', title: 'Reason about sets', prerequisites: [], state: 'developing', correct: 3, considered: 5, independent: 0, lastEvidenceAt: '2026-09-24T10:00:00Z', reviewDue: false, lessonIds: ['sets-intro'] },
      ],
      nextSteps: [
        { kind: 'practise', reason: 'belowProficient', objectiveId: 'sets', objectiveTitle: 'Reason about sets', lessonId: null, lessonTitle: null, blueprintId: null },
      ],
      goal: { goal: 'mastery', met: false, proficientObjectives: 0, objectiveCount: 3, completedLessons: 1, lessonCount: 3, readiness: null },
      lastActivityAt: '2026-09-24T10:00:00Z',
    },
  ],
  recentAttempts: [],
  completedAttempts: 0,
  activeAttempts: 0,
  completedLessons: 1,
};

async function render(data: DashboardDto) {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
  const fixture = TestBed.createComponent(DashboardPage);
  fixture.detectChanges();
  TestBed.tick();
  TestBed.inject(HttpTestingController).expectOne('/api/me/dashboard').flush(data);
  await fixture.whenStable();
  return (fixture.nativeElement as HTMLElement).textContent ?? '';
}

describe('DashboardPage', () => {
  it('shows enrolled courses with mastery, next steps and goal progress', async () => {
    const text = await render(sample);
    expect(text).toContain('My courses');
    expect(text).toContain('Reasoning foundations');
    expect(text).toContain('3 of last 5 correct');
    expect(text).toContain('Keep practising "Reason about sets"');
    expect(text).toContain('0 of 3 objectives proficient');
  });

  it('invites learners without courses to the library', async () => {
    const text = await render({ ...sample, courses: [] });
    expect(text).toContain('No courses yet.');
  });
});
```

Run: `npm test --prefix apps/web`
Expected: FAIL. The current page requests `/api/me/dashboard` through the old `fetch` path, so `expectOne` finds no request.

- [ ] **Step 2: Rewrite the page**

Replace `apps/web/src/app/pages/dashboard.ts`:
```ts
import { Component, inject } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { Api, message } from '../api';
import { CourseGoal, DashboardDto } from '../models';
import { MasteryBadge } from '../mastery-badge';
import { NextSteps } from '../next-steps';

const goalLabels: Record<CourseGoal, string> = {
  readiness: 'EXAM READINESS',
  mastery: 'MASTERY GOAL',
  completion: 'COMPLETION GOAL',
};

@Component({
  imports: [RouterLink, DecimalPipe, DatePipe, MasteryBadge, NextSteps],
  template: ` <div class="page-heading">
      <div>
        <p class="eyebrow">ONE STEP FURTHER</p>
        <h1>Welcome back, {{ api.user()?.displayName }}<span class="accent">.</span></h1>
        <p class="lead">Small steps today. Stronger understanding tomorrow.</p>
      </div>
      <a class="button" routerLink="/courses">Explore your courses <span>↗</span></a>
    </div>
    @if (dashboard.error(); as e) {
      <p class="alert error" role="alert">{{ message(e) }}</p>
    }
    @if (dashboard.hasValue()) {
      @let d = dashboard.value();
      <div class="stat-grid">
        <div class="stat">
          <span>LESSONS COMPLETED</span><strong>{{ d.completedLessons | number }}</strong
          ><small>Ideas you have explored</small>
        </div>
        <div class="stat">
          <span>PRACTICE SESSIONS</span><strong>{{ d.completedAttempts | number }}</strong
          ><small>Completed and saved</small>
        </div>
        <div class="stat">
          <span>IN PROGRESS</span><strong>{{ d.activeAttempts }}</strong
          ><small>Ready when you are</small>
        </div>
      </div>
      <div class="dashboard-grid">
        <section>
          <div class="section-heading">
            <h2>My courses</h2>
            <a routerLink="/courses" class="text-link">Browse the library →</a>
          </div>
          @for (course of d.courses; track course.packId) {
            <article class="panel path-panel">
              <div class="row">
                <span class="tiny-label">CONTINUE LEARNING</span
                ><span class="muted small"
                  >{{ course.completedLessons.length }} / {{ course.lessonCount }} lessons</span
                >
              </div>
              <h2>
                <a [routerLink]="['/courses', course.packId]"
                  >{{ course.title }} <span class="accent">↗</span></a
                >
              </h2>
              <progress
                [value]="course.completedLessons.length"
                [max]="course.lessonCount"
                [attr.aria-label]="course.title + ' completion'"
              ></progress>
              <h3 class="small">Next steps</h3>
              <lf-next-steps [packId]="course.packId" [steps]="course.nextSteps" />
              <div class="objective-list">
                @for (objective of course.objectives; track objective.id) {
                  <div>
                    <span>{{ objective.title }}</span>
                    <lf-mastery-badge
                      [state]="objective.state"
                      [correct]="objective.correct"
                      [considered]="objective.considered"
                      [reviewDue]="objective.reviewDue"
                    />
                  </div>
                }
              </div>
            </article>
          } @empty {
            <div class="panel empty">
              <h3>No courses yet.</h3>
              <p>
                Open a course from the library. It appears here as soon as you read a lesson or
                start a session.
              </p>
              <a routerLink="/courses" class="button">Browse the library →</a>
            </div>
          }
          <div class="section-heading">
            <h2>Recent practice</h2>
            <a routerLink="/attempts" class="text-link">All attempts →</a>
          </div>
          <div class="panel">
            @for (a of d.recentAttempts; track a.id) {
              <a class="attempt-row" [routerLink]="['/attempts', a.id]"
                ><div>
                  <strong>{{ a.title }}</strong
                  ><small
                    >{{ a.size }} · {{ a.mode }} · {{ a.startedAt | date: 'mediumDate' }}</small
                  >
                </div>
                <span class="pill" [class.success]="a.status === 'completed'">{{
                  a.status === 'completed'
                    ? (a.correctPercent | number: '1.0-0') + '% correct'
                    : 'Resume →'
                }}</span></a
              >
            } @empty {
              <div class="empty">
                <h3>Your first session is ahead.</h3>
                <p>Start a practice session from any course. Your results will appear here.</p>
              </div>
            }
          </div>
        </section>
        <aside>
          <div class="section-heading"><h2>Course goals</h2></div>
          @for (course of d.courses; track course.packId) {
            <div class="panel readiness">
              <span class="eyebrow">{{ goalLabel(course.goal.goal) }}</span>
              <h3>{{ course.title }}</h3>
              @switch (course.goal.goal) {
                @case ('readiness') {
                  @if (course.goal.readiness; as r) {
                    <p>{{ r.message }}</p>
                    <div class="readiness-track">
                      <strong>{{ r.short.streak }}<small>/{{ r.short.required }}</small></strong
                      ><span>consecutive short mocks</span>
                    </div>
                    <div class="readiness-track">
                      <strong>{{ r.full.streak }}<small>/{{ r.full.required }}</small></strong
                      ><span>consecutive full mocks</span>
                    </div>
                    <p class="small muted">
                      Each result must be strictly above {{ r.threshold }}% fully correct, with
                      fresh questions and independent work. Either streak qualifies.
                    </p>
                  }
                }
                @case ('mastery') {
                  <p>
                    {{ course.goal.proficientObjectives }} of {{ course.goal.objectiveCount }}
                    objectives proficient.
                  </p>
                }
                @default {
                  <p>
                    {{ course.goal.completedLessons }} of {{ course.goal.lessonCount }} lessons
                    completed.
                  </p>
                }
              }
              @if (course.goal.met) {
                <p><span class="pill success">Goal met</span></p>
              }
            </div>
          }
        </aside>
      </div>
    } @else if (dashboard.isLoading()) {
      <p class="empty">Loading your workspace…</p>
    }`,
})
export class DashboardPage {
  readonly api = inject(Api);
  readonly dashboard = httpResource<DashboardDto>(() => '/api/me/dashboard');
  readonly message = message;
  goalLabel(goal: CourseGoal) {
    return goalLabels[goal];
  }
}
```

In `apps/web/src/app/models.ts`, delete the legacy `Readiness`, `CourseProgress` and `Dashboard` interfaces. Confirm nothing else used them:
```bash
grep -rnE "\b(Readiness|CourseProgress|Dashboard)\b" apps/web/src --include='*.ts' | grep -v "generated/\|Dto\|DashboardPage"
```
Expected: no output.

- [ ] **Step 3: Verify**

Run: `npm test --prefix apps/web && npm run build --prefix apps/web`
Expected: 8 unit tests PASS; build succeeds.

- [ ] **Step 4: Commit**

```bash
git add apps/web
git commit -m "feat(web): dashboard lists enrolled courses with mastery, next steps and goals" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 16: Course page: enrollment, revised lessons, mastery map, accessible tabs and objective practice

**Files:**
- Modify: `apps/web/src/app/pages/course.ts` (full rewrite), `apps/web/src/app/app.config.ts`, `apps/web/src/styles.scss`, `apps/web/src/app/models.ts` (remove the legacy `Course` interface), `apps/web/package.json` (`@angular/aria`)

**Interfaces:**
- Consumes:
  - `GET /api/catalog/{id}` → `CourseCatalogDto`; `GET /api/me/courses/{id}` → `CourseProgressDto`; `PUT /api/me/enrollments/{id}`; `PUT /api/me/courses/{id}/lessons/{lessonId}`; `POST /api/me/attempts` with `focus`/`objectiveId` (Tasks 8–9).
  - The query params written by `stepLink` (Task 14): `tab`, `lesson`, `objective`, `blueprint`, `mode`.

- [ ] **Step 1: Install `@angular/aria` and enable route input binding**

```bash
npm install --prefix apps/web --save-exact @angular/aria@22.2.0
```
In `app.config.ts`, change `import { provideRouter } from '@angular/router';` to `import { provideRouter, withComponentInputBinding } from '@angular/router';` and `provideRouter(routes)` to `provideRouter(routes, withComponentInputBinding())`.

- [ ] **Step 2: Rewrite the course page**

Replace `apps/web/src/app/pages/course.ts`:
```ts
import { Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { form, FormField, required, submit } from '@angular/forms/signals';
import { Tab, TabContent, TabList, TabPanel, Tabs } from '@angular/aria/tabs';
import { Api, message } from '../api';
import { CourseCatalogDto, CourseProgressDto, EnrollmentStatus } from '../models';
import { MasteryBadge } from '../mastery-badge';
import { NextSteps } from '../next-steps';

interface PracticeSetup {
  blueprintId: string;
  mode: string;
  focus: string;
  objectiveId: string;
}

@Component({
  imports: [RouterLink, FormField, Tabs, TabList, Tab, TabPanel, TabContent, MasteryBadge, NextSteps],
  template: ` @if (catalog.error(); as e) {
      <p class="alert error" role="alert">{{ errorText(e) }}</p>
    }
    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
      <a routerLink="/attempts" class="text-link">Find your active session →</a>
    }
    @if (course(); as c) {
      <a routerLink="/courses" class="breadcrumb">← Learning library</a>
      <div class="page-heading">
        <div>
          <p class="eyebrow">YOUR LEARNING PATH</p>
          <h1>{{ c.title }}</h1>
          <p class="lead">{{ c.description }}</p>
        </div>
        <div class="row">
          <span class="pill">{{ c.lessons.length }} lessons</span>
          @if (progress.hasValue()) {
            @switch (progress.value().enrollment) {
              @case ('active') {
                <button class="button secondary" [disabled]="busy()" (click)="setEnrollment('archived')">
                  Archive course
                </button>
              }
              @case ('archived') {
                <button class="button" [disabled]="busy()" (click)="setEnrollment('active')">
                  Restore to my courses
                </button>
              }
              @default {
                <button class="button" [disabled]="busy()" (click)="setEnrollment('active')">
                  Add to my courses
                </button>
              }
            }
          }
        </div>
      </div>
      @if (progress.hasValue() && progress.value().nextSteps.length) {
        <section class="panel">
          <p class="eyebrow">A GOOD NEXT STEP</p>
          <lf-next-steps [packId]="c.id" [steps]="progress.value().nextSteps" />
        </section>
      }
      <div ngTabs>
        <div ngTabList class="tabs" selectionMode="follow" [(selectedTab)]="selectedTab" aria-label="Course views">
          @for (t of tabs; track t.id) {
            <button type="button" ngTab [value]="t.id">{{ t.title }}</button>
          }
        </div>
        <div ngTabPanel value="learn" class="tab-panel">
          <ng-template ngTabContent>
            <div class="lesson-layout">
              <nav class="panel lesson-nav" aria-label="Course lessons">
                @for (l of c.lessons; track l.id; let i = $index) {
                  <button [class.active]="current()?.id === l.id" (click)="lessonId.set(l.id)">
                    <span>{{ completed().has(l.id) ? '✓' : (i + 1).toString().padStart(2, '0') }}</span>
                    <div>
                      <strong>{{ l.title }}</strong><small>{{ l.summary }}</small>
                      @if (revised().has(l.id)) {
                        <small class="pill subtle">Updated since you read it</small>
                      }
                    </div>
                  </button>
                }
              </nav>
              @if (current(); as l) {
                <article class="panel reading">
                  <p class="eyebrow">BUILD YOUR UNDERSTANDING</p>
                  <h2>{{ l.title }}</h2>
                  <p class="lead">{{ l.summary }}</p>
                  @for (block of l.blocks; track $index) {
                    <section [class]="'content-block ' + block.kind">
                      @if (block.title) {
                        <h3>{{ block.title }}</h3>
                      }
                      @if (block.kind === 'code') {
                        <pre><code>{{ block.text }}</code></pre>
                      } @else {
                        <p>{{ block.text }}</p>
                      }
                    </section>
                  }
                  <div class="row">
                    <span class="muted small">Reading completion is separate from assessment performance.</span>
                    @if (api.user()) {
                      @if (revised().has(l.id)) {
                        <button class="button" [disabled]="busy()" (click)="complete(l.id)">Mark as re-read</button>
                      } @else {
                        <button class="button" [disabled]="busy() || completed().has(l.id)" (click)="complete(l.id)">
                          {{ completed().has(l.id) ? 'Lesson completed ✓' : 'Mark as complete' }}
                        </button>
                      }
                    } @else {
                      <a routerLink="/sign-in" class="button">Sign in to save progress</a>
                    }
                  </div>
                </article>
              }
            </div>
          </ng-template>
        </div>
        <div ngTabPanel value="map" class="tab-panel">
          <ng-template ngTabContent>
            <div class="section-heading">
              <div>
                <h2>How the ideas connect</h2>
                <p class="muted">Follow the prerequisites, then practise the objective.</p>
              </div>
            </div>
            <div class="map-grid">
              @for (o of c.objectives; track o.id; let i = $index) {
                <article class="panel objective-card">
                  <span class="eyebrow">OBJECTIVE {{ i + 1 }}</span>
                  <h3>{{ o.title }}</h3>
                  @if (mastery().get(o.id); as m) {
                    <lf-mastery-badge [state]="m.state" [correct]="m.correct" [considered]="m.considered" [reviewDue]="m.reviewDue" />
                  }
                  <p class="muted small">
                    {{ o.prerequisites.length ? 'Builds on: ' + prerequisiteNames(o.prerequisites) : 'A starting point — no prerequisites' }}
                  </p>
                  @for (l of c.lessons; track l.id) {
                    @if (l.objectiveIds.includes(o.id)) {
                      <button class="text-button" (click)="openLesson(l.id)">Study: {{ l.title }} →</button>
                    }
                  }
                  @if (api.user()) {
                    <button class="text-button" (click)="practise(o.id)">Practise this objective →</button>
                  }
                </article>
              }
            </div>
          </ng-template>
        </div>
        <div ngTabPanel value="practice" class="tab-panel">
          <ng-template ngTabContent>
            <div class="practice-layout">
              <form class="panel" (submit)="start($event)">
                <p class="eyebrow">PUT YOUR KNOWLEDGE TO WORK</p>
                <h2>Shape your session</h2>
                <label
                  >Session length<select [formField]="setupForm.blueprintId">
                    @for (b of c.blueprints; track b.id) {
                      <option [value]="b.id" [selected]="b.id === setup().blueprintId">
                        {{ b.title }} · {{ b.count }} questions · {{ b.minutes }} minutes
                      </option>
                    }
                  </select></label
                >
                <label
                  >Feedback mode<select [formField]="setupForm.mode">
                    <option value="learn">Learn — check answers as you go</option>
                    <option value="mock">Mock exam — results after submission</option>
                  </select></label
                >
                @if (setup().mode === 'learn') {
                  <label
                    >Practice focus<select [formField]="setupForm.focus">
                      <option value="">Balanced practice</option>
                      <option value="weak">Weakest objectives</option>
                      <option value="mistakes">Previous mistakes</option>
                      <option value="objective">One objective</option>
                    </select></label
                  >
                  @if (setup().focus === 'objective') {
                    <label
                      >Objective<select [formField]="setupForm.objectiveId">
                        <option value="">Choose an objective…</option>
                        @for (o of c.objectives; track o.id) {
                          <option [value]="o.id" [selected]="o.id === setup().objectiveId">{{ o.title }}</option>
                        }
                      </select></label
                    >
                  }
                }
                <p class="muted">
                  {{
                    setup().mode === 'mock'
                      ? 'The timer starts when you begin. Your responses save automatically; a refresh will not reset the deadline.'
                      : 'Take your time. Check each answer and learn from the explanation. Learning sessions build mastery evidence but do not count toward exam readiness.'
                  }}
                </p>
                @if (api.user()) {
                  <button class="button" [disabled]="busy()">{{ busy() ? 'Preparing…' : 'Begin session →' }}</button>
                } @else {
                  <a routerLink="/sign-in" class="button">Sign in to practise</a>
                }
              </form>
              <aside class="recommendation">
                <p class="eyebrow">PRACTICE WITH PURPOSE</p>
                <h2>Understanding comes<br />from doing.</h2>
                <p>
                  Every session draws from this course's question bank. You will see different ways to
                  demonstrate the same ideas.
                </p>
                <div class="feature-list">
                  <p>Single and multiple choice</p>
                  <p>Matching and dropdown blanks</p>
                  <p>Sequences and case studies</p>
                </div>
                <p class="small">
                  Focused practice may be shorter when your history contains fewer relevant questions.
                </p>
              </aside>
            </div>
          </ng-template>
        </div>
        <div ngTabPanel value="sources" class="tab-panel">
          <ng-template ngTabContent>
            <div class="panel">
              <h2>Sources & attribution</h2>
              <p>Content license: {{ c.license }} · Release {{ c.version }}</p>
              @for (s of c.sources; track s.url) {
                <p>
                  <a [href]="s.url" target="_blank" rel="noopener noreferrer" class="text-link">{{ s.title }} ↗</a>
                </p>
              } @empty {
                <p class="muted">This course contains original demonstration material.</p>
              }
            </div>
          </ng-template>
        </div>
      </div>
    } @else if (catalog.isLoading()) {
      <p class="empty">Loading the course…</p>
    }`,
})
export class CoursePage {
  readonly api = inject(Api);
  private readonly router = inject(Router);
  // Route and query parameters, bound through withComponentInputBinding().
  readonly id = input.required<string>();
  readonly tab = input<string>();
  readonly lesson = input<string>();
  readonly objective = input<string>();
  readonly blueprint = input<string>();
  readonly mode = input<string>();

  readonly catalog = httpResource<CourseCatalogDto>(() => `/api/catalog/${this.id()}`);
  readonly progress = httpResource<CourseProgressDto>(() =>
    this.api.user() ? `/api/me/courses/${this.id()}` : undefined,
  );
  readonly error = signal('');
  readonly busy = signal(false);
  readonly tabs = [
    { id: 'learn', title: 'Lessons' },
    { id: 'map', title: 'Content map' },
    { id: 'practice', title: 'Practice & exams' },
    { id: 'sources', title: 'References' },
  ];
  // string | undefined matches the ngTabList selectedTab model for two-way binding.
  readonly selectedTab = linkedSignal<string | undefined>(() => this.tab() || 'learn');
  readonly lessonId = linkedSignal(() => this.lesson() ?? '');
  readonly course = computed(() => (this.catalog.hasValue() ? this.catalog.value() : undefined));
  readonly current = computed(() => {
    const c = this.course();
    return c?.lessons.find((l) => l.id === this.lessonId()) ?? c?.lessons[0];
  });
  readonly completed = computed(() => new Set(this.progress.hasValue() ? this.progress.value().completedLessons : []));
  readonly revised = computed(() => new Set(this.progress.hasValue() ? this.progress.value().revisedLessons : []));
  readonly mastery = computed(
    () => new Map((this.progress.hasValue() ? this.progress.value().objectives : []).map((o) => [o.id, o] as const)),
  );
  // Deep links from next steps preselect the session; the learner can still change every field.
  readonly setup = linkedSignal<PracticeSetup>(() => ({
    blueprintId: this.blueprint() || this.course()?.blueprints[0]?.id || '',
    mode: this.mode() === 'mock' ? 'mock' : 'learn',
    focus: this.objective() ? 'objective' : '',
    objectiveId: this.objective() ?? '',
  }));
  readonly setupForm = form(this.setup, (p) => {
    required(p.blueprintId);
    required(p.objectiveId, {
      when: ({ valueOf }) => valueOf(p.mode) === 'learn' && valueOf(p.focus) === 'objective',
    });
  });

  errorText(e: unknown) {
    return message(e);
  }
  prerequisiteNames(ids: string[]) {
    return ids.map((id) => this.course()?.objectives.find((o) => o.id === id)?.title ?? id).join(', ');
  }
  openLesson(id: string) {
    this.lessonId.set(id);
    this.selectedTab.set('learn');
  }
  practise(objectiveId: string) {
    this.setup.update((s) => ({ ...s, mode: 'learn', focus: 'objective', objectiveId }));
    this.selectedTab.set('practice');
  }
  complete(lessonId: string) {
    return this.run(() => this.api.put(`/me/courses/${this.id()}/lessons/${lessonId}`));
  }
  setEnrollment(status: EnrollmentStatus) {
    return this.run(() => this.api.put(`/me/enrollments/${this.id()}`, { status }));
  }
  async start(event: Event) {
    event.preventDefault();
    await submit(this.setupForm, async () => {
      const s = this.setup();
      const learn = s.mode === 'learn';
      this.busy.set(true);
      this.error.set('');
      try {
        const attempt = await this.api.post<{ id: string }>('/me/attempts', {
          packId: this.id(),
          blueprintId: s.blueprintId,
          mode: s.mode,
          requestId: crypto.randomUUID(),
          focus: learn && s.focus ? s.focus : null,
          objectiveId: learn && s.focus === 'objective' ? s.objectiveId : null,
        });
        await this.router.navigate(['/attempts', attempt.id]);
      } catch (e) {
        this.error.set(message(e));
      } finally {
        this.busy.set(false);
      }
      return undefined;
    });
  }
  private async run(action: () => Promise<unknown>) {
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
      this.progress.reload();
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
```

- [ ] **Step 3: Style the accessible tabs and hidden panels**

In `apps/web/src/styles.scss`, replace the `.tabs button.active { ... }` rule with:
```scss
.tabs [aria-selected='true'] {
  border-color: var(--green);
  color: var(--green);
  font-weight: 600;
}
.tab-panel[inert] {
  display: none;
}
```
`@angular/aria` marks hidden panels `inert`; the rule above removes them visually.

- [ ] **Step 4: Remove the legacy `Course` interface**

In `models.ts`, delete `export interface Course extends CourseCard { ... }` and confirm:
```bash
grep -rn "\bCourse\b" apps/web/src/app --include='*.ts' | grep -v "generated/\|CourseCard\|CoursePage\|CourseCatalogDto\|CourseProgressDto\|CourseGoal"
```
Expected: no output.

- [ ] **Step 5: Verify**

Run: `npm test --prefix apps/web && npm run build --prefix apps/web`
Expected: unit tests PASS; build succeeds within the existing budgets.

Manual check (`make dev`, then open http://127.0.0.1:4300/courses/reasoning-foundations while signed in):
- Arrow keys move between tabs.
- "Add to my courses" turns into "Archive course".
- "Practise this objective" on the Content map opens Practice with "One objective" preselected.
- A dashboard next-step link opens the right tab.

- [ ] **Step 6: Commit**

```bash
git add apps/web
git commit -m "feat(web): course page with enrollment, revised lessons, mastery map, aria tabs and objective practice" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 17: Replace the remaining hand-written models with generated types

**Files:**
- Modify: `apps/web/src/app/models.ts` (and any narrowing fixes the compiler requests in `pages/attempt.ts`, `pages/history.ts`, `pages/courses.ts` or `question-input.ts`)

**Interfaces:**
- Produces: every web type now comes from `generated/types.gen.ts`. The existing names stay as aliases, so page imports do not change: `User`, `CourseCard`, `Objective`, `Lesson`, `Blueprint`, `Option`, `Question`, `Answer`, `Grade`, `AttemptSummary`, `Attempt`.

- [ ] **Step 1: Replace `models.ts`**

```ts
// All API types are generated from the OpenAPI document (`make api-types`); never edit generated/.
export type {
  Answer,
  AttemptSummaryDto as AttemptSummary,
  AttemptView as Attempt,
  Blueprint,
  CatalogSummaryDto as CourseCard,
  CourseCatalogDto,
  CourseGoal,
  CourseGoalStatusDto,
  CourseProgressDto,
  CurrentUserResponse as User,
  DashboardDto,
  DeliveryQuestion as Question,
  EnrollmentStatus,
  Grade,
  Lesson,
  MasteryState,
  NextStepDto,
  NextStepKind,
  NextStepReason,
  Objective,
  ObjectiveMasteryDto,
  Option,
} from './generated/types.gen';
```

- [ ] **Step 2: Build and fix what the stricter types reveal**

Run: `npm run build --prefix apps/web`
Expected: success. If the compiler reports errors, they come from nullability now being accurate, for example `scenarioId: string | null` or `matches: {...} | null`. Fix each at its use site with `?? ''` / `?? {}` / `|| 'general'`. Do not widen the types. The existing templates already guard most of these (`q.scenarioId || 'general'`, `g.matches || {}`).

Run: `npm test --prefix apps/web`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/web
git commit -m "refactor(web): use generated API types everywhere" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 18: Browser journeys

**Files:**
- Modify: `apps/web/e2e/learning.spec.ts`

- [ ] **Step 1: Extend the existing journey and add enrollment and keyboard coverage**

In `apps/web/e2e/learning.spec.ts`, add a helper at the top (after the import):
```ts
async function register(page: import('@playwright/test').Page, name: string) {
  await page.goto('/sign-in');
  await page.getByRole('button', { name: 'Create an account', exact: true }).click();
  await page.getByLabel('Display name').fill(name);
  await page.getByLabel('Email', { exact: true }).fill(`browser-${Date.now()}-${Math.random().toString(36).slice(2)}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page.getByRole('heading', { name: `Welcome back, ${name}.` })).toBeVisible();
}
```
In the first test, append before `expect(errors).toEqual([]);`:
```ts
  await page.getByRole('link', { name: 'Overview' }).click();
  await expect(page.getByRole('heading', { name: 'My courses' })).toBeVisible();
  const course = page.locator('.path-panel').filter({ hasText: 'Reasoning foundations' });
  await expect(course).toBeVisible();
  await expect(course.locator('.next-steps li').first()).toBeVisible();
  await expect(course.locator('lf-mastery-badge').filter({ hasNotText: 'Not started' }).first()).toBeVisible();
```
Append a new test:
```ts
test('learners add, archive and restore courses and switch tabs with the keyboard', async ({ page }) => {
  await register(page, 'Kai');
  await expect(page.getByRole('heading', { name: 'No courses yet.' })).toBeVisible();
  await page.goto('/courses/evidence-lab');
  await page.getByRole('button', { name: 'Add to my courses' }).click();
  await expect(page.getByRole('button', { name: 'Archive course' })).toBeVisible();
  await page.getByRole('link', { name: 'Overview' }).click();
  await expect(page.locator('.path-panel').filter({ hasText: 'Evidence lab' })).toBeVisible();

  await page.goto('/courses/evidence-lab');
  await page.getByRole('button', { name: 'Archive course' }).click();
  await expect(page.getByRole('button', { name: 'Restore to my courses' })).toBeVisible();
  await page.getByRole('link', { name: 'Overview' }).click();
  await expect(page.getByRole('heading', { name: 'No courses yet.' })).toBeVisible();

  await page.goto('/courses/evidence-lab');
  await page.getByRole('tab', { name: 'Lessons' }).focus();
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('tab', { name: 'Content map' })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByRole('heading', { name: 'How the ideas connect' })).toBeVisible();
});
```

- [ ] **Step 2: Run the journeys**

In one terminal: `make dev`. In another:
```bash
cd apps/web && npx playwright install chromium && npx playwright test
```
Expected: 3 tests PASS (the original two plus the new one).

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e
git commit -m "test(e2e): cover my courses, mastery, enrollment and keyboard tabs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 19: Documentation and project instructions

**Files:**
- Modify: `CLAUDE.md`, `docs/architecture.md`, `docs/assessment.md`, `docs/user-guide.md`, `docs/authoring.md`, `docs/api.md`, `docs/testing.md`

- [ ] **Step 1: Update `CLAUDE.md`**

Under **Commands**, add:
```sh
npm test --prefix apps/web      # Angular unit tests (Vitest via @angular/build:unit-test)
make api-types                  # regenerate apps/web/openapi/learnforge.json and src/app/generated types
make check-api-types            # fail if generated API types are stale
```
and change the e2e line to `cd apps/web && npx playwright test   # or npm run test:e2e; requires API and web dev servers`.

Under **Architecture**, replace the `apps/api` bullet's first sentence with: "**`apps/api`**: minimal-API endpoint modules in `Endpoints/*` (C# 14 extension members on `IEndpointRouteBuilder`), composed by `Program.cs`; request records are validated by `AddValidation()`." Then add two bullets:
- "`Services/Learning/LearningRecordService` derives enrollment, lesson progress, mastery (`MasteryEvaluator`), next steps (`NextStepPlanner`) and goal status on read. `Services/Content/ReleaseCache` keeps each immutable release deserialized once."
- "`Startup/DatabaseInitializer` migrates, backfills the evidence ledger (`EvidenceBackfill`), seeds packs and handles `--grant-publisher`."

Under **Invariants to preserve**, add:
```markdown
- **The evidence ledger is append-only.** `EvidenceRecord` holds one row per attempt and question, written when feedback is released (a learning check or attempt completion) in the same save as the attempt transition. Mastery, next steps and focused practice are derived from it on read.
- **Progress is keyed by stable IDs.** Lesson progress uses pack and lesson IDs plus a content hash (revised lessons are flagged, not reset); mastery uses question, family and objective IDs across releases. Readiness stays release-scoped and mock-only.
```

- [ ] **Step 2: Update the guides**

- `docs/assessment.md`: add a **Mastery** section after "Readiness rule":
  > Each objective shows one of four states: not started, getting started (fewer than `minimumEvidence` question families), developing, or proficient. The rule looks at the learner's latest answer in each question family, keeps the latest `window` families (default 5), and requires at least `proficientPercent` (default 80%) fully correct: 4 of the last 5 qualifies. Mock answers always count, and unanswered mock items count as incorrect. Learning-mode answers count only when answered, because they are first tries before feedback. Proficient objectives become "review due" after `reviewAfterDays` (default 60). Mastery is a transparent study aid; readiness is unchanged and remains release-scoped and mock-only.
- `docs/authoring.md`: in **Build a pack**, document the optional `goal` (`readiness` default, `mastery`, `completion`), the optional `mastery` object (`window`, `minimumEvidence`, `proficientPercent`, `reviewAfterDays` with the bounds from Global Constraints), and that `readiness` may be omitted. Note that a mastery goal requires at least `minimumEvidence` question families per objective, and that lesson IDs should stay stable, because changing a lesson's content flags it as updated for learners who completed it.
- `docs/user-guide.md`: add a **My courses** section covering auto-enrollment on the first lesson or session, "Add to my courses", "Archive course" and "Restore"; the four mastery states and what "3 of last 5 correct" means; next steps; the "Updated since you read it" badge with "Mark as re-read"; the goal cards; and the new **One objective** practice focus. Update **Use the dashboard**: objective evidence now includes learning-mode answers.
- `docs/api.md`, **Learner** section: add `GET /api/me/courses/{packId}` (course progress: enrollment, completed and revised lessons, objective mastery, next steps, goal), `PUT /api/me/enrollments/{packId}` with `{ "status": "active" | "archived" }`, the `objectiveId` field on `POST /api/me/attempts` (with `focus: "objective"`, learning mode only), the new dashboard shape, and export now including `enrollments`, `lessonProgress` and `evidence`. Note that `GET /api/catalog/{packId}` no longer returns `completedLessons`, and that invalid bodies return 400 problem details with an `errors` object.
- `docs/architecture.md`: add the evidence ledger, enrollment, lesson progress, release cache and endpoint modules to **API modules** and **Persistence model**. Add the ledger step ("write evidence rows") to the question-kind checklist under **Extending the framework**.
- `docs/testing.md`: update the test description to include mastery and next-step rules, the ledger, enrollment, cross-release progress, backfill, validation and migrations. Add `npm test --prefix apps/web` (Vitest) and `make check-api-types`.

- [ ] **Step 3: Full verification**

```bash
dotnet build LearnForge.slnx -c Release
dotnet test LearnForge.slnx
make check-content
make check-api-types
npm test --prefix apps/web
npm run build --prefix apps/web
```
Expected: every command succeeds. With `make dev` running, `cd apps/web && npx playwright test` also passes.

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md docs
git commit -m "docs: document mastery, enrollment, evidence ledger and new tooling" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Spec coverage (self-review)

| Spec requirement | Task |
| --- | --- |
| New Core enums, `PracticeFocus.Objective` | 1, 2, 3, 6, 9 |
| `MasteryPolicy`, optional `readiness`/`goal`/`mastery` on `Pack`, bounds and feasibility validation | 1 |
| `ContentHash.Of` | 1 |
| `MasteryEvaluator` rules (latest per family, window, states, review due) | 2 |
| `NextStepPlanner` (frontier, lesson before practice, review, mock only for readiness goals, limit, determinism) | 3 |
| Entities, indexes, cascade deletes, `FocusObjectiveId`, two migrations with the completion copy | 6 |
| Evidence written with learning checks and in `Finish`, in the same save | 7 |
| Backfill in the migration step, idempotent and concurrent-safe | 10 |
| `ReleaseCache` (bounded, stampede-safe, failed loads evicted) | 5 |
| `LearningRecordService` (enroll, archive, touch, lesson progress, course progress, dashboard) | 8 |
| Ledger-based seen families, mistakes and weak focus; objective focus with replay check | 9 |
| Endpoint modules with C# 14 extension members; `DatabaseInitializer` | 4 |
| `AddValidation()` with annotated request records | 12 |
| Catalog without personal data, with `Goal` | 8, 13 |
| Export of the new records; deletion cascades | 11 |
| Build-time OpenAPI, strict numbers, generated TypeScript, drift check | 13 |
| `HttpClient` + interceptor, `httpResource`, route input binding, Signal Forms, `@angular/aria` tabs | 13, 15, 16 |
| Shared `mastery-badge` and `next-steps` components | 14 |
| Dashboard and course page UX (goal cards, revised badge, enroll/archive/restore, mastery map, objective practice) | 15, 16 |
| Vitest, Core/API tests, Playwright | 1–18 |
| Docs and `CLAUDE.md` invariants | 19 |
| Review Focus items 1–5 | 8, 8, 8, 9, 10 |

Type consistency was checked across tasks: `ReleaseView.ReleaseId`, `LessonHashes`; `EvidenceWriter.Record/Countable/IsAnswered`; `LearningRecordService.Touch/SetEnrollment/CompleteLesson/CourseProgress/Dashboard/Evidence`; `TestApi.LoadPack/Account/Token/Start/Id/QuestionIds/Correct/Save/Complete/Publisher`; the web names `NextStepDto`, `MasteryState`, `CourseProgressDto`, `DashboardDto`, `CourseCatalogDto`, `EnrollmentStatus`; and the query params `tab`/`lesson`/`objective`/`blueprint`/`mode` shared by `stepLink` and `CoursePage`.
