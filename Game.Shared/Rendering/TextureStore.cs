using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace Armagetron.Game.Rendering
{
    /// <summary>
    /// Loads the designer's production PNGs from the copied <c>media/</c> tree at runtime
    /// (<c>Texture2D.FromStream</c> — no MGCB content pipeline) and hands them to the
    /// renderer by the stable string keys the neutral layer emits (<c>"nine/panel"</c>,
    /// <c>"btn/default"</c>, <c>"icon/gear/default"</c>, <c>"ingame/cycle"</c>, …). Textures load
    /// lazily and are cached; a missing file degrades to null so the renderer falls back rather
    /// than crashing. Also owns the per-key nine-slice insets parsed from <c>nine-slice.json</c>.
    /// </summary>
    public sealed class TextureStore : IDisposable
    {
        private readonly GraphicsDevice _gd;
        private readonly string _mediaRoot;
        private readonly Dictionary<string, Texture2D?> _cache = new Dictionary<string, Texture2D?>();

        public TextureStore(GraphicsDevice gd, string? mediaRoot = null)
        {
            _gd = gd;
            _mediaRoot = mediaRoot ?? Path.Combine(AppContext.BaseDirectory, "media");
        }

        /// <summary>Nine-slice corner insets (left, top, right, bottom) per key, from nine-slice.json.</summary>
        public static (int l, int t, int r, int b) Insets(string key) => key switch
        {
            "nine/panel"   => (28, 28, 28, 28),
            "btn/default"  => (28, 29, 28, 31),
            "btn/pressed"  => (28, 29, 28, 31),
            "btn/disabled" => (28, 29, 28, 31),
            "btn/secondary"=> (28, 29, 28, 31),
            _              => (8, 8, 8, 8),
        };

        /// <summary>Resolve a key to its on-disk media path (relative to the media root).</summary>
        private static string? RelativePath(string key)
        {
            switch (key)
            {
                case "nine/panel":    return "nine-slice/panel_9slice.png";
                case "btn/default":   return "nine-slice/button-default_9slice.png";
                case "btn/pressed":   return "nine-slice/button-pressed_9slice.png";
                case "btn/disabled":  return "nine-slice/button-disabled_9slice.png";
                case "btn/secondary": return "nine-slice/button-secondary_9slice.png";
                case "ingame/cycle":      return "in-game/cycle_128.png";
                case "ingame/explosion":  return "in-game/explosion_sheet_512.png";
                case "ingame/arena":      return "in-game/arena_tile_64.png";
                case "splash":            return "splash/splash_1080.png";
            }
            // Icons: "icon/<name>/<state>" → icons/png/<state>/<name>-2x.png
            if (key.StartsWith("icon/", StringComparison.Ordinal))
            {
                string[] parts = key.Split('/');
                if (parts.Length == 3)
                    return $"icons/png/{parts[2]}/{parts[1]}-2x.png";
                if (parts.Length == 2)
                    return $"icons/png/default/{parts[1]}-2x.png";
            }
            return null;
        }

        /// <summary>The texture for <paramref name="key"/>, or null if it can't be loaded.</summary>
        public Texture2D? Get(string key)
        {
            if (_cache.TryGetValue(key, out Texture2D? cached)) return cached;

            Texture2D? tex = null;
            string? rel = RelativePath(key);
            if (rel != null)
            {
                string full = Path.Combine(_mediaRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                {
                    try
                    {
                        using FileStream fs = File.OpenRead(full);
                        tex = Texture2D.FromStream(_gd, fs);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[TextureStore] failed to load {key} ({full}): {ex.Message}");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"[TextureStore] missing asset for {key}: {full}");
                }
            }
            _cache[key] = tex;
            return tex;
        }

        public void Dispose()
        {
            foreach (Texture2D? t in _cache.Values) t?.Dispose();
            _cache.Clear();
        }
    }
}
