# Learner roadmap: universal subjects on .NET 10 and Angular 22

Status: proposal. Date: 2026-09-25. Detailed design of the first sub-project: [learning record foundation](2026-09-25-learning-record-foundation-design.md).

## Purpose

LearnForge should serve a wide range of subjects as data only, and give learners strong study tools. It should use the .NET 10 and Angular 22 features that exist in this repository's toolchain.

Decisions made during brainstorming:

| Topic | Decision |
| --- | --- |
| Subject families | STEM and programming, languages, humanities and social science, certification. The foundation must be subject-neutral. |
| AI | Optional module, off by default. It is grounded only in pack content, honors answer-release rules and never affects scores. |
| Deployment | Public learner service with open sign-up: multiple replicas, account recovery, privacy, caching. |
| Sequencing | Foundation first: SP1 → SP2 ∥ SP6 → SP3 → SP4 → SP5 → SP7. |

Invariants every sub-project keeps: server-side grading, immutable releases and attempt snapshots, idempotent writes, stable family IDs, transparent readiness, a strict compiler, sanitized delivery.

Accessibility is a cross-cutting acceptance requirement from the first release of each sub-project. Follow the [WCAG 2.2 AA design baseline](../../accessibility/README.md) and complete its feature template and verification gates. The analysis below records the original planning snapshot; current implementation starting points are recorded in that baseline. Keyboard/focus behavior, accessible timing and content alternatives must not wait for SP5.

## Analysis of the current solution

### Universality gaps (content model)

| # | Finding | Evidence |
| --- | --- | --- |
| A1 | Lessons are plain text with four block kinds: no formatting, math, figures, tables, media or links. | `ContentBlock.cs`; `course.ts` renders `<p>{{block.text}}</p>` |
| A2 | All question kinds are selections. Adding one edits `if` chains in three places. | `QuestionKind.cs`, `Grader.cs`, `ContentEngine.Validate`, `question-input.ts` |
| A3 | No modules or time estimates. | `Pack.cs` |
| A4 | Blueprints cannot weight domains. | `Blueprint.cs` |
| A5 | One explanation per question: no per-option rationale, misconceptions, hints, difficulty or item citations. | `Question.cs` |
| A6 | No glossary or flashcards. | none exist |
| A7 | No language or direction metadata; the UI is hard-coded English. | `Pack.cs`, pages |
| A8 | The catalog has no subject, level, tags or hours; search is a substring match. | `CatalogSummaryDto.cs`, `courses.ts` |
| A9 | Readiness is mandatory and exam-centric. | `ReadinessPolicy.cs`, dashboard |

### Learner-facility gaps

| # | Finding | Evidence |
| --- | --- | --- |
| B1 | No spaced repetition. | roadmap only |
| B2 | Objective evidence counts only mocks, so learning-mode learners never see progress. | `Analytics.BuildCourse` |
| B3 | Recommendations ignore prerequisites, recency and completion. | `Analytics.cs` |
| B4 | No retry with a sibling variant, no hints, no confidence rating. | `attempt.ts`, `AttemptService.Save` |
| B5 | Lessons are passive: no inline checks, notes or search. The CLI's `search.json` is unused. | `course.ts`, `tools/cli/Program.cs` |
| B6 | Results lack per-objective breakdown, timing, trends and "practise these mistakes". | `attempt.ts`, `history.ts` |
| B7 | No study plan, target date or reminders. | none exist |
| B8 | Progress resets on every release, because completion and evidence are keyed by release ID. | `Completion.cs`, catalog endpoint, `Analytics` |
| B9 | No enrollment: the dashboard lists every catalog pack. | `Analytics.Dashboard` |
| B10 | Course tabs lack keyboard semantics; no extended time or display preferences; native `confirm()`. | `course.ts`, `attempt.ts`, `settings.ts` |
| B11 | No password reset or email confirmation. | auth endpoints |
| B12 | No offline reading or PWA. | none exist |

### Technical observations

| # | Finding | Evidence |
| --- | --- | --- |
| C1 | Requests repeatedly deserialize full pack JSON. The dashboard re-grades every attempt, and starting an attempt deserializes every past snapshot. | `Program.cs`, `Analytics.cs`, `AttemptService.Start` |
| C2 | All endpoints and manual validation live in `Program.cs`. | `Program.cs` |
| C3 | `models.ts` is hand-written; enums are typed as `string`. | `models.ts` |
| C4 | Loading state is repeated per page; `fetch` instead of `HttpClient`; template-driven forms. | pages |
| C5 | `QuestionInput` hard-codes `id="token-bank"`. | `question-input.ts` |
| C6 | No Angular unit tests. | `angular.json` |
| C7 | Startup seeding and the ExpiryWorker assume a single replica. | `Program.cs`, `ExpiryWorker.cs` |

## Platform features verified in the toolchain

**.NET 10** (SDK 10.0.400, ASP.NET Core 10.0.11, EF Core 10.0.12):
- `AddValidation()` for record DTOs.
- Identity passkeys (`IdentityPasskeyOptions`, `PasskeySignInAsync`).
- `TypedResults.ServerSentEvents`.
- OpenAPI 10 document generation.
- EF Core 10: complex types and collections with `ToJson()`, named query filters, `LeftJoin`.
- System.Text.Json `AllowDuplicateProperties` and `Strict`.
- C# 14 extension members.
- NuGet: Microsoft.Extensions.AI, VectorData and HybridCache 10.10; Aspire 13.5.

**Angular 22.2:**
- `resource`, `rxResource` and `httpResource` are stable.
- Signal Forms (`@angular/forms/signals`) are stable.
- `injectAsync` with `onIdle` is stable.
- `withRouterResources` is in developer preview.
- `@angular/aria`, `@angular/service-worker` and `@angular/localize` are available.
- The Vitest `@angular/build:unit-test` builder is available.

**Why HybridCache is not used for pack caching:**
- It defaults to a 1 MiB maximum payload and sizes entries by serialized length, while packs can be 2 MB.
- It reuses instances only for sealed `[ImmutableObject(true)]` types.
- Immutable per-release objects need no distributed layer.

## Sub-projects

Rule for every sub-project: touch it, modernize it. Endpoints we change move into feature modules with built-in validation. Pages we change move to `httpResource`, Signal Forms and `@angular/aria`. There is no big-bang rewrite.

### SP1 Learning record foundation
Enrollment, an append-only evidence ledger, lesson progress that survives releases, transparent mastery, prerequisite-aware next steps, per-pack goals, objective practice and a release cache. See the [design](2026-09-25-learning-record-foundation-design.md).

### SP2 Rich, safe content
- A restricted Markdown and TeX subset, compiled in Core into a typed AST (never HTML) and rendered by Angular components.
- Blocks: figure (reviewed alternatives with explicit decorative handling), media (required alternatives by media type, including captions and AA audio description), table, list, math, worked example, misconception, definition, primary source, inline check. Follow the [content accessibility contract](../../accessibility/content-and-authoring.md).
- Stable block IDs, modules, glossary terms, language and direction, catalog metadata.
- Pack-scoped assets with a SHA-256 manifest, MIME allowlist and a storage abstraction.
- Duplicate JSON properties rejected.

### SP3 Question engine v2
- A Core question-kind strategy registry and lazily loaded Angular renderers.
- New kinds: numeric (tolerance, significant figures, units), text entry and cloze (variants, accent policy), categorize, hotspot, written response with rubric self-assessment.
- Per-option rationale, misconception tags, progressive hints, difficulty.
- Blueprint domain weights with shortage reports; compiler-checked answer examples.

### SP4 Retention science
- FSRS spaced review as a pure Core function over the evidence ledger.
- Flashcards generated from the glossary or written by authors.
- "Try a variant", confidence ratings with a calibration view, diagnostic placement.
- Transparent adaptive practice and a review mode.
- Assisted evidence never counts toward readiness.

### SP5 Learner experience
- A "Today" plan from a target date and weekly minutes.
- Notes, highlights and bookmarks anchored to block IDs; search.
- Results v2 with per-objective breakdown, timing, trends and mistake practice.
- Display preferences, a PWA with offline reading, calendar export. Accessible mock timing is a foundation requirement under the [assessment accessibility design](../../accessibility/learning-and-assessment.md); SP5 may refine its personalization.
- SSE deadline and multi-tab sync; accessible dialogs.

### SP6 Public-service trust
- Email confirmation, password reset, passkeys, session management.
- Distributed locks for expiry and seeding.
- OpenTelemetry and an Aspire AppHost.

### SP7 Optional AI
- A provider-neutral `IChatClient`.
- "Explain my mistake": only after feedback is released, grounded in pack content, streamed, cited and labelled.
- Socratic hints that never reveal keys.
- Advisory rubric feedback; optional semantic search.

### Cross-cutting enablers
- TypeScript types generated from OpenAPI, with a drift check (from SP1).
- `HttpClient` and `httpResource` (from SP1).
- Vitest unit tests (from SP1).
- Unique question-instance and drag-and-drop list IDs, verified keyboard and single-pointer alternatives, route focus, accessible forms/status, adjustable mock timing and accessibility evidence (shared foundation; maintained in every sub-project).
