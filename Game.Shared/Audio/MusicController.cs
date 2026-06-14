using System;
using System.IO;
using Microsoft.Xna.Framework.Media;

namespace Armagetron.Game.Audio
{
    /// <summary>
    /// Background-music controller: loops one of two tracks from the asset pack depending on the
    /// app context — "01 - DMK" on the menu/connect screens, "02 - Asuncion" in-game — and honors
    /// the Settings Music toggle (silence when off). Driven once per frame from the host with the
    /// current (musicOn, inGame) state; it only touches <see cref="MediaPlayer"/> when the desired
    /// track changes. Loading/playback failures (e.g. a headless box with no audio device) degrade
    /// to silence rather than crashing the game. SFX are not in the pack yet — hooks only.
    /// </summary>
    public sealed class MusicController : IDisposable
    {
        private enum Track { None, Menu, Game }

        private readonly Song? _menu;
        private readonly Song? _game;
        private Track _current = Track.None;
        private bool _started;

        public MusicController(string? mediaRoot = null)
        {
            string root = mediaRoot ?? Path.Combine(AppContext.BaseDirectory, "media");
            string dir = Path.Combine(root, "audio", "music");
            // OGG Vorbis is what MonoGame DesktopGL/Android decode natively (the audio manifest's
            // "Music OGG list"); the shipped .mp3 masters are transcoded alongside. Prefer .ogg.
            _menu = TryLoad(dir, "Refestramus - Intourist - 01 - DMK", "menu-music");
            _game = TryLoad(dir, "Refestramus - Intourist - 02 - Asuncion", "game-music");
        }

        private static Song? TryLoad(string dir, string baseName, string name)
        {
            foreach (string ext in new[] { ".ogg", ".mp3" })
            {
                string path = Path.Combine(dir, baseName + ext);
                if (!File.Exists(path)) continue;
                try
                {
                    return Song.FromUri(name, new Uri(path));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[MusicController] could not load {baseName}{ext}: {ex.Message}");
                }
            }
            Console.Error.WriteLine($"[MusicController] no playable track for {baseName} in {dir}");
            return null;
        }

        /// <summary>Call once per frame. <paramref name="inGame"/> selects the in-match loop;
        /// otherwise the menu loop plays. When <paramref name="musicOn"/> is false, music stops.</summary>
        public void Update(bool musicOn, bool inGame)
        {
            Track want = !musicOn ? Track.None : inGame ? Track.Game : Track.Menu;
            if (want == _current && _started) return;
            _started = true;
            _current = want;

            try
            {
                switch (want)
                {
                    case Track.None: MediaPlayer.Stop(); break;
                    case Track.Menu: Play(_menu); break;
                    case Track.Game: Play(_game); break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MusicController] playback error: {ex.Message}");
            }
        }

        private static void Play(Song? song)
        {
            if (song == null) { MediaPlayer.Stop(); return; }
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = 0.5f;
            MediaPlayer.Play(song);
        }

        public void Dispose()
        {
            try { MediaPlayer.Stop(); } catch { /* no audio device */ }
            _menu?.Dispose();
            _game?.Dispose();
        }
    }
}
