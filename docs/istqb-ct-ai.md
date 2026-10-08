# ISTQB AI Testing 2.0 preparation

The `istqb-ct-ai-2` hybrid pack is English self-study for the ISTQB Certified Tester AI Testing (CT-AI) certification, using original examples and questions. It is aligned to the official CT-AI syllabus v2.0 (GA 2026-04-17), which replaces v1.0. English v1.0 exams retire on 2027-04-21. The pack is independent material, not an official exam or an accredited course. The ISTQB Foundation Level certificate is the entry requirement, so lessons assume Foundation terminology.

Contents:

- 18 lessons.
- 50 objectives: 7 chapter objectives and 43 syllabus objectives, `ai-x.y.z`.
- 203 questions: 160 in four fixed papers and 43 practice questions, one per objective.

CT-AI v2.0 covers testing AI-based systems, especially machine learning systems. Using GenAI as a testing tool is covered by the separate [CT-GenAI pack](istqb-ct-genai.md).

## Study route

1. Read **Start here**, then the 16 section lessons in syllabus order.
   - Sections 1.1, 5.1 and 6.1 have many objectives, so each is split into two parts.
   - Every section lesson has core concepts, a worked example, a common exam trap, a self-check, a worked case, an exercise with a worked solution, a **Syllabus details to know** list and section/page references.
   - Lessons that have hands-on objectives (HO-x) also include a guide you can follow with a spreadsheet or notebook. These exercises are not examined.
2. Practise the four K3 skills until they are routine:
   - confusion-matrix metrics (AI-3.3.1);
   - planning and running red teaming (AI-4.2.2);
   - classifying and checking dataset constraints (AI-5.1.5);
   - deriving metamorphic relations and follow-up tests (AI-6.1.5).
3. Use **Final revision** to explain the key distinctions, then take paper A under timed conditions. Review wrong answers and guesses, revisit the linked lessons, and take papers B to D on separate days.

## Papers and scoring

| Paper | Questions | Points | Standard time | Extended-time practice | Pass threshold |
| --- | ---: | ---: | ---: | ---: | ---: |
| A | 40 | 44 | 60 minutes | 75 minutes | 29 points |
| B | 40 | 44 | 60 minutes | 75 minutes | 29 points |
| C | 40 | 44 | 60 minutes | 75 minutes | 29 points |
| D | 40 | 44 | 60 minutes | 75 minutes | 29 points |

Each paper follows the CT-AI v2.0 table on p. 15 of the Exam Structures and Rules Tables v1.19:

- **Knowledge levels:** 36 K2 questions worth one point and four K3 questions worth two points (AI-3.3.1, AI-4.2.2, AI-5.1.5 and AI-6.1.5).
- **Questions (points) per chapter:** 6 (6), 3 (3), 7 (8), 7 (8), 6 (7), 9 (10), 2 (2).
- **Objective groups:** chapter 1 takes six questions from eight objectives, and the chapter 3 K2 group takes six from seven, each targeting a different objective. Every other objective gets exactly one question.

The grouped objectives rotate across papers, so A to D together examine all 43 objectives. The four papers share no question families. Each paper has three Select-TWO questions with five options. Select-N questions need exactly the stated number of answers and score nothing for a partial answer. Answer options shuffle, while paper membership is fixed. The 75-minute versions contain the same questions. Confirm eligibility for an actual non-native-language extension with your exam provider.

A **Mock pass** means at least 29 of 44 points. LearnForge's readiness rule is separate. It needs strictly more than 90% on each of the latest three full mocks, or five short mocks, with fresh families, completed before expiry. Learning sessions and 20-question short practice draw from the whole bank, including paper questions, so keep at least three papers unopened if you want fresh full-mock evidence. Repeating a paper is useful revision but cannot provide independent readiness evidence. Readiness is a practice recommendation, not a calibrated prediction of the external exam result.

## How the content was built

The lessons and questions were written from the full text of the v2.0 syllabus. The official sample exam A v2.2 was used only to calibrate style and difficulty; no official question is copied.

- **Explanations:** each explains the key and the main distractors, and ends with the section, printed page, objective ID and K level, for example `Syllabus reference: CT-AI v2.0, section 3.3.1 (p. 33); AI-3.3.1 (K3).`
- **Calculation keys:** the confusion-matrix keys were computed by script. Distractors model common mistakes: swapped precision and recall denominators, the arithmetic mean instead of F1, and the wrong matrix orientation.
- **Risk-to-test mappings:** table rows that wrap across page breaks were resolved from the PDF's text coordinates before the related questions were written.
- **Practice questions:** each has its own family and is a distinct item, not a paraphrase of a paper question, so answering one does not reduce a paper's freshness.

A generator script in the local reference library (`references/istqb/_build/`) produces the pack and its source map. It checks the paper rules and a port of the content-linter heuristics before writing.

## Content validation and maintenance

Run `make check-content` and `dotnet test LearnForge.slnx`. For the CT-AI pack, the parameterised `IstqbContentTests` check that:

- every objective is taught, practised and examined in at least one paper;
- each question's K level and point weight match the official table, an independent transcription in `tests/LearnForge.Tests/IstqbExamSpec.cs`;
- single-choice questions have four options and Select-TWO questions have five;
- every paper meets the chapter, point, K-level and group rules, and the papers share no families;
- the 75-minute twins match their papers;
- the linter reports only LF220.

`IstqbApiTests` scores paper A through the API at exactly 28 and 29 weighted points, checking the inclusive pass boundary, the 44 possible points, both deadlines and that no answer keys leak.

The bank intentionally keeps lint warning **LF220** because the exam uses multiple-choice formats. Keep question and lesson IDs stable and increment the pack version for any published change. Releases are immutable.

## Validation record

Version 1.0.0 was checked on 2026-10-09:

- **Content CLI:** passed with 18 lessons, 203 questions and 50 objectives; lint reports only LF220.
- **Backend:** all 209 tests passed, including the parameterised ISTQB content and API tests for all three ISTQB packs.
- **Browser:** both Chromium journeys for the specialist packs passed (`apps/web/e2e/istqb.spec.ts`), run against the local Google Chrome channel. Each opens the course, a lesson and paper A, answers to exactly the pass mark using weighted points, and checks the Mock pass result, the explanations and the narrow-screen layout.
- **Manual check:** a lesson was inspected at desktop and 390×844 widths.

This is an authoring and technical review, not independent subject-expert accreditation.

## Official references and attribution

- [CT-AI certification and exam format](https://istqb.org/certifications/certified-tester-ai-testing-ct-ai/)
- [CT-AI syllabus v2.0 (PDF)](https://istqb.org/?sdm_process_download=1&download_id=9558)
- [Exam Structures and Rules Tables v1.19 (PDF)](https://istqb.org/?download_id=3832&sdm_process_download=1)
- [ISTQB glossary](https://glossary.istqb.org/)
- Local [reference library](../references/istqb/ct-ai-2/README.md): the syllabus, sample exam A with answers, accreditation guidelines and exam rules, with a manifest of URLs and SHA-256 checksums, and a source map from objectives to pages, lessons and questions. The `/references` folder is gitignored.

ISTQB and the syllabus authors own the reference documents. Original LearnForge lessons and questions are provided under CC0-1.0; that licence does not apply to ISTQB's materials or trademark.
