# media/models — 3D model drop folder

Runtime-loaded 3D models for the perspective/VR views (Phase 2, see `DESIGN_BRIEF_3D.md`).

## Lightcycle

Drop the designer's lightcycle here as **`cycle.glb`** (binary glTF 2.0). The MonoGame heads load
it automatically at runtime via `ModelStore` → `GlbModelLoader` (parsed by the unit-tested
`Core.Protocol`/`Game.Shared` loader) and the 3D renderer draws it per cycle, tinted to each
player's colour. **Until `cycle.glb` is present, the renderer falls back to the flat billboard** —
so this folder being empty is fine; the model simply "turns on" the moment the file is dropped in.

Export requirements (from `DESIGN_BRIEF_3D.md` §1):
- `.glb`, glTF 2.0, **Y-up**, 1 unit = 1 arena unit (body ≈ 6–9 units long).
- **Nose pointing +X** at identity, wheels on the floor plane (Y = 0).
- White / greyscale master (tinted per player at runtime); ≤ ~3–5k tris, one ≤ 1024² atlas.
- Triangle mesh. Supported by the loader: POSITION/NORMAL/TEXCOORD_0, node transforms (baked),
  and the first material's baseColorFactor. (Sparse accessors / non-triangle modes are not.)

Unity (Oculus) loads its model through Unity's own glTF import, not this loader.
