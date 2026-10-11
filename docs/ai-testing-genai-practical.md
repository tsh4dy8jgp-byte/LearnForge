# AI Testing and Testing with GenAI: Practical QA

The `ai-testing-genai-practical` course is an English practical learning path for working QA testers. It assumes basic knowledge of requirements, test cases and defect reports. No certification, Python installation, model account or API key is needed.

Its 16 lessons teach two complementary activities: evaluating AI products and using GenAI to assist testing work. Each has a measurable objective, core concepts, a worked example, a common mistake, an exercise with supplied inputs and a clearly labelled worked solution. The 80 graded questions use five independent families per objective across single choice, multiple choice, matching, sequences and numeric calculations.

## Study route

| Lessons | Focus |
| --- | --- |
| 1–2 | The two roles of AI in testing; ML, LLMs and application context |
| 3–6 | Risks and acceptance criteria; data quality and leakage; metrics and subgroup outcomes; oracles and metamorphic tests |
| 7–9 | LLM applications and RAG; injection, privacy and authorization; regression evidence and monitoring |
| 10–13 | Prompt design; requirements review; test-case review; synthetic data |
| 14–15 | Automation review and failure analysis; defect reports and net GenAI value |
| 16 | Capstone: evaluate an AI support assistant and review a generated test plan |

Read in order and try each exercise before comparing with the worked solution. All needed inputs are included in the lesson. Optional Python and pseudocode examples are displayed for inspection; LearnForge does not execute them. Security exercises use synthetic accounts and bounded sandbox scenarios.

Use **Practice** for a course-wide learning session of 16 questions, one per objective. The composer requires coverage of every listed objective, so a ten-question session could not cover all 16. Objective-focused practice contains the five available questions for that objective. Sessions are untimed, with checked feedback and saved responses. There are no timed assessments or certification pass thresholds.

The goal is mastery under the existing default policy: at least three distinct families per objective, at least 80% fully correct in the latest window of up to five families. With three families this requires three correct; with five it requires four correct. Repeating a family updates its evidence rather than adding a new family. Proficient objectives become due for review after 60 days without newer evidence. Lesson completion records reading separately from question performance.

## Capstone and self-assessment

ShopHelp is a synthetic RAG support assistant with current and archived policies and customer-scoped order lookup. The lesson supplies requirements, five response traces and five generated test-plan proposals. Learners score policy correctness, evidential support, uncertainty handling and authorization, then review the proposals and recommend a release decision.

The worked solution explains the three passing traces, unsupported policy conclusion and isolation blocker. A five-part rubric scores the written work out of ten; aim for at least eight with no missed isolation blocker. Written exercises and the capstone are self-assessed. They are not uploaded, automatically graded or included in mastery evidence; knowledge questions separately check the same skills. The small evaluation set illustrates a decision process and does not establish production assurance.

## References and maintenance

The original examples and questions use CC0-1.0. Further reading includes [ISTQB AI Testing v2.0](https://istqb.org/certifications/certified-tester-ai-testing-ct-ai/), [ISTQB Testing with Generative AI v1.1](https://istqb.org/certifications/gen-ai/) and the [NIST AI Risk Management Framework](https://www.nist.gov/itl/ai-risk-management-framework). This course connects those areas through practical QA work; it is independent material and does not claim accreditation or full certification-syllabus coverage. The existing CT-AI and CT-GenAI courses remain the exam-preparation paths.

The pack is automatically included in API builds and seeded through the existing content workflow. Rebuild/restart the API to load it, or use the development pack watcher. Keep published lesson, objective and question IDs stable and increment the version for later content changes.

Run `make check-content`, `dotnet test LearnForge.slnx`, and the Playwright journey in `apps/web/e2e/ai-testing-genai.spec.ts` with development servers running. The pack must compile and pass strict lint with no warnings. Content tests check full session coverage, independent families, calculation keys and feasible mastery; API tests check enrollment, saved completion, feedback, evidence and protected keys.
