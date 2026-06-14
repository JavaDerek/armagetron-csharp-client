# Production Assets Request — Armagetron Android (Track 2)

**For the designer.** The `armagetron-advanced-design.html` foundation is approved — this is the
**export/lock** list: the actual importable files engineering needs to replace the in-code
placeholders. Everything here is already *designed*; we just need it delivered in these formats.

**Global delivery rules (all items):**
- Vector masters as **SVG**; raster as **PNG, 32-bit RGBA, sRGB, straight (non-premultiplied) alpha**.
- Provide **SVG + one high-res PNG** and we downscale, *or* the Android density ladder (mdpi/hdpi/xhdpi/xxhdpi/xxxhdpi).
- Layered source (Figma/AI/PSD) for anything we may re-export or recolor.
- One shared folder + an **asset manifest** (asset → where used → sizes) + a **palette file** + a **type-scale table**.

---

## 1. Fonts — *blocks all real text*
The three families from the comps, as **TTF/OTF files** (we bake MonoGame SpriteFonts; do NOT rely on runtime download):
- **Orbitron** (display) — weights **800 / 600 / 500** (title / section / subhead).
- **Rajdhani** (UI/body) — weights **500 / 600 / 700**.
- **Share Tech Mono** (numerals: HUD timer/score) — regular.
- Plus: **license proof** permitting app embedding/redistribution, and the **px type-scale table** the comps use (title 78, subhead 30, section 24, body ~16, HUD numerals size) so we bake the right sizes.

## 2. Logo / wordmark
- The **final 'A' monogram lockup** (the cycle-forming-A) **and** the full "ARMAGETRON / ADVANCED" wordmark.
- **SVG masters** + PNG exports; **horizontal + stacked** lockups; **1-color/mono** version.

## 3. App icon (Android adaptive)
- **Foreground** + **background** layers as **SVG + 432×432 PNG** each.
- **Monochrome** layer (themed icons) as SVG.
- **512×512** Play Store icon PNG (32-bit) + **round** + legacy **square** fallback.

## 4. Splash
- Splash **icon** (SVG; may reuse adaptive foreground) + **background hex** (looks like `#070A13`).
- Optional: full-bleed splash PNG (highest-res).

## 5. In-game art — *the "tintable white sprites · 1 sprite × 8 colors" set*
- **Cycle sprite:** tintable **white/grayscale** top-down sprite, **128×128 PNG straight alpha**, nose pointing **up** (we rotate + tint to the 8 player colors). Plus optional separate **glow** sprite.
- **Trail / wall:** confirm **procedural vs sprite.** If procedural, just give **core width + glow radius** specs (we have the 8 hex). If sprite, a **seamless tileable trail-segment PNG** (state tile width px).
- **Arena floor:** a **seamless tileable grid/texture PNG**, *or* the procedural spec (line color, **cell size in world units**, line weight).
- **Explosion (cycle death):** **sprite-sheet PNG atlas** — state the **grid layout (rows×cols), frame count, and target FPS**.
- Optional **spawn-in** effect: sprite sheet or spec.

## 6. UI icon set (SVG masters + PNG @1x/2x/3x)
The icons appearing in the comps: **pause, settings/gear, close (×), play, sound on/off, music on/off,
haptics, refresh, connect, direct-connect (+), ranked/lightning, ping-bars, info, chevrons (‹ ›)**.
Consistent grid/stroke; include **default / pressed / disabled** treatments (or a recolor spec).

## 7. Nine-slice panels & buttons
- **Nine-slice PNGs with stretch regions marked** for: modal/panel bg, HUD panel, and button bg
  (**default / pressed / disabled / secondary**). The comp specifies **inset 28px all sides, corners fixed,
  edges 1-axis stretch, one source PNG** — give the source PNG(s) + the **inset values**.
- The actual **gradient stops + glow color** as values (or just the rendered nine-slice PNG).

## 8. Per-screen redlines
Spacing / sizes / exact positions for each comp (**connect, HUD, results scoreboard, spectator overlay,
settings, server browser, pause + leave-confirm, toasts**) — a Figma inspect link or annotated PNGs, so our
placeholder layout math matches the design pixel-for-pixel.

## 9. Motion
Already specced (button **90ms** ease-out scale 0.97+glow, screen transition **240ms** ease-in-out cross-fade
+ grid parallax, countdown digit **1000ms ×3** scale 1.4→1.0). Just **confirm the explosion sprite FPS** and any
easing curves; sprite-sheet anything frame-based.

## 10. Audio manifest (if in scope — else the SFX/music vendor)
- **SFX, WAV 16-bit 44.1kHz mono:** engine-hum loop, turn, wall-grind, **explosion**, countdown beep, "GO",
  UI tap, connect-success, connect-fail, round-win, round-lose, player-join, player-leave.
- **Music, OGG Vorbis (note loop points):** menu loop, in-game loop.

## 11. Store
- **Feature graphic** 1024×500, **hi-res icon** 512×512, **listing copy** (≤80-char short + full).
- Screenshots: we can capture from the running app once UI ships (designer may frame them).

---

### Priority order for export
1. **Fonts (TTFs)** — unblocks all real text. 2. **Palette/type already adopted in code** (no export needed).
3. **UI icon set + nine-slice PNGs** — unblocks final screen chrome. 4. **Cycle sprite + arena floor + explosion sheet** — unblocks in-game look. 5. App icon + splash. 6. Audio. 7. Store.

*Note: the palette and the 8 trail colors are already values in the HTML, so engineering has adopted those
directly — no file needed for them. This list is only the things that must ship as binary/vector files.*
