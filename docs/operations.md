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
- Content__WatchSeconds (default 0, off) to poll the pack directories and publish new files or versions without a restart

Pack directories are seeded with the same compiler rules as the bundled `packs/` folder: one invalid pack stops startup, and a configured directory that does not exist is an error. Seeding never replaces a stored release. When a file changes but keeps a stored version, the API logs a warning and keeps serving the stored release; bump the version to publish the change. A release found twice in one pass (for example in two folders) is published once.

### Drop folder (hot reload)

With `Content__WatchSeconds` set to a positive number of seconds, the API rescans the bundled folder and every configured directory on that interval. It reads a file only after the file has stayed unchanged for a whole interval, so half-written files are skipped. A new id@version is published at once, and the newest `PublishedAt` becomes the course's latest release, so dropping an older version makes it current. Invalid files are logged once per change and skipped; the API keeps serving. Deleting a file changes nothing, because releases are immutable. Startup stays strict: a broken file still in a folder stops the next start. `make dev` watches the repository's `packs/` folder every 5 seconds.

Docker example (the image runs as a non-root user, so the folder must be readable):

~~~yaml
services:
  api:
    environment:
      Content__PackDirectories__0: /content
      Content__WatchSeconds: "10"
    volumes:
      - ./my-packs:/content:ro
~~~

Several replicas may watch a shared folder; a release that another replica or Content Studio publishes first is detected on the next scan.

The API persists Data Protection keys in data/keys. Back this directory securely and restrict access. Loss invalidates cookies and requires sign-in again.

## Migrations and backup

Automatic migration is convenient locally. In production set Database:AutoMigrate=false, run the reviewed migration step during maintenance (`dotnet LearnForge.Api.dll --migrate`), then start the API. SQLite and PostgreSQL have separate migration sets.

The migration step also backfills the evidence ledger and enrollments for attempts completed before the ledger existed, and logs how many attempts it wrote. Applying the schema another way (for example an EF SQL script) skips the backfill: the API then logs a warning at startup with the number of completed attempts that have no ledger rows, and mastery and question freshness stay incomplete until `--migrate` runs. The backfill is idempotent and safe to run on several instances.

Rolling back after this release means restoring the pre-upgrade backup: older images reject pack JSON that contains `goal` or `mastery` and attempts saved with objective practice. Likewise, images before weighted blueprints reject releases and attempt snapshots whose blueprints contain `objectiveWeights` (every exam/1 pack with domain weights).

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
