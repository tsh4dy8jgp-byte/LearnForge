# LearnForge

**A reusable education and exam-practice framework built with ASP.NET Core 10 and Angular 22.**

LearnForge connects teaching materials, curriculum objectives, mixed-format practice, saved attempts and evidence-based review. Subjects live in versioned JSON packs, so changing from reasoning to university coursework or certification preparation does not require rebuilding the application.

This repository contains a runnable implementation, two original example packs, a content compiler, automated tests and deployment scaffolding. It is a single-organization foundation. Institutional SSO, multi-tenancy and other future capabilities are listed in the [roadmap](docs/roadmap.md).

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
| Content workflow | Typed JSON templates, compiler, watch mode, deterministic artifacts, question diffs, Content Studio |
| Publishing | Publisher role, immutable versions, audit events, preserved historical attempts |
| Accounts | Identity password hashing, secure production cookies, CSRF, ownership checks, lockout, rate limits |
| Privacy | Password changes, history export, password-confirmed account deletion |
| Infrastructure | SQLite and PostgreSQL migrations, Docker images, same-origin nginx proxy |
| Verification | xUnit domain/API tests and Playwright desktop/mobile journeys |

The code follows one public type per file with domain, contract, persistence and service folders. Wire-compatible string values are backed by strongly typed enums, and API dashboards/attempts use named DTOs instead of untyped object responses.

Drag/drop has click and keyboard alternatives. Text is rendered as text; content templates cannot execute code or inject HTML. Code blocks display examples; student code execution is future work.

## Exam readiness

The default rule recommends considering the real exam when the latest **5 short mock attempts**, OR the latest **3 full mock attempts**, each have **strictly more than 90% fully correct answers**.

The rule is configurable per pack. By default, qualifying attempts must also finish before expiry, fall within 90 days and contain 100% previously unseen question families. Evidence is scoped to the current content release.

The partial-credit points score is separate. Exactly 90% does not qualify. Learning sessions never qualify. A failed, expired or insufficiently fresh mock interrupts the streak for that length; successful attempts are not cherry-picked.

This is a transparent practice recommendation, not a calibrated probability of passing an external exam. See [assessment and readiness](docs/assessment.md).

## Create a subject

```sh
dotnet run --project tools/cli -- init packs/my-subject.json
dotnet run --project tools/cli -- check packs/my-subject.json
dotnet run --project tools/cli -- check packs/my-subject.json --watch
dotnet run --project tools/cli -- build packs/my-subject.json --out build/my-subject-1.0.0
```

Edit the generated pack's identity, lessons, objectives, questions and exam blueprints. Validation checks contracts, references, prerequisite cycles, teaching/practice coverage, keys and exam feasibility.

New files under `packs/` are validated and seeded after rebuilding and restarting the API. Existing releases are never overwritten: increment `version`. To publish without restarting, register an account, grant it Publisher, sign out/in, then use **Content Studio**:

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

These original demonstration materials use CC0-1.0. They are not official certification questions or validated exams. Small banks and superficial variants cannot substantiate real readiness; production authors must create enough independent families.

The existing University and AI-103/AB-100 repositories were reviewed as design references and remain unchanged. Their content has **not** been bulk imported. See [source review](docs/source-review.md) and [migration guide](docs/migration.md).

## Repository layout

```text
apps/api/                API, Identity, persistence, expiry worker
apps/web/                Angular application and Playwright journeys
src/LearnForge.Core/      Contracts, compiler, grader, composer, readiness
tools/cli/               Content authoring CLI
packs/                   Versioned subject sources
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
