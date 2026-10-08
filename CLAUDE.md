# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

LearnForge: an education and exam-practice framework. ASP.NET Core 10 API + Angular 22 SPA, modular monolith with one database. Subjects are versioned JSON "packs" (`packs/*.json`), not code. `docs/` holds current guides and a clearly separated roadmap. Bundled packs are generic examples; application code must remain independent of their IDs.

## Commands

```sh
make setup                      # dotnet restore LearnForge.slnx + npm ci --prefix apps/web
make dev                        # API on 127.0.0.1:5080 + Angular on 127.0.0.1:4300 (checks ports first)
dotnet test LearnForge.slnx     # all xUnit tests (Core + HTTP integration)
dotnet test LearnForge.slnx --filter "FullyQualifiedName~CoreTests.Readiness"   # single test / class
dotnet build LearnForge.slnx -c Release
npm run build --prefix apps/web
make check-content              # compile both bundled packs and the exam/1 sample (sample also linted --strict)
npm test --prefix apps/web      # Angular unit tests (Vitest via @angular/build:unit-test)
cd apps/web && npx playwright test   # or npm run test:e2e; requires API and web dev servers already running
make api-types                  # regenerate apps/web/openapi/learnforge.json and src/app/generated types
make check-api-types            # fail if generated API types are stale
docker compose up --build -d    # Postgres + API + nginx web on 127.0.0.1:8088
```

- Use the web origin (4300) in the browser, not 5080: `apps/web/proxy.conf.json` forwards `/api` and `/health` so cookies and CSRF stay same-origin.
- `Directory.Build.props` sets `TreatWarningsAsErrors`, nullable and implicit usings for every C# project, so warnings break the build.
- Tests against PostgreSQL: `docker compose up -d db`, then set `LEARNFORGE_TEST_POSTGRES='Host=127.0.0.1;Port=55432;Database=learnforge_test;Username=learnforge;Password=learnforge-local-only'`. Otherwise `ApiFactory` uses a throwaway SQLite file in the temp dir. Never point tests at an operational database.
- Content CLI (`tools/cli`): `dotnet run --project tools/cli -- init|check|lint|build <pack.json> [--watch] [--json] [--strict] [--out dir]`. `lint` prints non-blocking LF2xx quality warnings; `--strict` fails on any.
- `make dev` sets `Content__PackDirectories__0=$PWD/packs` and `Content__WatchSeconds=5`, so packs saved in `packs/` are published without a restart.
- Grant Publisher role: `dotnet run --project apps/api -- --grant-publisher you@example.com` (same database as the app). `--migrate` applies migrations, backfills the evidence ledger, seeds packs, then exits.

## Architecture

- **`src/LearnForge.Core`**: subject-neutral, dependency-light rules. `ContentEngine` compiles and validates pack JSON (rejects unknown properties, bad IDs, missing references, prerequisite cycles, malformed keys, unsafe URLs, infeasible blueprints; expands templates under `ExpansionBudget`). A source with `"format": "exam/1"` (records in `Core/Domain/Authoring`) is expanded by `ExamSourceAdapter` into an exam pack first: option IDs are hashes of question, bank and text (never of the key role), so they leak nothing. `ContentLinter` reports LF2xx giveaway/bank-shape warnings that never fail a compile. `Grader` scores answers server-side (exact or normalized partial credit). `ExamComposer` selects questions for a blueprint, keeps scenario (case-study) groups atomic and honours optional `objectiveWeights` (largest-remainder share ±1 per primary objective). `ReadinessEvaluator` implements the exam-readiness rule and `MasteryEvaluator` the per-objective mastery rule (see `docs/assessment.md`); `NextStepPlanner` suggests prerequisite-aware next steps. Packs declare a `goal` (readiness, mastery or completion).
- **`apps/api`**: minimal-API endpoint modules in `Endpoints/*` (C# 14 extension members on `IEndpointRouteBuilder`), composed by `Program.cs` (groups `/api/auth`, `/api/me` with auth required, `/api/authoring` with the Publisher role); request records are validated by `AddValidation()`. `Program.cs` configures Identity cookies, antiforgery (`X-CSRF-TOKEN` header from `/api/auth/csrf`), rate limits and security headers. `Services/Attempts/AttemptService` is the attempt state machine: ownership, deterministic selection, idempotent writes by request ID, revision checks, section locks, deadlines, sanitized views. `ExpiryWorker` finalizes overdue mocks.
- `Services/Learning/LearningRecordService` derives enrollment, lesson progress, mastery (`MasteryEvaluator`), next steps (`NextStepPlanner`) and goal status on read. `Services/Content/ReleaseCache` keeps each immutable release deserialized once.
- `Startup/DatabaseInitializer` migrates, backfills the evidence ledger (`EvidenceBackfill`), seeds packs via `Services/Content/PackSeeder` (strict) and handles `--grant-publisher`. `PackWatcher` (when `Content:WatchSeconds` > 0) republishes settled new pack files leniently.
- **`apps/web`**: standalone Angular components grouped by feature in `src/app`, with colocated templates/specs following the Angular style guide. `http/ApiClient` owns transport, `authentication/Session` owns identity state, and the CSRF interceptor/token are separate. Pages use `httpResource`; `api-contracts.ts` aliases the OpenAPI-generated `src/app/generated` (never hand-edit generated files). `assessment/QuestionInput` owns question interactions. Route-scoped `attempts/AttemptState` and `authoring/DraftEditorState` own persistence/timer coordination. See `apps/web/README.md` for conventions. Vitest specs next to components (`*.spec.ts`), Playwright specs in `e2e/`.

### Invariants to preserve

- **Answer keys never reach the browser while an attempt is active.** Delivery uses `DeliveryQuestion`, which omits keys and explanations. Grading happens only on the server.
- **Releases are immutable.** A `PackRelease` is keyed by pack ID and version. Seeding and publishing skip or reject an existing version, so content changes need a `version` bump. Starting an attempt snapshots its questions, blueprint and readiness policy, so later publishes cannot change work in progress.
- **Packs are seeded at startup.** `packs/**/*.json` is copied into the API output. On startup every file is compiled, and one invalid pack makes the API throw. Restart or rebuild the API to pick up new pack files, unless `Content:WatchSeconds` is set: then `PackWatcher` publishes new files and versions while running (logging, not throwing, on invalid ones).
- Importers must go through `ContentEngine`, never write directly to release tables.
- **The evidence ledger is append-only.** `EvidenceRecord` holds one row per attempt and question, written when feedback is released (a learning check or attempt completion) in the same save as the attempt transition. Mastery, next steps and focused practice are derived from it on read.
- **Progress is keyed by stable IDs.** Lesson progress uses pack and lesson IDs plus a content hash (revised lessons are flagged, not reset); mastery uses question, family and objective IDs across releases. Readiness stays release-scoped and mock-only.

### Persistence: two providers, two migration sets

`AppDb` (SQLite, dev default via `appsettings.json`) and `PostgresDb : AppDb` (selected by `Database:Provider=Postgres`) each have their own migrations in `apps/api/Migrations/Sqlite` and `apps/api/Migrations/Postgres`, with design-time factories in `Infrastructure/Persistence`. A schema change needs a migration for **both** contexts, for example:

```sh
dotnet ef migrations add <Name> --project apps/api --context AppDb --output-dir Migrations/Sqlite
dotnet ef migrations add <Name> --project apps/api --context PostgresDb --output-dir Migrations/Postgres
```

Migrations run automatically on startup unless `Database:AutoMigrate=false`.

## Conventions

- One public type per file, in domain/contract/persistence/service folders (`Contracts/<Area>/*`, `Infrastructure/Entities/*`, `Core/Domain/*`).
- Wire string values are backed by enums, serialized camelCase through `JsonStringEnumConverter`. API responses use named record DTOs, never anonymous or `object` shapes.
- Content is rendered as text. Templates must not execute code or inject HTML.
- `docs/prompts/exam-question-generator.md` embeds `docs/examples/exam-sample.json` verbatim (a test enforces it); update both together.
- Adding a question kind touches every layer in this order: Core contract and validation, `Grader`, composer feasibility, sanitized delivery projection, the exam/1 mapping in `ExamSourceAdapter` (and any `ContentLinter` rules), Angular interaction with a keyboard alternative, then Core/API/Playwright tests and the authoring and learner docs (`docs/architecture.md`).
- Keep question IDs stable across releases for family analytics. Change the family ID only when the competency or prompt pattern changes.
