using System;
using Armagetron.Lib;
using Armagetron.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Armagetron.Game
{
    /// <summary>
    /// MonoGame host for the Armagetron desktop client. It is now a pure front-end over the
    /// ArmaLib <see cref="ArmaClient"/> facade: it reads keyboard input and forwards turns,
    /// pulls a render-ready snapshot each frame, and draws it. All networking, the session
    /// loop thread, and every protocol primitive live inside ArmaClient — this class never
    /// sees them.
    /// </summary>
    public sealed class ArmagetronGame : Microsoft.Xna.Framework.Game
    {
        // Arena runs 0→ArenaSize in both axes (empirical from spawn positions;
        // will be replaced by desc=51 decode once that's implemented).
        private const float ArenaSize   = 176.78f;
        private const float ArenaMargin = 10f;    // screen-pixel padding outside the walls
        private const int   ViewSize    = 800;    // square render area in pixels

        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch    = null!;
        private Texture2D   _pixel          = null!;

        private readonly string _title;

        private readonly ArmaClient _client;

        private KeyboardState _prevKeys;

        // Pure render model: all "what to draw" decisions (projection, segments, draw
        // order, colors) live here and are unit-tested; Draw() is only GPU glue.
        private readonly ArenaView    _view    = new ArenaView(ArenaSize, ArenaMargin, ViewSize);
        private readonly CyclePalette _palette = new CyclePalette();

        /// <summary>
        /// Construct with a client that has ALREADY connected+registered (see
        /// <see cref="ArmaClient.Connect"/>). The facade is driving its own background
        /// session loop by the time this window opens; we only render and feed it input.
        /// </summary>
        public ArmagetronGame(ArmaClient client, string title)
        {
            _client = client;
            _title  = title;

            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth  = ViewSize;
            _graphics.PreferredBackBufferHeight = ViewSize;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            Window.Title = _title;
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel       = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        protected override void Update(GameTime gameTime)
        {
            var keys = Keyboard.GetState();

            if (keys.IsKeyDown(Keys.Escape)) Exit();

            if (IsNewPress(keys, _prevKeys, Keys.Left))  _client.TurnLeft();
            if (IsNewPress(keys, _prevKeys, Keys.Right)) _client.TurnRight();

            _prevKeys = keys;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin();

            // The facade hands back cycles already dead-reckoned to "now"; the pure model
            // decides everything else. Here we only issue the GPU calls.
            CycleSnapshot[] cycles = _client.Snapshot();
            Scene scene = SceneBuilder.Build(cycles, _client.MyCycleId, _view, _palette);

            foreach (RenderSegment seg in scene.Segments)
                DrawLine(ToVec(seg.From), ToVec(seg.To), ToXna(seg.Color), seg.Thickness);

            foreach (RenderRect r in scene.Heads)
                _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, r.W, r.H), ToXna(r.Color));

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

        private static Vector2 ToVec(Vec2 v) => new Vector2(v.X, v.Y);
        private static Color ToXna(RenderColor c) => new Color(c.R, c.G, c.B, c.A);

        private static bool IsNewPress(KeyboardState cur, KeyboardState prev, Keys key) =>
            cur.IsKeyDown(key) && !prev.IsKeyDown(key);

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
