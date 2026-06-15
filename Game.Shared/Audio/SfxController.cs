using System;
using System.Collections.Generic;
using System.IO;
using Armagetron.Game.UI;
using Microsoft.Xna.Framework.Audio;

namespace Armagetron.Game.Audio
{
    /// <summary>
    /// Plays the manifest's sound effects from the asset pack. One-shots (turn, explosion,
    /// connect chimes, win/lose, ui taps, "GO", countdown) are fired via <see cref="PlayCues"/>
    /// from the shell's drained <see cref="SfxCueQueue"/>; the looping engine hum is started/
    /// stopped per-frame from a boolean via <see cref="SetEngine"/> (like the music loop).
    ///
    /// SFX use MonoGame's <see cref="SoundEffect"/> path (low-latency, fully decoded in RAM),
    /// so the files MUST be uncompressed WAV — <see cref="SoundEffect.FromStream"/> rejects
    /// ogg/mp3. Loading/playback failures (e.g. a headless box with no audio device) degrade
    /// to silence rather than crashing, matching <see cref="MusicController"/>.
    /// </summary>
    public sealed class SfxController : IDisposable
    {
        // SfxId → file stem in media/audio/sfx (1:1 with the audio manifest ids).
        private static readonly IReadOnlyDictionary<SfxId, string> Files = new Dictionary<SfxId, string>
        {
            [SfxId.UiTap]         = "ui_tap",
            [SfxId.Turn]          = "turn",
            [SfxId.CountdownBeep] = "countdown_beep",
            [SfxId.Go]            = "go",
            [SfxId.Explosion]     = "explosion",
            [SfxId.Win]           = "win",
            [SfxId.Lose]          = "lose",
            [SfxId.ConnectOk]     = "connect_ok",
            [SfxId.ConnectFail]   = "connect_fail",
            [SfxId.EngineLoop]    = "engine_loop",
            [SfxId.WallGrind]     = "wall_grind",
        };

        private readonly Dictionary<SfxId, SoundEffect> _effects = new Dictionary<SfxId, SoundEffect>();
        private SoundEffectInstance? _engine;
        private bool _engineOn;

        public SfxController(string? mediaRoot = null)
        {
            string root = mediaRoot ?? Path.Combine(AppContext.BaseDirectory, "media");
            string dir = Path.Combine(root, "audio", "sfx");
            foreach (KeyValuePair<SfxId, string> kv in Files)
            {
                SoundEffect? fx = TryLoad(dir, kv.Value);
                if (fx != null) _effects[kv.Key] = fx;
            }
            _engine = TryInstance(SfxId.EngineLoop, looped: true);
        }

        private static SoundEffect? TryLoad(string dir, string stem)
        {
            string path = Path.Combine(dir, stem + ".wav");
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"[SfxController] missing {path}");
                return null;
            }
            try
            {
                using FileStream s = File.OpenRead(path);
                return SoundEffect.FromStream(s);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SfxController] could not load {stem}.wav: {ex.Message}");
                return null;
            }
        }

        private SoundEffectInstance? TryInstance(SfxId id, bool looped)
        {
            if (!_effects.TryGetValue(id, out SoundEffect? fx)) return null;
            try
            {
                SoundEffectInstance inst = fx.CreateInstance();
                inst.IsLooped = looped;
                return inst;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SfxController] could not instance {id}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Play each drained one-shot cue once. No-op (and no looping side effects)
        /// when <paramref name="soundOn"/> is false.</summary>
        public void PlayCues(IReadOnlyList<SfxId> cues, bool soundOn)
        {
            if (!soundOn || cues.Count == 0) return;
            foreach (SfxId id in cues)
            {
                if (!_effects.TryGetValue(id, out SoundEffect? fx)) continue;
                try { fx.Play(); }
                catch (Exception ex) { Console.Error.WriteLine($"[SfxController] play {id} failed: {ex.Message}"); }
            }
        }

        /// <summary>Start/stop the looping engine hum. Driven once per frame from
        /// <see cref="AppShell.EngineRunning"/> &amp;&amp; the Sound toggle.</summary>
        public void SetEngine(bool running, bool soundOn)
        {
            bool want = running && soundOn;
            if (want == _engineOn) return;
            _engineOn = want;
            if (_engine == null) return;
            try { if (want) _engine.Play(); else _engine.Stop(); }
            catch (Exception ex) { Console.Error.WriteLine($"[SfxController] engine toggle failed: {ex.Message}"); }
        }

        public void Dispose()
        {
            try { _engine?.Stop(); } catch { /* no audio device */ }
            _engine?.Dispose();
            foreach (SoundEffect fx in _effects.Values) fx.Dispose();
            _effects.Clear();
        }
    }
}
