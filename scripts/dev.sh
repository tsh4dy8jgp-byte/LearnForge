#!/usr/bin/env bash
set -euo pipefail
# Existing tools/packages are used; startup and shutdown share the installer supervisor.
exec bash "$(dirname "$0")/install.sh" --no-install --no-browser "$@"
