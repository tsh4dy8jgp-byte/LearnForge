# Testing

Run the native suite:

~~~sh
dotnet test LearnForge.slnx
~~~

The tests cover compiler/template budgets, goal and mastery policy validation, all five graders, partial credit, invalid contracts, readiness, the mastery and next-step rules, scenario atomicity, delivery privacy, authentication, CSRF, IDOR, idempotent writes, server grading, expiry, the evidence ledger, enrollment, cross-release lesson progress, focused and objective practice, the ledger backfill, request validation, migrations, export, deletion, concurrent revisions, section locks, immutable publishing and dashboard readiness.

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

The browser journey registers a learner, completes a lesson, checks the map, uses all five formats, reloads/resumes, submits, reviews and confirms the course appears under My courses with mastery and next steps. A second journey adds, archives and restores a course and switches course tabs with the keyboard. The mobile test checks layout and that keys are absent from catalog JSON.

Content and image builds:

~~~sh
make check-content
docker compose build
~~~

The suite does not include load, fuzz, visual-regression, email, MFA/SSO, offline guarantees or formal accessibility audits. Add those before institutional certification.
