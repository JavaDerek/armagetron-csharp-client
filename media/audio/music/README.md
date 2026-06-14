# Drop your music here 🎵

This is your slot. Put the game's music tracks in this folder as **OGG Vorbis** (or WAV masters — the dev can re-export).

Expected cues (rename yours to match, or map them in `../audio_manifest.md`):

- `menu_loop.ogg` — plays on Connect / Server browser / Settings. Atmospheric, low energy, seamless loop.
- `ingame_loop.ogg` — plays during an active match. Driving, builds with the round, seamless loop.

For each, note **sample-accurate loop points** (start + end) in `../audio_manifest.md` so the loop is glitch-free. Target ~ −16 LUFS integrated, true-peak ≤ −1 dBTP.

Got more than two tracks (intro sting, victory theme, variations)? Drop them all and add rows to the manifest — more music is welcome, we'll wire the extra cues.

## Wired into the client (2026-06-14)

The two delivered `.mp3` masters are mapped by `Game.Shared/Audio/MusicController.cs`:
`01 - DMK` → menu/connect loop, `02 - Asuncion` → in-game loop (both looped, honoring the
Settings → Music toggle). MonoGame DesktopGL/Android decode **OGG Vorbis**, not MP3, so each
master is transcoded to a sibling `.ogg` (the loader prefers `.ogg`, falls back to `.mp3`).
Re-transcode after replacing a master:
`ffmpeg -y -i "<name>.mp3" -c:a vorbis -strict experimental -b:a 160k "<name>.ogg"`
(use `libvorbis`/`oggenc` instead if your ffmpeg has it — higher quality).
