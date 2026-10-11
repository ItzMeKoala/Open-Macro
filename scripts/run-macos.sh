#!/usr/bin/env bash
# Builds the latest source into ReWASD.app and opens it, closing any older copy first so you never look at a stale build.
#
#   git pull && scripts/run-macos.sh
set -euo pipefail

cd "$(dirname "$0")/.."

[[ "$(uname -m)" == "arm64" ]] && RID="osx-arm64" || RID="osx-x64"

scripts/package-macos.sh "$RID"
open -n "artifacts/macos/$RID/ReWASD.app"
echo "The bottom bar shows the build (version and git commit), so you can check it is the one you just made."
