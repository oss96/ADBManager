#!/usr/bin/env bash
# Runs the Swift (macOS app) and C# (Windows app) bridge smoke tests against
# the real Rust core and the in-process fake adb server. Each test is skipped
# when its toolchain is missing. Usage: scripts/check-bridges.sh
set -euo pipefail
cd "$(dirname "$0")/.."

cargo build -q -p adbm-ffi --release
cargo build -q -p adbm-core --features test-support --example fake_adb
PORT=${ADBM_FAKE_PORT:-5199}
target/debug/examples/fake_adb "$PORT" >/dev/null 2>&1 &
FAKE=$!
trap 'kill $FAKE 2>/dev/null || true' EXIT
sleep 0.5
LIB="$PWD/target/release"
status=0

if command -v swiftc >/dev/null; then
  echo "== Swift bridge (apps/macos)"
  out=$(mktemp -d)
  swiftc -swift-version 5 -I apps/macos/Sources/CAdbm \
    apps/macos/Sources/AdbManager/Core.swift apps/macos/Tools/BridgeSmoke/main.swift \
    -L"$LIB" -ladbm_ffi -o "$out/bridge-smoke"
  LD_LIBRARY_PATH="$LIB" DYLD_LIBRARY_PATH="$LIB" "$out/bridge-smoke" "$PORT" || status=1
else
  echo "== Swift bridge: skipped (no swiftc)"
fi

if command -v dotnet >/dev/null; then
  echo "== C# bridge (apps/windows)"
  dotnet build -v q -c Release apps/windows/AdbManager.Smoke >/dev/null
  LD_LIBRARY_PATH="$LIB" DYLD_LIBRARY_PATH="$LIB" \
    dotnet apps/windows/AdbManager.Smoke/bin/Release/net8.0/AdbManager.Smoke.dll "$PORT" || status=1
else
  echo "== C# bridge: skipped (no dotnet)"
fi
exit $status
