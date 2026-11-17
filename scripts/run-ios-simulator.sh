#!/bin/zsh
# Builds the iOS app for the simulator, installs it on an iPhone simulator and launches it with console output.
#   SIMULATOR_UDID=<udid> scripts/run-ios-simulator.sh     # pick a specific simulator
#   SKIP_BUILD=1 scripts/run-ios-simulator.sh              # reuse the last build
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${CONFIG:-Debug}"
BUNDLE_ID="com.yourcompany.blazornative"
ARCH="$(uname -m)"; [[ "$ARCH" == "x86_64" ]] && ARCH="x64"
RID="iossimulator-$ARCH"
APP="$ROOT/src/BlazorNative.iOS/bin/$CONFIG/net11.0-ios/$RID/BlazorNative.app"

if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
    dotnet build "$ROOT/src/BlazorNative.iOS/BlazorNative.iOS.csproj" -c "$CONFIG" -r "$RID"
fi

# Default to an available iPhone simulator (prefer a single-screen model over the foldable "iPhone Duo").
UDID="${SIMULATOR_UDID:-$(xcrun simctl list devices available -j | python3 -c '
import json, sys
devices = json.load(sys.stdin)["devices"]
phones = [d for runtime, lst in devices.items() if "iOS" in runtime for d in lst if d["name"].startswith("iPhone")]
phones.sort(key=lambda d: ("Duo" in d["name"], d["name"]))
print(phones[0]["udid"])
')}"

xcrun simctl bootstatus "$UDID" -b >/dev/null
xcrun simctl install "$UDID" "$APP"
echo "Launching $BUNDLE_ID on simulator $UDID (Ctrl+C to stop)"
exec xcrun simctl launch --console-pty "$UDID" "$BUNDLE_ID"
