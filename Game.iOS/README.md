# Game.iOS — iPhone / iPad head

Status: **STARTED 2026-06-14 (scaffolded, not yet compiled).** A MonoGame iOS head that reuses the
exact shared stack the desktop and Android heads use — `ArmagetronGame` (Game.Shared), `ArmaLib`,
and the render glue. Steering is touch (tap-to-turn) like Android.

## Why it is not built/verified yet

This dev Mac has only the `android` dotnet workload and Xcode **Command-Line Tools** (no full Xcode),
so the `net9.0-ios` target framework cannot be restored or built here. The project is therefore
**deliberately excluded from `ArmagetronClient.sln`** so `build-and-test.sh` stays green. Everything
here is correct-by-construction (mirrors the proven Android head) but needs a one-time toolchain
setup to compile and run.

## To build & run

```bash
# 1. Install the full Xcode app from the App Store, then point the toolchain at it:
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer

# 2. Install the iOS workload:
dotnet workload install ios

# 3. Build (simulator/device). A device build needs an Apple signing identity + provisioning;
#    a simulator build does not.
dotnet build Game.iOS/Game.iOS.csproj -c Debug

# 4. Run on a booted simulator:
dotnet build Game.iOS/Game.iOS.csproj -t:Run \
  -p:RuntimeIdentifier=iossimulator-arm64 -p:_DeviceName=:v2:udid=<SIMULATOR_UDID>
```

(Once it compiles, add it to `ArmagetronClient.sln` only if the CI/dev machine has the iOS workload;
otherwise keep it standalone so the solution build doesn't require Xcode.)

## How it maps to the existing architecture

| Concern            | Desktop                | Android                          | iOS (this head)                         |
|--------------------|------------------------|----------------------------------|-----------------------------------------|
| Host loop          | `ArmagetronGame`       | `ArmagetronGame`                 | `ArmagetronGame` (shared, unchanged)    |
| Entry point        | top-level `Program.cs` | `Activity1 : AndroidGameActivity`| `Program : UIApplicationDelegate`       |
| Input              | `DesktopShellInput`    | `AndroidShellInput` (TouchPanel) | `IosShellInput` (TouchPanel, same APIs) |
| Server access      | `UiArmaClient`         | `UiArmaClient`                   | `UiArmaClient` (identical)              |
| Media (fonts/etc.) | copied next to binary  | `AndroidAsset` → unpack to filesDir | `BundleResource` → read from .app bundle |
| Steering           | arrow keys             | tap-to-turn                      | tap-to-turn (`touchControls: true`)     |

The only iOS-specific code is `Program.cs` (app delegate) and `IosShellInput.cs` — both small and
modelled directly on the Android equivalents. `mediaRoot` points at the app bundle (a real directory
on iOS), so there is **no asset-unpack step** the way Android needs.

## Things to verify once it builds (can't be checked without the toolchain)

1. **BundleResource layout.** The `.csproj` uses `<LogicalName>media/…</LogicalName>` to preserve the
   `media/` tree inside the `.app`. Confirm the loaders find files at
   `NSBundle.MainBundle.BundlePath/media/...` — if Apple flattens or remaps, adjust `LogicalName`.
2. **Soft keyboard.** `KeyboardInput.Show` is wired exactly as on Android; confirm it pops for the
   connect-screen fields on a real device.
3. **Audio.** The shared `MusicController`/`SfxController` read `.ogg`/`.wav` from `mediaRoot`. Android
   needed an OpenFd/uncompressed workaround that does **not** apply to iOS (plain file paths), but
   confirm playback on device.
4. **Orientation/safe-area.** `Info.plist` is landscape-only; check the HUD respects the notch/safe
   area on modern iPhones (may want to inset the overlay).
5. **Live-server gate** (CLAUDE.md step 4): join a real `0.2.9.3.0` server and confirm register +
   render + turns, same as the other heads.
