# Operations

## Configuration

Use environment variables or a secret store:

- Database__Provider=Postgres
- ConnectionStrings__Database
- Database__AutoMigrate=false for controlled production migrations
- Auth__AllowRegistration=false for closed enrollment
- AllowedHosts
- ASPNETCORE_ENVIRONMENT=Production
- Content__PackDirectories__0, Content__PackDirectories__1, … to seed packs kept outside this repository, such as a content repository's generated packs

Pack directories are seeded with the same compiler rules as the bundled `packs/` folder: one invalid pack stops startup, and a configured directory that does not exist is an error. Seeding never replaces a stored release. When a file changes but keeps a stored version, the API logs a warning and keeps serving the stored release; bump the version to publish the change.

The API persists Data Protection keys in data/keys. Back this directory securely and restrict access. Loss invalidates cookies and requires sign-in again.

## Migrations and backup

Automatic migration is convenient locally. In production set Database:AutoMigrate=false, run the reviewed migration step during maintenance (`dotnet LearnForge.Api.dll --migrate`), then start the API. SQLite and PostgreSQL have separate migration sets.

The migration step also backfills the evidence ledger and enrollments for attempts completed before the ledger existed, and logs how many attempts it wrote. Applying the schema another way (for example an EF SQL script) skips the backfill: the API then logs a warning at startup with the number of completed attempts that have no ledger rows, and mastery and question freshness stay incomplete until `--migrate` runs. The backfill is idempotent and safe to run on several instances.

Rolling back after this release means restoring the pre-upgrade backup: older images reject pack JSON that contains `goal` or `mastery` and attempts saved with objective practice.

Back up PostgreSQL, Data Protection keys, published release artifacts and configuration secrets. Test restoring to an isolated database. A database backup without keys preserves records but invalidates sessions.

## Health and hosting

GET /health is the current liveness/database check. Add platform readiness, latency, 4xx/5xx, 409, 429, expiry-worker and publication metrics. The application logs safe categories and trace IDs but does not ship an OpenTelemetry collector.

Compose binds PostgreSQL to 55432 and nginx to 8088 on loopback. Images run non-root. Public deployments need TLS at ingress, explicit trusted forwarding, real secrets and private database networking. Multiple API replicas require shared keys and a distributed worker lock.

## Upgrade procedure

1. Review .NET, Angular, EF and Npgsql release notes.
2. Run compiler, Core, API and browser tests.
3. Review migrations and pack diagnostics.
4. Back up database, keys and release artifacts.
5. Deploy and run register/login/start/save/submit/export/authoring smoke tests.
6. Monitor errors and expiry processing.
7. Keep the previous image and backup for rollback.

Pack versions are immutable. Publish a new version to correct content.
