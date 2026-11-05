# ISTQB Foundation and AI Testing Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make practice safe for mock papers, make readiness honest, and deepen the Foundation and AI Testing courses so the owner can pass both real exams.

**Architecture:** Two small, opt-in Core changes (`shortAttempts: 0`, `reserveFixedPapers`) ship first and leave other packs unchanged. Content comes from local Python generators in the git-ignored `references/istqb/_build/`, which write `packs/*.json` through the existing `ContentEngine` contract. Foundation ships as 1.3.0, 1.4.0 and 1.5.0, then AI Testing as 1.1.0.

**Tech Stack:** .NET 10, xUnit, Angular 22, Vitest, Playwright, Python 3 generators.

**Spec:** `docs/superpowers/specs/2026-11-05-istqb-foundation-ai-expansion-design.md`

## Global Constraints

- **The owner is the learner. Question text never appears** in chat, task reports, commit messages, docs or quoted logs. Scripts and reports print only counts, IDs and defect types. Never print a pack's questions, options or explanations to a console. Lessons and flashcards may be shown.
- Foundation and AI Testing end with: `reserveFixedPapers: true`, `readiness: { shortAttempts: 0, fullAttempts: 3, threshold: 75, lookbackDays: 90, minimumFreshPercent: 100 }`.
- `packs/istqb-ct-genai-1.json` and `packs/ai-testing-genai-practical.json` stay byte-identical: `git diff --exit-code` on both passes after every task.
- Papers stay exam format: single or Select TWO only, 4 or 5 options, exact scoring. CTFL 40 points, pass 26. CT-AI 44 points, pass 29. 60 and 75 minute versions.
- Existing lesson, question and family IDs stay stable. Option IDs follow option text. Every published change bumps the version.
- New IDs: bank questions `bank-<section>-<nn>` (for example `bank-4-2-1-07`) with `familyId` equal to the ID; labs `lab-<section>` (for example `lab-4-2-1`).
- Every bank question has `objectiveIds: ["ch-N", "<prefix>x.y.z"]`, the official K level, the weight from `IstqbExamSpec.Points`, and an explanation ending `Syllabus reference: <cert>, section x.y.z (p. N); <code> (Kn).`
- No official ISTQB question is copied. Samples calibrate style and difficulty only.
- Each pack file stays at or under 1,900,000 bytes (the engine limit is 2,000,000). If exceeded, write the pack minified rather than cutting content.
- Counts and word targets are targets; quality gates win over counts.
- Running the app: `bash scripts/dev.sh --data-dir <scratchpad>/x` with `LEARNFORGE_EXPORT_PYTHON=$PWD/.venv-flashcards/bin/python`. Never use `apps/api/data`. Never set `ConnectionStrings__Database` (the launcher overrides it). Confirm with `lsof` which `.db` is open before testing. Run browser tests with `LEARNFORGE_BROWSER_CHANNEL=chrome npx playwright test --reporter=line` from `apps/web`.
- `references/` is git-ignored: commit only `packs/`, `tests/`, `apps/`, `src/` and `docs/`. Do not push. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **A new release after the owner has started mocks.** Readiness is release-scoped and seen papers cannot be fresh again, so any later version restarts the streak with fewer usable papers. The owner must be warned first, and docs must say so. Pinned in Task 3.
2. **A numeric answer typed with `%`, a comma or extra decimals.** A correct answer must not be marked wrong because of format. Prompts state the format and tolerance allows rounding. Pinned in Task 4.
3. **Exporting the whole deck.** One overlong card produces a card-specific export error, so a full Anki or PDF export must succeed. Pinned in Task 12.
4. **Drills on a phone.** Matching and numeric drills at 390 px must not scroll the page sideways. Pinned in Task 9.
5. **A short mock taken with the short route off.** Its results page must not call it readiness evidence. Pinned in Task 1.

## File map

| Area | Files |
| --- | --- |
| Readiness rule | `ReadinessEvaluator.cs`, `ContentEngine.cs`, `ContentLinter.cs`, `AttemptService.cs`, `NextStepPlanner.cs`, `LearningRecordService.cs` |
| Mock protection | `Domain/Content/Pack.cs`, new `Services/Assessment/PaperReservation.cs`, `ExamComposer.cs`, `AttemptService.cs` |
| Web | `dashboard/dashboard-page.html`, `attempts/attempt-results.html` and their specs, `apps/web/e2e/istqb.spec.ts` |
| Tests | New `ReadinessRouteTests.cs`, `PaperReservationTests.cs`; `IstqbContentTests.cs`, `IstqbExamSpec.cs`, `IstqbApiTests.cs`, `NextStepPlannerTests.cs`, `TestApi.cs` |
| Generators (local) | `_build/`: new `build_ctfl.py`, `drills.py`, `guards.py`, `calc_ctfl.py`, `calc_ai.py`, `calibrate.py`, `test_generators.py`, `ctfl_*.py`, `ai_bank.py`, `ai_labs.py`, `ai_cards.py`; extended `common.py`, `ai_spec.py`, `build_ai.py`, `ai_lessons.py` |

---

### Task 1: Full-mock-only readiness rule

**Files:**
- Create: `tests/LearnForge.Tests/ReadinessRouteTests.cs`
- Modify: `src/LearnForge.Core/Domain/Readiness/ReadinessEvaluator.cs:12`, `src/LearnForge.Core/Services/Content/ContentEngine.cs:265`, `src/LearnForge.Core/Services/Content/ContentLinter.cs:222`, `apps/api/Services/Attempts/AttemptService.cs` (the `a.Eligible =` line in `Finish`), `apps/web/src/app/dashboard/dashboard-page.html:128-141`, `apps/web/src/app/attempts/attempt-results.html`, `tests/LearnForge.Tests/TestApi.cs`, `docs/assessment.md`, `docs/authoring.md`
- Test: also `apps/web/src/app/dashboard/dashboard-page.spec.ts`, `apps/web/src/app/attempts/attempt-mock-result.spec.ts`

**Interfaces:**
- Produces: `ReadinessPolicy.ShortAttempts == 0` means the short route is off. `TestApi.WithFixedPaper(Pack pack, int count = 5) -> Pack` adds a full-size fixed blueprint `paper-x` over the first `count` standalone questions with distinct families, with `ObjectiveIds` set to their distinct first objectives, `RequiredKinds = []` and `Minutes = 10`.

- [ ] **Step 1: Write the failing tests** in `ReadinessRouteTests(ApiFactory factory)`

```csharp
[Fact] public void A_zero_required_track_is_never_met()
{
    var r = ReadinessEvaluator.Evaluate([], new(ShortAttempts: 0), Now);
    Assert.False(r.Ready); Assert.False(r.Short.Met); Assert.Equal(0, r.Short.Required);
}
[Fact] public void Short_mocks_cannot_declare_readiness_when_the_route_is_off()
    => Assert.False(ReadinessEvaluator.Evaluate(Mocks(AssessmentSize.Short, 20, 100m), new(ShortAttempts: 0), Now).Ready);
[Theory, InlineData(31, true), InlineData(30, false)]
public void Three_full_mocks_use_the_strict_bar(int correct, bool ready)
    => Assert.Equal(ready, ReadinessEvaluator.Evaluate(Mocks(AssessmentSize.Full, 3, correct * 100m / 40), new(ShortAttempts: 0, Threshold: 75), Now).Ready);
```

Also add:
- `An_ineligible_full_mock_breaks_the_streak`: three at 100%, newest with `Eligible: false`, so `Ready` is false.
- `Short_route_off_needs_a_full_blueprint`: `ContentEngine.Validate` has no `readiness` error with a full blueprint, has one with only short blueprints, and has one for `ShortAttempts: -1`.
- `Lint_ignores_the_short_route_when_it_is_off`: `ContentLinter.Lint` reports no LF222 for a pack whose full route is feasible.
- `A_short_mock_is_not_eligible_when_the_route_is_off`: publish `WithFixedPaper(reasoning-foundations)` with `Id = "route-off"` and `ShortAttempts = 0`, complete a short mock, assert `summary.eligible` false and `summary.freshPercent` 100.

Web specs:
- Dashboard `hides the short mock track when its required count is 0`: one `.readiness-track`, no text `consecutive short mocks`, no text `Either streak qualifies`.
- Attempt results `does not call a short mock readiness evidence when it is not eligible`: summary with `eligible: false, freshPercent: 100`, `size: 'short'`, `mode: 'mock'`. Text contains `Not counted toward readiness`, not `Used for exam readiness`.

- [ ] **Step 2: Run them and confirm they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~ReadinessRouteTests"` and `npm test --prefix apps/web -- --watch=false`
Expected: FAIL. `A_zero_required_track_is_never_met` fails on `Ready`, proving the `0 == 0` trap.

- [ ] **Step 3: Implement**

- `ReadinessEvaluator`: `Met` becomes `required > 0 && streak == required`.
- `ContentEngine`: bound becomes `r.ShortAttempts is < 0 or > 20`. Add `if (r.ShortAttempts == 0 && !p.Blueprints.Any(b => b.Size == AssessmentSize.Full)) Error("readiness", "A readiness policy with shortAttempts 0 needs a full blueprint.");`
- `ContentLinter`: build `plans` only from blueprints whose attempt count is above 0.
- `AttemptService.Finish`: `a.Eligible` also requires `a.Size != AssessmentSize.Short || s.Readiness.ShortAttempts > 0`.
- Dashboard: wrap the short track in `@if (r.short.required > 0)`. Show `Either streak qualifies.` only inside the same condition.
- Attempt results, `FULLY CORRECT` caption: `a.goal === 'readiness' ? (s.eligible ? 'Used for exam readiness' : 'Not counted toward readiness') : 'Independent correct answers'`.
- Attempt results, `FRESH QUESTIONS` caption: the final else becomes `s.freshPercent < 100 ? 'Repeated question families' : 'Not counted toward readiness'`.
- Docs: state in `assessment.md` and `authoring.md` that `shortAttempts` accepts 0, which switches the short route off.

- [ ] **Step 4: Run the full backend and web suites**

Run: `dotnet test LearnForge.slnx` and `npm test --prefix apps/web -- --watch=false`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src apps tests docs
git commit -m "Allow a pack to base readiness on full mocks only"
```

### Task 2: "Take a mock" points to an unseen full paper

**Files:**
- Modify: `src/LearnForge.Core/Domain/Learning/NextStepPlanner.cs:6,45`, `apps/api/Services/Learning/LearningRecordService.cs:100`
- Test: `tests/LearnForge.Tests/NextStepPlannerTests.cs`

**Interfaces:**
- Consumes: `ShortAttempts == 0` from Task 1.
- Produces: `NextStepPlanner.Plan(Pack pack, IReadOnlyCollection<ObjectiveMastery> mastery, IReadOnlySet<string> completedLessons, ReadinessResult? readiness, int limit = 3, IReadOnlySet<string>? seenFamilies = null)`.

- [ ] **Step 1: Write the failing tests**

Use `TestApi.LoadPack("istqb-ctfl-4") with { Readiness = new(ShortAttempts: 0) }`, all objectives proficient, readiness not ready.
- `Take_mock_points_to_the_first_unseen_full_paper`: empty seen gives `BlueprintId == "paper-a"`. Seen equal to paper A's families gives `"paper-b"`.
- `Take_mock_falls_back_to_the_first_full_paper_when_all_are_seen`: gives `"paper-a"`.
- `Take_mock_still_prefers_short_when_the_route_is_on`: default policy gives `"short"`.

- [ ] **Step 2: Run and confirm they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~NextStepPlannerTests"`
Expected: FAIL on the short-route-off cases.

- [ ] **Step 3: Implement**

When `pack.Readiness is { ShortAttempts: 0 }`, choose the first full blueprint with no family in `seenFamilies`, treating a blueprint without `QuestionIds` as unseen, and otherwise the first full blueprint. In `LearningRecordService`, load seen families only for such packs, using the same evidence query `AttemptService` uses (`db.Evidence` by user and pack, `FamilyId`).

- [ ] **Step 4: Run the full backend suite**

Run: `dotnet test LearnForge.slnx`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src apps tests
git commit -m "Point the mock next step at an unseen full paper"
```

### Task 3: Mock protection flag

**Files:**
- Create: `src/LearnForge.Core/Services/Assessment/PaperReservation.cs`, `tests/LearnForge.Tests/PaperReservationTests.cs`
- Modify: `src/LearnForge.Core/Domain/Content/Pack.cs`, `src/LearnForge.Core/Services/Assessment/ExamComposer.cs` (the non-fixed `groups` query), `apps/api/Services/Attempts/AttemptService.cs` (`standalone` in `Focused`), `docs/content-engine.md`, `docs/authoring.md`, `docs/assessment.md`

**Interfaces:**
- Consumes: `TestApi.WithFixedPaper` from Task 1.
- Produces: `Pack.ReserveFixedPapers` (`bool`, default false, `JsonIgnore` when default, JSON `reserveFixedPapers`). `public static class PaperReservation { public static HashSet<string> HiddenFamilies(Pack pack, IReadOnlySet<string> seenFamilies); }` returns families of questions in any blueprint with `QuestionIds`, minus seen, and is empty when the flag is false.

- [ ] **Step 1: Write the failing tests** in `PaperReservationTests(ApiFactory factory)`

Build the pack with `WithFixedPaper(reasoning-foundations) with { ReserveFixedPapers = true }`.
- `Composed_sessions_exclude_unseen_paper_families`: over 20 seeds on `short`, no chosen family is in the paper.
- `A_seen_paper_family_returns_to_practice`: `HiddenFamilies` omits a family once it is seen.
- `The_flag_is_omitted_from_json_when_off`: `Assert.DoesNotContain("reserveFixedPapers", Json.Write(demo))`, and old release JSON still reads.
- `A_fixed_paper_still_starts`: `Compose` on `paper-x` returns its IDs.
- `An_undersized_practice_bank_fails_validation`: `TestApi.LoadPack("istqb-ct-genai-1") with { ReserveFixedPapers = true }` gives a non-empty `ContentEngine.Validate`.
- `Practice_leaves_the_paper_fresh_and_eligible` (API): publish the pack, complete a short learn session and objective-focused practice, then a `paper-x` mock. Assert `freshPercent == 100` and `eligible == true`.
- `Mock_mistakes_reach_mistake_practice` (API): fail `paper-x`, start `focus: "mistakes"`, and assert the IDs include a paper question.
- `A_new_release_restarts_the_streak` (API, Review Focus 1): after an eligible `paper-x` mock, publish `Version + ".1"`. `GET /api/me/dashboard` gives `courses[].goal.readiness.full.streak == 0`, and completing `paper-x` again gives `freshPercent == 0` and `eligible == false`.

- [ ] **Step 2: Run and confirm they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~PaperReservationTests"`
Expected: build failure on `ReserveFixedPapers`.

- [ ] **Step 3: Implement**

Add the property and helper. In `ExamComposer.Compose`, compute hidden families and add `.Where(g => g.All(q => !hidden.Contains(q.FamilyId)))` to the non-fixed `groups` query. In `AttemptService.Focused`, add the same filter to `standalone`. Document the flag, and that any new release restarts the readiness streak, in `content-engine.md`, `authoring.md` and `assessment.md`.

- [ ] **Step 4: Run the full backend suite and check other packs**

Run: `dotnet test LearnForge.slnx`, then `git diff --exit-code packs/`
Expected: all pass, no pack changed.

- [ ] **Step 5: Commit**

```bash
git add src apps tests docs
git commit -m "Add an opt-in flag that keeps unseen mock papers out of practice"
```

### Task 4: ISTQB content tests accept drills and enforce the new floors

**Files:**
- Modify: `tests/LearnForge.Tests/IstqbContentTests.cs:41-55`, `tests/LearnForge.Tests/IstqbExamSpec.cs`, `tests/LearnForge.Tests/TestApi.cs:48`

**Interfaces:**
- Produces: `IstqbExamSpec` trailing optional parameters `bool ReservesPapers = false`, `int MinK3Independent = 0`, `int MinOtherIndependent = 0`, `(int[] Chapters, int Percent)? HeavyChapters = null`. `TestApi.Correct` returns `new([], [], q.Grading.Correct[0])` for `Numeric` and `CodeOutput`.

- [ ] **Step 1: Write the failing tests**

- `Practice_floors_and_readiness_policy_hold` (skip when `!spec.ReservesPapers`): `ReserveFixedPapers` true; `Readiness == new(0, 3, 75, 90, 100)`. A question is independent when its family is in no paper. Every K3 objective has at least `MinK3Independent`, and at least half of those are single or multiple. Every other syllabus objective has at least `MinOtherIndependent`.
- `Ten_short_sessions_leave_every_paper_unseen` (same skip): compose `short` with 10 seeds, adding seen families each time, and assert none is in a paper.
- `Pack_source_stays_within_budget` (all three packs): file length at most 1,900,000.
- `Numeric_drills_state_their_format` (Review Focus 2): every numeric question's prompt contains `Enter a number only` or `Enter a whole number only`, and any key with decimals has `Grading.Tolerance > 0`.
- `Heavy_chapters_get_their_share` (skip when `HeavyChapters` is null): words in `section-*` and `lab-*` lessons whose first objective is a listed chapter are at least `Percent`% of all such words.

Narrow the existing `Assert.All(pack.Questions, ...)`. Kind, `Exact` policy, option counts and `SelectCount` apply to paper questions and to bank single or multiple questions. Objective count, K level and weight apply to all. `Grader.Score(q, TestApi.Correct(pack, q.Id)).Earned == q.Weight` applies to all.

- [ ] **Step 2: Run them**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~Istqb"`
Expected: PASS with `ReservesPapers` false everywhere. To prove the floors bite, temporarily set `Ctfl4` to `ReservesPapers: true, MinK3Independent: 20, MinOtherIndependent: 3`, see FAIL, then revert.

- [ ] **Step 3: Commit**

```bash
git add tests
git commit -m "Let ISTQB content tests cover drills and practice floors"
```

### Task 5: Refresh references and calibrate against official samples

**Files (local, not committed):**
- Create: `references/istqb/ctfl-4/` official Sample Exams A-D (questions and answers), `ISTQB_Exam_Structures_and_Rules_Tables_v1.20.pdf`, `references/istqb/_build/calibrate.py`, `references/istqb/ctfl-4/calibration.json`, `calibration.md`
- Modify: `references/istqb/ctfl-4/manifest.json`, `references/istqb/ct-ai-2/manifest.json`, `README.md` in both

**Committed:** `tests/LearnForge.Tests/IstqbExamSpec.cs` comment and values, `docs/istqb-preparation.md`, `docs/istqb-ct-ai.md`.

**Interfaces:**
- Produces: `calibration.json` for CTFL and CT-AI, as `{ "<pack>": { "k1|k2|k3": { "medianWords": n, "p25": n, "p75": n } } }`, counting the stem plus all options.

- [ ] **Step 1: Download**

Find links on the [CTFL page](https://istqb.org/certifications/certified-tester-foundation-level-ctfl-v4-0/) (Sample A and B v1.7, C v1.6, D v1.5, Tables v1.20) and record each URL, version, retrieval date and SHA-256 in the manifest.

- [ ] **Step 2: Compare Tables v1.19 and v1.20**

Extract text with PDFKit as the library README describes. Compare the CTFL and CT-AI tables with `IstqbExamSpec.Ctfl4` and `CtAi2`. Update values and the header comment if anything changed. Report changes as table facts, never sample questions.

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~Istqb"`
Expected: PASS.

- [ ] **Step 3: Write `calibrate.py`**

It prints only statistics. K levels come from the official answer documents. Also compute the same statistics for current paper questions, and print how many per paper fall outside the official p25-p75 band. Record stem patterns and distractor style in `calibration.md` in general terms, with no quoted text.

- [ ] **Step 4: Commit**

```bash
git add tests docs
git commit -m "Check ISTQB exam rules against tables v1.20"
```

### Task 6: Foundation generator overlay and guards

**Files (local):**
- Create in `references/istqb/_build/`: `baseline/istqb-ctfl-4-1.2.0.json`, `drills.py`, `guards.py`, `calc_ctfl.py`, `build_ctfl.py`, `test_generators.py`, empty `ctfl_rewrites.py`, `ctfl_bank_k3_ch4.py`, `ctfl_bank_k3_ch5.py`, `ctfl_bank_other.py`, `ctfl_labs.py`, `ctfl_lessons.py`, `ctfl_cards.py`

**Interfaces:**
- Produces in `drills.py`:
  - `numeric(prompt, value, decimals, explanation)` appends `Enter a number only, rounded to N decimal place(s).`, or `Enter a whole number only.` when `decimals` is 0, and sets `tolerance` to half the last unit.
  - `sequence(prompt, steps, explanation)` gives policy `partial`, with `correct` as option IDs in order and 3-8 steps.
  - `matching(prompt, pairs, extras, explanation)` gives slots `s1...`, a shared bank with at least one extra, and policy `partial`.
  - `emit(qid, lo, q, cert_label) -> dict` produces the record shape in `common.emit`, with `selectCount` 0 for numeric, and reuses `common.single` and `common.two`.
- Produces in `guards.py`: `overlap(text, prompt) -> float`, the share of the prompt's word trigrams found in `text`. `check_overlap(candidates, paper_prompts, limit=0.5)` exits non-zero listing IDs only.
- Produces in `calc_ctfl.py`: pure functions `partitions`, `bva_two`, `bva_three`, `decision_rules`, `transition_coverage`, `statement_coverage`, `branch_coverage`, `three_point(a, m, b)`, `ratio_estimate`, `extrapolate`, `prioritize`.
- Produces in `build_ctfl.py`: `python3 -I build_ctfl.py [--version V]` applies the modules in order, writes `packs/istqb-ctfl-4.json` and `ctfl-4/source-map.json`, and prints counts only.

- [ ] **Step 1: Write failing tests in `test_generators.py`**

`calc_ctfl` against hand-worked values (for example `three_point(2, 4, 12) == 5` and integer range 10-20 gives two-value boundaries `{9, 10, 20, 21}`); `numeric` tolerance and format text; `overlap` on identical text is 1.0 and on unrelated text is 0.0.

Run: `python3 -I -m unittest test_generators` from `_build/`
Expected: FAIL.

- [ ] **Step 2: Implement the helpers until the tests pass**

Run the same command. Expected: PASS.

- [ ] **Step 3: Prove the overlay round-trips**

With empty modules and no `--version`, run `python3 -I build_ctfl.py`, then `git diff --exit-code packs/istqb-ctfl-4.json`.
Expected: no change. Fix serialization until this holds. Update the local project note on the pack builder to say CTFL is now generated.

No commit: everything here is local.

### Task 7: Foundation K3 bank, chapter 4

**Files (local):** `references/istqb/_build/ctfl_bank_k3_ch4.py`

**Interfaces:**
- Consumes: `drills`, `calc_ctfl`, `guards` from Task 6, `calibration.json` from Task 5.

- [ ] **Step 1: Author 100 questions, 20 each for FL-4.2.1, 4.2.2, 4.2.3, 4.2.4 and 4.5.3**

IDs `bank-4-2-1-01` onward. At least 10 per objective are exam format. The rest are numeric, sequence or matching drills. Exam-format items sit in the calibrated K3 band. Keys come from `calc_ctfl` calls, not typed literals. Distractors model real mistakes (off-by-one boundary, omitted invalid partition, wrong rule count, missed transition). Scenarios, numbers and domains differ from paper questions.

- [ ] **Step 2: Build and check**

Run `python3 -I build_ctfl.py`, then `dotnet run --project tools/cli -- lint packs/istqb-ctfl-4.json | grep -oE 'LF[0-9]+' | sort | uniq -c`.
Expected: the build exits 0 with no overlap failures, and lint shows only LF220 or nothing. Do not commit the pack yet: it stays unpublished until Task 9.

### Task 8: Foundation K3 bank, chapter 5

**Files (local):** `references/istqb/_build/ctfl_bank_k3_ch5.py`

- [ ] **Step 1: Author 60 questions, 20 each for FL-5.1.4, 5.1.5 and 5.5.1**

Same rules as Task 7. Estimation covers ratios, extrapolation, Wideband Delphi and three-point. Prioritization covers risk, coverage and dependency orders. Defect reports use realistic incident narratives, asking which field or statement is missing or best.

- [ ] **Step 2: Build and check** as in Task 7. Expected: the same.

### Task 9: Foundation K1/K2 bank and release 1.3.0

**Files:**
- Local: `references/istqb/_build/ctfl_bank_other.py`
- Committed: `packs/istqb-ctfl-4.json`, `tests/LearnForge.Tests/IstqbExamSpec.cs`, `tests/LearnForge.Tests/IstqbApiTests.cs`, `apps/web/e2e/istqb.spec.ts`, `docs/istqb-preparation.md`

**Interfaces:**
- Consumes: spec parameters from Task 4, flag from Task 3, rule from Task 1.

- [ ] **Step 1: Author the K1/K2 bank**

At least 3 independent questions per K1/K2 objective. Allocation, which also satisfies lint LF221's 0.75x-1.5x chapter bounds:

| Chapter | New questions |
| --- | --- |
| 1 | 32 |
| 2 | 23 |
| 3 | 18 |
| 4 (K1/K2) | 24 |
| 5 (K1/K2) | 35 |
| 6 | 8 |

Coverage-percentage items in 4.3 use `numeric` with `statement_coverage` and `branch_coverage`. If LF221 appears, add to the under-share chapter rather than suppressing the warning.

- [ ] **Step 2: Set the release values and build**

In `build_ctfl.py`: version `1.3.0`, updated description counts, `reserveFixedPapers: true`, the readiness block from Global Constraints. Run with `--version 1.3.0`. Set `Ctfl4` in `IstqbExamSpec` to `ReservesPapers: true, MinK3Independent: 20, MinOtherIndependent: 3` and the new question count.

- [ ] **Step 3: Add the end-to-end proof**

- `IstqbApiTests.Practice_leaves_every_paper_fresh`, a `[Theory]` over pack ID, K3 objectives and pass points, starting with `istqb-ctfl-4` and 26: a learner completes `short` plus objective-focused practice on every K3 objective, then `paper-a` as a mock. Assert `freshPercent == 100`, `eligible == true` and the pack's `passPoints`.
- `apps/web/e2e/istqb.spec.ts` (Review Focus 4): open a matching and a numeric bank question at 390x844 and assert `document.documentElement.scrollWidth <= innerWidth`. If it fails, fix the layout.

- [ ] **Step 4: Independent review**

Dispatch a fresh reviewer subagent over all new K3 questions and 10% of the rest, checking each against the syllabus. It reports only `ID: wrong-key | off-syllabus | ambiguous | giveaway | near-duplicate-of-paper`. Fix the defects and rebuild.

- [ ] **Step 5: Verify**

Run: `dotnet test LearnForge.slnx`; `make check-content > <scratchpad>/cc.log 2>&1` with codes counted as in Task 7; `git diff --exit-code packs/istqb-ct-genai-1.json packs/ai-testing-genai-practical.json`; the browser suite under the Global Constraints setup.
Expected: all pass.

- [ ] **Step 6: Update docs and commit**

Replace the "preserve three unopened papers" advice, the 90% rule and the short-mock route in `docs/istqb-preparation.md`. Record validation results.

```bash
git add packs/istqb-ctfl-4.json tests apps docs
git commit -m "Release Foundation 1.3.0 with protected mocks and a larger practice bank"
```

- [ ] **Step 7: Hand off to the owner**

Say that practice is now safe, and to study on a new account in a new data folder: `bash scripts/install.sh --no-install --data-dir .learnforge/data/istqb`.

### Task 10: Foundation K3 labs

**Files (local):** `references/istqb/_build/ctfl_labs.py`

- [ ] **Step 1: Author eight lab lessons, 1,000-1,400 words each**

`lab-4-2-1`, `lab-4-2-2`, `lab-4-2-3`, `lab-4-2-4`, `lab-4-5-3`, `lab-5-1-4`, `lab-5-1-5`, `lab-5-5-1`. Placed after `section-4-2`, `section-4-5`, `section-5-1` and `section-5-5` respectively. Title form `Lab · <skill>`. `objectiveIds: ["ch-N", "fl-x.y.z"]`. Blocks: `text` method steps; three `example` worked cases of rising difficulty; `callout` common traps; an `example` exercise with a clearly labelled worked solution. Worked values come from `calc_ctfl`.

- [ ] **Step 2: Build and check**

`guards.check_overlap` runs on every lab block. Build and lint as in Task 7. Expected: pass.

### Task 11: Foundation lesson depth and study route

**Files (local):** `references/istqb/_build/ctfl_lessons.py`

- [ ] **Step 1: Deepen chapter 4 and 5 section lessons**

Add `text` and `example` blocks where the syllabus has the most official minutes. Lesson IDs stay stable. Target: about 30,000 total lesson words, with chapters 4-5 at 60% or more of `section-*` and `lab-*` words.

- [ ] **Step 2: Add two lessons before `final-revision`**

`commonly-confused` (paired distinctions such as test level and test type, confirmation and regression, verification and validation) and `exam-technique` (time per question, Select TWO, reading qualifiers like BEST, when to return later).

- [ ] **Step 3: Add the study route to `start-here`**

An 8-week route with a 4-week variant, as plain text:
- Weeks 1-5: chapters, labs, daily flashcards and practice.
- Week 6: paper A.
- Weeks 7-8: more papers and weak-topic practice.
- Final days: official Sample Exams A-D outside the app.

It states the ready bar (31/40 on three fresh papers in a row, with Mock pass shown separately at 26/40), and that later course updates restart the streak.

- [ ] **Step 4: Build and check** as in Task 7. Expected: pass.

### Task 12: Foundation flashcards and release 1.4.0

**Files:**
- Local: `references/istqb/_build/ctfl_cards.py`
- Committed: `packs/istqb-ctfl-4.json`, `tests/LearnForge.Tests/IstqbExamSpec.cs`, `tests/LearnForge.Tests/IstqbContentTests.cs`, `apps/web/e2e/istqb.spec.ts`, `docs/istqb-preparation.md`

- [ ] **Step 1: Author about 200 notes**

Decks `keywords` (each syllabus chapter keyword, using `basic` or `reversible`) and `lists` (principles, activities, review roles, quality characteristics and similar, using `cloze`). `objectiveIds` are `["ch-N", "fl-x.y.z"]`, `language: "en"`, tags by chapter. Every objective has at least one note. Fronts are recall prompts, not paper questions; `check_overlap` runs on them.

- [ ] **Step 2: Write the failing export test**

`IstqbContentTests.Flashcards_cover_every_objective` (skip for packs without decks): every objective appears in at least one note.

For Review Focus 3, run outside the test suite:
`dotnet run --project tools/cli -- export-flashcards packs/istqb-ctfl-4.json --format apkg --out <scratchpad>/anki`
and the same with `--format pdf --paper a4 --mode duplex --binding long --out <scratchpad>/pdf`.
Expected: both succeed for the whole pack. Split any card reported as overlong.

- [ ] **Step 3: Release**

Build `--version 1.4.0`. Set `Ctfl4` to `Lessons: 34`, the new question count and `HeavyChapters: ([4, 5], 60)`. Update the lesson count to 34 in `apps/web/e2e/istqb.spec.ts`. Add a flashcard deck check to that spec's journey: the course shows flashcards and a review starts.

- [ ] **Step 4: Review lessons and verify**

A reviewer subagent checks labs and new lesson blocks against the syllabus. Run the verification from Task 9 Step 5. Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add packs/istqb-ctfl-4.json tests apps docs
git commit -m "Release Foundation 1.4.0 with skill labs, deeper lessons and flashcards"
```

### Task 13: Foundation mock realism and release 1.5.0

**Files:**
- Local: `references/istqb/_build/ctfl_rewrites.py`
- Committed: `packs/istqb-ctfl-4.json`, `docs/istqb-preparation.md`

- [ ] **Step 1: Rewrite weak paper questions**

Rewrite in place the 56 paper K3 questions, plus any K1/K2 paper item outside the official p25-p75 band for its K level per `calibration.json`. `ctfl_rewrites.py` maps question ID to new content. IDs, family IDs, objectives, K level and paper membership do not change. Keys for calculations come from `calc_ctfl`. Do not lengthen items just to hit the band: added words must carry scenario information an official item would.

- [ ] **Step 2: Add the generator gate**

`build_ctfl.py` fails if a paper's median words for any K level falls outside the official p25-p75 band, or if `check_overlap` finds a rewritten prompt in any lesson, card or bank question. It prints IDs only.

- [ ] **Step 3: Release and verify**

Build `--version 1.5.0`. Have a reviewer subagent check all rewritten questions, reporting by ID and defect type only. Run the verification from Task 9 Step 5, including `Each_paper_matches_official_chapter_level_and_objective_group_rules`. Record the before-and-after statistics in the docs, with no question text.

- [ ] **Step 4: Commit and tell the owner**

```bash
git add packs/istqb-ctfl-4.json docs
git commit -m "Release Foundation 1.5.0 with papers calibrated to official samples"
```

Tell the owner Foundation is frozen: no further version will be published without warning them first, because a new release restarts the streak (Review Focus 1).

### Task 14: AI Testing generator extensions and gap review

**Files (local):**
- Modify in `references/istqb/_build/`: `common.py`, `ai_spec.py`, `build_ai.py`
- Create: `baseline/istqb-ct-ai-2-1.0.0.json`, `references/istqb/ct-ai-2/gap-review.md`, empty `ai_bank.py`, `ai_labs.py`, `ai_cards.py`

**Committed:** `docs/istqb-ct-ai.md`.

**Interfaces:**
- Produces in `common.py`: optional `Spec` attributes with defaults that reproduce today's output: `bank` (LO to list of bank questions), `decks`, `reserve_fixed_papers = False`, `readiness = {current block}`. `ai_spec.rotation(lo, paper)` covers papers `a`-`f`.

- [ ] **Step 1: Prove defaults are unchanged**

Add the optional attributes. Run `python3 -I build_ai.py` and `python3 -I build_genai.py`, then `git diff --exit-code packs/istqb-ct-ai-2.json packs/istqb-ct-genai-1.json`.
Expected: no change.

- [ ] **Step 2: Run the gap review**

Compare the full syllabus text (enumerations, keywords, lists) with lessons and questions, the way Foundation 1.2.0's review did. Write `gap-review.md` listing untested topics and question IDs with defects, by topic and ID only. Summarize in `docs/istqb-ct-ai.md`. Apply lesson gaps in Task 15 and question defects in Task 16.

- [ ] **Step 3: Commit**

```bash
git add docs/istqb-ct-ai.md
git commit -m "Record the AI Testing syllabus gap review"
```

### Task 15: AI Testing labs and depth

**Files (local):** `references/istqb/_build/ai_labs.py`, `ai_lessons.py`, `calc_ai.py`, `test_generators.py`

- [ ] **Step 1: Author four labs, 1,200-1,600 words each**

`lab-3-3-1` (metrics), `lab-4-2-2` (red teaming), `lab-5-1-5` (dataset constraints), `lab-6-1-5` (metamorphic testing), placed after the owning section lesson. Same block structure as Task 10. Confusion-matrix values come from a small `calc_ai.py` with tests in `test_generators.py`.

- [ ] **Step 2: Deepen lessons**

Apply `gap-review.md` findings and deepen chapter 3. Target: about 29,000 total lesson words, with chapter 3 at 28% or more of `section-*` and `lab-*` words.

- [ ] **Step 3: Build and check** as in Task 7, using `build_ai.py`. Expected: pass.

### Task 16: AI Testing bank, papers E and F, flashcards and release 1.1.0

**Files:**
- Local: `references/istqb/_build/ai_bank.py`, `ai_cards.py`, `ai_spec.py`, question modules
- Committed: `packs/istqb-ct-ai-2.json`, `tests/LearnForge.Tests/IstqbExamSpec.cs`, `tests/LearnForge.Tests/IstqbApiTests.cs`, `apps/web/e2e/istqb.spec.ts`, `docs/istqb-ct-ai.md`

- [ ] **Step 1: Author about 200 bank questions**

At least 25 per K3 skill (`bank-3-3-1-01` onward), at least half exam format, K3 weight 2. At least 3 independent per K2 objective, with extras weighted to chapters 3 and 6. Apply the question defects from `gap-review.md` in place.

- [ ] **Step 2: Author papers E and F**

80 questions appended to each objective's list so papers A-D keep their question IDs. Extend `rotation` so grouped objectives rotate across six papers and each paper meets the chapter 1 and chapter 3 group rules. Add a generator check that papers A-D differ from the baseline only by listed gap-review fixes.

- [ ] **Step 3: Author about 120 flashcard notes**

Same rules as Task 12, Step 1.

- [ ] **Step 4: Release**

Set `version 1.1.0`, `reserveFixedPapers: true` and the Global Constraints readiness block. Set `CtAi2` to `Papers: a-f`, `Lessons: 22`, the new question count, `ReservesPapers: true, MinK3Independent: 25, MinOtherIndependent: 3, HeavyChapters: ([3], 28)`. Update the `lessons: 18` value for this pack in `apps/web/e2e/istqb.spec.ts`. Extend the Theory from Task 9 to `istqb-ct-ai-2` with its four K3 objectives and `passPoints == 29`. In `docs/istqb-ct-ai.md`, replace the 90% rule, the short-mock route and the "keep three papers unopened" advice, and list six papers. In `docs/istqb-preparation.md`, change "four 44-point papers" to six.

- [ ] **Step 5: Review and verify**

Run the independent review as in Task 9 Step 4, the full-pack exports as in Task 12 Step 2, and the verification from Task 9 Step 5. Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add packs/istqb-ct-ai-2.json tests apps docs
git commit -m "Release AI Testing 1.1.0 with protected mocks, labs and six papers"
```

### Task 17: Final verification

**Files:** `docs/istqb-preparation.md`, `docs/istqb-ct-ai.md` (validation records)

- [ ] **Step 1: Run everything on a fresh scratch data folder**

Run: `dotnet test LearnForge.slnx`; `npm test --prefix apps/web -- --watch=false`; `npm run build --prefix apps/web`; `make check-content`; `make check-api-types`; `node --test scripts/tests/install.test.mjs scripts/tests/launcher.test.mjs scripts/tests/bootstrap.test.mjs`; the browser suite.
Expected: all pass. Confirm `apps/api/data/learnforge.db` still has SHA-256 prefix `a09d8627`.

- [ ] **Step 2: Record results and commit**

State that this is a technical review, not subject-expert accreditation.

```bash
git add docs
git commit -m "Record validation of the ISTQB expansion"
```

- [ ] **Step 3: Report to the owner**

Give counts only. Remind them that the backup in `apps/api/data/backups/2026-11-05-before-merge-run/` can be deleted, and that the generators exist only on this machine.
