# Fonts — OFL, bundle-safe

Downloaded from `github.com/google/fonts` (SIL OFL 1.1; each family's `OFL.txt` is the license proof, shipped alongside). These are the three faces the design foundation specifies.

## Weight mapping (design → file)
The designer asked for **5 Orbitron weights**. Google Fonts ships Orbitron only as a variable font (`Orbitron[wght].ttf`), so the 5 static instances were generated with `fonttools varLib.instancer`:

| Design weight | File |
|---|---|
| Orbitron 500 | `orbitron/Orbitron-Medium.ttf` |
| Orbitron 600 | `orbitron/Orbitron-SemiBold.ttf` |
| Orbitron 700 (PAUSED / splash) | `orbitron/Orbitron-Bold.ttf` |
| Orbitron 800 (titles / winner) | `orbitron/Orbitron-ExtraBold.ttf` |
| Orbitron 900 (countdown) | `orbitron/Orbitron-Black.ttf` |

`Orbitron[wght].ttf` (the variable master) is kept too, in case the content pipeline prefers it.

- **Rajdhani** (UI/body): `Light/Regular/Medium/SemiBold/Bold` static cuts.
- **Share Tech Mono** (HUD timer/score numerals): `ShareTechMono-Regular.ttf`.

## For the integrator
Bake these to MonoGame SpriteFonts (Content Pipeline `.spritefont`, one per role/size from `redlines/redline_spec.md`), or rasterize at runtime — then retire the placeholder `PixelFont` in `Core.Protocol/Game/PixelFont.cs`. Roles & px sizes are in the redline spec (reference frame 2400×1080).
