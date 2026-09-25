# Architecture

LearnForge is a modular monolith. The API owns identity, content releases, attempts, grading and analytics; Angular owns presentation and interaction; the Core library contains subject-neutral rules.

## Runtime flow

~~~text
Angular browser
  /api through same-origin proxy
ASP.NET Core API
  Identity + antiforgery + rate limiting
  AttemptService + Analytics + expiry worker
Entity Framework Core
  SQLite (development) or PostgreSQL (shared deployment)
~~~

The API loads a compiled pack into a PackRelease. Starting an attempt copies the selected questions, scenarios, blueprint and readiness policy into an immutable snapshot. A newly published release cannot rewrite work already in progress.

## Core contracts

A pack contains Objective, Lesson, Question, Scenario, Blueprint, ReadinessPolicy and SourceReference records. Lessons use text, callout, example and code blocks. Questions use stable IDs, family IDs, objective links, typed options/slots, grading contracts and explanations.

The implementation uses enums for question kinds, block kinds, scoring policies, assessment mode/size, attempt status and practice focus. API request and response contracts are named records; dashboard, attempt and result endpoints do not expose untyped object return values or anonymous public response shapes.

The compiler rejects unknown JSON properties, invalid IDs, missing references, prerequisite cycles, unsupported kinds, malformed answer keys, unsafe source URLs and infeasible blueprints.

## API modules

Program.cs configures Identity cookies, CSRF, security headers, rate limits, authorization, database migration and seed loading.

AttemptService is the state machine. It validates ownership, creates deterministic selections, shuffles delivery copies, saves idempotently, enforces revisions and locks, finalizes deadlines and produces sanitized views. DeliveryQuestion deliberately omits keys and explanations while an attempt is active.

Analytics derives dashboard evidence from completed attempts. Mock evidence is kept separate from learning feedback. Objective samples, recommendations and readiness remain inspectable data.

ExpiryWorker periodically finds overdue mock attempts and finalizes the server's latest responses. It is safe to retry because completion is guarded by status and EF concurrency.

## Persistence model

Users and Identity tables hold credentials and roles. PackReleases stores immutable compiled JSON plus a content hash. Attempts hold a snapshot, answers, revision, timing, outcome and freshness. ResponseEvents provide request-ID idempotency and an append-only trail for writes. Completions store lesson progress. AuditEvents record publishing and lifecycle actions.

The current implementation stores pack JSON and answers as relational text. PostgreSQL is the deployment provider; JSONB, partitioning and materialized analytics can be introduced after measuring real workloads.

## Extending the framework

Add a question kind in this order:

1. Add its Core contract and validation rules.
2. Add server Grader validation and scoring.
3. Add composer feasibility rules.
4. Add the sanitized delivery projection.
5. Add Angular interaction and a keyboard alternative.
6. Add Core, API and Playwright coverage.
7. Update the authoring and learner guides.

Keep answer IDs stable across releases when you want meaningful family analytics. Change family ID when the competency or prompt pattern changes. Importers should map external material into the pack contract and run the compiler; they should not bypass validation or write directly to release tables.
