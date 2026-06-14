using System;
using Armagetron.Game.UI;
using Armagetron.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Armagetron.Game
{
    /// <summary>
    /// MonoGame host for the Armagetron client, shared by every platform head (desktop,
    /// Android). It is a thin loop over the pure <see cref="AppShell"/>: each frame it forwards
    /// platform input (taps/turn-keys/back/text via <see cref="IShellInput"/>) to the shell,
    /// ticks it, draws gameplay when the shell is showing a game, and draws the shell's
    /// screen-space overlay (connect/HUD/pause/settings) on top. The shell decides everything;
    /// this class only issues GPU calls. All networking and the choice of input device live
    /// outside — desktop hands it a keyboard/mouse input, Android hands it touch.
    /// </summary>
    public sealed class ArmagetronGame : Microsoft.Xna.Framework.Game
    {
        // Arena runs 0→ArenaSize in both axes (empirical; replaced by desc=51 decode later).
        private const float ArenaSize   = 176.78f;
        private const float ArenaMargin = 10f;
        private const int   WindowSize  = 800;

        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        private Texture2D   _pixel       = null!;

        private readonly string _title;
        private readonly IUiClient _client;
        private readonly IShellInput _input;
        private readonly AppShell _shell;
        private readonly CyclePalette _palette = new CyclePalette();

        private ArenaView _view = null!;
        private int _offsetX, _offsetY, _w, _h, _side = -1;
        private long _nowMs;
        private CycleSnapshot[] _snapshot = Array.Empty<CycleSnapshot>();

        /// <summary>
        /// Construct with a UI-facing client (NOT pre-connected — the shell drives the connect
        /// screen) and the shell + platform input. <paramref name="fullscreen"/> uses the device
        /// resolution (Android); otherwise an 800×800 window is requested (desktop).
        /// </summary>
        public ArmagetronGame(IUiClient client, IShellInput input, AppShell shell,
                              string title, bool fullscreen = false)
        {
            _client = client;
            _input  = input;
            _shell  = shell;
            _title  = title;

            _graphics = new GraphicsDeviceManager(this);
            if (fullscreen)
            {
                _graphics.IsFullScreen = true;
            }
            else
            {
                _graphics.PreferredBackBufferWidth  = WindowSize;
                _graphics.PreferredBackBufferHeight = WindowSize;
            }
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            Window.Title = _title;
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel       = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            EnsureView();
        }

        // Recompute the centred square arena view whenever the back-buffer size changes
        // (window resize / device rotation), so the arena always fills the shorter edge.
        private void EnsureView()
        {
            _w = GraphicsDevice.Viewport.Width;
            _h = GraphicsDevice.Viewport.Height;
            int side = Math.Min(_w, _h);
            if (side == _side) return;
            _side    = side;
            _view    = new ArenaView(ArenaSize, ArenaMargin, side);
            _offsetX = (_w - side) / 2;
            _offsetY = (_h - side) / 2;
        }

        protected override void Update(GameTime gameTime)
        {
            EnsureView();
            _nowMs = (long)gameTime.TotalGameTime.TotalMilliseconds;

            foreach (TapPoint t in _input.Taps())          _shell.HandleTap(t.X, t.Y, _w, _h);
            foreach (TurnDirection d in _input.TurnKeys())  _shell.OnTurn(d);
            if (_input.BackPressed())                       _shell.OnBack();
            _input.ApplyTextEditing(_shell);

            _snapshot = _client.Snapshot();
            _shell.Tick(_snapshot, _nowMs);

            if (_shell.ExitRequested) Exit();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin();

            if (_shell.ShowsGameplay)
            {
                Scene game = SceneBuilder.Build(_snapshot, _client.MyCycleId, _view, _palette);
                DrawScene(game, _offsetX, _offsetY);
            }

            DrawScene(_shell.BuildOverlay(_w, _h, _nowMs), 0, 0);

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        // ── GPU glue (the only rendering code that needs a graphics device) ───────

        private void DrawScene(Scene scene, int dx, int dy)
        {
            foreach (RenderSegment seg in scene.Segments)
                DrawLine(seg.From.X + dx, seg.From.Y + dy, seg.To.X + dx, seg.To.Y + dy,
                         ToXna(seg.Color), seg.Thickness);
            foreach (RenderRect r in scene.Heads)
                _spriteBatch.Draw(_pixel, new Rectangle(r.X + dx, r.Y + dy, r.W, r.H), ToXna(r.Color));
            foreach (RenderText t in scene.Texts)
                DrawText(t, dx, dy);
        }

        private void DrawText(RenderText t, int dx, int dy)
        {
            Color color = ToXna(t.Color);
            for (int i = 0; i < t.Text.Length; i++)
            {
                Glyph g = PixelFont.Get(t.Text[i]);
                int gx = t.X + dx + i * PixelFont.Advance * t.Scale;
                for (int row = 0; row < PixelFont.GlyphHeight; row++)
                    for (int col = 0; col < PixelFont.GlyphWidth; col++)
                        if (g.IsLit(col, row))
                            _spriteBatch.Draw(_pixel,
                                new Rectangle(gx + col * t.Scale, t.Y + dy + row * t.Scale, t.Scale, t.Scale),
                                color);
            }
        }

        private void DrawLine(float x0, float y0, float x1, float y1, Color color, float thickness)
        {
            var from = new Vector2(x0, y0);
            Vector2 diff = new Vector2(x1, y1) - from;
            if (diff == Vector2.Zero) return;
            float angle = MathF.Atan2(diff.Y, diff.X);
            _spriteBatch.Draw(_pixel, from, null, color, angle, Vector2.Zero,
                new Vector2(diff.Length(), thickness), SpriteEffects.None, 0f);
        }

        private static Color ToXna(RenderColor c) => new Color(c.R, c.G, c.B, c.A);

        protected override void UnloadContent()
        {
            _client.Disconnect();
            _pixel?.Dispose();
            _spriteBatch?.Dispose();
            base.UnloadContent();
        }
    }
}
