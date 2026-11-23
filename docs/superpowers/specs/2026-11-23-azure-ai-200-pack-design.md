# AI-200 preparation pack: design

Date: 23 Nov 2026. Branch: `ai-200` (cut from `main` at ac1e946).

## Intent

A generic LearnForge pack for anyone preparing for Microsoft Exam AI-200, *Developing AI Cloud Solutions on Azure* (Microsoft Certified: Azure AI Cloud Developer Associate). It replaces AZ-204 as the Azure developer exam: course AZ-204T00 retired on 29 May 2026 in favour of AI-200T00. The pack should match the depth of the ISTQB Foundation pack (`istqb-ctfl-4`) and the SAFe 1.1.0 packs, and be grounded in Microsoft Learn content read through the Microsoft Learn MCP tools (`microsoft_docs_search`, `microsoft_docs_fetch`, `microsoft_code_sample_search`).

The owner sits the exam in about two weeks (around 7 Dec 2026). That affects only the **delivery order**. The content stays generic: depth follows the official exam weights, not the owner's strengths, and the pack contains no personal or dated plan.

## Exam facts the pack is built on (read on MS Learn, 23 Nov 2026)

- Study guide: https://learn.microsoft.com/en-us/credentials/certifications/resources/study-guides/ai-200. It lists four domains, nine skill groups and 27 skills. The 27 skills are the same as in `references/azure/ai-200/_build/spec.py` (captured 1 Nov).
- Weights on the live en-US page: containers 20–25%, data management services 25–30%, connect to and consume services 20–25%, secure/monitor/troubleshoot 20–25%. MS Learn's search index also holds a variant "as of October 7, 2026" that raises data services to 30–35% (its own domain heading still says 25–30%). **Use the live page.** Recheck it before each release. If the live page changes, change the spec weights.
- Audience: back-end developer, Python-first, with SDKs, data services, messaging, monitoring, vector databases and containers.
- Format (exam duration page): typically 40–60 questions; 100 minutes, or 120 if the exam contains labs; pass mark 700 on a scaled score. Question types: multiple choice, build list, drag and drop, hot area, case studies, problem–solution sets and labs. Microsoft Learn is open during the exam, with no extra time. After a break you can't return to earlier questions.
- Microsoft offers a free practice assessment (sign-in required). The pack never copies it, and never uses exam dumps.

## Goals

1. Cover all 27 skills. Objectives have two levels: 4 domains plus 27 skill objectives, and every question, lesson and card is tagged with both.
2. A bank of about **400 original questions**:
   - a **practice pool of about 150**, including 2 case studies;
   - **five fixed, disjoint full papers of 50 questions** (250 questions), each with one 5-question case study.

   Every skill has at least 10 questions in total and at least 4 in the practice pool. Questions per domain follow the weights by largest remainder; each paper's domain mix is 12/14/12/12.
3. Microsoft-style formats, all supported by `main` with no engine change. Target mix ±5 points:

   | Format (pack kind) | Target share |
   | --- | --- |
   | Single choice (`single`) | 55% |
   | Select TWO/THREE (`multiple`, partial credit) | 15% |
   | Hot area and code completion (`dropdown`, at least 3 choices per blank) | 10% |
   | Drag and drop, and Yes/No statement grids (`matching`; grids use `reuse: true` with Yes and No as the targets) | 10% |
   | Build list (`sequence`) | 6% |
   | Calculations such as RUs, TTLs, retry counts and replica counts (`numeric`) | 4% |

   Cognitive mix about 20% recall, 50% apply, 30% analyse.

   *Builder's call:* LF209 flags any dropdown blank with fewer than 3 choices and any single-choice question with fewer than 4 options. So Yes/No grids are modelled as reusable matching. Problem–solution sets ("Does this solution meet the goal?") are not modelled as questions; the `exam-technique` lesson explains them. Changing the linter is not worth an engine change on `main` for this.
4. Mocks and readiness, shaped like CTFL:
   - full papers: 50 questions, 100 minutes, `passPoints` 35 (70%, a stated approximation of 700 scaled);
   - weighted short mock: 20 questions, 40 minutes, practice only;
   - `reserveFixedPapers: true`;
   - readiness `shortAttempts 0, fullAttempts 3, threshold 80, lookbackDays 90, minimumFreshPercent 100`.
5. Lessons in the CTFL shape (final release):
   - `start-here`, with a suggested route by week for 2- and 4-week preparation, not by date;
   - one lesson per skill (27);
   - about six labs, each a worked, code-first walkthrough:
     - ACR Tasks to Container Apps with KEDA;
     - Cosmos DB vector search in Python;
     - pgvector index and metadata-filtered RAG;
     - Service Bus dead-letter handling;
     - Functions v2 app with bindings, deployed on Flex Consumption;
     - Key Vault and App Configuration with managed identity, plus OpenTelemetry traces queried with KQL;
   - `commonly-confused` (for example Service Bus, Event Grid and Event Hubs; App Service, Container Apps and AKS; consistency levels; HNSW, IVFFlat and DiskANN);
   - `exam-technique` (Learn access during the exam, case-study navigation, breaks, partial credit);
   - `final-revision`.
6. Flashcards: at least 150 notes, with at least 3 per skill.
7. Every fact traceable to a Microsoft Learn page or the Azure SDK source.

## Non-goals

- Audio scripts (possible later version), live Azure lab environments, C# or other language tracks, other Azure exams, Anki export work.
- Engine or UI changes. If the pack needs one, it goes to `main` first, with tests on bundled demo packs, and `ai-200` is rebased.
- C# content tests on the branch. This is my call, following the branch-layout rule (content files plus Makefile, README and doc lines). `safe` has none either. Verification is listed under *Verification* below.

## Anti-goals (a failure even if it builds)

- Questions copied or paraphrased from Microsoft's practice assessment, exam dumps or third-party question banks.
- A key or explanation that rests on memory rather than a fetched MS Learn page or SDK source.
- Answers built on retired or renamed services, unless the question is about the change. Examples: Azure Cache for Redis where the skill says Azure Managed Redis, or the Linux Consumption plan for new function apps.
- Giveaway options: the longest-option cue, absolute words, a key that repeats the stem.
- Showing the owner any question text. The owner studies from this pack, so builders print counts and IDs only.
- Tailoring to the owner.

## Constraints

- Releases are immutable and keyed by version. Question IDs are positional per domain and **only appended**, never reordered. Families stay one per question.
- **Publishing a new version restarts the readiness streak, and papers already taken are no longer fresh** (`docs/assessment.md`). This drives the staging below.
- A running dev server publishes anything saved in `packs/` within 5 s. Build with `--draft` (to `build/draft/`) and write to `packs/` only with no server running.
- The shared dev database holds releases from other branches. Verify on an isolated API/web pair (5091/4311, scratch DB and key directory), never with `ConnectionStrings__Database` through the launcher.
- Pack JSON must match `main`'s Core records exactly, because the deserializer is strict.
- **Study database** (builder's call). Both releases go into the same launcher data directory, `.learnforge/data/ai-200`, which is fresh and so holds no earlier `azure-ai-200` draft. Start it with `bash scripts/install.sh --no-install --data-dir .learnforge/data/ai-200`. The shared `apps/api/data` database holds `chinese-writing-pilot` releases that break the catalog on every branch except `chinese`. Keeping 1.0.0 and 1.1.0 in one database keeps the papers fresh, flags lessons as revised and keeps the evidence together. `docs/azure-ai-200.md` states this.
- **Licence** (builder's call): `CC0-1.0`, as on `istqb-ctfl-4` and the 1 Nov draft. Everything is written in the pack's own words: no text is copied from MS Learn, code samples are written fresh and checked against the SDK, and sources are cited.

## Approaches considered

- **A. SAFe build tools, seeded with the 1 Nov draft (chosen by the owner).** Port `references/safe/_build/common.py`, which already has fixed disjoint papers, flashcards, table blocks, topic objectives, reference lines, the LF2xx lint port and key-length checks, into `references/azure/ai-200/_build/`. Extend it for case-study scenarios inside papers and for the `dropdown` and `numeric` kinds. Keep the 1 Nov 27-skill spec, and audit its 15 lessons and roughly 120 questions before reusing them.
- B. Finish the 1 Nov builder as it stands. Rejected: it lacks papers, cards and length checks, so it would rebuild A's tools.
- C. Author in the `exam/1` source format. Rejected: that format has no lessons or flashcards.

## Delivery: two releases, then freeze

| Release | Target | Contents |
| --- | --- | --- |
| **1.0.0 "learn and practise"** | Thu 26 Nov | Practice pool (~150, including both 1 Nov case studies), weighted short mock and a generated full mock drawn from the practice pool, the 15 audited 1 Nov lessons (they cover all 27 skills), at least 60 cards. No fixed papers. |
| **1.1.0 "complete"** | Mon 30 Nov | Appends the 250 paper questions as five fixed papers (all fresh, because none existed in 1.0.0), the full lesson set, labs and cards (at least 150). Each 1.0.0 lesson ID becomes the first skill lesson of its group, so lesson progress is flagged as revised, not reset. The generated full mock from 1.0.0 is removed, so a non-fresh full attempt cannot break the readiness streak. |

**If time runs short (the owner's decision):** keep all five papers and every verification step, and let 1.1.0 slip by up to two days, to Wed 2 Dec. Never ship fewer papers or skip checks to hold a date. 1.0.0 follows the same rule (builder's call): it slips rather than ships unchecked.

After 1.1.0, freeze: only a wrong key or factual error justifies 1.1.1. The final release notes must say that a new version restarts readiness. Both releases must pass `lint --strict` with zero findings. If 1.0.0 cannot satisfy LF222 without papers, the builder adjusts that release's readiness policy and records why.

## Grounding in Microsoft Learn

- The spec gains a source registry: key → title, URL and the date it was fetched. Every question, card and lesson block cites at least one key. The build fails on an uncited item or a key with no fetch date.
- Pages are read with `microsoft_docs_fetch` and saved as text under `references/azure/ai-200/_build/text/<key>.md` with the date, so audits and fact-checks reuse the same reading. Python samples are cross-checked with `microsoft_code_sample_search` and the SDK clones in `references/azure/ai-200/sdk/`.
- Learn training modules are cited where one covers a skill. The AI-200T00 modules aren't in the index yet; AZ-204-era modules are cited only where they match current docs.
- Most questions test generally available features. Preview features appear only where the skill bullet needs them and MS Learn treats them as commonly used. The explanation says "preview".
- The 1 Nov material was written before this rule. Each reused item is re-verified against a fresh fetch, or rewritten.
- The pack's `sources`, description and `start-here` lesson state that it is independent and not Microsoft material.

## Verification (each release)

1. Builder checks:
   - per-skill and per-format counts;
   - citations;
   - ported LF2xx lint;
   - key-length rank: key longest 10–25% and shortest under 40%;
   - paper disjointness;
   - case studies kept whole.
2. `dotnet run --project tools/cli -- check` and `lint --strict` on the pack, with zero findings. Add both to `make check-content` on the branch.
3. Every `Python…` code block compiles, and its imports resolve in `references/azure/ai-200/.venv`. No network calls.
4. Blind review: fresh subagents answer the practice pool and each paper without keys. Each disagreement is settled against the saved MS Learn text and recorded. Then one fact-check subagent per domain re-reads keys and explanations against MS Learn through the MCP tools.
5. Walkthrough on the isolated pair using the draft. Start the short mock, a fixed paper and a case study, then submit. Check timers, scoring, `passPoints` and that no keys appear in delivery.
6. Write to `packs/` with no dev server running.

## Branch contents

- `packs/azure-ai-200.json`
- `Makefile` (`check-content` lines)
- one `README.md` line
- `docs/azure-ai-200.md`: learner and author guide covering contents, exam facts with dates, how to use the papers and readiness, suggested 2- and 4-week routes, how to rebuild, and the independence notice
- this design and its plan under `docs/superpowers/`

The generator stays in the gitignored `references/azure/ai-200/_build/`, with `STATUS.md` beside it, as for SAFe and ISTQB.

## Left to the builder

- Case-study scenarios and all question, lesson and card wording.
- Exact per-skill counts above the floors, and the format mix within ±5 points.
- Which MS Learn pages to cite per skill, which labs to write from the list, and how to split modules.
- How `make_papers` places case studies and balances domains.
