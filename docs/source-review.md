# Source review — 25 September 2026

Read-only analysis of the working copies below. This review did not modify either source repository and did not run a complete build or browser regression suite. Counts and observations refer to the inspected state, including uncommitted files.

## University / Orbit

Root: `/Users/bakhtiyarkussainov/university`.

Observed stack: `net10.0`, EF Core 10.0.11/SQLite, Angular `^22.1.0`, TypeScript `~6.0.2`. The project already supplies the user's requested framework family. See [backend project](/Users/bakhtiyarkussainov/university/server/Orbit.Api.csproj:4) and [frontend dependencies](/Users/bakhtiyarkussainov/university/client/package.json:21).

Direct enumeration of `server/Content/courses.json` found 5 subjects, 42 units, 86 lessons and 640 catalog exercises: 588 single choice, 17 multi-select, 23 numeric, 4 ordering and 8 code output. These are catalog exercises only, excluding separate advanced prompts, activities, written revision questions and workshop banks. Some README counts describe earlier content subsets, so the proposal uses direct enumeration.

Reusable assets:

- Rich lesson structure: orientation, standard/focus readings, worked examples, misconceptions, glossaries, reference sources, code bridges and retrieval checks.
- Generic course/lesson routes, syllabus ordering, prerequisite validation and server-computed unit access.
- Deterministic exercise scoring and separate applied-work evidence.
- Workshop attempts already have ownership capabilities, revisions, idempotent start behavior, private grading definitions and delayed feedback. [Workshop response shaping](/Users/bakhtiyarkussainov/university/server/Features/Workshop/WorkshopAssessmentService.cs:29) is a useful pattern for the shared assessment engine.
- Study Studio distinguishes self-assessed written work from quiz scores and completion requirements.
- Existing content validators and tests capture valuable publishing rules.

Generalization gaps:

| Observation | Implication |
|---|---|
| A `Course` record represents one subject unit | Normalize Course → Module/Unit → Lesson during import |
| General progress uses `demo-learner` and learner-ID routes | Introduce authenticated ownership before shared deployment |
| Course DTOs embed answer keys/explanations | Separate public learning content, restricted assessment delivery and private grading data |
| Workshop ownership is separate from general learning ownership | Unify account/enrollment context while preserving history |
| Large generated TypeScript fallbacks duplicate server content | Generate revisioned delivery/offline artifacts from one source |
| Exercise models accumulate optional fields | Adopt type-specific contracts and renderer/grader registration |
| Subject-specific pages and authoring generators coexist with generic routes | Move content-specific behavior into packs and explicit extensions |
| Existing prerequisite validator rejects cross-subject prerequisites | Support explicitly authorized cross-course relationships in the generalized graph |
| Code runner deliberately works only in development | Production labs require a hardened isolated execution service |

Evidence: [progress client](/Users/bakhtiyarkussainov/university/client/src/app/core/progress.service.ts:60), [catalog exercise DTO](/Users/bakhtiyarkussainov/university/server/Features/Catalog/Dtos/ExerciseDto.cs:13), [catalog endpoints](/Users/bakhtiyarkussainov/university/server/Features/Catalog/CatalogEndpoints.cs), [grading](/Users/bakhtiyarkussainov/university/server/Features/Progress/ExerciseScorer.cs), [catalog validator](/Users/bakhtiyarkussainov/university/server/Features/Catalog/CatalogValidator.cs), [runner](/Users/bakhtiyarkussainov/university/server/Features/Workshop/Execution/DockerCodeRunner.cs).

Verification performed: `node --test scripts/validate-curriculum.test.mjs scripts/validate-exam-prep.test.mjs scripts/validate-study-studio.test.mjs` — **13 tests passed**. These check curriculum membership, ordering, prerequisites, revision coverage, objective/retry links, transfer pairs and studio consistency. This is not evidence that the entire application or all executable labs pass.

The [project instructions](/Users/bakhtiyarkussainov/university/AGENTS.md) freeze Data Engineering content. The proposed work leaves that content unchanged.

## AI-103 and AB-100 / Northstar

Root: `/Users/bakhtiyarkussainov/Documents/ChatGPT/AI-103 and AB-100 helper`.

Observed stack: React/Next with Vinext/Vite tooling. Main learner behavior is concentrated in `components/exam-app.tsx`; source banks are TypeScript modules. Exam content, objective references and selection ideas are reusable independently of React.

The question audit executed during this review passed with **598 original questions**, **24 case studies**, references on all 598 questions, and coverage of **64/64 AI-103** and **74/74 AB-100** outline bullets. Its collection counts were 158 core, 80 Foundry SDK, 48 Copilot Studio, 64 optional Claude-origin and 248 AB-100 questions. At that audit snapshot, cases had four questions each. These are snapshot counts, not a claim about later edits.

Reusable assets:

- Exam-specific objective hierarchies with direct documentation citations.
- Separate exam domains and specialty collections.
- Learn Map generated from question references and a terminology glossary.
- Unseen-first and least-recently-seen rotation, including complete scenario selection.
- Immediate reservation of selected questions when an attempt starts.
- Focused practice based on mistakes/objectives and per-objective history.
- A new common grading helper, mixed-format renderer, builders and exam composer.

During inspection, the main exam component changed from its older choice-only implementation to importing `QuestionInput`, `scoreQuestion` and `composeExam`. The updated source records earned/possible points and introduces changed session/case settings. Therefore, the initial observation that mixed formats were not wired into the main screen was superseded. Treat this as **active integration work** and verify its completed revision before migration.

Sources: [question model](</Users/bakhtiyarkussainov/Documents/ChatGPT/AI-103 and AB-100 helper/lib/questions.ts>), [grading](</Users/bakhtiyarkussainov/Documents/ChatGPT/AI-103 and AB-100 helper/lib/grading.ts>), [composition](</Users/bakhtiyarkussainov/Documents/ChatGPT/AI-103 and AB-100 helper/lib/session-composer.ts>), [rotation](</Users/bakhtiyarkussainov/Documents/ChatGPT/AI-103 and AB-100 helper/lib/question-rotation.ts>), [main screen](</Users/bakhtiyarkussainov/Documents/ChatGPT/AI-103 and AB-100 helper/components/exam-app.tsx>).

Generalization gaps:

| Observation | Implication |
|---|---|
| Exam IDs, domains, tracks and configuration are code constants | Move them into versioned course packs and exam blueprints |
| Grading runs in the browser with bundled keys | Move authoritative grading and release policy to the backend |
| Completed history and rotation are in localStorage | Add persistent server attempts and cross-device ownership |
| Active answers and countdown are component state in the inspected flow | Persist active attempts and use server deadlines |
| Objective references use published text strings | Introduce stable objective IDs with revisioned wording |
| New grading awards points per part | Make item weighting and partial credit explicit across subjects |
| Composer can top up from other domains; focused sessions can widen scope | Add strict/relaxed constraints and explicit shortage reporting |
| Content checks contain Microsoft-specific terminology/link rules | Keep generic validation in the framework and subject rules in packs |

Verification performed: `node --experimental-strip-types scripts/audit-question-bank.mjs` and `node --experimental-strip-types scripts/audit-question-rotation.mjs` both passed at the audit snapshot. The first tests the original bank's structure and content heuristics; the second tests rotation. They do not prove mixed-format browser integration, scientific scoring calibration, factual correctness of every explanation, or continued validity after concurrent edits.

## Implementation decision

Use Orbit as a donor for Angular/.NET structure, educational components and server assessment patterns. Use Northstar as a donor for content/objective mapping and exam behavior. Build a shared core in `eduframe` with explicit imports. Avoid merging both applications' state models and subject constants directly; validate the shared design through small representative slices first.
