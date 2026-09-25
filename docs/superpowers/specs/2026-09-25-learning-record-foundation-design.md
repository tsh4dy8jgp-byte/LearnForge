# SP1 design: learning record foundation

Status: design for review. Date: 2026-09-25. Part of the [learner roadmap](2026-09-25-learner-roadmap.md).

## Why

LearnForge has four problems that block a public, multi-subject learner service:

- Publishing a new pack version resets every learner's visible lesson progress and objective evidence, because both are keyed by release ID.
- Learners who study only in learning mode never see objective progress, because only mocks count as evidence.
- The dashboard lists every catalog pack, since there is no enrollment.
- Readiness is the only course goal, which does not fit university courses, languages or hobby subjects.

Later sub-projects also need a durable record of what each learner answered: spaced review (SP4), study plans and results (SP5) and the AI tutor (SP7).

## Decisions

| Topic | Decision |
| --- | --- |
| Mastery rule | Per objective, look at the latest 5 distinct question families. Proficient requires at least 3 pieces of evidence and at least 80% fully correct. |
| Mastery evidence | Mock answers always count; unanswered mock items count as incorrect. Learning-mode answers count when answered, as a first try before feedback. Skipped learning items are ignored. |
| Readiness | Unchanged: release-scoped and mock-only. |
| Revised lessons | Completion carries over by lesson ID. If the lesson's content hash changed, show "Updated since you read it" and keep it complete. |
| Enrollment | Automatic on the first lesson completion or session start. Learners can also add a course or archive it; archiving keeps all data. |
| Pack caching | A purpose-built in-memory cache keyed by immutable release ID. HybridCache is not used, for the reasons in the roadmap. |

## Goals and non-goals

**Goals**
- Progress that survives releases.
- Learning-mode evidence counts toward mastery.
- "My courses" through enrollment.
- Transparent per-objective mastery.
- A prerequisite-aware next step.
- A per-pack goal: readiness, mastery or completion.
- "Practise one objective" sessions.
- An append-only evidence ledger.
- No more per-request deserialization and re-grading.

**Non-goals**
- Spaced repetition, study plans, results v2.
- Content model or question kind changes.
- Account recovery or passkeys.

## Core (`src/LearnForge.Core`)

### New enums (`Domain/Enums`)

| Enum | Values |
| --- | --- |
| `CourseGoal` | `Readiness`, `Mastery`, `Completion` |
| `MasteryState` | `NotStarted`, `Emerging`, `Developing`, `Proficient` |
| `EvidenceSource` | `MockSubmission`, `LearningCheck`, `LearningSubmission` |
| `EnrollmentStatus` | `Active`, `Archived` |
| `NextStepKind` | `ReadLesson`, `Practise`, `Review`, `TakeMock` |
| `NextStepReason` | `StartObjective`, `ContinueReading`, `NeedsEvidence`, `BelowProficient`, `ReviewDue`, `ReadyForMock` |

`PracticeFocus` gains `Objective`.

### Pack contract
- `MasteryPolicy(int Window = 5, int MinimumEvidence = 3, decimal ProficientPercent = 80, int ReviewAfterDays = 60)`.
- `Pack` becomes `(..., Blueprint[] Blueprints, SourceReference[] Sources, ReadinessPolicy? Readiness = null, CourseGoal Goal = CourseGoal.Readiness, MasteryPolicy? Mastery = null)`.
- JSON property names are unchanged, so existing packs compile without edits and default to the readiness goal. A mastery or completion pack may omit `readiness`.
- Call sites use `pack.Readiness ?? new()` and `pack.Mastery ?? new()`.

Example:

```json
{ "goal": "mastery", "mastery": { "window": 5, "minimumEvidence": 3, "proficientPercent": 80, "reviewAfterDays": 60 } }
```

### Compiler rules (`ContentEngine.Validate`)
- Mastery bounds: `1 ≤ minimumEvidence ≤ window ≤ 20`, `50 ≤ proficientPercent ≤ 100`, `1 ≤ reviewAfterDays ≤ 365`.
- When the goal is mastery, every objective must have at least `minimumEvidence` distinct question families. Otherwise the goal would be unreachable, which is the same kind of feasibility check the compiler already does for blueprints.

### Content hash
`Services/Content/ContentHash.Of<T>(T value)` returns the lowercase SHA-256 hex of `Json.Write(value)`. It is used for lesson revision detection.

### Mastery evaluator
Types:
- `MasteryEvidence(long Order, string FamilyId, string[] ObjectiveIds, bool FullyCorrect, bool Independent, DateTime At)`
- `ObjectiveMastery(ObjectiveId, State, Correct, Considered, Independent, LastEvidenceAt, ReviewDue)`
- `MasteryEvaluator.Evaluate(objectives, evidence, policy, now)`

For each objective:
1. Take the evidence linked to the objective.
2. Keep the latest record per family (by `At`, then `Order`).
3. Keep the latest `Window` records.
4. Let `n` be the count, and `k` the number of fully correct records.
5. The state is:
   - `NotStarted` when `n = 0`;
   - `Emerging` when `n < MinimumEvidence`;
   - `Proficient` when `k × 100 ≥ ProficientPercent × n`;
   - `Developing` otherwise.
6. `ReviewDue` is true when the state is Proficient and the latest evidence is older than `ReviewAfterDays`.

The caller filters the input: mock records always count, and learning records count only when answered. Learners see the rule in plain words: "4 of your last 5 question families on this objective were fully correct."

### Next-step planner
`NextStep(Kind, Reason, ObjectiveId?, LessonId?, BlueprintId?)` and `NextStepPlanner.Plan(pack, mastery, completedLessonIds, readiness?, limit = 3)`:

1. The **frontier** is every objective that is not Proficient and whose prerequisites are all Proficient. It is ordered by prerequisite depth, then pack order.
2. For each frontier objective:
   - If a linked lesson is incomplete, the step is `ReadLesson` with the first incomplete lesson in pack order. The reason is `StartObjective` when the state is NotStarted, otherwise `ContinueReading`.
   - Otherwise the step is `Practise`. The reason is `NeedsEvidence` for NotStarted or Emerging, and `BelowProficient` for Developing.
3. Proficient objectives with `ReviewDue` produce `Review` with reason `ReviewDue`. Until SP4 adds scheduled review, the UI starts these as objective practice.
4. For the readiness goal, when every objective is Proficient and readiness is not met, the step is `TakeMock` with the first short blueprint (reason `ReadyForMock`).
5. The output is deterministic and truncated to `limit`.

## Persistence (`apps/api/Infrastructure`)

| Entity | Key and columns | Notes |
| --- | --- | --- |
| `Enrollment` | `(UserId, PackId)`; `Status`, `EnrolledAt`, `LastActivityAt` | FK to user, cascade on delete |
| `EvidenceRecord` | `long Id`; `UserId`, `PackId`, `ReleaseId`, `AttemptId`, `QuestionId`, `FamilyId`, `ObjectiveIds`, `Source`, `Answered`, `FullyCorrect`, `Earned`, `Possible`, `At` | Append-only. Unique `(AttemptId, QuestionId)`; index `(UserId, PackId, At)`; FKs to user and attempt with cascade. `ObjectiveIds` is an EF primitive collection (JSON in SQLite, `text[]` in PostgreSQL) |
| `LessonProgress` | `(UserId, PackId, LessonId)`; `CompletedAt`, `ContentHash?` | Replaces `Completion`. A null hash means unknown, so the lesson is not flagged |
| `Attempt` (changed) | adds `FocusObjectiveId?` | Records the objective of an objective-focused session, so a replayed start request with a different objective is rejected |

Enums use string conversion, matching the existing `Attempt` configuration.

### Migration `LearningRecordFoundation` (SQLite and PostgreSQL)
1. Create the three tables.
2. Copy completions with SQL that is valid in both providers:

   ```sql
   INSERT INTO "LessonProgress" ("UserId","PackId","LessonId","CompletedAt","ContentHash")
   SELECT c."UserId", p."PackId", c."LessonId", MIN(c."At"), NULL
   FROM "Completions" c JOIN "Packs" p ON p."Id" = c."ReleaseId"
   GROUP BY c."UserId", p."PackId", c."LessonId";
   ```
3. Drop `Completions`.

### Evidence backfill
`Services/Learning/EvidenceBackfill` runs only in the migration step (automatic migration or `--migrate`), never on every replica start.

- Completed attempts without evidence get one record per snapshot question, graded with `Grader.Score`. Mock attempts produce `MockSubmission`; checked learning questions produce `LearningCheck`; other learning questions produce `LearningSubmission`. `At` is the completion time.
- In-progress attempts need no backfill: `Finish` records any question without a row when the attempt completes.
- Enrollments are created for every user and pack with attempts or lesson progress.
- Each attempt is saved separately. A unique-key violation means another run finished it, so it is skipped. Running the backfill twice is safe.

## API (`apps/api`)

### Services
- **`ReleaseCache` and `ReleaseView`** (a deserialized `Pack` plus a lesson-hash map):
  - A bounded `MemoryCache` holds up to `ReleaseCache:Capacity` releases (default 64) with a sliding expiry.
  - Values are `Lazy<Task<ReleaseView>>` keyed by the immutable release ID, created under a lock, so concurrent misses load once. A failed load is evicted so it is retried.
  - Finding the latest release for a pack stays a single indexed query, so replicas never need cache invalidation.
- **`LearningRecordService`:**
  - Enroll, archive and touch activity. New activity reactivates an archived enrollment.
  - Complete a lesson with the current content hash. Marking a revised lesson again clears the flag.
  - Build course progress: evidence query → mastery → next steps → goal status. Readiness still comes from the current release's mock attempts.
  - Build the dashboard: active enrollments ordered by last activity, the five most recent attempt summaries, and counts.
- **`EvidenceWriter`:** builds evidence records from an attempt, question, grade and source.
- **`Analytics`:** keeps only `Summary`.

### Attempt changes
- A learning-mode check appends one `LearningCheck` record.
- Finishing an attempt (now `async`) appends a record for every question that has no evidence row yet. Questions with released feedback use `LearningCheck`; otherwise mocks produce `MockSubmission` and learning sessions `LearningSubmission`. `Answered` means a non-empty answer. Checks made before the upgrade are therefore recorded when their attempt completes.
- Records are written in the same `SaveChanges` as the attempt transition, guarded by the attempt's revision concurrency token. An expiry-versus-submit race therefore commits or rolls back both together.
- Starting an attempt reads seen families from the ledger instead of deserializing every past snapshot.
- The "mistakes" focus uses questions whose latest countable record is not fully correct. The "weak" focus uses the two objectives with the lowest `k/n` among objectives with evidence.
- The new **objective** focus (learning mode, `StartRequest.ObjectiveId`) selects standalone questions for that objective:
  - order: unseen families first, then previously missed questions, then the rest;
  - within those groups the order is seeded from the request ID;
  - up to the blueprint count.

  With no eligible questions it returns 409 and an actionable message.
- Starting a session and completing a lesson auto-enroll and update activity.

### Endpoint modules and validation
- Endpoints move out of `Program.cs` into `Endpoints/AuthEndpoints`, `CatalogEndpoints`, `LearnerEndpoints`, `AttemptEndpoints` and `AuthoringEndpoints`, written with C# 14 extension members:

  ```csharp
  public static class LearnerEndpoints
  {
      extension(IEndpointRouteBuilder app)
      {
          public RouteGroupBuilder MapLearner() { /* ... */ }
      }
  }
  ```
- Migration, seeding, backfill and publisher grants move to `Startup/DatabaseInitializer`. `Program.cs` becomes composition only.
- `builder.Services.AddValidation()` validates request records annotated with `[property: ...]` DataAnnotations. This replaces the manual length checks on registration, login, password change, start and enrollment requests.
- The existing cookie 401/403 handlers stay.
- `Microsoft.Extensions.ApiDescription.Server` generates the OpenAPI document on demand (`make api-types`), skipped by default so ordinary and Docker builds are unaffected. Startup database work is skipped when the entry assembly is `GetDocument.Insider`.
- HTTP JSON uses `JsonNumberHandling.Strict`, so the schema describes numbers as numbers rather than `number | string`.

### Endpoints and contracts (`Contracts/Learning`, one type per file)

| Endpoint | Change |
| --- | --- |
| `GET /api/catalog`, `GET /api/catalog/{packId}` | Served from `ReleaseCache`. `CompletedLessons` is removed from `CourseCatalogDto`, so the catalog is public data only; `Goal` is added |
| `GET /api/me/dashboard` | `DashboardDto(CourseProgressDto[] Courses, AttemptSummaryDto[] RecentAttempts, int CompletedAttempts, int ActiveAttempts, int CompletedLessons)` |
| `GET /api/me/courses/{packId}` | New: `CourseProgressDto` |
| `PUT /api/me/enrollments/{packId}` | New: `EnrollmentRequest(EnrollmentStatus Status)`; 204, or 404 for an unknown pack |
| `PUT /api/me/courses/{packId}/lessons/{lessonId}` | Same route, stored in `LessonProgress` |
| `POST /api/me/attempts` | Accepts optional `ObjectiveId` |
| `GET /api/me/export` | Adds enrollments, lesson progress and evidence |

Contract shapes:
- `CourseProgressDto(PackId, Title, Version, EnrollmentStatus? Enrollment, int LessonCount, string[] CompletedLessons, string[] RevisedLessons, ObjectiveMasteryDto[] Objectives, NextStepDto[] NextSteps, CourseGoalStatusDto Goal, DateTime? LastActivityAt)`
- `ObjectiveMasteryDto(Id, Title, Prerequisites, State, Correct, Considered, Independent, LastEvidenceAt, ReviewDue, LessonIds)`
- `NextStepDto(Kind, Reason, ObjectiveId?, ObjectiveTitle?, LessonId?, LessonTitle?, BlueprintId?)`
- `CourseGoalStatusDto(Goal, Met, ProficientObjectives, ObjectiveCount, CompletedLessons, LessonCount, ReadinessResult? Readiness)`
- Export records: `EnrollmentExportDto`, `LessonProgressExportDto`, `EvidenceExportDto`.

Reasons travel as enums, not English sentences, so the UI can localize them later.

## Web (`apps/web`)
- **`app.config.ts`:** add `provideHttpClient(withFetch(), withInterceptors([csrfInterceptor]))` and `withComponentInputBinding()`. `Api` keeps its promise methods but is implemented over `HttpClient`, so untouched pages keep working and there is one HTTP stack.
- **Generated types:** `npm run api:types` runs `@hey-api/openapi-ts` (types plugin only; it supports TypeScript 6, unlike `openapi-typescript`) on `apps/web/openapi/learnforge.json` to produce `src/app/generated/types.gen.ts`. `models.ts` re-exports and aliases these types, so enums become string-literal unions. `make check-api-types` fails on drift.
- **Shared components:**
  - `mastery-badge`: state label plus icon plus "k of last n", so meaning never depends on color alone.
  - `next-steps`: maps reason enums to sentences and links to a lesson, to objective practice or to a mock.
- **Dashboard:**
  - `httpResource(() => '/api/me/dashboard')`, showing enrolled courses only;
  - a goal card that depends on the course goal: readiness streak, "X of Y objectives proficient", or lesson completion;
  - next steps and objective mastery;
  - a "Browse the library" empty state.
- **Course page:**
  - route input binding and `httpResource` for the catalog and, when signed in, `/api/me/courses/{id}`;
  - "Add to my courses" and "Restore" buttons;
  - completed and "Updated since you read it" badges, with "Mark as re-read";
  - mastery badges on the content map;
  - `@angular/aria` tabs with arrow keys and tab panels;
  - a Signal Forms practice form with "Practise one objective".
- **Unit tests:** an `angular.json` `test` target using `@angular/build:unit-test` (Vitest, jsdom). `npm test` runs unit tests, and `npm run test:e2e` runs Playwright.

## Invariants and errors
- Answer keys never reach the browser. The new contracts carry IDs, titles, counts and states only.
- Releases stay immutable. Carry-over reads the newest release and never modifies one.
- Attempt snapshots are unchanged, and evidence is derived from them.
- Readiness stays release-scoped and mock-only.
- Ledger writes share the attempt transaction. Conflicts return the existing 409 response.
- Unknown pack, lesson or objective: 404 or 400. Objective practice with no eligible questions: 409.
- `CLAUDE.md` gains three invariants:
  - the evidence ledger is append-only;
  - progress is keyed by stable pack, lesson and question IDs;
  - readiness remains release-scoped.

## Testing
- **Core unit tests:**
  - mastery states and boundaries (4 of 5 qualifies; 2 records is Emerging);
  - latest-per-family deduplication, the window, review due;
  - planner: roots first, lesson before practice, review due, mock only for readiness goals, deterministic order;
  - packs without `readiness` or `goal` compile as readiness; mastery feasibility and bounds diagnostics;
  - content hash stability.
- **API integration tests** (SQLite, plus PostgreSQL with `LEARNFORGE_TEST_POSTGRES`):
  - one record per check; remaining records at submit and at expiry; replays add nothing;
  - learning answers change mastery;
  - completion survives a new release; revised lessons are flagged and re-reading clears the flag;
  - auto-enrollment; archive hides the course but keeps data;
  - objective practice selection;
  - the existing readiness test still passes;
  - backfill is idempotent;
  - export includes the new data and account deletion removes it.
- **Vitest:** mastery badge, next-step reason mapping, and dashboard rendering with `provideHttpClientTesting`.
- **Playwright:**
  - after a learning session, the dashboard shows the course under "My courses" and objectives no longer say "Needs evidence";
  - a next step is visible;
  - enroll and archive work;
  - course tabs work from the keyboard.

## Documentation
Update `docs/architecture.md`, `docs/assessment.md`, `docs/user-guide.md`, `docs/authoring.md`, `docs/api.md`, `docs/testing.md` and `CLAUDE.md`.

## Verification
1. `dotnet build LearnForge.slnx -c Release` and `make check-content`: both bundled packs compile unchanged.
2. `dotnet test LearnForge.slnx`, then again against PostgreSQL (`docker compose up -d db`).
3. `npm run api:types` leaves no diff; `npm run build --prefix apps/web`; `npm test --prefix apps/web`.
4. `make dev`, then `cd apps/web && npx playwright test`.
5. Manual checks at http://127.0.0.1:4300:
   - A learning session moves mastery, and the dashboard shows only that course.
   - Publishing a second version with one edited lesson keeps completion and shows the "Updated" badge.
   - Archive and restore work.
   - Objective practice starts.
   - Running `--migrate` twice on a copy of an existing SQLite database is idempotent and carries old completions over.
