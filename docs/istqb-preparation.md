# ISTQB Foundation 4.0 preparation

The `istqb-ctfl-4` hybrid pack provides English self-study for beginners using original examples and questions. It has 24 lessons, 64 individual syllabus objectives plus six chapter objectives, 160 questions and four non-overlapping 40-question papers. It aligns to the official CTFL syllabus v4.0.1, the errata revision for Foundation 4.0. It is independent material, not an official exam or accredited course.

## Study route

1. Read **Start here**, then study the 22 syllabus-section lessons in order. Solve each self-check before reading its solution.
2. Practise calculation and application techniques, then use the final revision checklist to explain the important distinctions in your own words.
3. Take paper A under timed conditions. Review wrong answers and guesses, revisit their objective lessons, then take B, C and D on separate occasions.

Untimed learning and 20-question short sessions draw from the same bank as the full papers. Practice therefore exposes questions that would otherwise be fresh. Preserve at least three unopened papers if you want fresh full-mock readiness evidence. Repeating a paper is useful revision but does not establish independent readiness.

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

## Content validation and maintenance

Run `make check-content` and `dotnet test LearnForge.slnx`. The content tests verify teaching/practice coverage of every objective, paper membership, group and cognitive distributions, exact scoring and the 60/75-minute versions. API tests cover pass boundaries, expiry, learning-mode behavior and preservation of thresholds across releases.

The bank intentionally retains lint warning **LF220** because the exam uses multiple-choice formats. All other quality warnings must be resolved; the content test rejects additional warning codes. The general linter is unchanged. Computational keys were checked through their worked arithmetic, and explanations describe why distractors do not satisfy the question. Structural validation is not independent subject-expert accreditation.

Content uses existing text, example and callout blocks. Models, boundaries and decision rules have textual descriptions; selection instructions are explicit. Browser verification covers lesson rendering, paper selection, scoring, review and a narrow-screen view. Keep question and lesson IDs stable and increment the pack version for published changes.

### Verification record

Reviewed by Codex on 2026-10-07 for pack `istqb-ctfl-4@1.0.0`, source SHA-256 `b19626d24a007f14436d447ebd803175c1f51ec72b701fc1ace80e508e13468f`. This is an authoring and technical review, not independent subject-expert accreditation.

Passed: 199 backend tests, 32 frontend unit tests, two Chromium browser journeys, the production Angular build and `make check-content`. Browser checks used macOS ARM64, Chromium 153, desktop and 390×844 mobile viewports. The mobile pass-result and explanation layout was visually inspected. Verification found and corrected UTC timestamp loss after SQLite reload and the timer's server-clock initialization; regression tests preserve these fixes. The sole retained content lint diagnostic is the documented LF220 format warning.

## Official references and attribution

- [CTFL certification and exam format](https://istqb.org/certifications/certified-tester-foundation-level-ctfl-v4-0/)
- [CTFL syllabus v4.0.1 — PDF](https://istqb.org/wp-content/uploads/2024/11/ISTQB_CTFL_Syllabus_v4.0.1.pdf)
- [Exam Structures and Rules Tables v1.19 — PDF](https://istqb.org/?download_id=3832&sdm_process_download=1)
- [ISTQB glossary](https://glossary.istqb.org/)

ISTQB and the authors listed in its official syllabus own those reference documents. Original LearnForge lessons and questions are provided under CC0-1.0; that license does not apply to ISTQB's reference materials or trademark. Official sample-exam questions have not been copied into the pack.
