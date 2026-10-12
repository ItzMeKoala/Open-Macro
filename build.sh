#!/usr/bin/env bash
# Builds the ReWASD app for this computer and puts it in ./dist :
#   macOS   ->  dist/ReWASD.app   (double-click it, or drag it to Applications)
#   Windows ->  dist/ReWASD.exe   (from Git Bash; or just double-click Build.cmd)
#
#   ./build.sh                 # for this computer
#   ./build.sh windows         # the Windows .exe (works from a Mac too)
#   ./build.sh mac             # the Mac .app
#   ./build.sh --run           # open the result afterwards
set -euo pipefail

cd "$(dirname "$0")"

TARGET=""
RUN=0
for arg in "$@"; do
  case "$arg" in
    mac|macos) TARGET="mac" ;;
    windows|win) TARGET="windows" ;;
    --run) RUN=1 ;;
    *) echo "Unknown option: $arg" >&2; exit 1 ;;
  esac
done

if [[ -z "$TARGET" ]]; then
  case "$(uname -s)" in
    Darwin) TARGET="mac" ;;
    *) TARGET="windows" ;;
  esac
fi

mkdir -p dist

if [[ "$TARGET" == "mac" ]]; then
  scripts/package-macos.sh
  RID="$([[ "$(uname -m)" == "arm64" ]] && echo osx-arm64 || echo osx-x64)"
  rm -rf dist/ReWASD.app
  cp -R "artifacts/macos/$RID/ReWASD.app" dist/ReWASD.app
  echo
  echo "Done: $PWD/dist/ReWASD.app  (double-click it, or drag it to Applications)"
  [[ "$RUN" == 1 ]] && open -n dist/ReWASD.app
else
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "The .NET 9 SDK is needed to build ReWASD: https://dotnet.microsoft.com/download/dotnet/9.0" >&2
    exit 1
  fi
  rm -rf artifacts/windows
  dotnet publish src/ReWASD.UI/ReWASD.UI.csproj -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none -o artifacts/windows
  mv artifacts/windows/ReWASD.UI.exe artifacts/windows/ReWASD.exe
  cp artifacts/windows/ReWASD.exe dist/ReWASD.exe
  echo
  echo "Done: $PWD/dist/ReWASD.exe  (just double-click it on Windows; no install needed)"
fi
