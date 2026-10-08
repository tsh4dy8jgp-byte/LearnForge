# ISTQB Foundation 4.0 preparation

The `istqb-ctfl-4` hybrid pack provides English self-study for beginners using original examples and questions. It has 24 lessons, 64 individual syllabus objectives plus six chapter objectives, 366 questions (280 in fixed papers and 86 practice questions) and seven non-overlapping 40-question papers. It aligns to the official CTFL syllabus v4.0.1, the errata revision for Foundation 4.0. It is independent material, not an official exam or accredited course.

## Study route

1. Read **Start here**, then study the 22 syllabus-section lessons in order. Each has detailed concepts, a worked case, an exercise, a worked solution and a **Syllabus details to know** list. Solve self-checks and exercises before reading solutions.
2. Practise calculation and application techniques, then use the final revision checklist to explain the important distinctions in your own words.
3. Take paper A under timed conditions. Review wrong answers and guesses, revisit their objective lessons, then take B to G on separate occasions.

Untimed learning and 20-question short sessions draw from the full 366-question bank, including the 280 questions used in full papers. Practice therefore exposes questions that would otherwise be fresh. 46 practice questions are variants of a paper question and share its family, so answering a variant also uses up that paper question's freshness. Preserve at least three unopened papers if you want fresh full-mock readiness evidence. Repeating a paper is useful revision but does not establish independent readiness.

## Gap analysis and expansion in version 1.2.0

Version 1.2.0 compares the lessons and bank with the full text of syllabus v4.0.1 and exam rules v1.2. Rule 4.1.4 makes every syllabus keyword examinable, and chapters 1-6 are examinable at K1 beyond the numbered objectives. Every objective was covered, but the analysis found these gaps:

- **Untested syllabus content.** Many enumerations and keywords appeared in no question, and often in no lesson either. Examples: ISO/IEC 25010 characteristics, acceptance-testing forms, integration strategies, the technical review and review leader, review planning and initiation, 3-value BVA coverage, all transitions coverage, guard conditions, decision-table notation, unconditional branches, fault attacks, INVEST and the 3 C's, Wideband Delphi, the Definition of Ready, project-risk categories, risk transfer, metric categories, baselines, defect statuses and several tool categories. Three of the seven principles were never the key of a question.
- **Guessable items.** About 30 questions used distractors unrelated to testing, such as salaries, office rental or lunch choices. Two keys (d-40, c-40) used content from the retired v3.1 syllabus, and c-30 repeated its key in the stem.
- **Overstated freshness.** 46 of the 64 practice questions paraphrased a paper question but had their own family, so practising them did not count against the paper question's freshness.
- **Format.** Select-TWO questions had four options; the official exam uses five.

Fixes in this release:

- Each of the 22 section lessons gains a **Syllabus details to know** block with the missing enumerations and definitions. Lesson IDs and counts are unchanged; revised lessons are flagged for learners rather than reset.
- 54 existing questions were edited in place: plausible syllabus-based distractors, corrected keys, and a fifth option for all 17 Select-TWO questions. Question IDs and paper membership are unchanged; option IDs follow the option text.
- The 46 practice variants now share the family of the paper question they paraphrase, giving 320 distinct families. `IstqbContentTests` checks that each shared family contains exactly one paper question with the same objectives.
- 142 new questions each target a previously untested sub-topic: 120 form papers E, F and G, and 22 extend practice. Calculation keys (EP and BVA sets, rule counts, coverage percentages, state models, estimates and prioritization orders) and code-coverage answers were recomputed by script during authoring. Papers E-G follow the same chapter, K-level and objective-group rules as A-D.
- Prompts may contain line breaks and indentation, so decision tables, state models and pseudocode render line by line. The web app's `.prompt-text` style preserves them; prompts remain plain text.

## Expanded material in version 1.1.0

All 22 syllabus-section lessons add four blocks each: detailed concepts, an original worked case, an exercise and its worked solution. Topics include traceability, lifecycle feedback, review responsibilities, each black-box technique, coverage interpretation, acceptance examples, risk response, reporting denominators and reproducible configuration evidence. Calculation exercises include full two-value and three-value boundary coverage, rule coverage, transition paths, weighted effort and uncertainty, and additional-coverage prioritization. Start and revision lessons also add practice guidance.

The bank adds 64 independently authored single-choice questions, one per numbered syllabus objective, with objective-appropriate K levels, exact scoring, explanatory distractor feedback and PDF citations. IDs use the practice prefix and distinct families. They supplement learning and short sessions; papers A-D retain their fixed 40-question membership. Version 1.2.0 later merged 46 of these into their paper question's family (see above). Lesson counts and existing IDs remain stable.

The original temperature boundary question now explicitly targets the valid partition boundary values, distinguishing its six values from full three-value coverage across all partition boundaries. Lesson examples show the full coverage calculation.

## Papers and scoring

| Paper | Questions | Standard time | Extended-time practice | Pass threshold |
| --- | ---: | ---: | ---: | ---: |
| A | 40 | 60 minutes | 75 minutes | 26 points |
| B | 40 | 60 minutes | 75 minutes | 26 points |
| C | 40 | 60 minutes | 75 minutes | 26 points |
| D | 40 | 60 minutes | 75 minutes | 26 points |
| E | 40 | 60 minutes | 75 minutes | 26 points |
| F | 40 | 60 minutes | 75 minutes | 26 points |
| G | 40 | 60 minutes | 75 minutes | 26 points |

Standard and extended versions of a paper contain the same questions. Confirm eligibility for an actual non-native-language time allowance with the exam provider. Answer options shuffle; fixed question membership remains unchanged on repeats.

Single-choice questions require one answer. Select-N questions state the required count and award one point only when the complete correct set is selected. As in the official exam, every Select-TWO question offers five options. Unanswered and partly correct responses earn zero. Expiry automatically submits saved answers and shows the scored result with the expiry status.

Each paper has chapter counts 8/6/4/11/9/2 and cognitive counts 8 K1, 24 K2 and 8 K3. The independently transcribed objective groups in `IstqbContentTests` check the official learning-objective distribution and distinct-objective rules, not merely the chapter totals. The short session is balanced practice with approximate chapter weights, rather than a full certification simulation.

A **Mock pass** means at least 26/40 on this simulation. LearnForge's readiness rule remains separate: strictly above 90% on each of the latest three full mocks or five short mocks, with fresh families and completion before expiry. This is a practice recommendation, not a calibrated prediction of passing an external examination.

## Expansion validation

Version 1.2.0 was checked on 2026-10-08. The content CLI passed with 24 lessons, 366 questions and 70 objectives, and the question linter reports only the documented LF220 warning. Passed: all 199 backend tests (including the seven ISTQB content and API tests), 32 frontend unit tests, the production Angular build, `make check-content`, and both ISTQB Chromium browser journeys, run against the local Google Chrome channel because no Playwright-managed browser was installed. Browser checks of papers E and F confirmed that pseudocode, state-model and decision-table prompts keep their line breaks and indentation, at desktop width and at 390×844, including the review summary after submission. Separately, the active-attempt page is 401 pixels wide at a 390-pixel viewport; that overflow also occurs with the previous styles and is not caused by this release. Calculation and code-coverage keys were recomputed by script; this remains an authoring and technical review, not independent subject-expert accreditation. The previous `IstqbApiTests` threshold test hard-coded version 1.0.0 and now derives its versions from the pack.

Version 1.1.0 was checked on 2026-10-08. Content validation passed with 24 lessons, 224 questions and 70 objectives. All three targeted ISTQB content tests passed, covering teaching/practice coverage, scoring, fixed-paper distributions and short-session composition. The question linter reports only the documented LF220 multiple-choice format warning. The reference map covers all 224 questions; no frontend or browser tests were rerun for this content expansion.

## Content validation and maintenance

Run `make check-content` and `dotnet test LearnForge.slnx`. The content tests verify teaching/practice coverage of every objective, paper membership, group and cognitive distributions, official K levels for every question, four options for single choice and five for Select TWO, the shared-family rule, exact scoring and the 60/75-minute versions. API tests cover pass boundaries, expiry, learning-mode behavior and preservation of thresholds across releases.

The bank intentionally retains lint warning **LF220** because the exam uses multiple-choice formats. All other quality warnings must be resolved; the content test rejects additional warning codes. The general linter is unchanged. Computational keys were checked through their worked arithmetic, and explanations describe why distractors do not satisfy the question. Structural validation is not independent subject-expert accreditation.

Content uses existing text, example and callout blocks. Models, boundaries and decision rules have textual descriptions; selection instructions are explicit. Browser verification covers lesson rendering, paper selection, scoring, review and a narrow-screen view. Keep question and lesson IDs stable and increment the pack version for published changes.

### Verification record for the previous release

Reviewed by Codex on 2026-10-07 for pack `istqb-ctfl-4@1.0.0`, source SHA-256 `b19626d24a007f14436d447ebd803175c1f51ec72b701fc1ace80e508e13468f`. This is an authoring and technical review, not independent subject-expert accreditation.

Passed: 199 backend tests, 32 frontend unit tests, two Chromium browser journeys, the production Angular build and `make check-content`. Browser checks used macOS ARM64, Chromium 153, desktop and 390×844 mobile viewports. The mobile pass-result and explanation layout was visually inspected. Verification found and corrected UTC timestamp loss after SQLite reload and the timer's server-clock initialization; regression tests preserve these fixes. The sole retained content lint diagnostic is the documented LF220 format warning.

## Local syllabus references

The [reference library](../references/istqb/ctfl-4/README.md) contains the official CTFL v4.0.1 syllabus, exam rules v1.2, exam tables v1.19 and v4.0 release notes, downloaded on 2026-10-08. Its manifest records official URLs and SHA-256 checksums. The [source map](../references/istqb/ctfl-4/source-map.json) links every syllabus objective to its PDF section and page, lessons and practice questions, and records the full-paper blueprint.

Pack `istqb-ctfl-4@1.0.1` adds section/page references to all 22 section lessons and all 160 answer explanations, plus guidance to study the complete chapter content and keyword lists. All chapters 1-6 are examinable at K1, beyond the numbered objectives (syllabus section 0.5, p. 11). Use the downloaded documents and the library's authoring steps when maintaining lessons and exams. The verification record above applies to version 1.0.0; it does not describe validation of this reference update.

Reference-update validation on 2026-10-08: the content CLI passed for version 1.0.1 with 24 lessons, 160 questions and 70 objectives (64 syllabus objectives plus six chapters). Question K levels were compared with those extracted from the downloaded syllabus. No application test suites were rerun for this update.

## Next certifications

Foundation is the entry requirement for two specialist packs that use the same lesson and paper format:

- [AI Testing 2.0 (`istqb-ct-ai-2`)](istqb-ct-ai.md) covers testing AI-based and machine learning systems. It has four 44-point papers with a pass mark of 29.
- [Testing with Generative AI 1.1 (`istqb-ct-genai-1`)](istqb-ct-genai.md) covers using generative AI in test activities. It has four 46-point papers with a pass mark of 30.

Both specialist exams weight K3 questions at two points. Their reference libraries live next to this one in `references/istqb/`.

## Official references and attribution

- [CTFL certification and exam format](https://istqb.org/certifications/certified-tester-foundation-level-ctfl-v4-0/)
- [CTFL syllabus v4.0.1 — PDF](https://istqb.org/wp-content/uploads/2024/11/ISTQB_CTFL_Syllabus_v4.0.1.pdf)
- [Exam Structures and Rules Tables v1.19 — PDF](https://istqb.org/?download_id=3832&sdm_process_download=1)
- [ISTQB glossary](https://glossary.istqb.org/)

ISTQB and the authors listed in its official syllabus own those reference documents. Original LearnForge lessons and questions are provided under CC0-1.0; that license does not apply to ISTQB's reference materials or trademark. Official sample-exam questions have not been copied into the pack.
