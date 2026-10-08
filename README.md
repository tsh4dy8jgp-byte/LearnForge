# LearnForge

**A reusable education and exam-practice framework built with ASP.NET Core 10 and Angular 22.**

LearnForge connects teaching materials, curriculum objectives, mixed-format practice, saved attempts and evidence-based review. Subjects live in versioned JSON packs, so changing from reasoning to university coursework or certification preparation does not require rebuilding the application.

This repository contains a runnable implementation, original example packs, ISTQB preparation packs (Foundation, AI Testing and Testing with Generative AI), a content compiler, automated tests and deployment scaffolding. It is a single-organization foundation. Institutional SSO, multi-tenancy and other future capabilities are listed in the [roadmap](docs/roadmap.md).

## Quick start

Install the **.NET 10 SDK**, **Node.js 22.22.3** and npm. SQLite is included; no database installation is required.

```sh
dotnet restore LearnForge.slnx
npm ci --prefix apps/web
```

Run these in separate terminals from the repository root:

```sh
dotnet run --project apps/api
```

```sh
npm start --prefix apps/web
```

Open **http://127.0.0.1:4300** and create an account. The API runs on port **5080**. Use the web address for normal use; its proxy keeps authentication and API calls on one origin.

On macOS/Linux, `make setup` followed by `make dev` starts the same environment with port checks. Stop with Ctrl+C.

### Docker with PostgreSQL

```sh
docker compose up --build -d
```

Open **http://127.0.0.1:8088**. The first build downloads dependencies. Inspect `docker compose logs api` if startup is still in progress.

Compose is configured for **local development**, bound to loopback. Its sample password and Development environment are local defaults. Follow the [operations guide](docs/operations.md) before public hosting. `docker compose down` retains data; `down -v` intentionally destroys database and key volumes.

## Features implemented

| Area | Available now |
| --- | --- |
| Teaching | Course library, lesson reader, text/callout/example/code blocks, saved completion |
| Curriculum | Objective map, prerequisite relationships, linked teaching and practice |
| Questions | Single choice, select N of M, matching with drag/drop or click placement, dropdown blanks, sequences, numeric answers with tolerance, program-output answers |
| Scoring | Server-side exact or normalized partial credit, stable IDs, explanations, unanswered items scored zero |
| Sessions | Timed short/full mocks, untimed learning with checked feedback, mistake and weak-objective practice |
| Case studies | Shared backgrounds, atomic selection, optional forward-only mock sections |
| Reliability | Server deadlines, automatic expiry, immutable snapshots, revisions, idempotent writes, resume |
| Learner area | Dashboard, history, results review, objective analysis, lesson suggestions, readiness |
| Content workflow | Typed JSON templates, compact exam/1 sources, giveaway lint, compiler, watch mode, hot-reload pack folders, deterministic artifacts, question diffs, Content Studio |
| Product profiles | Course, exam, and hybrid pack capabilities; runtime branding and date/number locales; saved publisher drafts and learner preview |
| Appearance | Three responsive layouts, five colour schemes including dark mode, browser preferences and deployment defaults |
| Publishing | Publisher role, immutable versions, audit events, preserved historical attempts |
| Accounts | Identity password hashing, secure production cookies, CSRF, ownership checks, lockout, rate limits |
| Privacy | Password changes, history export, password-confirmed account deletion |
| Infrastructure | SQLite and PostgreSQL migrations, Docker images, same-origin nginx proxy |
| Verification | xUnit domain/API tests and Playwright desktop/mobile journeys |

The code follows one public type per file with domain, contract, persistence and service folders. Wire-compatible string values are backed by strongly typed enums, and API dashboards/attempts use named DTOs instead of untyped object responses.

Drag/drop has click and keyboard alternatives. Text is rendered as text; content templates cannot execute code or inject HTML. Code blocks display examples; student code execution is future work.

Use **Appearance** in the top bar to try Workspace, Campus, or Focus with Site brand, Ocean, Plum, Terracotta, or Midnight. See the [appearance guide](docs/appearance.md) for deployment defaults and extending the presets.

## Exam readiness

The default rule recommends considering the real exam when the latest **5 short mock attempts**, OR the latest **3 full mock attempts**, each have **strictly more than 90% fully correct answers**.

The rule is configurable per pack. By default, qualifying attempts must also finish before expiry, fall within 90 days and contain 100% previously unseen question families. Evidence is scoped to the current content release.

The partial-credit points score is separate. Exactly 90% does not qualify. Learning sessions never qualify. A failed, expired or insufficiently fresh mock interrupts the streak for that length; successful attempts are not cherry-picked.

This is a transparent practice recommendation, not a calibrated probability of passing an external exam. See [assessment and readiness](docs/assessment.md).

## Create a subject

For exam preparation, the fastest route is one compact JSON file. Use the [exam question generator prompt](docs/prompts/exam-question-generator.md) with any capable AI assistant: it interviews you about your exam, confirms a blueprint, then writes a 100–150-question bank in the `exam/1` format. You can also copy the [sample exam](docs/examples/exam-sample.json). Save the file in `packs/` while `make dev` runs, or import it in Content Studio. LearnForge derives option IDs, scoring and weighted mock exams, and `lint` flags likely answer giveaways:

```sh
dotnet run --project tools/cli -- check packs/my-exam.json
dotnet run --project tools/cli -- lint packs/my-exam.json
```

For courses with lessons, or full control over the pack format:

```sh
dotnet run --project tools/cli -- init packs/my-subject.json
dotnet run --project tools/cli -- check packs/my-subject.json
dotnet run --project tools/cli -- check packs/my-subject.json --watch
dotnet run --project tools/cli -- build packs/my-subject.json --out build/my-subject-1.0.0
```

Edit the generated pack's identity, lessons, objectives, questions and exam blueprints. Validation checks contracts, references, prerequisite cycles, teaching/practice coverage, keys and exam feasibility.

The CLI also creates profile-specific starters:

```sh
dotnet run --project tools/cli -- init packs/my-course.json --profile course
dotnet run --project tools/cli -- init packs/my-exam.json --profile exam
dotnet run --project tools/cli -- init packs/my-hybrid.json --profile hybrid
```

Course packs can be lesson-only and use a completion or mastery goal. Exam packs can be question-first and use assessment/readiness policies. The pack profile and capabilities control which learner surfaces and session modes are available.

New files under `packs/` are validated and seeded when the API starts. With `make dev`, the API also watches `packs/` and publishes a new file or version within about ten seconds. Other deployments can watch a folder too (see [operations](docs/operations.md#drop-folder-hot-reload)). Existing releases are never overwritten: increment `version`. To publish from the browser, register an account, grant it Publisher, sign out/in, then use **Content Studio**:

```sh
dotnet run --project apps/api -- --grant-publisher you@example.com
```

The command must target the same database as the application. Docker equivalent:

```sh
docker compose exec api dotnet LearnForge.Api.dll --grant-publisher you@example.com
```

There is no default administrator account or embedded administrator password. Publisher access exposes private source keys; grant it only to trusted authors.

Read the [authoring guide](docs/authoring.md) for templates and question contracts, and [content engine guide](docs/content-engine.md) for validation and artifact handling.

## Examples and existing solutions

| Pack | Purpose |
| --- | --- |
| [Reasoning foundations](packs/reasoning-foundations.json) | 3 objectives, 3 lessons, 40 template-generated questions across all five formats |
| [Evidence lab](packs/evidence-lab.json) | A different subject: 2 shared cases, 10 questions and section-locking blueprints |
| [ISTQB Foundation 4.0](packs/istqb-ctfl-4.json) | 24 expanded English lessons, all 64 syllabus objectives, 366 original questions and seven fixed full papers with standard and extended-time versions; see the [preparation guide](docs/istqb-preparation.md) and [official reference library](references/istqb/ctfl-4/README.md) |
| [ISTQB AI Testing 2.0](packs/istqb-ct-ai-2.json) | 18 English lessons, all 43 CT-AI v2.0 objectives, 203 original questions and four fixed papers weighted like the exam (44 points, pass 29); see the [CT-AI guide](docs/istqb-ct-ai.md) and [reference library](references/istqb/ct-ai-2/README.md) |
| [ISTQB Testing with Generative AI 1.1](packs/istqb-ct-genai-1.json) | 16 English lessons, all 37 CT-GenAI v1.1 objectives, 197 original questions and four fixed papers weighted like the exam (46 points, pass 30); see the [CT-GenAI guide](docs/istqb-ct-genai.md) and [reference library](references/istqb/ct-genai-1/README.md) |
| [Web foundations sample](docs/examples/exam-sample.json) | The compact exam/1 format: 3 weighted domains, all seven question kinds and a case study (not seeded; copy it into `packs/` to try it) |

These original demonstration materials use CC0-1.0. They are not official certification questions or validated exams. Small banks and superficial variants cannot substantiate real readiness; production authors must create enough independent families.

The existing University and AI-103/AB-100 repositories were reviewed as design references and remain unchanged. Their content has **not** been bulk imported. See [source review](docs/source-review.md) and [migration guide](docs/migration.md).

## Repository layout

```text
apps/api/                API, Identity, persistence, expiry worker
apps/web/                Angular application and Playwright journeys
src/LearnForge.Core/      Contracts, compiler, grader, composer, readiness
tools/cli/               Content authoring CLI
packs/                   Versioned subject sources
references/istqb/        Official ISTQB syllabi, sample exams and exam rules with source maps (local, gitignored)
tests/LearnForge.Tests/   Domain and HTTP integration tests
deploy/                  nginx configuration
scripts/                 Development helper
docs/                    Setup, usage, authoring and engineering guides
```

One API and one database form a modular monolith. Framework rules live in a dependency-light core library. Browser bundles contain renderers and interaction contracts; keys stay on the server until feedback is authorized.

## Verify and build

```sh
dotnet test LearnForge.slnx
dotnet build LearnForge.slnx -c Release
npm run build --prefix apps/web
make check-content
```

With both development servers running:

```sh
cd apps/web
npx playwright install chromium
npx playwright test
```

See [testing](docs/testing.md) for PostgreSQL tests and container verification. Never use an operational database for tests.

## Documentation

| Guide | Purpose |
| --- | --- |
| [Setup](docs/setup.md) | Install, configure, run and troubleshoot |
| [Learner guide](docs/user-guide.md) | Study, practise, resume and review |
| [Authoring](docs/authoring.md) | Create packs, templates and questions |
| [Learning platform prompt](docs/prompts/learning-platform-builder.md) | Interview a learner, design a program, and build a personalized learning experience |
| [Exam question generator prompt](docs/prompts/exam-question-generator.md) | Interview a candidate, then write a 100–150-question exam/1 bank that avoids answer giveaways |
| [Content engine](docs/content-engine.md) | Validation, artifacts and cross-validation limits |
| [Assessment](docs/assessment.md) | Scoring, composition, timing and readiness |
| [Architecture](docs/architecture.md) | Modules, data contracts and extension points |
| [Accessibility design](docs/accessibility/README.md) | WCAG 2.2 AA requirements, interaction/content contracts and release verification |
| [API](docs/api.md) | Integrate accounts, attempts, analytics and publishing |
| [Security](docs/security.md) | Trust boundaries and deployment requirements |
| [Operations](docs/operations.md) | Hosting, migrations, backups and upgrades |
| [Testing](docs/testing.md) | Automated verification |
| [Migration](docs/migration.md) | Map existing University and certification materials |
| [Roadmap](docs/roadmap.md) | Future capabilities |

Historical [architecture](docs/architecture-proposal.md) and [content](docs/content-proposal.md) proposals are preserved for context; they are not inventories of implemented features.

## Technical baseline

ASP.NET Core/EF Core packages are pinned to **10.0.12**, Angular/CDK to **22.2.0**, and Npgsql EF to **10.0.0**. `global.json` selects .NET 10 with SDK feature-band roll-forward; `package-lock.json` records exact frontend dependencies. Docker uses .NET 10 and Node 22.22.3.

SQLite supports local development; PostgreSQL is the target for shared deployments, with its own migrations. Large-scale reporting, load targets, identity recovery integration and independent security/accessibility assessments remain deployment work.
