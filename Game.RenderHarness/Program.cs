using System;
using System.IO;
using Armagetron.Game;
using Armagetron.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Armagetron.Game.RenderHarness
{
    /// <summary>
    /// Headless render harness (Tier 2): renders a SCRIPTED GameWorld — no socket, no
    /// live server — to an offscreen RenderTarget and writes a PNG. Lets rendering be
    /// inspected as an artifact without a window or a human. The scene routes through
    /// the same pure <see cref="SceneBuilder"/> the desktop client uses, so the PNG is a
    /// faithful render of production geometry.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string outPath = args.Length > 0 ? args[0] : "/tmp/aa_render_spike.png";
            using var game = new HarnessGame(BuildDeathFreezeScene(), outPath);
            game.Run();
            return File.Exists(outPath) ? 0 : 1;
        }

        /// <summary>
        /// Scenario: a remote cycle drives right, turns up, then DIES at the wall
        /// (alive=false → frozen). Visually proves the death-freeze: the trail must
        /// stop at the corner+wall and not coast past. Plus our own (green) cycle.
        /// </summary>
        private static GameWorld BuildDeathFreezeScene()
        {
            var w = new GameWorld();
            w.SetMyCycleId(5);

            // Remote cycle 9: right along y=40, turn up at x=120, then die at (120,160).
            w.UpdateRemoteCycle(9, new Vec2(10, 40),  new Vec2(1, 0), nowMs: 0,    alive: true,  speed: 30f);
            w.UpdateRemoteCycle(9, new Vec2(120, 40), new Vec2(1, 0), nowMs: 1000, alive: true,  speed: 30f);
            w.UpdateRemoteCycle(9, new Vec2(120, 90), new Vec2(0, 1), nowMs: 2000, alive: true,  speed: 30f);
            w.UpdateRemoteCycle(9, new Vec2(120, 160), new Vec2(0, 1), nowMs: 3000, alive: false, speed: 30f);

            // Our cycle 5: a short straight run along the bottom.
            w.MoveLocalCycle(5, new Vec2(40, 12), new Vec2(1, 0));
            w.MoveLocalCycle(5, new Vec2(90, 12), new Vec2(1, 0));

            return w;
        }
    }

    internal sealed class HarnessGame : Microsoft.Xna.Framework.Game
    {
        private const float ArenaSize = 176.78f;
        private const float Margin    = 10f;
        private const int   ViewSize  = 800;

        private readonly GraphicsDeviceManager _graphics;
        private readonly GameWorld _world;
        private readonly string _outPath;
        private readonly ArenaView _view = new ArenaView(ArenaSize, Margin, ViewSize);
        private readonly CyclePalette _palette = new CyclePalette();

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _pixel = null!;
        private bool _captured;

        public HarnessGame(GameWorld world, string outPath)
        {
            _world = world;
            _outPath = outPath;
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = ViewSize,
                PreferredBackBufferHeight = ViewSize,
            };
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        protected override void Update(GameTime gameTime)
        {
            if (_captured) { Exit(); return; }

            var rt = new RenderTarget2D(GraphicsDevice, ViewSize, ViewSize);
            GraphicsDevice.SetRenderTarget(rt);
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin();

            Scene scene = SceneBuilder.Build(
                _world.Snapshot(nowMs: 9_999), _world.MyCycleId, _view, _palette);

            foreach (RenderSegment seg in scene.Segments)
                DrawLine(seg.From, seg.To, ToXna(seg.Color), seg.Thickness);
            foreach (RenderRect r in scene.Heads)
                _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, r.W, r.H), ToXna(r.Color));

            _spriteBatch.End();
            GraphicsDevice.SetRenderTarget(null);

            using (var fs = File.Create(_outPath))
                rt.SaveAsPng(fs, ViewSize, ViewSize);
            Console.Error.WriteLine($"wrote {_outPath}");
            _captured = true;
        }

        private void DrawLine(Vec2 from, Vec2 to, Color color, float thickness)
        {
            var f = new Vector2(from.X, from.Y);
            var t = new Vector2(to.X, to.Y);
            Vector2 diff = t - f;
            if (diff == Vector2.Zero) return;
            float angle = MathF.Atan2(diff.Y, diff.X);
            float len = diff.Length();
            _spriteBatch.Draw(_pixel, f, null, color, angle, Vector2.Zero,
                new Vector2(len, thickness), SpriteEffects.None, 0f);
        }

        private static Color ToXna(RenderColor c) => new Color(c.R, c.G, c.B, c.A);
    }
}
