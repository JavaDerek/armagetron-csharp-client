using System;
using System.Collections.Generic;
using System.Threading;
using Armagetron.Net;
using Armagetron.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Armagetron.Game
{
    /// <summary>
    /// MonoGame host for the Armagetron desktop client.
    /// Runs the protocol session on a background thread and renders the game world
    /// on the main thread at 60 Hz.
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

        private readonly GameWorld    _world;
        private readonly PlayerSession _session;
        private Thread?               _sessionThread;

        private KeyboardState _prevKeys;

        // Pure render model: all "what to draw" decisions (projection, segments, draw
        // order, colors) live here and are unit-tested; Draw() is only GPU glue.
        private readonly ArenaView    _view    = new ArenaView(ArenaSize, ArenaMargin, ViewSize);
        private readonly CyclePalette _palette = new CyclePalette();

        /// <summary>
        /// Construct with a session that has ALREADY registered (reached Playing) on the
        /// caller's uncontended thread — see <see cref="PlayerSession.RunUntilPlaying"/>.
        /// The render loop here would starve the registration race if it ran first, so
        /// registration is done before this object exists; we only continue the loop.
        /// </summary>
        public ArmagetronGame(PlayerSession session, GameWorld world, string title)
        {
            _session = session;
            _world   = world;
            _title   = title;

            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth  = ViewSize;
            _graphics.PreferredBackBufferHeight = ViewSize;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            Window.Title = _title;

            // Registration already happened on the main thread; just keep the session
            // pumping for the gameplay phase. Render contention now only affects
            // dead-reckoning/turns, which tolerate it (unlike one-shot registration).
            _sessionThread = new Thread(() => _session.RunLoop())
            {
                IsBackground = true,
                Name         = "ProtocolThread",
            };
            _sessionThread.Start();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel       = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        protected override void Update(GameTime gameTime)
        {
            var keys = Keyboard.GetState();

            if (keys.IsKeyDown(Keys.Escape)) Exit();

            if (IsNewPress(keys, _prevKeys, Keys.Left))
                _session.QueueTurn(TurnDirection.Left);
            if (IsNewPress(keys, _prevKeys, Keys.Right))
                _session.QueueTurn(TurnDirection.Right);

            _prevKeys = keys;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin();

            // Dead-reckon remote cycles to "now" so they move smoothly between sparse syncs,
            // then let the pure model decide everything; here we only issue the GPU calls.
            CycleSnapshot[] cycles = _world.Snapshot(Environment.TickCount64);
            Scene scene = SceneBuilder.Build(cycles, _world.MyCycleId, _view, _palette);

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
            _session.RequestStop();
            _sessionThread?.Join(millisecondsTimeout: 1000);
            _session.Dispose();
            _pixel?.Dispose();
            _spriteBatch?.Dispose();
            base.UnloadContent();
        }
    }
}
