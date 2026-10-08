# ISTQB Foundation 4.0 preparation

The `istqb-ctfl-4` hybrid pack provides English self-study for beginners using original examples and questions. It has 24 lessons, 64 individual syllabus objectives plus six chapter objectives, 224 questions (160 in fixed papers and 64 additional objective exercises) and four non-overlapping 40-question papers. It aligns to the official CTFL syllabus v4.0.1, the errata revision for Foundation 4.0. It is independent material, not an official exam or accredited course.

## Study route

1. Read **Start here**, then study the 22 syllabus-section lessons in order. Each now adds detailed concepts, a worked case, an exercise and a worked solution. Solve self-checks and exercises before reading solutions.
2. Practise calculation and application techniques, then use the final revision checklist to explain the important distinctions in your own words.
3. Take paper A under timed conditions. Review wrong answers and guesses, revisit their objective lessons, then take B, C and D on separate occasions.

Untimed learning and 20-question short sessions draw from the full 224-question bank, including the 160 questions used in full papers. Practice therefore exposes questions that would otherwise be fresh. Preserve at least three unopened papers if you want fresh full-mock readiness evidence. Repeating a paper is useful revision but does not establish independent readiness.

## Expanded material in version 1.1.0

All 22 syllabus-section lessons add four blocks each: detailed concepts, an original worked case, an exercise and its worked solution. Topics include traceability, lifecycle feedback, review responsibilities, each black-box technique, coverage interpretation, acceptance examples, risk response, reporting denominators and reproducible configuration evidence. Calculation exercises include full two-value and three-value boundary coverage, rule coverage, transition paths, weighted effort and uncertainty, and additional-coverage prioritization. Start and revision lessons also add practice guidance.

The bank adds 64 independently authored single-choice questions, one per numbered syllabus objective, with objective-appropriate K levels, exact scoring, explanatory distractor feedback and PDF citations. IDs use the practice prefix and distinct families. They supplement learning and short sessions; papers A-D retain their fixed 40-question membership. Lesson counts and existing IDs remain stable.

The original temperature boundary question now explicitly targets the valid partition boundary values, distinguishing its six values from full three-value coverage across all partition boundaries. Lesson examples show the full coverage calculation.

## Papers and scoring

| Paper | Questions | Standard time | Extended-time practice | Pass threshold |
| --- | ---: | ---: | ---: | ---: |
| A | 40 | 60 minutes | 75 minutes | 26 points |
| B | 40 | 60 minutes | 75 minutes | 26 points |
| C | 40 | 60 minutes | 75 minutes | 26 points |
| D | 40 | 60 minutes | 75 minutes | 26 points |

Standard and extended versions of a paper contain the same questions. Confirm eligibility for an actual non-native-language time allowance with the exam provider. Answer options shuffle; fixed question membership remains unchanged on repeats.

Single-choice questions require one answer. Select-N questions state the required count and award one point only when the complete correct set is selected. Unanswered and partly correct responses earn zero. Expiry automatically submits saved answers and shows the scored result with the expiry status.

Each paper has chapter counts 8/6/4/11/9/2 and cognitive counts 8 K1, 24 K2 and 8 K3. The independently transcribed objective groups in `IstqbContentTests` check the official learning-objective distribution and distinct-objective rules, not merely the chapter totals. The short session is balanced practice with approximate chapter weights, rather than a full certification simulation.

A **Mock pass** means at least 26/40 on this simulation. LearnForge's readiness rule remains separate: strictly above 90% on each of the latest three full mocks or five short mocks, with fresh families and completion before expiry. This is a practice recommendation, not a calibrated prediction of passing an external examination.

## Expansion validation

Version 1.1.0 was checked on 2026-10-08. Content validation passed with 24 lessons, 224 questions and 70 objectives. All three targeted ISTQB content tests passed, covering teaching/practice coverage, scoring, fixed-paper distributions and short-session composition. The question linter reports only the documented LF220 multiple-choice format warning. The reference map covers all 224 questions; no frontend or browser tests were rerun for this content expansion.

## Content validation and maintenance

Run `make check-content` and `dotnet test LearnForge.slnx`. The content tests verify teaching/practice coverage of every objective, paper membership, group and cognitive distributions, exact scoring and the 60/75-minute versions. API tests cover pass boundaries, expiry, learning-mode behavior and preservation of thresholds across releases.

The bank intentionally retains lint warning **LF220** because the exam uses multiple-choice formats. All other quality warnings must be resolved; the content test rejects additional warning codes. The general linter is unchanged. Computational keys were checked through their worked arithmetic, and explanations describe why distractors do not satisfy the question. Structural validation is not independent subject-expert accreditation.

Content uses existing text, example and callout blocks. Models, boundaries and decision rules have textual descriptions; selection instructions are explicit. Browser verification covers lesson rendering, paper selection, scoring, review and a narrow-screen view. Keep question and lesson IDs stable and increment the pack version for published changes.

### Verification record for the previous release

Reviewed by Codex on 2026-10-07 for pack `istqb-ctfl-4@1.0.0`, source SHA-256 `b19626d24a007f14436d447ebd803175c1f51ec72b701fc1ace80e508e13468f`. This is an authoring and technical review, not independent subject-expert accreditation.

Passed: 199 backend tests, 32 frontend unit tests, two Chromium browser journeys, the production Angular build and `make check-content`. Browser checks used macOS ARM64, Chromium 153, desktop and 390×844 mobile viewports. The mobile pass-result and explanation layout was visually inspected. Verification found and corrected UTC timestamp loss after SQLite reload and the timer's server-clock initialization; regression tests preserve these fixes. The sole retained content lint diagnostic is the documented LF220 format warning.

## Local syllabus references

The [reference library](../references/istqb/ctfl-4/README.md) contains the official CTFL v4.0.1 syllabus, exam rules v1.2, exam tables v1.19 and v4.0 release notes, downloaded on 2026-10-08. Its manifest records official URLs and SHA-256 checksums. The [source map](../references/istqb/ctfl-4/source-map.json) links every syllabus objective to its PDF section and page, lessons and practice questions, and records the full-paper blueprint.

Pack `istqb-ctfl-4@1.0.1` adds section/page references to all 22 section lessons and all 160 answer explanations, plus guidance to study the complete chapter content and keyword lists. All chapters 1-6 are examinable at K1, beyond the numbered objectives (syllabus section 0.5, p. 11). Use the downloaded documents and the library's authoring steps when maintaining lessons and exams. The verification record above applies to version 1.0.0; it does not describe validation of this reference update.

Reference-update validation on 2026-10-08: the content CLI passed for version 1.0.1 with 24 lessons, 160 questions and 70 objectives (64 syllabus objectives plus six chapters). Question K levels were compared with those extracted from the downloaded syllabus. No application test suites were rerun for this update.

## Official references and attribution

- [CTFL certification and exam format](https://istqb.org/certifications/certified-tester-foundation-level-ctfl-v4-0/)
- [CTFL syllabus v4.0.1 — PDF](https://istqb.org/wp-content/uploads/2024/11/ISTQB_CTFL_Syllabus_v4.0.1.pdf)
- [Exam Structures and Rules Tables v1.19 — PDF](https://istqb.org/?download_id=3832&sdm_process_download=1)
- [ISTQB glossary](https://glossary.istqb.org/)

ISTQB and the authors listed in its official syllabus own those reference documents. Original LearnForge lessons and questions are provided under CC0-1.0; that license does not apply to ISTQB's reference materials or trademark. Official sample-exam questions have not been copied into the pack.
