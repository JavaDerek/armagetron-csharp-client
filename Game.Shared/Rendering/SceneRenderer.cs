using System;
using Armagetron.Game;      // Scene, Render* commands, BlendKind
using Armagetron.Protocol;  // Vec2
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Armagetron.Game.Rendering
{
    /// <summary>
    /// The single GPU draw loop for a neutral <see cref="Scene"/>, shared by the desktop head and
    /// the offscreen PNG harness so a screenshot is a faithful render of production geometry.
    /// Walks the insertion-ordered command stream front-to-back, drawing segments/rects with a
    /// 1px texture, sprites (stretch / nine-slice / sheet-frame, alpha or additive) via the
    /// <see cref="TextureStore"/>, and text via the <see cref="TextRenderer"/> — switching blend
    /// state mid-stream as additive FX require. Owns its own SpriteBatch Begin/End per call.
    /// </summary>
    public sealed class SceneRenderer : IDisposable
    {
        private static readonly BlendState Additive = new BlendState
        {
            ColorSourceBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.SourceAlpha, AlphaDestinationBlend = Blend.One,
        };

        private readonly SpriteBatch _batch;
        private readonly Texture2D _pixel;
        private readonly TextureStore _textures;
        private readonly TextRenderer _text;

        public SceneRenderer(GraphicsDevice gd, TextureStore textures, TextRenderer text)
        {
            _batch = new SpriteBatch(gd);
            _pixel = new Texture2D(gd, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _textures = textures;
            _text = text;
        }

        public TextureStore Textures => _textures;
        public TextRenderer Text => _text;

        /// <summary>Draw a scene, offsetting every command by (<paramref name="dx"/>,
        /// <paramref name="dy"/>) — the gameplay letterbox offset, or 0 for the screen overlay.</summary>
        public void Render(Scene scene, int dx, int dy)
        {
            BlendState current = BlendState.NonPremultiplied;
            _batch.Begin(SpriteSortMode.Deferred, current, SamplerState.LinearClamp);

            foreach (object cmd in scene.Commands)
            {
                BlendState needed = (cmd is RenderSprite sp && sp.Blend == BlendKind.Additive)
                    ? Additive : BlendState.NonPremultiplied;
                if (!ReferenceEquals(needed, current))
                {
                    _batch.End();
                    _batch.Begin(SpriteSortMode.Deferred, needed, SamplerState.LinearClamp);
                    current = needed;
                }

                switch (cmd)
                {
                    case RenderSegment seg: DrawLine(seg, dx, dy); break;
                    case RenderRect r:
                        _batch.Draw(_pixel, new Rectangle(r.X + dx, r.Y + dy, r.W, r.H), ToXna(r.Color));
                        break;
                    case RenderSprite s: DrawSprite(s, dx, dy); break;
                    case RenderText t: _text.Draw(_batch, t, dx, dy); break;
                }
            }
            _batch.End();
        }

        private void DrawSprite(RenderSprite s, int dx, int dy)
        {
            var dest = new Rectangle(s.X + dx, s.Y + dy, s.W, s.H);
            Color tint = ToXna(s.Tint);
            Texture2D? tex = _textures.Get(s.Key);

            if (tex == null)   // graceful fallback: a tinted block so layout is still visible
            {
                _batch.Draw(_pixel, dest, tint);
                return;
            }

            if (s.NineSlice) { DrawNine(tex, s.Key, dest, tint); return; }

            Rectangle? src = s.HasSource ? new Rectangle(s.SrcX, s.SrcY, s.SrcW, s.SrcH) : (Rectangle?)null;

            if (s.Rotation != 0f)
            {
                int sw = src?.Width ?? tex.Width, sh = src?.Height ?? tex.Height;
                var origin = new Vector2(sw / 2f, sh / 2f);
                var center = new Vector2(dest.X + dest.Width / 2f, dest.Y + dest.Height / 2f);
                var scale = new Vector2((float)dest.Width / sw, (float)dest.Height / sh);
                _batch.Draw(tex, center, src, tint, s.Rotation, origin, scale, SpriteEffects.None, 0f);
            }
            else
            {
                _batch.Draw(tex, dest, src, tint);
            }
        }

        // 9-patch: fixed corners, stretched edges/center. Corner insets are clamped if the dest
        // is smaller than their sum (short buttons) so corners never overlap.
        private void DrawNine(Texture2D tex, string key, Rectangle dest, Color tint)
        {
            (int l, int t, int r, int b) = TextureStore.Insets(key);
            int tw = tex.Width, th = tex.Height;

            int dl = l, dr = r;
            if (dl + dr > dest.Width) { float k = (float)dest.Width / (dl + dr); dl = (int)(dl * k); dr = dest.Width - dl; }
            int dt = t, db = b;
            if (dt + db > dest.Height) { float k = (float)dest.Height / (dt + db); dt = (int)(dt * k); db = dest.Height - dt; }

            int[] sx = { 0, l, tw - r, tw };
            int[] sy = { 0, t, th - b, th };
            int[] dxs = { dest.Left, dest.Left + dl, dest.Right - dr, dest.Right };
            int[] dys = { dest.Top, dest.Top + dt, dest.Bottom - db, dest.Bottom };

            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                var srcRect = new Rectangle(sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row]);
                var dstRect = new Rectangle(dxs[col], dys[row], dxs[col + 1] - dxs[col], dys[row + 1] - dys[row]);
                if (srcRect.Width <= 0 || srcRect.Height <= 0 || dstRect.Width <= 0 || dstRect.Height <= 0) continue;
                _batch.Draw(tex, dstRect, srcRect, tint);
            }
        }

        private void DrawLine(RenderSegment seg, int dx, int dy)
        {
            var from = new Vector2(seg.From.X + dx, seg.From.Y + dy);
            Vector2 diff = new Vector2(seg.To.X + dx, seg.To.Y + dy) - from;
            if (diff == Vector2.Zero) return;
            float angle = MathF.Atan2(diff.Y, diff.X);
            _batch.Draw(_pixel, from, null, ToXna(seg.Color), angle, Vector2.Zero,
                new Vector2(diff.Length(), seg.Thickness), SpriteEffects.None, 0f);
        }

        private static Color ToXna(RenderColor c) => new Color(c.R, c.G, c.B, c.A);

        public void Dispose()
        {
            _pixel.Dispose();
            _batch.Dispose();
        }
    }
}
