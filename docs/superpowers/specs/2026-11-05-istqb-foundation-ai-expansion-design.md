# ISTQB Foundation + AI Testing expansion: design

Status: approved 2026-11-05. Build plan: [2026-11-05-istqb-foundation-ai-expansion.md](../plans/2026-11-05-istqb-foundation-ai-expansion.md).

## Context

The owner is the only learner. They have barely started the courses, want to sit ISTQB Foundation (CTFL) within about two months (no date yet), then AI Testing (CT-AI, which requires the Foundation certificate). **Success means passing the real exams.** Realistic mocks and an honest readiness signal matter more than polish or course size.

Measured problems in today's packs (`packs/istqb-ctfl-4.json` v1.2.0, `packs/istqb-ct-ai-2.json` v1.0.0):

- **Practice spoils mocks.** `ExamComposer.Compose` (non-fixed path) and `AttemptService.Focused` draw from every question, including fixed-paper ones. A mock counts toward readiness only at `FreshPercent >= 100` (`AttemptService.cs:202`). Simulated: one 20-question short session leaves about 0.5 of 7 Foundation papers and about 0.04 of 4 AI Testing papers fully unseen. Focused practice on BVA has 9 questions, 7 of them in papers.
- **Thin hands-on practice.** Practice-only K3 questions: CT-AI 4 (one per K3 skill); CTFL 13 across 8 K3 objectives. Only 40 CTFL and 43 CT-AI practice questions have a family independent of the papers.
- **Depth misplaced.** Lessons are about 16k and 19k words (about 1.5 hours of reading) against about 19 hours of official minimum course time. CTFL chapters 4-5 are 64% of official time but 47% of lesson text; CT-AI chapter 3 is 32% vs 23%.
- **CTFL realism is unverified.** CTFL questions average 40 words including options, against 79 for CT-AI. Official samples A-D are not in the local library.
- **Unused formats.** ISTQB packs use only single and multiple choice, and have no flashcards.
- **Source drift.** ISTQB now lists Exam Rules Tables v1.20; the library and `IstqbExamSpec` use v1.19. Syllabi are unchanged (CTFL v4.0.1, CT-AI v2.0).

Keyword coverage is mostly fine, so it is not a focus.

## Goals, non-goals, anti-goals

**Goals:** a safe practice loop that never burns unseen mocks; enough hands-on drilling on K3 skills; lesson depth that follows exam and syllabus weight; mocks at realistic difficulty; recall tools (flashcards); a clear 8-week route.

**Non-goals:** CT-GenAI and Practical QA content (they must keep working unchanged); study-planner or adaptive-practice software; rich-content block kinds (only text, callout, example and code exist, so use those); multi-question case-study groups in mock papers; more Foundation papers (seven is enough).

**Anti-goals, a failure even if everything compiles:**
- The owner is shown generated question text. Chat, docs, commit messages and reports use only counts, IDs and defect types. The owner may read lessons and flashcards.
- Lesson examples, flashcards or new practice questions paraphrase a paper question and quietly leak it.
- Official ISTQB questions are copied. Samples calibrate style and difficulty only.
- Papers lose exam fidelity: chapter counts, K-level counts, objective-group rules, points or pass marks.
- Word-count padding. Length is added only where exam or syllabus weight justifies it.

## Decisions by the owner

- **Sequence:** merge, then Foundation, then AI Testing. "AI Testing" means `istqb-ct-ai-2` only.
- **Ready score:** 31/40 or better on three fresh full mocks in a row (`readiness.threshold: 75`, which is compared strictly, so 75% itself does not count). Mock pass still shows separately at the real pass mark.
- **Study account:** a new account, so all seven Foundation papers count. Nothing is deleted. The reviewer reports that the existing account's one finished 20-question session already touched papers A-D, and the evidence ledger is append-only, so rewriting those questions would not reset their freshness. It must live in a new data folder (`bash scripts/install.sh --no-install --data-dir .learnforge/data/istqb`), because `apps/api/data/learnforge.db` holds a `french-b2-grammar` release that the merged code cannot read.
- **Short mocks:** only full mocks can declare readiness. Short sessions stay as timed practice and may contain drills.

## Part 1: merge (done)

`main` and `chinese` were merged into `istqb` on 2026-11-05 (commits `7bc13a2`, `30abfc4`), not pushed. The mock pass/fail panel, multi-line prompts and server-clock timer fix moved from the deleted `pages/` folder into `attempts/` and `authoring/`.

Verified on the merged tree: 255 backend tests, 65 web tests, 22 installer tests, 27 browser tests, `make check-content` and `make check-api-types`.

Two things the browser run needs on this machine:
- **`bash scripts/dev.sh --data-dir <scratch>`.** The launcher overrides `ConnectionStrings__Database`. Merged code cannot read `apps/api/data/learnforge.db`, because it holds a `french-b2-grammar` release using `shortAnswer` and `ignoreDiacritics` from the unmerged `french` branch.
- **`make setup-flashcards`, then `LEARNFORGE_EXPORT_PYTHON=$PWD/.venv-flashcards/bin/python`.** Flashcard export tests time out without it.

## Part 3: mock protection and full-mock readiness (app change)

**Full-mock-only readiness.** Allow `readiness.shortAttempts: 0` to switch the short route off:
- `ContentEngine.cs:231`: accept 0 for `ShortAttempts`. Reject a pack that sets 0 but has no full blueprint. `FullAttempts` stays at least 1.
- `ReadinessEvaluator.cs:12`: **a zero-required track must never be met.** Today `streak == required` is true at 0 == 0, which would report "ready" immediately. Add a regression test.
- `NextStepPlanner.cs:45`: when the short route is off, "take a mock" points to the first full paper whose families are still unseen, not `short`. This needs the seen families passed in from `LearningRecordService`.
- Web: hide the short track wherever readiness is displayed when its required count is 0.
- Packs that omit the setting keep the default of 5, so CT-GenAI and existing releases are unchanged.

**Mock protection.** New optional pack property `reserveFixedPapers` (default false, omitted from JSON when false, so existing releases and CT-GenAI/Practical QA are unaffected). When true, a question whose **family** belongs to any fixed paper (a blueprint with `questionIds`) is excluded from non-fixed composition and from focused practice, **unless the learner has already seen that family**.

- A small Core helper is shared by `ExamComposer.Compose` (`src/LearnForge.Core/Services/Assessment/ExamComposer.cs`) and `AttemptService.Focused` (`apps/api/Services/Attempts/AttemptService.cs`). The existing `seen` family set already comes from the evidence ledger.
- Mock mistakes re-enter practice once seen. Fixed-paper start is unchanged. Starting a paper in Learn mode stays available as an explicit choice.
- The compiler already composes every blueprint during validation, so an undersized practice bank fails at compile time. **The flag ships in the same pack release as the larger bank.**
- Tests: Core (exclusion, seen re-entry, flag off = identical selection, old release JSON still reads) and API (a short session and focused practice leave papers at 100% fresh; a paper is still readiness-eligible afterwards). Update `docs/content-engine.md`, `docs/authoring.md`, `docs/assessment.md`.

## Part 4: Foundation (`istqb-ctfl-4`)

**Pipeline.** CTFL has no generator (only CT-AI and CT-GenAI do). Add `references/istqb/_build/build_ctfl.py` as an overlay: baseline snapshot of v1.2.0 plus modules for rewrites, lessons, practice and cards, reusing `common.py` (`oid`, `single`, `two`, `block`, `lint_question`). It writes the pack and `ctfl-4/source-map.json`. Calculation keys are computed in the generator, with distractors modelling real mistakes.

**1.3.0: calibration, protection and practice bank**
- Download official samples A-D (questions and answers) and Rules Tables v1.20; record URLs and SHA-256 in the manifest. Diff v1.19 vs v1.20 for CTFL and CT-AI and update `IstqbExamSpec` if anything changed.
- Write `references/istqb/ctfl-4/calibration.md` with statistics only: words per question by K level, stem patterns, distractor style. These become numeric targets in the generator.
- About 300 new questions with new families, all outside papers:
  - At least 20 per K3 objective (FL-4.2.1 to 4.2.4, 4.5.3, 5.1.4, 5.1.5, 5.5.1), at least half in exam format and the rest numeric, sequence, matching or code-output drills.
  - At least 3 independent questions per K1/K2 objective, weighted to chapters 4-5.
- Enable `reserveFixedPapers`, and set `readiness.threshold: 75` and `readiness.shortAttempts: 0`.
- **Handoff once this release is live:** tell the owner practice is now safe and to create the new study account. Until then, lessons only.

**1.4.0: learning material and flashcards**
- Eight K3 labs as new lessons placed after their section: method, three worked examples of rising difficulty, common traps.
- Deepen existing chapter 4-5 lessons (IDs stable). Add a "commonly confused terms" lesson and an exam-technique lesson. Add the 8-week route, with a 4-week compressed variant, to `start-here` as plain text.
- Target about 30k lesson words, with chapters 4-5 at 60% or more of section text.
- About 200 flashcard notes in `flashcardDecks` (keywords and syllabus lists, including basic and cloze notes), with recall coverage of every objective.

**1.5.0: mock realism**
- Rewrite in place, keeping IDs and paper membership, the 56 paper K3 questions plus any K1/K2 items that miss the calibration targets. Must land before the owner opens any paper (week 6 of the route).
- Official samples stay untouched in the library for the owner's final-week rehearsal outside the app.

## Part 5: AI Testing (`istqb-ct-ai-2`), after Foundation

Extend the existing generator (`build_ai.py`, `ai_*.py`, `rebalance_ai.py`) and release as 1.1.0:
- Syllabus gap review like Foundation's 1.2.0 (never done for this pack), with fixes.
- Four K3 labs: metrics (AI-3.3.1), red teaming (AI-4.2.2), dataset constraints (AI-5.1.5), metamorphic testing (AI-6.1.5). Deeper chapter 3 (at least 28% of section text). About 29k lesson words.
- About 200 new questions: at least 25 per K3 skill, at least 3 independent per K2 objective.
- Papers E and F (six total), meeting every rule in `IstqbExamSpec.CtAi2`, sharing no families. About 120 flashcard notes. `reserveFixedPapers` on, `readiness.threshold: 75`, `readiness.shortAttempts: 0`.

## Constraints to preserve

- Releases are immutable, so each change bumps the version. Lesson, question and family IDs stay stable; option IDs follow option text. Importers go through `ContentEngine`.
- **Pack source limit is 2 MB** (`ContentEngine.MaxSourceBytes`, also the request-body limit). Projected about 1.6 MB for CTFL and 1.3 MB for CT-AI. Add a test budget of 1.9 MB; if exceeded, write the pack minified rather than cutting content.
- `IstqbContentTests` assumes every question is single or multiple with 4 or 5 options (lines 42-50): narrow those checks to paper questions and add drill checks. K-level and point-weight checks stay for all questions. Lint stays LF220-only, so LF230-233 must be clean. Update the counts in `IstqbExamSpec`.
- Generator guard: fail the build when new practice, lesson or flashcard text overlaps heavily with a paper question.
- `/references` is gitignored, so generators exist only on this machine. (Worth backing up; not part of this work.)

## Calls made by Claude (owner to check)

- `reserveFixedPapers` is an opt-in pack flag, not a global behaviour change.
- Short sessions may include drill kinds, which is safe now they can't declare readiness; full mocks stay exam-format only.
- The 31/40 bar and full-mock-only rule also apply to AI Testing. Readiness counts fully correct questions, so there it is still 31 of 40 questions, not points. CT-GenAI keeps its current rule.
- Short-route off is expressed as `shortAttempts: 0`, not a separate flag.
- Keep seven Foundation papers and harden them; AI Testing grows to six.
- Bank sizes, word targets and flashcard counts above are targets, not contracts. Quality gates win over counts.
- The study route is plain lesson text, not software.

## Verification

**End to end:** start the servers with `bash scripts/dev.sh --data-dir <scratchpad>/x`, never the owner's dev DB. The launcher overrides `ConnectionStrings__Database`, so confirm with `lsof` which `.db` is open. Then `cd apps/web && LEARNFORGE_BROWSER_CHANNEL=chrome npx playwright test`. Confirm in the browser: mock pass/fail panel, multi-line prompts, timer, flashcard review.

**Each content release:**
- Simulation with the real composer, starting from a clean account: after 10 short sessions and focused practice on every K3 objective, all papers remain 100% fresh and readiness-eligible.
- Readiness tests: no number of short mocks reports "ready"; three fresh full mocks at 31/40 do; one at 30/40 breaks the streak (`IstqbApiTests` and `docs/istqb-*.md`, which state the old 90% rule, are updated to match).
- `dotnet test --filter Istqb`; content CLI check and lint; byte size under budget.
- An independent reviewer subagent checks a sample of questions against the syllabus and reports defects by ID and type only.
- Calculation keys recomputed by script.
- Results are recorded in `docs/istqb-preparation.md` and `docs/istqb-ct-ai.md`, stating that this is a technical review, not subject-expert accreditation.
