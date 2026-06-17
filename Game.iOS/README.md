# Game.iOS — iPhone / iPad head

Status: **BUILDS & RUNS ON THE SIMULATOR (2026-06-16).** A MonoGame iOS head that reuses the
exact shared stack the desktop and Android heads use — `ArmagetronGame` (Game.Shared), `ArmaLib`,
and the render glue. Steering is touch (tap-to-turn) like Android. First compiled and launched on
the iPhone 17 Pro simulator (iOS 26.5) once Xcode 26.5 + the `ios` dotnet workload were installed.

## Verified on the simulator (2026-06-16)

Launched on iOS 26.5 (iPhone 17 Pro sim). Confirmed via screenshot + `simctl`/`log show`:
- **#1 BundleResource layout ✅** — the connect screen renders with the real Rajdhani font and the
  nine-slice UI panels; the shared loaders find media at `NSBundle.MainBundle.BundlePath/media/...`.
  No FontStash "no font source" crash (the bug that hit Android's APK asset tree did not recur).
- **#2 Soft keyboard ✅** — `KeyboardInput.Show` pops the iOS soft keyboard for the connect fields.
- **#3 Audio ✅** — iOS `AVPlayer`/`URLAsset` loads the bundled `.ogg` music with no errors (the
  plain file-path play path; no Android-style OpenFd workaround needed).
- **#4 Orientation/safe-area ⏳** — content renders landscape; notch/safe-area insets not yet
  eyeballed on a notched device.
- **#5 Live-server gate ⏳** — needs the `0.2.9.3.0` server up plus a CONNECT tap (simctl has no
  tap primitive); register/render/turns over `.61:4534` not yet exercised from the simulator.

Three fixes were needed to take it from scaffold to running (all on 2026-06-16):
1. **Explicit `Core.Protocol` reference** (also added to Desktop/Android/RenderHarness/Web) — ArmaLib
   bundles Core.* with `PrivateAssets="all"`, so its types stopped flowing transitively to consumers
   when ArmaLib became a publishable NuGet package (commit 7c856cd). This had silently broken the
   whole-solution build, not just iOS.
2. **`MediaPlayer` alias** in `Game.Shared/Audio/MusicController.cs` — the iOS SDK's `MediaPlayer`
   *namespace* shadows MonoGame's `MediaPlayer` *class*.
3. **`#if !IOS` guard** on `Game.Exit()` in `ArmagetronGame.cs` — Apple forbids programmatic exit.

## To build & run

```bash
# 1. Install the full Xcode app from the App Store, then point the toolchain at it (the App Store
#    install usually sets this already — check with `xcode-select -p`):
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer

# 2. Install the iOS workload (needs sudo on macOS):
sudo dotnet workload install ios

# 3. Build for the simulator RID. A device build additionally needs an Apple signing identity +
#    provisioning; a simulator build does not.
dotnet build Game.iOS/Game.iOS.csproj -c Debug -p:RuntimeIdentifier=iossimulator-arm64

# 4. Boot a simulator, then install + launch via simctl. This is more reliable than the
#    `-t:Run` / mlaunch path, which raced on simulator state ("Unable to lookup ... Shutdown"):
UDID=$(xcrun simctl list devices available | grep -m1 "iPhone 17 Pro" | grep -oE "[0-9A-F-]{36}")
xcrun simctl boot "$UDID"; open -a Simulator; xcrun simctl bootstatus "$UDID" -b
APP=Game.iOS/bin/Debug/net9.0-ios/iossimulator-arm64/Armagetron.iOS.app
xcrun simctl install "$UDID" "$APP"
xcrun simctl launch "$UDID" com.armagetron.client
xcrun simctl io "$UDID" screenshot /tmp/aa_ios.png   # eyeball the result
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
