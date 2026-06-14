using System;
using System.Collections.Generic;
using System.IO;
using Armagetron.Game;
using Armagetron.Game.UI;
using Armagetron.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Armagetron.Game.RenderHarness
{
    /// <summary>
    /// Headless render harness (Tier 2): builds a pure <see cref="Scene"/> — no socket, no
    /// live server — renders it to an offscreen RenderTarget, and writes a PNG. Lets every
    /// rendered surface (gameplay AND the UI screens) be inspected as an artifact without a
    /// window or a human. Each scenario routes through the SAME pure builders the real client
    /// uses (<see cref="SceneBuilder"/> for gameplay, the screen view builders for UI), so a
    /// PNG is a faithful render of production geometry and text.
    /// </summary>
    internal static class Program
    {
        private const int Size = 800;

        private static int Main(string[] args)
        {
            string scenario = args.Length > 0 ? args[0] : "freeze";
            string outPath  = args.Length > 1 ? args[1] : $"/tmp/aa_render_{scenario}.png";
            using var game = new HarnessGame(BuildScene(scenario), Size, outPath);
            game.Run();
            return File.Exists(outPath) ? 0 : 1;
        }

        private static Scene BuildScene(string scenario) => scenario switch
        {
            "font" => FontProof(),
            "connect" or "connecting" or "playing" or "paused" or "settings" => ScreenShot(scenario),
            _      => Gameplay(scenario),
        };

        /// <summary>Render a UI screen via the real <see cref="AppShell"/> (gameplay behind it
        /// when applicable), so the PNG is exactly what the front-end would draw.</summary>
        private static Scene ScreenShot(string name)
        {
            var client = new HarnessClient();
            var shell = new AppShell(client, UiTheme.Default, "192.168.68.61", 4534, "AaBot", touchControls: true);
            long now = 800;

            var conn = Layouts.Connect(Size, Size).Connect;
            void StartPlaying()
            {
                shell.HandleTap(conn.CenterX, conn.CenterY, Size, Size); // → Connecting
                client.Status = ConnectionStatus.Connected;
                shell.Tick(Array.Empty<CycleSnapshot>(), now);          // → Playing
                shell.OnRoundStart(0);
            }

            switch (name)
            {
                case "connecting":
                    shell.HandleTap(conn.CenterX, conn.CenterY, Size, Size);
                    break;
                case "playing":
                    StartPlaying(); now = 65_000;
                    break;
                case "paused":
                    StartPlaying(); shell.OnBack();
                    break;
                case "settings":
                    StartPlaying(); shell.OnBack();
                    var sb = Layouts.Menu(Size, Size, 3).Buttons[1];
                    shell.HandleTap(sb.CenterX, sb.CenterY, Size, Size);
                    break;
            }

            var buf = new SceneBuf();
            if (shell.ShowsGameplay) buf.Append(Gameplay("freeze"));
            buf.Append(shell.BuildOverlay(Size, Size, now));
            return buf.ToScene();
        }

        /// <summary>
        /// A remote cycle drives right, turns up, reaches the TOP WALL; the render is taken
        /// long after the final sync. "freeze" → final sync alive=false (head stops on the
        /// wall); "ghost" → treated as alive (head pokes through). Same scenarios as before.
        /// </summary>
        private static Scene Gameplay(string scenario)
        {
            bool alive = scenario == "ghost";
            const float topWall = 176.78f;

            var w = new GameWorld();
            w.SetMyCycleId(5);
            w.UpdateRemoteCycle(9, new Vec2(10, 40),       new Vec2(1, 0), nowMs: 0,    alive: true,  speed: 30f);
            w.UpdateRemoteCycle(9, new Vec2(120, 40),      new Vec2(1, 0), nowMs: 1000, alive: true,  speed: 30f);
            w.UpdateRemoteCycle(9, new Vec2(120, 90),      new Vec2(0, 1), nowMs: 2000, alive: true,  speed: 30f);
            w.UpdateRemoteCycle(9, new Vec2(120, topWall), new Vec2(0, 1), nowMs: 3000, alive: alive, speed: 30f);
            w.MoveLocalCycle(5, new Vec2(40, 12), new Vec2(1, 0));
            w.MoveLocalCycle(5, new Vec2(90, 12), new Vec2(1, 0));

            var view = new ArenaView(arenaSize: topWall, margin: 10f, viewSize: Size);
            return SceneBuilder.Build(w.Snapshot(nowMs: 9_999), w.MyCycleId, view, new CyclePalette());
        }

        /// <summary>Exercises every placeholder glyph so the font can be eyeballed.</summary>
        private static Scene FontProof()
        {
            var texts = new List<RenderText>
            {
                new RenderText("ABCDEFGHIJKLM", 40, 60,  RenderColor.White, scale: 6),
                new RenderText("NOPQRSTUVWXYZ", 40, 130, RenderColor.White, scale: 6),
                new RenderText("0123456789",    40, 200, new RenderColor(124, 252, 0), scale: 6),
                new RenderText(".,:/-_!?()+=<>%*", 40, 270, new RenderColor(0, 255, 255), scale: 5),
                new RenderText("CONNECT TO SERVER", 40, 360, new RenderColor(255, 200, 0), scale: 4),
                new RenderText("HOST: 192.168.68.61", 40, 420, RenderColor.White, scale: 3),
                new RenderText("PORT: 4534   PING 24MS", 40, 470, RenderColor.White, scale: 3),
                new RenderText("the quick brown fox 99%", 40, 540, new RenderColor(180, 180, 180), scale: 3),
            };
            return new Scene(Array.Empty<RenderSegment>(), Array.Empty<RenderRect>(), texts);
        }
    }

    /// <summary>A no-socket <see cref="IUiClient"/> for screenshotting UI screens.</summary>
    internal sealed class HarnessClient : IUiClient
    {
        public ConnectionStatus Status { get; set; } = ConnectionStatus.Idle;
        public string? LastError => null;
        public int MyCycleId => -1;
        public CycleSnapshot[] Snapshot() => Array.Empty<CycleSnapshot>();
        public void BeginConnect(string host, int port, string name) => Status = ConnectionStatus.Connecting;
        public void Disconnect() => Status = ConnectionStatus.Idle;
        public void TurnLeft() { }
        public void TurnRight() { }
        public IReadOnlyList<MatchEvent> DrainEvents() => Array.Empty<MatchEvent>();
    }

    internal sealed class HarnessGame : Microsoft.Xna.Framework.Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private readonly Scene _scene;
        private readonly int _size;
        private readonly string _outPath;

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _pixel = null!;
        private bool _captured;

        public HarnessGame(Scene scene, int size, string outPath)
        {
            _scene = scene;
            _size = size;
            _outPath = outPath;
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = size,
                PreferredBackBufferHeight = size,
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

            var rt = new RenderTarget2D(GraphicsDevice, _size, _size);
            GraphicsDevice.SetRenderTarget(rt);
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin();

            foreach (RenderSegment seg in _scene.Segments)
                DrawLine(seg.From, seg.To, ToXna(seg.Color), seg.Thickness);
            foreach (RenderRect r in _scene.Heads)
                _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, r.W, r.H), ToXna(r.Color));
            foreach (RenderText t in _scene.Texts)
                DrawText(t);

            _spriteBatch.End();
            GraphicsDevice.SetRenderTarget(null);

            using (var fs = File.Create(_outPath))
                rt.SaveAsPng(fs, _size, _size);
            Console.Error.WriteLine($"wrote {_outPath}");
            _captured = true;
        }

        // Draw a RenderText one lit glyph-cell at a time (placeholder PixelFont). When the
        // designer's real font lands this is replaced by a SpriteFont DrawString.
        private void DrawText(RenderText t)
        {
            Color color = ToXna(t.Color);
            for (int i = 0; i < t.Text.Length; i++)
            {
                Glyph g = PixelFont.Get(t.Text[i]);
                int gx = t.X + i * PixelFont.Advance * t.Scale;
                for (int row = 0; row < PixelFont.GlyphHeight; row++)
                    for (int col = 0; col < PixelFont.GlyphWidth; col++)
                        if (g.IsLit(col, row))
                            _spriteBatch.Draw(_pixel,
                                new Rectangle(gx + col * t.Scale, t.Y + row * t.Scale, t.Scale, t.Scale),
                                color);
            }
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
