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

        private static readonly Color[] CyclePalette =
        {
            Color.Red, Color.Cyan, Color.Yellow, Color.Magenta,
            Color.Orange, Color.HotPink, Color.DodgerBlue, Color.Lime,
        };

        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch    = null!;
        private Texture2D   _pixel          = null!;

        private readonly string _title;

        private readonly GameWorld    _world;
        private readonly PlayerSession _session;
        private Thread?               _sessionThread;

        private KeyboardState _prevKeys;

        // Per-session color assignment so colors are stable within a round.
        private readonly Dictionary<int, Color> _cycleColors = new Dictionary<int, Color>();
        private int _nextColorIndex;

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

            DrawArena();

            int           myId     = _world.MyCycleId;
            // Dead-reckon remote cycles to "now" so they move smoothly between sparse syncs.
            CycleSnapshot[] cycles = _world.Snapshot(Environment.TickCount64);

            // Draw other cycles first, our cycle on top.
            foreach (var c in cycles)
                if (c.CycleId != myId)
                    DrawCycle(c, CycleColor(c.CycleId, myId));

            foreach (var c in cycles)
                if (c.CycleId == myId)
                    DrawCycle(c, Color.LawnGreen);

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        // ── Rendering helpers ─────────────────────────────────────────────────

        private void DrawArena()
        {
            float w = ArenaSize;
            var tl = ToScreen(new Vec2(0, w));
            var tr = ToScreen(new Vec2(w, w));
            var br = ToScreen(new Vec2(w, 0));
            var bl = ToScreen(new Vec2(0, 0));

            DrawLine(tl, tr, Color.White);
            DrawLine(tr, br, Color.White);
            DrawLine(br, bl, Color.White);
            DrawLine(bl, tl, Color.White);
        }

        private void DrawCycle(CycleSnapshot cycle, Color color)
        {
            // Trail: segments between consecutive waypoints.
            for (int i = 0; i + 1 < cycle.Trail.Length; i++)
                DrawLine(ToScreen(cycle.Trail[i]), ToScreen(cycle.Trail[i + 1]), color);

            // Active segment: last waypoint → current dead-reckoned position.
            if (cycle.Trail.Length > 0)
                DrawLine(ToScreen(cycle.Trail[cycle.Trail.Length - 1]), ToScreen(cycle.Position), color);

            // Cycle head: small filled square.
            Vector2 head = ToScreen(cycle.Position);
            _spriteBatch.Draw(_pixel, new Rectangle((int)head.X - 3, (int)head.Y - 3, 7, 7), color);
        }

        private void DrawLine(Vector2 from, Vector2 to, Color color, float thickness = 2f)
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

        private Vector2 ToScreen(Vec2 world)
        {
            float scale = (ViewSize - 2f * ArenaMargin) / ArenaSize;
            return new Vector2(
                ArenaMargin + world.X * scale,
                ViewSize - ArenaMargin - world.Y * scale); // flip Y: game Y+ is up, screen Y+ is down
        }

        private Color CycleColor(int cycleId, int myId)
        {
            if (cycleId == myId) return Color.LawnGreen;
            if (!_cycleColors.TryGetValue(cycleId, out Color c))
            {
                c = CyclePalette[_nextColorIndex % CyclePalette.Length];
                _nextColorIndex++;
                _cycleColors[cycleId] = c;
            }
            return c;
        }

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
