using System;
using Armagetron.Lib;
using Armagetron.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Armagetron.Game
{
    /// <summary>
    /// MonoGame host for the Armagetron client, shared by every platform head (desktop,
    /// Android). It is a pure front-end over the ArmaLib <see cref="ArmaClient"/> facade:
    /// it pulls turn intents from a platform-supplied <see cref="ITurnInput"/>, forwards
    /// them, grabs a render-ready snapshot each frame, and draws it. All networking, the
    /// session-loop thread, every protocol primitive, AND the choice of input device live
    /// outside this class — desktop hands it a keyboard input, Android hands it touch.
    /// </summary>
    public sealed class ArmagetronGame : Microsoft.Xna.Framework.Game
    {
        // Arena runs 0→ArenaSize in both axes (empirical from spawn positions;
        // will be replaced by desc=51 decode once that's implemented).
        private const float ArenaSize   = 176.78f;
        private const float ArenaMargin = 10f;    // screen-pixel padding outside the walls
        private const int   WindowSize  = 800;    // desktop window side in pixels

        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch    = null!;
        private Texture2D   _pixel          = null!;

        private readonly string _title;

        private readonly ArmaClient _client;
        private readonly ITurnInput _input;

        // The pure render model is built in LoadContent once the real back-buffer size is
        // known: the arena is drawn as a centred square sized to the smaller screen edge,
        // so it fills a desktop window and letterboxes correctly on any phone aspect ratio.
        private ArenaView    _view    = null!;
        private int          _offsetX, _offsetY;
        private readonly CyclePalette _palette = new CyclePalette();

        /// <summary>
        /// Construct with a client that has ALREADY connected+registered (see
        /// <see cref="ArmaClient.Connect"/>) and a platform input source. When
        /// <paramref name="fullscreen"/> is true (Android) the device resolution is used;
        /// otherwise an 800×800 window is requested (desktop).
        /// </summary>
        public ArmagetronGame(ArmaClient client, ITurnInput input, string title, bool fullscreen = false)
        {
            _client = client;
            _input  = input;
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

            // Square arena sized to the shorter screen edge, centred in the back buffer.
            int w    = GraphicsDevice.Viewport.Width;
            int h    = GraphicsDevice.Viewport.Height;
            int side = Math.Min(w, h);
            _view    = new ArenaView(ArenaSize, ArenaMargin, side);
            _offsetX = (w - side) / 2;
            _offsetY = (h - side) / 2;
        }

        protected override void Update(GameTime gameTime)
        {
            foreach (TurnDirection dir in _input.Poll())
            {
                if (dir == TurnDirection.Left) _client.TurnLeft();
                else                           _client.TurnRight();
            }

            if (_input.ExitRequested) Exit();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin();

            // The facade hands back cycles already dead-reckoned to "now"; the pure model
            // decides everything else. Here we only issue the GPU calls, shifting by the
            // centring offset so the square arena sits in the middle of the back buffer.
            CycleSnapshot[] cycles = _client.Snapshot();
            Scene scene = SceneBuilder.Build(cycles, _client.MyCycleId, _view, _palette);

            foreach (RenderSegment seg in scene.Segments)
                DrawLine(ToVec(seg.From), ToVec(seg.To), ToXna(seg.Color), seg.Thickness);

            foreach (RenderRect r in scene.Heads)
                _spriteBatch.Draw(_pixel, new Rectangle(r.X + _offsetX, r.Y + _offsetY, r.W, r.H), ToXna(r.Color));

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        // ── GPU glue (the only rendering code that needs a graphics device) ───

        private void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
        {
            Vector2 diff = to - from;
            if (diff == Vector2.Zero) return;
            float angle = MathF.Atan2(diff.Y, diff.X);
            float len   = diff.Length();
            _spriteBatch.Draw(
                _pixel, from, null, color, angle,
                Vector2.Zero, new Vector2(len, thickness),
                SpriteEffects.None, 0f);
        }

        private Vector2 ToVec(Vec2 v) => new Vector2(v.X + _offsetX, v.Y + _offsetY);
        private static Color ToXna(RenderColor c) => new Color(c.R, c.G, c.B, c.A);

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void UnloadContent()
        {
            _client.Disconnect();
            _pixel?.Dispose();
            _spriteBatch?.Dispose();
            base.UnloadContent();
        }
    }
}
