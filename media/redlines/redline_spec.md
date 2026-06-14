# Redline Spec — Armagetron Advanced

**Reference frame:** `2400 × 1080` landscape. **Global safe inset:** ~5% (≈ 120 px L/R, ≈ 54 px T/B) — all HUD and interactive content lives inside it. All radii in px @ reference. Percentages are of the screen (or stated container) so they scale to any landscape device; convert to px with `pct × axisLength`.

Annotated visuals: `redline_hud.png`, `redline_connect.png`.

---

## 5.1 Connect
| Element | Anchor | Size | Notes |
|---|---|---|---|
| Brand lockup | left 6%, V-center | max-width 38% | wordmark 38px + ADVANCED sub |
| Form panel | right 6%, V-center | width 42% | radius 10, border `#2C3F60`, blur 2 |
| Field (host) | in panel | height **42** | radius 6, mono text, focus = cyan inset |
| Port / Name row | in panel | height **42**, gap 12 | port flex 1, name flex 2 |
| Status strip | in panel | height 38 | success border/tint |
| CTA "CONNECT ▸" | in panel | height **50** | radius 6, gradient + glow 24 |
| States | — | — | idle · focused (cyan inset) · connecting (spinner) · error (danger border + toast) · success |

## 5.2 In-game HUD
| Element | Anchor | Size | Notes |
|---|---|---|---|
| Round timer | **top-center**, top 4% | mono 38 | "ROUND n/5" caption under |
| Standings panel | **top-left**, top 5% / left 6.5% | min-width 150 | surface @72%, radius 7, blur 2 |
| Ping chip | **top-right**, top 5% / right 6.5% | auto | bars + ms, success/warn color |
| Local player chip | **bottom-center**, bottom 5% | auto, radius 20 | cyan border + glow |
| Name tags | follow each cycle | 12–16 | player color, hard text-shadow |
| Arena border | inset 7% / 6% | 2px | `#1FE3FF`, glow 16 + inner 26 |

## 5.3 Touch control
| Element | Anchor | Size | Notes |
|---|---|---|---|
| Turn-left zone | left edge | width **50%** | tint gradient, dashed inner divider |
| Turn-right zone | right edge | width **50%** | mirror |
| First-run hint card | center | auto | shown once, tap to dismiss |
| Ripple hint | over a zone | 50 dia | pulse 1.4s |
| (setting) zone size + tint opacity adjustable; optional on-screen-buttons mode |

## 5.4 Countdown
| Element | Anchor | Size | Notes |
|---|---|---|---|
| Numerals 3·2·1 | center | **120**, Orbitron 900 | scale-in + glow pulse, 1s each |
| "GET READY" | under numerals | 16, tracked .5em | |
| GO | center | snaps green `#46E8A0`, hold 400ms, fade |

## 5.5 Round-over banner
| Element | Anchor | Size | Notes |
|---|---|---|---|
| Band | center, full width | height 118 | gradient + 1px cyan edges |
| "WINNER" eyebrow | center | 12, tracked .4em | |
| Winner name | center | **44**, Orbitron 800 | tinted to winner color; loser variant = danger red "ELIMINATED" |

## 5.6 Match results
| Element | Anchor | Size | Notes |
|---|---|---|---|
| Container pad | — | 5% V / 6% H | |
| Title "FINAL STANDINGS" | top | 22 | duration caption right |
| Standing rows | list | gap 5, pad 8/13, radius 6 | #1 highlighted cyan tint/border |
| PLAY AGAIN | bottom, flex 1 | height **42** | gradient + glow |
| DISCONNECT | bottom | 140 × 42 | outline |

## 5.7 Death / spectator
| Element | Anchor | Size | Notes |
|---|---|---|---|
| Dim veil | full | `rgba(4,6,12,.55)` | arena keeps playing at reduced brightness |
| "ELIMINATED" | center 46% | 40, danger | |
| Watching chip | bottom 6%, center | radius 20 | spectated player color |
| Pause | top 6% / right 6.5% | 36 × 36 | radius 6 |

## 8 Settings
| Element | Anchor | Size | Notes |
|---|---|---|---|
| Sheet | center | width 62% | radius 12, pad 26/30, scrim `rgba(4,6,12,.55)` |
| Field | grid | height **38** | radius 6 |
| Toggle | row right | **42 × 24** | cyan=on / muted=off |
| Slider | row | track h **6**, knob **16** | glow-filled |
| Color swatch | row | 22 × 22, radius 5 | 2px white outline on active |

## 9 Server browser
| Element | Anchor | Size | Notes |
|---|---|---|---|
| Container pad | — | 4% V / 5% H | |
| Row grid | — | `2.4fr 1fr 1fr 0.9fr 96px`, gap 12 | header row 10px tracked |
| Row | list | pad 9/14, radius 6 | selected = cyan border + glow |
| Ping bars | col | ▰▰▰ <50 green · ▰▰▱ 50–100 · ▰▱▱ >100 warn | |
| JOIN cell | col | 96 wide | gradient (primary) / outline / FULL muted |
| Direct-connect field | bottom | height 38 | |

## 10 Pause / confirm
| Element | Anchor | Size | Notes |
|---|---|---|---|
| Panel | center | width 66% | radius 12, scrim `rgba(4,6,12,.72)` |
| Buttons | stack | height **42**, gap 9 | RESUME (primary) · SETTINGS (outline) · DISCONNECT (danger outline) |
| Confirm variant | — | — | "Leave match? Round progress is lost." + CANCEL / LEAVE |

## 11 Toasts
| Element | Anchor | Size | Notes |
|---|---|---|---|
| Stack | top-left region | max-width 78%, gap 9 | slide-in L, auto-dismiss 4s, max 3 |
| Toast | — | pad 9/13, radius 7, left accent **2px** | accent = type: join `#8DFF3A` · leave muted · chat cyan · error danger |

## 12 Panels & buttons → see `../nine-slice/nine-slice.json`
Nine-slice inset **28** (corners fixed). Button height **46**, radius **7**, states default/pressed/disabled/secondary.

## 13 Motion (for completeness)
Button press 90ms ease-out (scale .97 + glow) · screen transition 240ms ease-in-out (cross-fade + 12px grid parallax) · explosion 16f @30fps (~530ms) · toast in/out 200/160ms.
