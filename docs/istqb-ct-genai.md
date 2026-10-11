# ISTQB Testing with Generative AI 1.1 preparation

The `istqb-ct-genai-1` hybrid pack is English self-study for the ISTQB Certified Tester Testing with Generative AI (CT-GenAI) certification, using original examples and questions. It is aligned to the official CT-GenAI syllabus v1.1 (2026-04-27). That is a minor update of v1.0 with the same structure, learning objectives and scope. The pack is independent material, not an official exam or an accredited course. The ISTQB Foundation Level certificate is the entry requirement.

Contents:

- 16 lessons.
- 42 objectives: 5 chapter objectives and 37 syllabus objectives, `genai-x.y.z`.
- 197 questions: 160 in four fixed papers and 37 practice questions, one per objective.

CT-GenAI covers using generative AI to support software testing. Testing AI-based systems is covered by the [CT-AI pack](istqb-ct-ai.md).

## Study route

1. Read **Start here**, then the 14 section lessons in syllabus order. Section 2.2 is split into two parts because it holds five K3 objectives.
   - Every section lesson has core concepts, a worked example, a common exam trap, a self-check, a worked case, an exercise with a worked solution, a **Syllabus details to know** list and section/page references.
   - Lessons with hands-on objectives (HO-x) also include a practice guide. These exercises are not examined.
2. Spend the most time on chapter 2, prompt engineering, which carries 16 of the 46 points. Practise the six K3 skills:
   - applying GenAI to test analysis (GenAI-2.2.1);
   - test design and implementation (2.2.2);
   - automated regression testing (2.2.3);
   - test monitoring and control (2.2.4);
   - choosing prompting techniques (2.2.5);
   - identifying hallucinations, reasoning errors and biases (3.1.2).
3. Use **Final revision**, then take paper A under timed conditions. Review wrong answers and guesses, revisit the linked lessons, and take papers B to D on separate days.

## Papers and scoring

| Paper | Questions | Points | Standard time | Extended-time practice | Pass threshold |
| --- | ---: | ---: | ---: | ---: | ---: |
| A | 40 | 46 | 60 minutes | 75 minutes | 30 points |
| B | 40 | 46 | 60 minutes | 75 minutes | 30 points |
| C | 40 | 46 | 60 minutes | 75 minutes | 30 points |
| D | 40 | 46 | 60 minutes | 75 minutes | 30 points |

Each paper follows the CT-GenAI table on p. 25 of the Exam Structures and Rules Tables v1.19:

- **Knowledge levels:** 8 K1 and 26 K2 questions worth one point, and 6 K3 questions worth two points.
- **Questions (points) per chapter:** 7 (7), 11 (16), 10 (11), 5 (5), 7 (7).
- **Objective counts:** GenAI-1.1.2, 2.1.1 and 3.2.2 get two questions each; every other objective gets exactly one.

Each paper therefore examines all 37 objectives. The four papers share no question families. Each paper has two Select-TWO questions with five options. Select-N questions need exactly the stated number of answers and score nothing for a partial answer. Answer options shuffle, while paper membership is fixed. The 75-minute versions contain the same questions. Confirm eligibility for an actual extension with your exam provider.

A **Mock pass** means at least 30 of 46 points. LearnForge's readiness rule is separate. It needs strictly more than 90% on each of the latest three full mocks, or five short mocks, with fresh families, completed before expiry. Learning and short sessions can expose paper questions, so keep at least three papers unopened if you want fresh full-mock evidence. Readiness is a practice recommendation, not a calibrated prediction of the external exam result.

## How the content was built

The lessons and questions were written from the full text of the v1.1 syllabus, including Appendix D for keyword definitions and the v1.1 release notes. The official sample exam A v1.1 was used only to calibrate style and difficulty; no official question is copied.

- **Explanations:** each explains the key and the main distractors, and ends with the section, printed page, objective ID and K level, for example `Syllabus reference: CT-GenAI v1.1, section 2.2.1 (p. 24); GenAI-2.2.1 (K3).`
- **K3 items:** each presents a concrete test task or LLM output. The key keeps human verification and supplies the inputs the task needs.
- **Practice questions:** each has its own family and is a distinct item, not a paraphrase of a paper question.

A generator script in the local reference library (`references/istqb/_build/`) produces the pack and its source map. It checks the paper rules and a port of the content-linter heuristics before writing.

## Content validation and maintenance

Run `make check-content` and `dotnet test LearnForge.slnx`. For the CT-GenAI pack, the parameterised `IstqbContentTests` check that:

- every objective is taught, practised and examined in at least one paper;
- each question's K level and point weight match the official table, an independent transcription in `tests/LearnForge.Tests/IstqbExamSpec.cs`;
- single-choice questions have four options and Select-TWO questions have five;
- every paper meets the chapter, point, K-level and per-objective rules, and the papers share no families;
- the 75-minute twins match their papers;
- the linter reports only LF220.

`IstqbApiTests` scores paper A through the API at exactly 29 and 30 weighted points, checking the inclusive pass boundary, the 46 possible points, both deadlines and that no answer keys leak.

Keep question and lesson IDs stable and increment the pack version for any published change. Releases are immutable.

## Validation record

Version 1.0.0 was checked on 2026-10-09:

- **Content CLI:** passed with 16 lessons, 197 questions and 42 objectives; lint reports only LF220.
- **Backend:** all 209 tests passed.
- **Browser:** the CT-GenAI Chromium journey in `apps/web/e2e/istqb.spec.ts` passed against the local Google Chrome channel. It opens the course and a lesson, answers paper A to exactly 30 weighted points, and checks the Mock pass result, the explanations and the narrow-screen layout.
- **Manual check:** a lesson and a K3 question were inspected at desktop width, and the lesson also at 390×844.

This is an authoring and technical review, not independent subject-expert accreditation.

## Official references and attribution

- [CT-GenAI certification and exam format](https://istqb.org/certifications/gen-ai/)
- [CT-GenAI syllabus v1.1 (PDF)](https://istqb.org/?sdm_process_download=1&download_id=6295)
- [Exam Structures and Rules Tables v1.19 (PDF)](https://istqb.org/?download_id=3832&sdm_process_download=1)
- [ISTQB glossary](https://glossary.istqb.org/)
- Local [reference library](../references/istqb/ct-genai-1/README.md): the syllabus, sample exam A with answers, release notes, accreditation guidelines and exam rules, with a manifest of URLs and SHA-256 checksums, and a source map from objectives to pages, lessons and questions. The `/references` folder is gitignored.

ISTQB and the syllabus authors own the reference documents. Original LearnForge lessons and questions are provided under CC0-1.0; that licence does not apply to ISTQB's materials or trademark.
