# Drop your music here 🎵

This is your slot. Put the game's music tracks in this folder as **OGG Vorbis** (or WAV masters — the dev can re-export).

Expected cues (rename yours to match, or map them in `../audio_manifest.md`):

- `menu_loop.ogg` — plays on Connect / Server browser / Settings. Atmospheric, low energy, seamless loop.
- `ingame_loop.ogg` — plays during an active match. Driving, builds with the round, seamless loop.

For each, note **sample-accurate loop points** (start + end) in `../audio_manifest.md` so the loop is glitch-free. Target ~ −16 LUFS integrated, true-peak ≤ −1 dBTP.

Got more than two tracks (intro sting, victory theme, variations)? Drop them all and add rows to the manifest — more music is welcome, we'll wire the extra cues.
