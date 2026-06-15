#!/usr/bin/env bash
# Build the engine-neutral core as netstandard2.1 DLLs and drop them where Unity consumes them
# (Assets/Plugins/Armagetron). These three assemblies ARE the VR head's contract with the game —
# protocol, networking, render/camera model, and the ArmaLib facade — and they build & verify in
# the normal dotnet toolchain (no Unity needed). Re-run whenever the core changes.
set -euo pipefail
cd "$(dirname "$0")/.."   # repo root

OUT="Game.Oculus/Assets/Plugins/Armagetron"
mkdir -p "$OUT"

echo "==> building ArmaLib (pulls in Core.Protocol + Core.Net) for netstandard2.1"
dotnet build ArmaLib/ArmaLib.csproj -c Release

SRC="ArmaLib/bin/Release/netstandard2.1"
cp -v "$SRC/Armagetron.Protocol.dll" "$OUT/"
cp -v "$SRC/Armagetron.Net.dll"      "$OUT/"
cp -v "$SRC/Armagetron.Lib.dll"      "$OUT/"

echo
echo "✅ Core DLLs copied to $OUT"
echo "   Open Game.Oculus in Unity; the Armagetron.Oculus asmdef references these by name."
