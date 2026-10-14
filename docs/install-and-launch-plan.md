# Install and launch plan

Status: implemented by scripts/install.sh, scripts/install.ps1, and scripts/install.mjs. The executable behavior and current platform/option details are documented in [setup.md](setup.md#automatic-installation-and-startup). The design below records the original plan; native Windows/Linux installation still requires verification on those machines.

Running one command from an extracted or cloned LearnForge checkout should check the machine, install missing dependencies, prepare the application, start both servers, and open the working site in the default browser. Running the same command again should reuse valid installations and preserve accounts, progress, published courses, and session keys.

## Scope and entry points

Plan assumption: support macOS, Windows, and selected Linux distributions. Start with macOS versions supported by .NET 10, Windows 11, Ubuntu 22.04/24.04, and Debian 12/13, on x64 and ARM64 where the official binaries support them. Verify the intersection of .NET and Node support during implementation. Other distributions need an explicit dependency adapter before claiming automatic installation support.

Entry-point commands:

```sh
# macOS / Linux, from any working directory
bash /path/to/LearnForge/scripts/install.sh
```

```powershell
# Windows; execution-policy override applies only to this invocation
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\path\to\LearnForge\scripts\install.ps1
```

The normal invocation installs what is missing, starts the app in the foreground, and opens the browser. Ctrl+C stops the servers. Re-running the command relaunches the application. First installation requires internet access; subsequent launches can use installed tools and cached packages.

This is the native local application path using SQLite and two servers. The SDK includes the needed .NET and ASP.NET Core runtimes. A separate SQLite installation, Docker, PostgreSQL, Git, Make, a global Angular CLI, and Playwright browsers are not prerequisites for this path.

## Files and responsibilities

| File | Responsibility |
| --- | --- |
| `scripts/install.sh` | Bash 3.2-compatible bootstrap for macOS/Linux; locate checkout, detect native platform, acquire Node, invoke shared installer. |
| `scripts/install.ps1` | Windows PowerShell 5.1-compatible bootstrap with the same public behavior. |
| `scripts/install.mjs` | Shared version checks, .NET acquisition, package restore, builds, content validation, server supervision, readiness checks, and browser launch. Use Node built-ins so bootstrapping requires no npm dependencies. |
| `.node-version` | One plain-text Node bootstrap version, initially `22.22.3`. |
| `.gitignore` | Ignore `.learnforge/`, which holds managed tools, verified download cache, logs, install fingerprints, generated proxy configuration, and instance records. |
| `Makefile`, `scripts/dev.sh` | Add a convenient install target; share server supervision with the new launcher rather than maintain two conflicting startup implementations. Preserve a start-without-install mode for established development workflows. |
| `README.md`, `docs/setup.md`, CI workflow | Document the one-command workflow and add installer verification. Keep Node version declarations consistent with `.node-version`. |

Bootstraps must work without Node, Python, Homebrew, WinGet, or Make already installed. Bootstrap Node downloads use an atomic version-specific lock; the shared installer uses a separate checkout installation lock. Interrupted or concurrent runs must not leave a partial tool installation marked ready.

## Execution sequence

1. **Check the machine and checkout.** Resolve the repository from the script's location, including paths containing spaces. Check required project files, OS version, native CPU architecture, writable installation/data directories, sufficient free storage, and downloader/extraction/checksum facilities. Distinguish an ARM machine running an emulated shell from a native x64 machine. Report an unsupported platform before changing it. Before bootstrapping Node, install any missing download/archive tools, certificates, or Node native-library prerequisites using the supported OS adapter; this early adapter must run in Bash or PowerShell without depending on Node or the .NET SDK. `--check` and `--no-install` report missing facilities without attempting that repair.

2. **Find or install Node and npm.** Check managed tools and executable candidates on PATH. Initially reuse the repository's tested pair, Node `22.22.3` and npm `10.9.8`; otherwise acquire the official platform archive into `.learnforge/tools/node/`. That Node release includes the required npm version. Use a private tool path for this application, preserving other installations and shell profiles. Later broaden reuse to other compatible versions only with explicit compatibility checks and test coverage.

3. **Find or install the .NET SDK.** Read `global.json`, currently SDK `10.0.100` with `rollForward: latestFeature`. Test candidates from the repository root so SDK selection honors that file. Require a stable SDK that satisfies the policy and the matching ASP.NET Core runtime; a runtime-only installation or an incompatible SDK major is insufficient. If missing, resolve a current stable SDK within the allowed 10.0 channel from Microsoft's release metadata and extract its official archive into `.learnforge/tools/dotnet/`. Record the exact selected version. Configure `DOTNET_ROOT` only for a managed SDK and put the chosen tools first in the child environment.

4. **Install missing operating-system facilities.** On supported Linux distributions, use explicit distro/version mappings for the .NET native libraries and bootstrap tools, including TLS certificates, ICU, OpenSSL, compression, Kerberos, and C/C++ runtime libraries as applicable. Detect what is missing before calling the native package manager. Only that package-manager step may require sudo; never run the whole application as root. On Windows, diagnose missing native runtime dependencies and invoke the official prerequisite installer if required, handling elevation and restart-required results. Recheck the tools after installation. SDK/archive installation alone does not prove these facilities are present.

5. **Restore and build the application.** Restore NuGet packages using repository configuration and build the API and content CLI. Run `npm ci --prefix apps/web` when dependencies are absent or the successful install fingerprint differs; include the lockfile, package manifest, Node/npm versions, OS, and architecture in that fingerprint. Write success markers only after completion. Invoke the repository-local Angular CLI, never a global CLI. Use incremental .NET builds on repeat launches. Ordinary startup does not run the test suite or regenerate OpenAPI contracts.

6. **Validate courses before startup.** Recursively enumerate every `packs/**/*.json` file and invoke the content compiler. Report the exact file and diagnostic on failure before starting servers. Do not rely on the current Makefile's explicit course list. Compiler errors stop startup; quality lint remains informational unless strict checking is requested, since existing demonstration packs have known warnings. Preserve the existing database and Data Protection directory under `apps/api/data/`; the API applies migrations and publishes valid packs during its normal startup.

7. **Select ports and generate the proxy.** Prefer API port `5080` and web port `4300`, bound to `127.0.0.1`. If a default port is occupied by another application, choose an available port from a bounded range and print the selected URL. Explicit port overrides should fail clearly when occupied. Generate `.learnforge/run/proxy.json` targeting the selected API port for both `/api` and `/health`; pass its absolute path to Angular without editing tracked configuration. Account for bind races: treat an address-in-use startup failure as a bounded retry, not success.

8. **Start and supervise both servers.** Launch the built API DLL directly with the selected dotnet host, working directory `apps/api`, and explicit content root. Launch the local Angular CLI with the selected Node executable and working directory `apps/web`. Set Development, SQLite, loopback hosts, the repository's pack directory, and a five-second pack-watch interval in child environments. Keep separate API, web, and installer logs in `.learnforge/logs/`; show concise progress and useful log tails on failure. Track actual child processes and process groups rather than shell wrappers. A server exiting should stop its companion and make the launcher exit with a failure code.

9. **Wait for application readiness.** Poll the API `/health` until its database is healthy, then confirm its catalog endpoint responds. Wait for the web page and verify `/health` and `/api/catalog` through the web origin, proving the Angular proxy works. Apply configurable, finite deadlines, check child liveness while polling, and report the failed check with the relevant log path. Do not open the browser just because the processes were spawned.

10. **Open the site and retain supervision.** Open the final web URL using `open` on macOS, Windows URL handling, or `xdg-open`/`gio open` on a Linux desktop. Install a missing Linux opener through the supported package adapter when possible. Browser launching must not block supervision. If there is no graphical session, registered browser, or working opener, leave the healthy servers running and print the URL and actionable reason. `--no-browser` explicitly supports headless use. Hold the launcher open and clean up only its own processes on Ctrl+C or termination.

## Repeat runs and failure behavior

- A verified, live instance record for this checkout can be reused: confirm process identity/start time, recorded executable/checkout paths, and readiness, then open its recorded URL. Do not identify an instance solely by a port or a health response. Clear stale records without terminating unrelated processes.
- Reuse installed tools and verified cached archives. Download into temporary files, check official SHA-256 checksums for Node and SHA-512 metadata for .NET, extract into staging directories, and promote only after executable/version checks pass. Use finite network timeouts and bounded retries. Respect proxy settings without printing credentials or bypassing certificate checks.
- If system dependencies require elevation, display the packages and invoke the OS's normal sudo/UAC mechanism. A noninteractive run without necessary privileges must exit with the exact remaining installation command rather than hang or claim success.
- Package restore or content validation failures must prevent server startup. Startup failures must stop companion processes. Browser-opening failures must preserve an otherwise healthy server session.
- Preserve data on every normal run. Do not reset databases, grant Publisher access, install a background service, or change firewall settings as part of this installer. Publisher role setup remains the existing explicit maintenance operation.
- Installing from archives intentionally keeps application tooling separate from global development tools. Microsoft documents archive-based installation and the need to configure environment variables and native dependencies; this plan uses that approach so a package manager is not itself a prerequisite.

## Command options

Keep Bash and PowerShell options equivalent, mapping their arguments into the shared installer:

| Option | Behavior |
| --- | --- |
| Default | Check, install missing requirements, prepare, start, wait, open browser, supervise. |
| `--check` | Read-only facility report and compatibility status; do not bootstrap missing Node just to produce the report. |
| `--install-only` | Install and prepare successfully, then exit without servers or browser. |
| `--no-install` | Require existing compatible facilities and prepared dependencies; build/start without installing tools or packages. |
| `--no-browser` | Start and supervise, printing the URL. |
| `--api-port`, `--web-port` | Explicit loopback ports with synchronized generated proxy configuration. |
| `--startup-timeout` | Override the finite readiness deadline. |
| `--help` | Explain commands, data/log locations, installation behavior, and shutdown. |

Emit separate exit codes for unsupported/check failures, installation failures, restore/build/content failures, and server-readiness failures. Treat user-requested shutdown as a clean exit.

## Implementation order and verification

1. Add the shared supervisor and readiness/browser handling, using the existing installed tools. Route `scripts/dev.sh` through that path and verify account access, pack hot reload, and shutdown.
2. Add manifest-driven version detection, managed tool acquisition, archive verification, and Bash/PowerShell bootstraps.
3. Add distro-specific prerequisite adapters, repeated-run fingerprints, instance reuse, command options, and failure reporting.
4. Update documentation and add CI checks. Keep macOS's built-in Bash and Windows PowerShell compatibility explicit.

Acceptance checks should exercise behavior that could strand a user or damage an installation:

- Fresh machines with no SDK or Node complete installation, return a healthy same-origin site, and attempt browser opening only after readiness. Exercise x64 and ARM64 paths on real native hosts before claiming both architectures are supported.
- Missing SDK versus runtime-only .NET, wrong SDK major, wrong Node version, and mismatched npm select the correct repair path while preserving existing tools.
- A second run performs no unnecessary tool downloads or npm reinstall, preserves accounts and progress, and reuses a live instance of the same checkout.
- Changed lockfiles trigger dependency preparation; interrupted downloads/extraction, checksum mismatch, failed restore, denied elevation, and invalid course JSON leave no false success state.
- Occupied ports generate a correct replacement proxy; explicit occupied ports fail; unrelated processes remain running.
- Slow startup, either child crashing, Ctrl+C, and stale instance records produce correct cleanup and useful diagnostics. No API or Angular descendants remain after shutdown.
- Headless Linux and opener failure print the usable URL while keeping healthy servers available; paths containing spaces work on every supported OS.

Use isolated data directories for automated smoke tests. Run shell syntax checks and shared Node tests for process supervision/download failures, then the existing relevant API/browser journeys to verify the application behavior is preserved. OS-library installation needs minimal supported Linux images or VMs; preinstalled CI runner tools alone do not verify the fresh-machine path. Browser-opener dispatch can be stubbed in CI, with a manual desktop smoke test on each platform.

## Official references

- [Node 22.22.3 archive, bundled npm version, platform binaries, and checksums](https://nodejs.org/en/download/archive/v22.22.3).
- [Detect installed .NET SDKs and runtimes](https://learn.microsoft.com/en-us/dotnet/core/install/how-to-detect-installed-versions).
- [.NET installation on macOS, including manual installation and supported OS versions](https://learn.microsoft.com/en-us/dotnet/core/install/macos).
- [.NET archive installation and native dependencies on Linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual).
- [.NET installation and manual environment configuration on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows).
- [Ubuntu dependency and installation guidance](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install) and [Debian guidance](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian).
- [.NET release metadata index](https://github.com/dotnet/core/blob/main/release-notes/releases-index.json).
