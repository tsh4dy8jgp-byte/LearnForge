# Azure AI-200 preparation pack

`azure-ai-200` is a generic LearnForge pack for Microsoft Exam AI-200, *Developing AI Cloud Solutions on Azure* (Microsoft Certified: Azure AI Cloud Developer Associate). It follows the official skills list and weights, and every fact traces to a Microsoft Learn page or the Azure SDK source. It contains no personal plan and no study-plan dates.

## What is in 1.1.0

- 400 original questions covering all 27 skills in 4 domains:
  - 150 practice questions, including 2 case studies, used by learning sessions, focused practice and the short mock.
  - 250 paper questions, split across five fixed papers, A to E. Papers share no questions.
- A weighted short mock: 20 questions, 40 minutes, practice questions only.
- Five fixed mock papers, A to E. Each has 50 questions, 100 minutes and one 5-question case study, and passes at 35 of 50 points.
- 37 lessons: one for each of the 27 skills, 6 worked labs (`lab-*`) and four cross-cutting lessons (`start-here` with the suggested routes, `commonly-confused`, `exam-technique` and `final-revision`).
- 163 flashcards in four decks, one for each domain, tagged by skill.

## Release history and the freeze

- 1.0.0: 150 practice questions, a short mock, a generated full mock, 15 lessons and 72 flashcards.
- 1.1.0: adds the 250 paper questions and five fixed papers, 22 more lessons (including the labs) and 91 more flashcards. The generated full mock is removed in favour of the papers.

1.1.0 is the final release before the exam and is frozen: no further version is planned. A new version would restart readiness (see below), so the content stays as it is.

## Upgrading from 1.0.0

Publish 1.1.0 into the same database as 1.0.0 (see [Study database](#study-database)). What carries over:

- Lesson progress, for the 15 lessons whose IDs were kept. All 15 were revised for 1.1.0, so a lesson you completed in 1.0.0 stays completed and is marked "Updated since you read it". It is shown as revised, not reset.
- The evidence ledger. All 150 practice question IDs are kept, so mastery, next steps and focused practice keep your 1.0.0 answers. Questions you saw in 1.0.0 count as seen in 1.1.0.

What doesn't carry over is readiness. It is scoped to a release, so any 1.0.0 full mock streak starts again at 0 on 1.1.0. All 250 paper questions are new in 1.1.0, so every paper starts unstarted and fully fresh, however much you practised on 1.0.0. Practice questions you saw in 1.0.0 count as seen, but readiness on 1.1.0 uses only the papers, so this doesn't affect it.

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
- The fixed papers pass at 70% (35 of 50 points), a stated approximation of 700 scaled, not a prediction. The short mock has no pass line.
- Microsoft changes study guides. If the live weights differ from the table, trust the live page.

## Mocks, papers and readiness

Readiness counts full mocks only. The pack's policy is `shortAttempts 0`, `fullAttempts 3`, `threshold 80`, `lookbackDays 90` and `minimumFreshPercent 100`. In plain terms, the latest three full mocks within 90 days must each score strictly above 80% fully correct and use only questions you haven't seen before. Fully correct counts only answers that are entirely right: partial credit earns points but doesn't count towards this percentage. A newer failed, expired or ineligible full mock breaks the streak, and there is no cherry-picking of good attempts. The short mock never counts.

In 1.1.0 the full mocks are the five fixed papers. Each paper is reserved for full mocks (`reserveFixedPapers`): until you have seen a paper's questions, learning sessions, focused practice and short mocks leave them out and draw from the 150 practice questions, so practice never spends an unopened paper. Mistakes from a paper you have taken return to practice afterwards. A paper is fresh the first time only, so a retake doesn't count and the five papers allow at most five eligible full mocks. Take them on separate days and review each before the next.

Readiness is scoped to a release, while evidence is permanent. Publishing a new version restarts the readiness streak, which is why 1.1.0 is frozen. Read `docs/assessment.md` for the full rule.

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

Always try `--draft` first. Write `packs/` only with no dev server running, otherwise the watcher may publish a half-finished file. Releases are immutable, so a content change needs a `version` bump, and a bump restarts readiness for every learner on the pack. Then run `make check-content`, which compiles the pack and lints it with `--strict`.

## How content was verified

- Every question, lesson and card carries citations to Microsoft Learn pages or Azure SDK source, kept as extracts in the gitignored `text/` folder and in a source map.
- Facts were fact-checked against those extracts.
- A blind review had fresh reviewers answer the practice pool using only the delivery view and MS Learn. Each disagreement was settled against the sources and fixed.
- A code check compiles and runs the code samples in the lessons.
- The CLI `check` and `lint --strict` pass with no warnings.

## Known engine issues

These come from the LearnForge engine, not the pack content. Both are planned to be fixed on `main` later. The pack stays frozen in the meantime.

- At 390 px phone width, about 20 questions' option, matching-token or sequence rows overflow the page sideways. Long code identifiers in the options don't wrap, and sequence rows push their buttons off the row. Lessons, labs, tables and case backgrounds are not affected. Use a wider window or landscape mode for those questions.
- An attempt view includes every case background in the pack, not just the one its questions use. The page shows only the case in use, and no questions, keys or explanations are exposed, but someone reading the raw response could see a paper's case background before taking that paper. The cause is that `AttemptService` copies all of `pack.Scenarios` into the attempt.

## Independence

This pack is not affiliated with or endorsed by Microsoft. It never uses Microsoft's practice assessment or exam dumps. All questions, lessons and cards are original, written from public documentation. The pack is released under CC0-1.0. Passing it doesn't predict an exam result.
