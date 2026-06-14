# Audio Manifest — Armagetron Advanced

**Flag: separate vendor.** Audio ships from a sound/music source, not the visual pipeline. This sheet is the spec + drop-in slots. The composer (you) maps real tracks into the right-hand columns and drops files into `music/` and `sfx/`.

Engine note from the motion spec: everything frame-based is sprite-sheeted (no Lottie/AE in-engine); audio is event-triggered against those same beats.

---

## SFX — `WAV · 16-bit · 44.1 kHz · mono`

| ID | Filename | Trigger | Loop | Target len | Design note | ▶ YOUR FILE |
|---|---|---|---|---|---|---|
| `engine_loop` | `sfx/engine_loop.wav` | While alive, always | **yes** | seamless | Low hum, pitch ↑ slightly with speed |  |
| `turn` | `sfx/turn.wav` | Each 90° turn | no | ~120 ms | Tight digital tick; pairs with haptic |  |
| `wall_grind` | `sfx/wall_grind.wav` | Cycle near a wall | **yes** | seamless | Rising tension, ducks under engine |  |
| `explosion` | `sfx/explosion.wav` | Cycle death | no | ~530 ms | Sync to 16-frame @30fps blast |  |
| `countdown_beep` | `sfx/countdown_beep.wav` | 3 · 2 · 1 ticks | no | ~200 ms | Three identical, rising pitch optional |  |
| `go` | `sfx/go.wav` | "GO" frame | no | ~400 ms | Brighter, resolves the countdown |  |
| `ui_tap` | `sfx/ui_tap.wav` | Any button press | no | ~80 ms | Crisp; matches 90 ms press anim |  |
| `connect_ok` | `sfx/connect_ok.wav` | Handshake success | no | ~500 ms | Affirmative two-note |  |
| `connect_fail` | `sfx/connect_fail.wav` | Connect error | no | ~500 ms | Low, negative |  |
| `win` | `sfx/win.wav` | Round/match win | no | ~1.2 s | Tied to winner-color banner |  |
| `lose` | `sfx/lose.wav` | Local elimination | no | ~1.2 s | Under the red ELIMINATED overlay |  |

## MUSIC — `OGG Vorbis · with loop points`

| ID | Filename | Used | Loop | Loop points | Design note | ▶ YOUR TRACK |
|---|---|---|---|---|---|---|
| `menu_loop` | `music/menu_loop.ogg` | Connect · browser · settings | **yes** | set start/end (samples) | Atmospheric, low energy |  |
| `ingame_loop` | `music/ingame_loop.ogg` | Active match | **yes** | set start/end (samples) | Driving, builds with the round |  |

> **Composer:** drop your files in `music/` and `sfx/`, fill the **YOUR FILE / YOUR TRACK** columns with the exact names you ship, and note loop points (sample-accurate) for each music cue. If your tracks aren't named to the IDs above, just map them here and the dev will alias.

---

## Levels & delivery
- Normalize all music to a consistent integrated loudness (target **−16 LUFS** for mobile; true-peak ≤ −1 dBTP). SFX peak-normalize to ~ −3 dBFS, balanced by ear against the engine loop.
- Music as **OGG Vorbis** (loop-friendly, small). SFX as **WAV** (no decode latency on trigger).
- Provide loop points as sample offsets in a sidecar (this sheet, or per-file `.loop` / metadata) — don't rely on file-edge looping for music.
- Keep a `/masters` of full-res WAV stems if you want the dev to re-export OGG at a chosen quality.
