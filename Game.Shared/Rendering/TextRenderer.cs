using System;
using System.Collections.Generic;
using System.IO;
using Armagetron.Game; // FontRole, TextAlign, RenderText, RenderColor
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Armagetron.Game.Rendering
{
    /// <summary>
    /// Runtime TTF text rendering for the neutral <see cref="RenderText"/> command, backed by
    /// FontStashSharp (chosen over MGCB SpriteFonts: no content-pipeline build step, ONE TTF
    /// scales to every redline px size on demand, and the exact same code path runs in the
    /// desktop head, the PNG harness, and Android). Maps each <see cref="FontRole"/> to a real
    /// OFL face/weight (Orbitron / Rajdhani / Share Tech Mono) and resolves Left/Center/Right +
    /// vertical-middle alignment using the REAL measured glyph metrics — so the neutral layer
    /// never needs proportional font math.
    /// </summary>
    public sealed class TextRenderer : IDisposable
    {
        private readonly Dictionary<FontRole, FontSystem> _systems = new Dictionary<FontRole, FontSystem>();
        private readonly string _fontRoot;

        public TextRenderer(string? mediaRoot = null)
        {
            string root = mediaRoot ?? System.IO.Path.Combine(AppContext.BaseDirectory, "media");
            _fontRoot = System.IO.Path.Combine(root, "fonts");

            Load(FontRole.Display, "orbitron/Orbitron-Black.ttf");
            Load(FontRole.Title,   "orbitron/Orbitron-ExtraBold.ttf");
            Load(FontRole.Heading, "orbitron/Orbitron-Bold.ttf");
            Load(FontRole.Body,    "rajdhani/Rajdhani-Medium.ttf");
            Load(FontRole.Label,   "rajdhani/Rajdhani-SemiBold.ttf");
            Load(FontRole.Mono,    "sharetechmono/ShareTechMono-Regular.ttf");
        }

        private void Load(FontRole role, string rel)
        {
            string path = System.IO.Path.Combine(_fontRoot, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var fs = new FontSystem();
            if (File.Exists(path))
                fs.AddFont(File.ReadAllBytes(path));
            else
                Console.Error.WriteLine($"[TextRenderer] missing font {role}: {path}");
            _systems[role] = fs;
        }

        // Orbitron is a tall, wide display face; the UI/mono faces read better a touch larger.
        // pixelSize = K(role) * Scale keeps the new fonts at roughly the old bitmap block size,
        // and for Mono makes the advance ≈ the layout's assumed cell so caret/columns line up.
        private static int PixelSize(FontRole role, int scale)
        {
            double k = role switch
            {
                FontRole.Display => 9.0,
                FontRole.Title   => 8.5,
                FontRole.Heading => 8.5,
                FontRole.Mono    => 10.5,
                _                => 10.0,
            };
            int px = (int)Math.Round(k * scale);
            return px < 8 ? 8 : px > 400 ? 400 : px;
        }

        private DynamicSpriteFont Font(FontRole role, int scale) =>
            _systems[role].GetFont(PixelSize(role, scale));

        /// <summary>Measured pixel width of a line (used for chip/column sizing in the head).</summary>
        public float MeasureWidth(string text, FontRole role, int scale)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return Font(role, scale).MeasureString(text).X;
        }

        /// <summary>Draw a neutral text command, resolving alignment from the real metrics.
        /// <paramref name="dx"/>/<paramref name="dy"/> letterbox the gameplay layer (0 for UI).</summary>
        public void Draw(SpriteBatch batch, RenderText t, int dx, int dy)
        {
            if (string.IsNullOrEmpty(t.Text)) return;
            DynamicSpriteFont font = Font(t.Role, t.Scale);
            Vector2 size = font.MeasureString(t.Text);

            float x = t.X;
            if (t.Align == TextAlign.Center) x = t.X - size.X / 2f;
            else if (t.Align == TextAlign.Right) x = t.X - size.X;

            float y = t.Middle ? t.Y - size.Y / 2f : t.Y;

            font.DrawText(batch, t.Text, new Vector2(x + dx, y + dy),
                          new Color(t.Color.R, t.Color.G, t.Color.B, t.Color.A));
        }

        public void Dispose()
        {
            foreach (FontSystem fs in _systems.Values) fs.Dispose();
            _systems.Clear();
        }
    }
}
