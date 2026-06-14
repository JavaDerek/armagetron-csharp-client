# Design Brief — Armagetron Android Client

**Prepared for:** external designer
**Prepared by:** engineering (Claude)
**Date:** 2026-06-14
**Goal:** everything needed to take the Android client from a working wireframe to a finished, shippable game.

---

## 0. What this app is (context)

A mobile **light-cycle game** (Tron-style): you ride a cycle that leaves a solid wall behind
it; you turn left/right to avoid walls and box opponents in. Last cycle alive wins the round.
It connects over the network to a game server with other live players.

**Engine & rendering reality — please read, it dictates every format below:**

- Built on **MonoGame** (a 2D/3D C# game framework). The client renders in **2D** using a
  sprite batch: every visual is a **textured quad** (a PNG drawn to the screen) or a
  **bitmap-font glyph**. There is no HTML/CSS, no native Android view system for the game
  screen — so we need **image and font assets**, not UI component libraries.
- Current view is **top-down 2D** (bird's-eye of the arena). See the rendering-perspective
  decision in §1 before designing in-game art.
- Targets **Android phones** primarily; the same code also runs on desktop. Touch input is
  **tap left half of screen = turn left, tap right half = turn right** today.

**Current screen, for reference:** black background, 8 player colors drawn as glowing
line trails, a square arena outline, a small square marking each cycle's head. That's all
that exists. Everything in this brief is net-new.

---

## 1. Decisions

> **DECIDED (2026-06-14): Phase 1 is top-down 2D.** We ship the 2D top-down client first,
> then revisit a 3D chase-cam as a later phase. **This entire brief targets 2D Phase 1.**
> Everything we commission now carries forward to a future 3D version *except* the in-game
> art in §6 (cycle/trail/arena/explosion), which is 2D-specific — and even those concepts
> translate. When we take on 3D, we'll write a separate model brief (glTF/FBX models, PBR
> textures, rigs) and reuse the branding, icon, fonts, palette, screens, and UX unchanged.

Still to lock — each has my recommendation:

| # | Decision | Options | My recommendation |
|---|----------|---------|-------------------|
| 1 | ~~Rendering perspective~~ | — | **DECIDED: top-down 2D for Phase 1** (see above). |
| 2 | **Screen orientation** | Landscape / Portrait / Both | **Landscape** for gameplay (arena is wide-friendly, two thumbs for turning), menus can be orientation-agnostic. |
| 3 | **Art direction** | Neon/retro "Tron", clean/minimal flat, other | Designer's call — but please give **one** cohesive direction with a mood board, not options to mix. |
| 4 | **Final app name + branding** | — | Needed for icon, splash, store listing. |

---

## 2. General delivery rules (apply to every asset)

- **Vector masters as SVG** for anything that scales (icons, logo, UI frames). I rasterize
  per density from the SVG.
- **Raster as PNG**, 32-bit RGBA, **sRGB**, **straight (non-premultiplied) alpha** — our
  content pipeline premultiplies. (JPG only for photographic store art.)
- **Layered source files** (Figma / AI / PSD) for anything I might need to re-export or recolor.
- **Color values as hex**, plus a single palette file (a swatch list or `.ase` is fine).
- **Densities:** either give me the **highest-res PNG + the SVG** and I downscale, or provide
  the Android density ladder: mdpi ×1 (baseline), hdpi ×1.5, xhdpi ×2, xxhdpi ×3, xxxhdpi ×4.
  Prefer **SVG + one high-res PNG**; less for you to export.
- **Naming + manifest:** kebab-case names, plus a short spreadsheet mapping
  *asset → where it's used → sizes provided*.
- **Delivery:** one shared Figma project + a Drive/zip folder of exports.
- **Design canvas / safe areas:** design landscape at **2400×1080** reference; keep critical
  UI inside a **safe inset** (~5% all edges) to clear notches/cutouts and the gesture-nav bar.

---

## 3. Brand & identity

- **App name** (final) + optional tagline.
- **Logo / wordmark:** SVG master + horizontal and stacked lockups; PNG exports; a
  mono/1-color version.
- **Color palette (hex):** primary, secondary, accent, plus UI neutrals (bg, surface, text,
  muted text, success, warning/danger).
- **8 player/trail colors:** distinct and **colorblind-distinguishable**, each readable as a
  glowing line on black. (We currently use red/cyan/yellow/magenta/orange/pink/blue/lime —
  improve or replace.) The local player has a reserved signature color.
- **Typography:** 1–2 font families (a display face + a UI/numeric face), **licensed for app
  redistribution**. Deliver the **TTF/OTF files** (I bake them into the game). Must include
  **digits 0–9 with good monospaced legibility** for the round timer and score.

---

## 4. App icon (Android adaptive icon)

- **Foreground layer** + **background layer**, designed on a **108×108 dp** canvas with the
  inner **72 dp** as the safe zone (outer ring gets masked/animated by the OS).
  Deliver each layer as **SVG + 432×432 PNG**.
- **Monochrome layer** (single-color silhouette) for Android 13+ themed icons — SVG.
- **Legacy square icon** 512×512 PNG and a **round** variant.
- **Play Store hi-res icon:** 512×512 PNG, 32-bit.

---

## 5. Launch / splash screen

- Android 12+ uses a system splash: **centered icon + a solid background color**. Provide the
  splash icon (SVG, can reuse the adaptive foreground) + background **hex**.
- *(Optional)* a branded full-bleed splash PNG (highest-res + I scale).

---

## 6. In-game art (top-down 2D — assumes Decision 1 = top-down)

For each item: a **tintable white/grayscale sprite is strongly preferred** so I can color it
per player from the palette at runtime (one sprite, 8 colors) rather than 8 separate files.

- **Cycle / head marker:** top-down sprite of the cycle, nose pointing "up"; I rotate it to
  heading. **128×128 PNG, alpha, tintable**, with its glow baked in or as a separate glow
  sprite.
- **Light trail / wall:** how the trail looks — solid neon line, gradient, glow halo. Either a
  **seamless tileable trail-segment PNG** (state tile width) **or** written specs for a
  procedural line (core thickness, glow radius, color = player color) and I'll draw it.
- **Arena floor:** a **seamless tileable** grid/texture PNG **or** specs for a procedural grid
  (line color, cell size in world units, line weight, optional center markings).
- **Arena wall / border:** style spec (glow color, thickness, corner treatment).
- **Explosion / crash effect:** a **sprite-sheet PNG atlas** (state grid layout, frame count,
  and target FPS) — this plays when a cycle dies.
- *(Optional polish)* spawn-in effect, speed/boost streaks, grid pulse on death.

---

## 7. Screens / UX (wireframes + final comps)

Deliver as **Figma** (wireframe → final comp → redlines with spacing/sizes) plus PNG exports.
I implement all layout, state, and animation — I just need the visual design and specs.

Screens, in rough priority:

1. **Connect screen** *(the current code TODO)* — fields for **server host, port, player
   name**, a **Connect** button, connection status/spinner, error state.
2. **In-game HUD** — round **timer**, **score/standings**, local player **name + color**,
   opponent **name tags**, **connection indicator**, optional speedometer.
3. **Touch-control overlay** — visual treatment of the left/right turn zones (subtle zone
   tint, or optional on-screen turn buttons). Include the "first-run hint" affordance.
4. **Round countdown** (3·2·1·GO) and **round-over** banner.
5. **Match results / scoreboard** — final standings, play-again / disconnect.
6. **Death / spectator overlay** — what the loser sees while the round continues.
7. **Pause / disconnect confirm.**
8. **Settings** — player name, control sensitivity/zone size, sound toggles.
9. **Transient toasts** — chat messages, player joined/left, kicked/error notices.
10. *(Later)* **Server browser** — list of servers, ping, join.

---

## 8. UI sprites & iconography

- **Button icon set** (SVG masters): turn-left, turn-right, settings/gear, back/close, play,
  pause, sound on/off, connect, refresh/retry, info. Consistent grid/stroke weight.
- **Scalable panels/frames:** modal backgrounds, HUD panels, button backgrounds. Provide as
  **nine-slice PNGs with stretch regions marked**, or SVG + I implement nine-slice.
- **States:** for interactive elements, provide **default / pressed / disabled** treatments
  (can be a recolor spec rather than separate files).

---

## 9. Fonts for the engine (how the type actually gets in)

Two acceptable delivery paths — **either** is fine:

- **(Preferred) TTF/OTF files** (licensed) + the point sizes the design uses (e.g. 16 / 24 /
  48 px) and any outline/glow style. I generate the in-game bitmap fonts from these.
- **OR pre-baked bitmap-font atlas:** AngelCode/BMFont `.fnt` + PNG atlas, with the character
  set (ASCII minimum; flag any extra glyphs) and sizes.

Call out separately the **timer/score numerals** if they use a distinct "digital" face.

---

## 10. Audio (if in scope — otherwise flag as a separate vendor)

- **SFX** (deliver **WAV, 16-bit PCM, 44.1 kHz, mono**): engine-hum loop, turn/steer,
  wall-grind/near-miss, **explosion/crash**, countdown beep, "GO", UI tap, connect
  success, connect fail, round win, round lose.
- **Music** (deliver **OGG Vorbis**, note loop points): menu loop, in-game loop.
- Normalize levels; include a naming convention.

---

## 11. Motion & feel spec

A short doc (or annotated Figma) covering: button-press feedback, screen-transition style &
durations, countdown animation, explosion playback FPS, trail-draw behavior, and any HUD
animations. Sprite-sheet anything frame-based (we can't run Lottie/AE in-engine) — describe
timing for the rest.

---

## 12. Store / marketing assets (Google Play)

- **Feature graphic:** 1024×500 PNG/JPG.
- **Phone screenshots:** I can capture real frames once the UI exists; designer optionally
  frames/annotates them.
- **Hi-res icon:** 512×512 (also listed in §4).
- **Listing copy:** short description (≤80 chars) + full description.
- *(Optional)* promo video.

---

## 13. What I build vs. what I need from the designer

- **I build:** all layout, screen logic, animation, rendering, font/content pipeline,
  tinting, nine-slice, the connect flow, HUD wiring, store packaging.
- **I need from you:** visual direction + comps/wireframes + redline specs, source assets
  (SVG/PNG), font files, hex palette, sprite sheets, and (optionally) audio.
- **I do NOT need:** any code, XML layouts, or platform-specific implementation.

---

## 14. Priority tiers (so partial delivery still unblocks me)

**Tier 1 — unblocks "finish the core game" (start here):**
- Decisions §1 locked (esp. perspective + orientation)
- Color palette + hex (incl. 8 player colors)
- Font file(s)
- App icon (adaptive layers)
- Connect-screen design
- In-game HUD design
- Cycle sprite (tintable) + trail style + arena/grid spec
- Explosion sprite sheet
- Countdown / round-over / results screens
- Core button icon set

**Tier 2 — polish & store:**
- Splash, settings, server browser
- Nine-slice panels, button states
- Audio
- Motion spec
- Play Store marketing assets

---

*Hand this whole file to the designer. When assets land, drop them in a shared folder and I'll
map each into the build, working Tier 1 → Tier 2.*
