#!/usr/bin/env bash
# Builds ReWASD.app for macOS (self-contained: the .NET runtime is bundled, nothing to install).
#
#   scripts/package-macos.sh              # for this Mac's CPU
#   scripts/package-macos.sh osx-x64      # Intel
#   scripts/package-macos.sh osx-arm64    # Apple silicon
#
# The app is ad-hoc signed, which is enough to run it on the Mac that built it. macOS ties the Accessibility and
# Input Monitoring permissions to the app's signature, so after rebuilding you may have to allow it again.
set -euo pipefail

cd "$(dirname "$0")/.."

RID="${1:-}"
if [[ -z "$RID" ]]; then
  [[ "$(uname -m)" == "arm64" ]] && RID="osx-arm64" || RID="osx-x64"
fi

# A running copy would keep showing the old build, so close it first
osascript -e 'tell application "ReWASD" to quit' >/dev/null 2>&1 || true
pkill -f "ReWASD.app/Contents/MacOS/ReWASD" >/dev/null 2>&1 || true

if ! command -v dotnet >/dev/null 2>&1; then
  echo "The .NET 9 SDK is needed to build ReWASD: https://dotnet.microsoft.com/download/dotnet/9.0" >&2
  exit 1
fi

OUT="artifacts/macos/$RID"
APP="$OUT/ReWASD.app"
VERSION="0.1.0"

rm -rf "$OUT"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

dotnet publish src/ReWASD.UI.Mac/ReWASD.UI.Mac.csproj \
  -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=false -p:DebugType=none \
  -o "$APP/Contents/MacOS"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>ReWASD</string>
  <key>CFBundleDisplayName</key><string>ReWASD Open Source</string>
  <key>CFBundleIdentifier</key><string>org.rewasd.opensource</string>
  <key>CFBundleExecutable</key><string>ReWASD</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSSupportsAutomaticGraphicsSwitching</key><true/>
</dict>
</plist>
PLIST

chmod +x "$APP/Contents/MacOS/ReWASD"
codesign --force --deep --sign - "$APP"

echo
echo "Built $APP"
echo "Open it with:  open \"$APP\""
echo "On first use, allow Accessibility and Input Monitoring in System Settings > Privacy & Security."
