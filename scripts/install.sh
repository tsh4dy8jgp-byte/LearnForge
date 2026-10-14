#!/usr/bin/env bash
# Compatible with the Bash 3.2 shipped by macOS. Node is not a bootstrap prerequisite.
set -euo pipefail
task_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$task_root"
task_read_only=false
task_native_only=false
task_expect_value=
task_check=false
task_prepare=false
task_no_install=false
for task_argument in "$@"; do
  if [ -n "$task_expect_value" ]; then
    if [ -z "$task_argument" ] || [[ "$task_argument" == --* ]]; then echo "Missing value for $task_expect_value" >&2; exit 2; fi
    if [ "$task_expect_value" != --data-dir ]; then
      [[ "$task_argument" =~ ^[0-9]{1,5}$ ]] || { echo "Invalid value for $task_expect_value" >&2; exit 2; }
      task_limit=65535
      if [ "$task_expect_value" = --startup-timeout ]; then task_limit=3600; fi
      if [ "$((10#$task_argument))" -lt 1 ] || [ "$((10#$task_argument))" -gt "$task_limit" ]; then echo "Invalid value for $task_expect_value" >&2; exit 2; fi
    fi
    task_expect_value=
    continue
  fi
  case "$task_argument" in
    --check) task_read_only=true; task_check=true ;;
    --no-install) task_read_only=true; task_no_install=true ;;
    --install-only) task_prepare=true ;;
    --no-browser) ;;
    --api-port|--web-port|--startup-timeout|--data-dir) task_expect_value="$task_argument" ;;
    --repair-native) task_native_only=true ;;
    --help|-h)
      cat <<'HELP'
LearnForge: install missing requirements, start the local servers, and open your browser.
  bash scripts/install.sh [options]
  --check                 Read-only requirement report
  --install-only          Prepare the application without starting it
  --no-install            Use existing tools/packages; build and start
  --no-browser            Print the URL without opening a browser
  --api-port PORT         Require this API port (default: find one near 5080)
  --web-port PORT         Require this web port (default: find one near 4300)
  --startup-timeout SEC   Readiness timeout (default: 180 seconds)
  --data-dir DIRECTORY    Separate database and session keys (default: apps/api/data)
Tools/cache/logs: .learnforge/   Stop both servers: Ctrl+C
HELP
      exit 0 ;;
    *) echo "Unknown option: $task_argument" >&2; exit 2 ;;
  esac
done
[ -z "$task_expect_value" ] || { echo "Missing value for $task_expect_value" >&2; exit 2; }
if "$task_prepare" && { "$task_check" || "$task_no_install"; }; then echo '--install-only cannot be combined with --check or --no-install.' >&2; exit 2; fi
trap 'echo "LearnForge bootstrap failed. Review the preceding error." >&2; exit 3' ERR

task_system="$(uname -s)"
case "$task_system" in
  Darwin)
    task_os=darwin
    task_os_major="$(sw_vers -productVersion | cut -d. -f1)"
    [ "$task_os_major" -ge 14 ] || { echo 'LearnForge requires macOS 14 or newer.' >&2; exit 2; }
    task_machine="$(uname -m)"
    if [ "$(sysctl -in hw.optional.arm64 2>/dev/null || true)" = 1 ]; then task_machine=arm64; fi
    ;;
  Linux)
    task_os=linux
    task_machine="$(uname -m)"
    task_id="$(sed -n 's/^ID=//p' /etc/os-release | tr -d '"')"
    task_release="$(sed -n 's/^VERSION_ID=//p' /etc/os-release | tr -d '"')"
    case "$task_id:$task_release" in
      ubuntu:22.04) task_icu=libicu70; task_ssl=libssl3 ;;
      ubuntu:24.04) task_icu=libicu74; task_ssl=libssl3t64 ;;
      debian:12) task_icu=libicu72; task_ssl=libssl3 ;;
      debian:13) task_icu=libicu76; task_ssl=libssl3t64 ;;
      *) echo "Automatic installation supports Ubuntu 22.04/24.04 and Debian 12/13; found $task_id $task_release." >&2; exit 2 ;;
    esac
    ;;
  *) echo "Unsupported operating system: $task_system. On Windows use scripts/install.ps1." >&2; exit 2 ;;
esac
case "$task_machine" in
  arm64|aarch64) task_arch=arm64 ;;
  x86_64|amd64) task_arch=x64 ;;
  *) echo "Unsupported CPU architecture: $task_machine" >&2; exit 2 ;;
esac

# Install only missing distro packages. Never elevate the whole launcher.
if [ "$task_os" = linux ]; then
  task_packages=(ca-certificates curl tar gzip xz-utils libc6 libgcc-s1 libstdc++6 libgssapi-krb5-2 "$task_icu" "$task_ssl" tzdata zlib1g)
  if [ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]; then task_packages+=(xdg-utils); fi
  task_missing=()
  for task_package in "${task_packages[@]}"; do
    if [ "$(dpkg-query -W -f='${Status}' "$task_package" 2>/dev/null || true)" != 'install ok installed' ]; then
      task_missing+=("$task_package")
    fi
  done
  if [ "${#task_missing[@]}" -gt 0 ]; then
    echo "Missing OS packages: ${task_missing[*]}"
    if "$task_read_only"; then
      echo "Install with: sudo apt-get update && sudo apt-get install -y ${task_missing[*]}" >&2
      exit 2
    fi
    if [ "$(id -u)" = 0 ]; then
      task_elevate=()
    elif command -v sudo >/dev/null 2>&1; then
      task_elevate=(sudo)
      if [ ! -t 0 ] && ! sudo -n true 2>/dev/null; then
        echo "Run interactively, or install: sudo apt-get update && sudo apt-get install -y ${task_missing[*]}" >&2
        exit 3
      fi
    else
      echo "Install as administrator: apt-get update && apt-get install -y ${task_missing[*]}" >&2
      exit 3
    fi
    "${task_elevate[@]}" apt-get update
    "${task_elevate[@]}" apt-get install -y "${task_missing[@]}"
    for task_package in "${task_missing[@]}"; do
      [ "$(dpkg-query -W -f='${Status}' "$task_package")" = 'install ok installed' ] || exit 3
    done
  fi
fi
if "$task_native_only"; then exit 0; fi

task_node_version="$(tr -d '[:space:]' < "$task_root/.node-version")"
[[ "$task_node_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo 'Invalid .node-version.' >&2; exit 2; }
task_managed="$task_root/.learnforge/tools/node/v$task_node_version"
task_node=
# Use a Node/npm pair from the same installation; do not mix hosts from different PATH entries.
for task_candidate in "$task_managed/bin/node" "$(command -v node || true)"; do
  [ -n "$task_candidate" ] && [ -x "$task_candidate" ] || continue
  if [ "$("$task_candidate" --version 2>/dev/null || true)" = "v$task_node_version" ] && [ "$("$task_candidate" -p 'process.arch' 2>/dev/null || true)" = "$task_arch" ]; then
    task_npm_cli="$(dirname "$task_candidate")/../lib/node_modules/npm/bin/npm-cli.js"
    task_expected_npm="$("$task_candidate" -p "JSON.parse(require('fs').readFileSync('apps/web/package.json','utf8')).packageManager.replace('npm@','')")"
    if [ -f "$task_npm_cli" ] && [ "$("$task_candidate" "$task_npm_cli" --version 2>/dev/null || true)" = "$task_expected_npm" ]; then
      task_node="$task_candidate"
      break
    fi
  fi
done
if [ -z "$task_node" ] && "$task_read_only"; then
  echo "Required: Node $task_node_version with the npm version in apps/web/package.json." >&2
  echo 'Run bash scripts/install.sh to acquire a private copy.' >&2
  if command -v dotnet >/dev/null 2>&1; then dotnet --version || true; fi
  exit 2
fi
if [ -z "$task_node" ]; then
  for task_tool in curl tar shasum; do
    command -v "$task_tool" >/dev/null 2>&1 || {
      if [ "$task_tool" = shasum ] && command -v sha256sum >/dev/null 2>&1; then continue; fi
      echo "Missing bootstrap utility: $task_tool" >&2; exit 3;
    }
  done
  mkdir -p "$task_root/.learnforge/tools/node" "$task_root/.learnforge/cache"
  task_lock="$task_root/.learnforge/node-bootstrap.lock"
  if ! mkdir "$task_lock" 2>/dev/null; then
    task_owner="$(cat "$task_lock/pid" 2>/dev/null || true)"
    if [[ "$task_owner" =~ ^[0-9]+$ ]] && ! kill -0 "$task_owner" 2>/dev/null; then
      rm -f "$task_lock/pid"; rmdir "$task_lock" 2>/dev/null || true
      mkdir "$task_lock" 2>/dev/null || { echo 'Another Node installation is running.' >&2; exit 3; }
    else
      echo 'Another Node installation is running; retry when it finishes.' >&2; exit 3
    fi
  fi
  echo "$$" > "$task_lock/pid"
  task_stage="$(mktemp -d "$task_root/.learnforge/tools/node/staging.XXXXXX")"
  task_archive="node-v$task_node_version-$task_os-$task_arch"
  if [ "$task_os" = linux ]; then task_extension=tar.xz; else task_extension=tar.gz; fi
  task_archive="$task_archive.$task_extension"
  task_cache="$task_root/.learnforge/cache/$task_archive"
  task_partial="$task_cache.part.$$"
  task_sums="$task_stage/SHASUMS256.txt"
  task_cleanup() {
    rm -f "$task_partial" "$task_lock/pid"
    rm -rf "$task_stage"
    rmdir "$task_lock" 2>/dev/null || true
  }
  trap task_cleanup EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM
  echo "Installing Node $task_node_version ($task_os/$task_arch)…"
  task_base="https://nodejs.org/dist/v$task_node_version"
  curl --fail --location --proto '=https' --proto-redir '=https' --connect-timeout 20 --max-time 120 --retry 2 "$task_base/SHASUMS256.txt" -o "$task_sums"
  task_hash="$(awk -v file="$task_archive" '$2 == file {print $1}' "$task_sums")"
  [[ "$task_hash" =~ ^[a-fA-F0-9]{64}$ ]] || { echo 'Official Node checksum was not found.' >&2; exit 3; }
  task_verify() {
    [ -f "$task_cache" ] || return 1
    if command -v shasum >/dev/null 2>&1; then
      task_actual="$(shasum -a 256 "$task_cache" | awk '{print $1}')"
    else
      task_actual="$(sha256sum "$task_cache" | awk '{print $1}')"
    fi
    [ "$task_actual" = "$task_hash" ]
  }
  if ! task_verify; then
    curl --fail --location --proto '=https' --proto-redir '=https' --connect-timeout 20 --max-time 600 --retry 2 "$task_base/$task_archive" -o "$task_partial"
    mv "$task_partial" "$task_cache"
    task_verify || { echo 'Node archive checksum mismatch; installation aborted.' >&2; exit 3; }
  fi
  mkdir "$task_stage/extracted"
  tar -xf "$task_cache" -C "$task_stage/extracted" --strip-components=1
  [ "$("$task_stage/extracted/bin/node" --version)" = "v$task_node_version" ] || exit 3
  task_npm_cli="$task_stage/extracted/lib/node_modules/npm/bin/npm-cli.js"
  task_expected_npm="$("$task_stage/extracted/bin/node" -p "JSON.parse(require('fs').readFileSync('apps/web/package.json','utf8')).packageManager.replace('npm@','')")"
  [ "$("$task_stage/extracted/bin/node" "$task_npm_cli" --version)" = "$task_expected_npm" ] || { echo 'Downloaded npm does not match packageManager.' >&2; exit 3; }
  # Keep an interrupted or invalid prior copy; never overwrite a working global tool.
  if [ -e "$task_managed" ]; then mv "$task_managed" "$task_managed.previous.$$"; fi
  mv "$task_stage/extracted" "$task_managed"
  task_node="$task_managed/bin/node"
  task_cleanup
  trap - EXIT INT TERM
fi
export PATH="$(dirname "$task_node"):$PATH"
exec "$task_node" "$task_root/scripts/install.mjs" "$@"
