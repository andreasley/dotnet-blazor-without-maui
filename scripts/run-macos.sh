#!/bin/zsh
# Builds the macOS app and runs it from the terminal so that log output is visible.
#   CONFIG=Release scripts/run-macos.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${CONFIG:-Debug}"
ARCH="$(uname -m)"; [[ "$ARCH" == "x86_64" ]] && ARCH="x64"
RID="osx-$ARCH"

dotnet build "$ROOT/src/BlazorNative.macOS/BlazorNative.macOS.csproj" -c "$CONFIG" -r "$RID"
exec "$ROOT/src/BlazorNative.macOS/bin/$CONFIG/net11.0-macos/$RID/BlazorNative.app/Contents/MacOS/BlazorNative"
