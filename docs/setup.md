# Setup and first run

## Prerequisites

Use the .NET 10 SDK, Node.js 22.22.3 and npm 10+ for the tested toolchain. Other Node versions must satisfy [Angular's compatibility table](https://angular.dev/reference/versions). Docker Compose is optional.

```sh
dotnet --version
node --version
npm --version
docker compose version
```

Commands start from the repository root. No external identity provider, paid service or cloud account is needed locally.

## Native development

1. Run `dotnet restore LearnForge.slnx`.
2. Run `npm ci --prefix apps/web`.
3. Start `dotnet run --project apps/api`.
4. In another terminal, start `npm start --prefix apps/web`.
5. Open http://127.0.0.1:4300 and register.

The launch profile sets Development and API port 5080. The frontend proxy forwards API requests. Startup applies migrations and validates/seeds bundled packs.

Local data lives in `apps/api/data/learnforge.db`; session Data Protection keys live under `apps/api/data/keys`. Both are ignored by version control.

On macOS/Linux, `make setup` and `make dev` automate this. The launcher checks its ports and does not stop unrelated applications. On Windows, use the manual two-terminal setup. Environment examples use POSIX syntax; PowerShell uses `$env:Name = 'value'`.

## Docker with PostgreSQL

```sh
docker compose up --build -d
docker compose ps
docker compose logs --tail=50 api
```

Open http://127.0.0.1:8088. PostgreSQL is available on local port 55432. API and nginx images run as non-root.

Optionally copy `.env.example` to `.env` to change local ports/password. PostgreSQL initialization variables apply only to a new data directory; editing the file alone does not change the password inside an existing database.

`docker compose down` stops services and preserves named volumes. Removing volumes intentionally deletes their data.

## Native API against PostgreSQL

```sh
docker compose up -d db
Database__Provider=Postgres \
ConnectionStrings__Database='Host=127.0.0.1;Port=55432;Database=learnforge;Username=learnforge;Password=learnforge-local-only' \
dotnet run --project apps/api
```

These credentials are for the local sample database. Keep real connection strings in a secret manager or development user secrets. SQLite data does not automatically migrate to PostgreSQL when the provider changes.

## Enable Content Studio

Register, then grant the role against the same database:

```sh
dotnet run --project apps/api -- --grant-publisher author@example.com
```

The process grants the role and exits without starting another listening server. For Docker:

```sh
docker compose exec api dotnet LearnForge.Api.dll --grant-publisher author@example.com
```

Sign out/in. Content Studio appears. Upload or paste a source pack (a full pack or a compact exam/1 file), validate, inspect diagnostics and quality warnings, then publish a new immutable version.

Without Studio, `make dev` also watches the repository's `packs/` folder: save a valid pack there and it is published within about ten seconds. The [operations guide](operations.md#drop-folder-hot-reload) covers watched folders for other setups.

## Ports and origins

Change the Angular start port and API launch profile if necessary. Update `apps/web/proxy.conf.json` when changing the API port. Set `LEARNFORGE_WEB_URL` for Playwright on a different web port.

Use one hostname consistently: `localhost` and `127.0.0.1` have different cookies and browser storage.

## Troubleshooting

| Symptom | Resolution |
| --- | --- |
| API unavailable | Inspect API startup output and proxy port |
| Address in use | Choose another port or stop only the process you own |
| Login disappears | Use the proxy origin; local HTTP uses Development; Production requires HTTPS |
| CSRF token expired | Refresh, then sign in again if needed |
| Studio missing | Verify role grant used the correct database; sign out/in |
| Pack edits absent | Increment version, then rebuild/restart or publish |
| Another session cannot start | Resume or finish the active session for that course |
| Readiness false | Check strict >90%, latest streak, expiry, freshness and content version |
| Compiler output error | Use an empty/new build directory |
| PostgreSQL connection fails | Check health, connection variables and existing volume credentials |

A restart does not pause or reset deadlines. The worker finalizes expired saved attempts after startup.
