# Azure AI-200 preparation pack

`azure-ai-200` is a generic LearnForge pack for Microsoft Exam AI-200, *Developing AI Cloud Solutions on Azure* (Microsoft Certified: Azure AI Cloud Developer Associate). It follows the official skills list and weights, and every fact traces to a Microsoft Learn page or the Azure SDK source. It contains no personal plan and no study-plan dates.

## What is in 1.0.0

- 150 original practice questions, including 2 case studies, covering all 27 skills in 4 domains.
- A weighted short mock: 20 questions, 40 minutes, practice only.
- A generated full mock: 50 questions, 100 minutes, drawn from the practice pool and weighted like the exam.
- 15 lessons, including `start-here` with the suggested routes.
- 72 flashcards, tagged by skill.

## What 1.1.0 will add

No date is promised. The next version is planned to add five fixed, disjoint 50-question papers (each with a 5-question case study), the full lesson set with worked labs, and 150 or more flashcards.

## Exam facts

Checked on MS Learn on 24 Nov 2026 (the date the `start-here` lesson shows). The domain weights were rechecked on 26 Nov 2026 before release and were unchanged.

| Domain | Weight |
| --- | --- |
| Develop containerized solutions on Azure | 20-25% |
| Develop AI solutions by using Azure data management services | 25-30% |
| Connect to and consume Azure services | 20-25% |
| Secure, monitor, and troubleshoot Azure solutions | 20-25% |

- The exam typically has 40-60 questions. Associate exams without labs give you 100 minutes, and 120 if the exam contains labs. A scaled score of 700 passes.
- Microsoft lists question types such as multiple choice, build list, drag and drop, hot area, case studies and labs, but doesn't publish the exact types for each exam.
- The 1.0.0 mocks have no pass line. The 1.1.0 fixed papers will use 70% (35 of 50 points) as a stated approximation of 700 scaled, not a prediction.
- Microsoft changes study guides. If the live weights differ from the table, trust the live page.

## Mocks, papers and readiness

Readiness counts full mocks only. The pack's policy is `shortAttempts 0`, `fullAttempts 3`, `threshold 80`, `lookbackDays 90` and `minimumFreshPercent 100`. In plain terms, the latest three full mocks within 90 days must each score strictly above 80% fully correct and use only questions you haven't seen before. Fully correct counts only answers that are entirely right: partial credit earns points but doesn't count towards this percentage. A newer failed, expired or ineligible full mock breaks the streak, and there is no cherry-picking of good attempts. The short mock never counts.

In 1.0.0 the full mock is generated from the practice pool, and learning sessions draw from the same pool. Heavy practice therefore uses up fresh questions: learn first, then keep enough of the pool unseen for three full mocks. Fresh questions are what make a mock score meaningful, so the pool is the limit.

Readiness is scoped to a release, while evidence is permanent. Publishing a new version restarts the readiness streak, and questions you already saw in 1.0.0 stay in 1.1.0 but count as seen, so they are no longer fresh there. The 1.0.0 generated full mock is removed in 1.1.0 in favour of the fixed papers. Once 1.1.0 ships its fixed papers, each paper is reserved for full mocks (`reserveFixedPapers`), so learning sessions don't spend them and they stay fresh for the readiness check. Read `docs/assessment.md` for the full rule.

## Study database

Run the pack in its own database so its attempts and readiness stay apart from other packs and branches:

```sh
bash scripts/install.sh --no-install --data-dir .learnforge/data/ai-200
```

The shared `apps/api/data` database holds `chinese-writing-pilot` releases that break the catalog on every branch except `chinese`. Put both 1.0.0 and 1.1.0 into this same directory: keeping them in one database keeps the papers fresh, flags revised lessons as revised and keeps your evidence together. Starting a new directory for 1.1.0 would lose that history.

## Suggested routes

These match the `start-here` lesson. They are by week, not by date.

Four weeks, about an hour a day:

| Week | Study | Practice |
| --- | --- | --- |
| 1 | Domain 1 lessons (1.1 to 1.4) and their flashcards | Learning sessions on Domain 1; review every miss |
| 2 | Domain 2 lessons (2.1 to 2.5); the heaviest domain | Learning sessions on Domain 2; flashcards for Domain 1 |
| 3 | Domain 3 and 4 lessons (3.1 to 4.2) | Learning sessions on Domains 3 and 4; one short mock at the end |
| 4 | Revisit the weakest skills on your progress page | Full mocks on separate days, reviewing each before the next |

Two weeks, about two hours a day, for someone who already works with most of these services:

| Week | Study | Practice |
| --- | --- | --- |
| 1 | Domains 1 and 2 (lessons 1.1 to 2.5), two or three lessons a day | A short learning session after each lesson; flashcards daily |
| 2 | Domains 3 and 4 (lessons 3.1 to 4.2) in the first half of the week | Then full mocks on separate days, revisiting the lessons behind each miss |

## Rebuilding the pack

The generator is gitignored and lives under `references/azure/ai-200/_build`. The published file is `packs/azure-ai-200.json`.

```sh
cd references/azure/ai-200/_build
bash run.sh build.py --draft   # writes build/draft/azure-ai-200.json and prints counts only
bash run.sh build.py           # writes packs/azure-ai-200.json
```

Always try `--draft` first. Write `packs/` only with no dev server running, otherwise the watcher may publish a half-finished file. Releases are immutable, so a content change needs a `version` bump. Then run `make check-content`, which compiles the pack and lints it with `--strict`.

## How content was verified

- Every question, lesson and card carries citations to Microsoft Learn pages or Azure SDK source, kept as extracts in the gitignored `text/` folder and in a source map.
- Facts were fact-checked against those extracts.
- A blind review had fresh reviewers answer the practice pool using only the delivery view and MS Learn. Each disagreement was settled against the sources and fixed.
- A code check compiles and runs the code samples in the lessons.
- The CLI `check` and `lint --strict` pass with no warnings.

## Independence

This pack is not affiliated with or endorsed by Microsoft. It never uses Microsoft's practice assessment or exam dumps. All questions, lessons and cards are original, written from public documentation. The pack is released under CC0-1.0. Passing it doesn't predict an exam result.
