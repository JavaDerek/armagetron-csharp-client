# Game.Oculus — Meta Quest / OpenXR VR head

Status: **STARTED 2026-06-14 (scaffolded, not yet opened in Unity).** Per `ARCHITECTURE.md`, VR is
the **Unity** path: Unity ingests the engine-neutral core (`netstandard2.1`) and adds an XR rig on
top. This folder is that Unity project's source — the integration scripts, assembly definition, and
package manifest. The heavy lifting (protocol, dead-reckoning, geometry, camera math) is **reused
unchanged** from Core.Protocol, including the `Scene3DBuilder` / `Camera3D` written for the desktop
3D views — so the Unity-specific code here is deliberately thin.

## Why it is not built/verified yet

There is no Unity Editor in this dev environment, so none of the `UnityEngine`-referencing scripts
can be compiled here. What **is** verified: the three core DLLs the project consumes build clean as
`netstandard2.1` (run `./build-core-dlls.sh`). The Unity scripts are written against ArmaLib's real
public API + standard UnityEngine APIs and will compile once opened in the Editor with the DLLs in
place. This project is **not** in `ArmagetronClient.sln` (Unity owns its own build).

## First-time setup

```bash
# 1. Build the core DLLs into Assets/Plugins/Armagetron (git-ignored; regenerate any time):
./build-core-dlls.sh

# 2. Open this folder (Game.Oculus) as a project in Unity Hub.
#    Target editor: Unity 6 (6000.0 LTS). Add the Android Build Support + OpenJDK/SDK/NDK modules.
```

Then in the Editor:
1. **Switch platform** to Android (File ▸ Build Settings ▸ Android).
2. **XR Plug-in Management ▸ OpenXR** (Android tab): enable OpenXR + the **Meta Quest** feature
   group. Add an interaction profile (Oculus Touch Controller).
3. Let Unity resolve the packages in `Packages/manifest.json` (OpenXR, XR Management, XR Interaction
   Toolkit, Input System, URP). Unity will generate `.meta`, `ProjectSettings/`, and `Library/` on
   first open — those are intentionally **not** committed here.
4. Create a scene with an **XR Origin (VR)**, add an empty GameObject, attach `ArmagetronRunner`,
   and wire its Inspector fields:
   - `XrOrigin` → the XR Origin transform
   - `WallMaterial` → an unlit/URP material with **Cull = Off** and vertex-colour + emission (neon)
   - `FloorMaterial` → the designer's `arena_tile` (or a grid material)
   - `CycleMaterial` → a tintable material (placeholder until the Phase-2 lightcycle model)
5. Press Play (Quest Link) or build & deploy the APK to the headset.

## What the scaffold does (and the reuse story)

`ArmagetronRunner` (one MonoBehaviour) is the entire head:
- Connects via `UiArmaClient` — **identical** to desktop/Android/iOS.
- Each frame calls the engine-neutral `Scene3DBuilder.Build(...)` → `WorldScene`, then the
  engine-neutral, unit-tested `WallMesh.From(world)` tessellates the quads (vertex order, triangle
  winding, per-vertex colour, 16-/32-bit index decision) and `WallMeshBuilder` just copies those
  arrays into one Unity mesh via `VrConvert`; a pooled cube is placed per cycle.
- Because the head connects through the shared `UiArmaClient`, it inherits the spectator-join
  behaviour: it reaches `Connected` and renders the live arena a second or two after login (our own
  cycle appears at the next round), instead of blocking on spawn.
- The VR camera is **rig placement only** (the HMD owns head rotation):
  - **First-person:** rig anchored at the cycle head (`CameraSettings.EyeHeight`).
  - **Third-person:** rig placed at the chase eye from `Camera3D` (same math as the desktop 3D view).

`VrConvert` is the only type bridge (core `Vec2`/`Vec3`/`RenderColor` → `UnityEngine.Vector3`/`Color`);
rule 4 keeps those out of the core. World space already matches Unity (Y-up), so it's a copy.

## VR porting plan / open work (in rough order)

1. **Open in Unity & make it compile** — resolve packages, fix any API drift (Unity version skew),
   confirm the asmdef picks up the three DLLs.
2. **Comfort.** First-person on a turning lightcycle is nausea-prone. Add: a static cockpit/reference
   frame, vignette-on-turn (tunnelling), snap vs. smooth options. Third-person is the safe default —
   ship that first.
3. **Steering via XR controllers.** `HandleSteering()` currently reads `Input` (Editor-testable).
   Replace with Input System / XR Interaction Toolkit actions (e.g. left/right grip or thumbstick).
4. **Walls/floor materials.** Author the neon URP materials (Cull Off, emissive, vertex colour);
   wire the designer's `arena_tile` for the floor. See `DESIGN_BRIEF_3D.md`.
5. **Lightcycle model.** Replace the placeholder cube with the Phase-2 `.glb` prefab (tinted per
   player) — same asset the desktop 3D view will use.
6. **HUD in VR.** The 2D `AppShell` overlay doesn't apply in VR; build a world-space/wrist canvas for
   connect + score/round state (the data is on `UiArmaClient`: `Status`, `DrainEvents()`).
7. **Connect UX.** The defaults connect on Start; add a world-space connect panel (host/port/name)
   instead of hard-coded fields.
8. **Live-server gate** (CLAUDE.md step 4): register + render + steer against a real `0.2.9.3.0`
   server from the headset.

## Reviewed findings (2026-06-15, by inspection — no Editor available)

Code review of `ArmagetronRunner` against the ArmaLib API while extracting `WallMesh`. None block
opening the project; fix as part of the porting plan above. Ordered roughly by impact:

- **Per-cycle material instancing leak.** `SyncCycles` sets `r.material.color` each frame, which
  instantiates a material clone per pooled cube and never destroys them on teardown. Use a
  `MaterialPropertyBlock` (`r.SetPropertyBlock`) to tint without instancing — the idiomatic, GC-free
  Quest approach — or cache the instanced material and `Destroy` it in `OnDestroy`.
- **Wall mesh + arrays rebuilt every frame.** `WallMesh.From(...)` allocates fresh vertex/index/
  colour arrays each `Update`; on a mobile Quest GPU that is avoidable GC churn. Consider rebuilding
  only when the wall set changes, or reusing buffers (`Mesh.SetVertices(List<>)`).
- **Third-person forces `XrOrigin.rotation`.** Rotating the XR tracking space (not just a camera)
  yaws the player's whole world and is a known comfort/nausea hazard in VR. Prefer rotating a camera
  pivot *inside* a fixed origin, or leave yaw to the HMD — folds into the comfort work (item 2).
- **First-person rig omits `NoseOffset`.** `PositionRig` FP places the eye at `(pos, EyeHeight)`
  with no forward nudge, unlike `Camera3D.Compute` FP (which adds `forward * NoseOffset` to clear the
  player's own wall). Reuse `Camera3D` for the FP eye too, for parity with the desktop 3D view.
- **`ArenaSize`/`WallHeight` hardcoded.** Same root issue as every head — see
  the arena-bounds note: the real arena size should come from `desc=51`, not the `176.78` default,
  or the floor quad and walls mis-scale on bigger maps.

## Notes

- Default name is `Vlad` (the server's name gate rejects freshly-invented names like `AaBot` — see the
  registration_timing_race / registration_auth_research notes).
- `WallHeight = 8` is a tuned constant; the wire protocol carries no Z yet (same caveat as the desktop
  3D view). Revisit if `ARENA_SIZE`/height are ever decoded from `desc=51`.
- Clean-room: this head consumes only our own assemblies — no GPL Armagetron source is referenced.
