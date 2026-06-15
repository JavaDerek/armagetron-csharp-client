# Design Brief — Phase 2: 3D Perspective Views

This is the "separate model brief" that `DESIGN_BRIEF.md` (Phase 1) promised we'd write once we
took on 3D. The 3D **camera system and procedural geometry already ship** (third-person chase +
first-person cockpit, see `ARCHITECTURE.md` / `Core.Protocol/Game/Camera3D.cs`). The client builds
the whole scene procedurally from the wire data — light walls are the cycle trails extruded
straight up, the floor is the existing tiled `arena_tile_64.png`, and cycles are **placeholder
billboards** of the existing `cycle_128.png` sprite stood up to face the camera.

So nothing here blocks the feature. This brief is the **polish pass**: art that replaces the
placeholders to make the 3D views look like the C++ client (and better). Everything is optional and
incremental — the client renders fine without any of it.

## What carries over unchanged (no new work)

- The 8-colour CVD-checked player palette (`--p1…--p8`) and the neon-on-black additive-glow recipe.
- Typography (Orbitron / Rajdhani / Share Tech Mono) — the HUD overlay is drawn in 2D on top of the
  3D view, identical to the 2D mode.
- App icon, splash, store assets.

## What we'd like, in priority order

### 1. Lightcycle model (the one real "model" deliverable)
- **Format:** glTF 2.0 (`.glb`, single file) preferred; FBX acceptable. Y-up, 1 unit = 1 arena unit
  (the cycle body is roughly **6–9 units** long; see `Scene3DBuilder`/`Camera3D` scale).
- **Style:** Tron lightcycle silhouette, **white / greyscale master** so we tint it to the player
  colour at runtime (same one-model-eight-colours trick as the sprites). Emissive mask welcome for
  the glow edges.
- **Orientation:** nose pointing **+X** at identity (matches `Camera3D.Heading`), wheels on the
  floor plane (Y=0).
- **Budget:** low-poly, mobile-friendly (this also has to run on Android) — target ≤ ~3–5k tris,
  one texture atlas ≤ 1024².
- **Rig:** none required for v1 (no animation). A spinning-wheel bone is a nice-to-have, not needed.

### 2. Wall texture / material
- Right now walls are flat bright-colour quads. A **tileable wall texture** (a vertical neon-grid /
  energy-field strip, white master, tinted per player, with an emissive channel) would replace the
  flat fill. 256×256 tileable, designed to repeat along the wall length.
- Want a **height cue**: brighter at the base, fading toward the top, so tall walls read as walls.

### 3. Floor upgrade (optional)
- The current floor is the 2D `arena_tile_64.png` tiled in 3D. A **3D-specific floor** with a subtle
  horizon glow / vignette and a brighter grid near the player would sell the depth better. Same
  64×64 (or 128×128) tileable PNG slot — drop-in.

### 4. Skybox / backdrop (optional)
- A dark gradient or starfield backdrop instead of pure black behind the arena rim. Either a cubemap
  or a single equirectangular PNG.

### 5. Explosion in 3D (optional)
- We already have `explosion_sheet_512.png` (4×4, 16 frames). In 3D it can billboard at the crash
  point. If you'd rather, a small **3D particle burst spec** (count, colours, lifetime) is welcome,
  but the sprite sheet is sufficient.

## Camera reference (so model scale/feel matches)

- **Third-person chase:** floats behind + above the cycle, orbit-able with the mouse (right-drag),
  zoom on scroll. Default ~32 units back, ~28° elevation.
- **First-person cockpit:** eye ~3 units above the floor, just ahead of the player's own wall,
  looking along the heading.
- Toggle order (key `C`): top-down 2D → third-person → first-person. `R` recentres the chase orbit.

## Hand-off

Same pipeline as Phase 1: Figma/Drive export → drop assets into `media/in-game/` (models into a new
`media/models/` folder) → update `PRODUCTION_ASSETS.md` with the export lock list. The client maps
asset keys in `Game.Shared/Rendering/TextureStore.cs` (and a model loader to be added when the first
`.glb` lands).
