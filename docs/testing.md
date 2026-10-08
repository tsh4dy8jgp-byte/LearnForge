# Testing

Accessibility acceptance is defined in the [WCAG 2.2 AA verification plan](accessibility/verification.md), with [criterion traceability](accessibility/conformance-matrix.md) and complete learner/publisher journeys. It requires manual keyboard, assistive-technology and content evaluation as well as planned automated scans. The commands below run existing tests; they are not evidence of full WCAG conformance.

Run the native suite:

~~~sh
dotnet test LearnForge.slnx
~~~

The tests cover compiler/template budgets, the compact exam/1 format and its opaque option IDs, quality lint rules, weighted blueprint composition (including an oracle check that unweighted papers are unchanged), pack-directory seeding and the hot-reload watcher, goal and mastery policy validation, all question graders, partial credit, invalid contracts, readiness, the mastery and next-step rules, scenario atomicity, delivery privacy, authentication, CSRF, IDOR, idempotent writes, server grading, expiry, the evidence ledger, enrollment, cross-release lesson progress, focused and objective practice, the ledger backfill, request validation, migrations, export, deletion, concurrent revisions, section locks, immutable publishing and dashboard readiness.

Run against a disposable PostgreSQL database:

~~~sh
docker compose up -d db
LEARNFORGE_TEST_POSTGRES='Host=127.0.0.1;Port=55432;Database=learnforge_test;Username=learnforge;Password=learnforge-local-only' dotnet test LearnForge.slnx --no-restore
~~~

Frontend build, unit tests (Vitest) and the generated API type drift check:

~~~sh
npm run build --prefix apps/web
npm test --prefix apps/web
make check-api-types
~~~

With API on 5080 and Angular on 4300:

~~~sh
npm start --prefix apps/web
dotnet run --project apps/api
cd apps/web
npx playwright install chromium
npx playwright test
~~~

The browser journey registers a learner, completes a lesson, checks the map, uses the five selection formats, reloads/resumes, submits, reviews and confirms the course appears under My courses with mastery and next steps. A second journey adds, archives and restores a course and switches course tabs with the keyboard. The mobile test checks layout and that keys are absent from catalog JSON. Numeric and program-output interactions also have component tests.

Vitest specs live beside their feature code. They cover reusable learning components, question interactions, appearance, dashboard rendering, course capabilities and deep-link setup, session/CSRF boundaries, Content Studio diagnostics and ungraded preview, attempt response recovery/results, and page-scoped clock/autosave cleanup. The browser suite also verifies actual route query binding, page titles and navigation focus.

Content and image builds (`make check-content` also checks the exam/1 sample and lints it with `--strict`):

~~~sh
make check-content
docker compose build
~~~

The suite does not include load, fuzz, visual-regression, email, MFA/SSO, offline guarantees or formal accessibility audits. Add those before institutional certification.
