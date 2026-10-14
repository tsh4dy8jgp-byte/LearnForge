# Setup and first run

Flashcard exports require the Python environment and Unicode font documented in [flashcard setup](flashcards.md#setup-and-cli). Docker includes these dependencies. Native users run `make setup-flashcards` and set `LEARNFORGE_EXPORT_PYTHON` before starting the API.

## Automatic installation and startup

Download/extract or clone the repository, then run:

~~~sh
# macOS 14+, Ubuntu 22.04/24.04, or Debian 12/13
bash scripts/install.sh
~~~

~~~powershell
# Windows 11, Windows PowerShell 5.1 or PowerShell 7
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/install.ps1
~~~

Both entry points support x64 and ARM64 official binaries. The PowerShell execution-policy override applies only to this invocation. Git, Make, Docker, PostgreSQL, a separate SQLite installation, and a global Angular CLI are not prerequisites for the native installer.

The installer checks the Node/npm pair pinned by .node-version and packageManager, and a stable .NET SDK selected by global.json. It uses compatible installed tools or installs official, checksum-verified archives under .learnforge/tools/. Global tools and shell profiles are preserved. On supported Linux distributions, missing native libraries and download utilities are installed using apt-get; sudo can request your password. Run interactively when those packages are missing.

It then restores/builds the API and content compiler, installs locked web dependencies, validates all JSON files recursively under packs/, and starts both servers on loopback. Defaults are 5080 for the API and 4300 for the website; occupied default ports select nearby available ports and a generated proxy follows them. Explicit occupied ports fail without terminating the other application.

The browser opens only after API, database, web page, and proxied API checks succeed. If browser opening is unavailable, the terminal prints the URL and keeps the servers running. Keep the terminal open; Ctrl+C stops the servers. Running the same command while this checkout is already running reuses the verified instance.

| Option | Purpose |
| --- | --- |
| --check | Read-only toolchain/native-facility report; missing requirements produce exit code 2. Web preparation status is informational. |
| --install-only | Install and prepare, then exit. |
| --no-install | Require existing tools/packages; build, validate, and start without acquiring them. |
| --no-browser | Print the URL without launching a browser. |
| --api-port 5180 --web-port 4380 | Choose explicit ports; proxy configuration is generated automatically. |
| --startup-timeout 300 | Allow up to 300 seconds for server readiness; default 180, maximum 3600. |
| --data-dir "/path/with spaces" | Use a separate SQLite database and session-key directory. Relative paths are resolved against the checkout. |
| --help | Print usage. |

On macOS/Linux, make install runs the whole installer, make setup prepares it, and make dev starts with existing tools/packages and no browser. Windows uses the PowerShell entry point with the same options.

Application data defaults to apps/api/data/learnforge.db and apps/api/data/keys/. Installer state is ignored by Git under .learnforge/: tools/, cache/, logs/installer.log, separate timestamped server logs, and run/proxy.json. Successful web dependency installs are fingerprinted by the lockfile and toolchain, so repeat installations skip npm ci until those inputs change. API builds are incremental; ordinary launches do not run tests or regenerate API types.

Exit codes: 0 success or requested shutdown; 2 missing/invalid requirements or arguments; 3 installation failure; 4 restore/build/course-validation failure; 5 server startup/runtime failure. A failed build or invalid pack prevents startup. Inspect the printed log path for details.

Use the explicit manual API commands below for PostgreSQL or other hosting configuration; the installer starts the local SQLite configuration. When using --data-dir, maintenance commands must target the same database via ConnectionStrings__Database and session keys via DataProtection__KeyDirectory.

## Manual prerequisites

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

Environment examples below use POSIX shell syntax. In PowerShell, assign variables with `$env:Name = 'value'`.

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
