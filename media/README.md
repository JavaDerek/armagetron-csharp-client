# Armagetron Advanced — Production Asset Pack

Everything the dev's checklist asked for that must ship as an actual **binary / vector file**, exported from the locked design foundation (`../Armagetron Advanced - Design.dc.html`). Generated 2026-06-14.

> **Palette & the 8 trail colors are NOT in here on purpose.** They're *values* — pull them straight from the design doc / the table below. This pack is only the files that can't be expressed as a hex code.
>
> **8 trail colors:** P1 `#1FE3FF` · P2 `#FF9A2E` · P3 `#FF2FA4` · P4 `#8DFF3A` · P5 `#3D8BFF` · P6 `#FF4D4D` · P7 `#FFE03A` · P8 `#B968FF`
> **Neutrals:** void `#04060C` · surface `#0C1322` · raised `#16223A` · text `#DCEAF2` · muted `#647994` · primary `#1FE3FF` · success `#46E8A0` · warn `#FFC23D` · danger `#FF4D5E`

---

## What's here

### `../Type Scale & Fonts.dc.html`  *(open in browser)*
The font half of the blocker. Every text role → exact px @ 2400×1080, face + weight, line-height, tracking. License proof + sourcing for all three faces.
**⚠ Ship 5 Orbitron weights (500·600·700·800·900)** — the dev's note missed 700 (splash/PAUSED) and 900 (countdown). 10 static cuts total.

### `icons/`
17 UI icons. **`svg/`** = vector masters (24px grid, 2px stroke, `currentColor`). **`png/{default,pressed,disabled}/`** = rasters at `@1×/2×/3×` (24/48/72 px). `_contact-sheet.png` previews all 17 × 3 states.
default `#9BB0C8` · pressed `#1FE3FF` + glow · disabled 30% opacity.
Set: pause play close plus chevron-left chevron-right back info gear sound music haptics refresh retry connect(link) ranked(bolt) ping.

### `nine-slice/`
`panel` + 4 button states (`default / pressed / disabled / secondary`). Each as: `*_9slice.png` (source), `*.9.png` (Android nine-patch w/ guide border), plus `*_preview.png`. **`nine-slice.json`** has every stretch inset, gradient stop, glow value. Panel inset **28** · button h **46** / radius **7**.

### `app-icon/`
Adaptive: `fg.svg` `bg.svg` `mono.svg` + `ic_launcher_{fg,bg,mono}_432.png`. Legacy `legacy_square_512.png` / `legacy_round_512.png`. Play hi-res `playstore_512.png`. The 'A' = light-trail strokes + cycle-head apex dot.

### `splash/`
`splash_1080.png` (full-bleed landscape lockup) · `splash_icon_432.png` (transparent mark) · `splash_system_icon_512.png` (Android-12 masked icon).
System splash bg = **`#04060C`**; centered animated-icon = the fg layer.

### `store/`
`feature_graphic_1024x500.png` + `listing_copy.md` (title / short / full description / asset checklist).

### `in-game/`
`cycle_128.png` (white, tintable, nose-up, cockpit cut) · `explosion_sheet_512.png` (4×4, 16f, white, additive) + `explosion_sheet.json` (fps 30, ~533ms) · `arena_tile_64.png` (tileable grid cell). **Trails are procedural** (confirmed) — no sprite. `_preview.png` shows them tinted.

### `audio/`
`audio_manifest.md` (SFX WAV list + Music OGG list, triggers, loop, levels). **`music/`** and **`sfx/`** are your drop folders — drop tracks in, map them in the manifest. Music ~ −16 LUFS, loop points sample-accurate.

### `redlines/`
`redline_spec.md` (every screen → anchors, sizes, offsets, all at 2400×1080 ref) + annotated `redline_hud.png` / `redline_connect.png`.

---

## Tint recipe (one white sprite → 8 colors)
Draw the white master, then `source-in` fill with the player hex (or multiply-tint in-engine) and draw **additive** with an outer glow ≈ 0.5 alpha, blur scaled to sprite size. Same recipe for `cycle_128` and `explosion_sheet`.

## Still needs a human pass (not exportable here)
- **CVD sim** the 8 trails (deuteranopia/protanopia) before final lock — flagged as a recommendation in the doc.
- **Phone screenshots** — capture from real frames once the UI ships, then annotate.
- **Font TTFs** — 2-min download from `github.com/google/fonts` (`ofl/orbitron`, `ofl/rajdhani`, `ofl/sharetechmono`); each folder's `OFL.txt` is your license proof. Ship those next to the TTFs.

## Attribution
- Fonts: Orbitron, Rajdhani, Share Tech Mono — all **SIL OFL 1.1**, free to bundle/embed.
- A handful of UI icon glyphs use **Feather** geometry (MIT) as a base; redrawn to the 24px/2px system.
