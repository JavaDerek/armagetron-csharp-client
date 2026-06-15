using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Armagetron.Game;
using Armagetron.Game.Rendering;
using Armagetron.Game.UI;
using Armagetron.Lib;
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
        // Landscape, matching the design's 2400×1080 reference and the desktop window's aspect.
        private const int W = 1280;
        private const int H = 720;

        private static int Main(string[] args)
        {
            string scenario = args.Length > 0 ? args[0] : "freeze";
            string outPath  = args.Length > 1 ? args[1] : $"/tmp/aa_render_{scenario}.png";

            // 3D perspective scenarios: render the WorldScene from a fixed camera pose so the
            // third-person / first-person views can be eyeballed as a PNG with no window.
            if (scenario == "3d" || scenario == "3d-third" || scenario == "3d-first")
            {
                (WorldScene world, CameraPose pose) = Build3D(scenario);
                using var game3d = new HarnessGame(new List<Layer>(), W, H, outPath, world, pose);
                game3d.Run();
                return File.Exists(outPath) ? 0 : 1;
            }

            // LIVE scenarios: actually connect to a running server, capture a real snapshot, and
            // render it — the headless live-server verification of the 3D views (CLAUDE.md step 4).
            //   live-3d-third | live-3d-first | live-2d   [out.png] [host] [port] [name]
            if (scenario.StartsWith("live", StringComparison.Ordinal))
            {
                string host = args.Length > 2 ? args[2] : "192.168.68.61";
                int port = args.Length > 3 ? int.Parse(args[3]) : 4534;
                string name = args.Length > 4 ? args[4] : "Vlad";

                (CycleSnapshot[] snap, int myId) = CaptureLive(host, port, name);
                Console.Error.WriteLine($"[live] captured {snap.Length} cycles (myId={myId})");

                if (scenario == "live-2d")
                {
                    int side = Math.Min(W, H);
                    var view = new ArenaView(176.78f, 10f, side);
                    Scene s2d = SceneBuilder.BuildWithArt(snap, myId, view, new CyclePalette(), 8);
                    using var live2d = new HarnessGame(
                        new List<Layer> { new Layer(s2d, (W - side) / 2, (H - side) / 2) }, W, H, outPath);
                    live2d.Run();
                }
                else
                {
                    const float arena = 176.78f, wall = 8f;
                    Vec2 pos = new Vec2(arena / 2f, arena / 2f), dir = new Vec2(1, 0);
                    foreach (CycleSnapshot c in snap)
                        if (c.CycleId == myId) { pos = c.Position; dir = c.Direction; }
                    var cam = new CameraController(CameraSettings.Default);
                    cam.SetMode(scenario == "live-3d-first" ? CameraMode.FirstPerson : CameraMode.ThirdPerson);
                    WorldScene world = Scene3DBuilder.Build(snap, myId, new CyclePalette(), arena, wall);
                    using var live3d = new HarnessGame(new List<Layer>(), W, H, outPath, world, cam.Pose(pos, dir));
                    live3d.Run();
                }
                return File.Exists(outPath) ? 0 : 1;
            }

            using var game = new HarnessGame(BuildLayers(scenario), W, H, outPath);
            game.Run();
            return File.Exists(outPath) ? 0 : 1;
        }

        // Connect to a live server, wait until joined, then sample snapshots for a few seconds and
        // return the richest one (most cycles, local cycle preferred) so the render has real
        // geometry. Falls back to whatever we have on timeout. Name 'Vlad' clears the cheat gate.
        private static (CycleSnapshot[] snap, int myId) CaptureLive(string host, int port, string name)
        {
            using var client = new UiArmaClient();
            Console.Error.WriteLine($"[live] BeginConnect {host}:{port} as '{name}'");
            client.BeginConnect(host, port, name);

            var sw = Stopwatch.StartNew();
            while (client.Status == ConnectionStatus.Connecting && sw.Elapsed.TotalSeconds < 55)
                Thread.Sleep(100);
            Console.Error.WriteLine($"[live] Status={client.Status} MyCycleId={client.MyCycleId}");

            CycleSnapshot[] best = Array.Empty<CycleSnapshot>();
            if (client.Status == ConnectionStatus.Connected)
            {
                // Sample for ~8s; keep the snapshot with the most cycles (prefer one containing ours).
                var sampleSw = Stopwatch.StartNew();
                while (sampleSw.Elapsed.TotalSeconds < 8)
                {
                    CycleSnapshot[] s = client.Snapshot();
                    bool hasMine = Array.Exists(s, c => c.CycleId == client.MyCycleId);
                    bool bestHasMine = Array.Exists(best, c => c.CycleId == client.MyCycleId);
                    if (s.Length > best.Length || (hasMine && !bestHasMine))
                        best = s;
                    Thread.Sleep(120);
                }
            }
            int myId = client.MyCycleId;
            client.Disconnect();
            return (best, myId);
        }

        // Build the demo world plus a camera pose for the requested 3D view.
        private static (WorldScene, CameraPose) Build3D(string scenario)
        {
            const float arena = 176.78f, wall = 8f;
            (CycleSnapshot[] snap, int myId) = DemoWorld(arena);

            Vec2 pos = new Vec2(arena / 2f, arena / 2f), dir = new Vec2(1, 0);
            foreach (CycleSnapshot c in snap)
                if (c.CycleId == myId) { pos = c.Position; dir = c.Direction; }

            var cam = new CameraController(CameraSettings.Default);
            cam.SetMode(scenario == "3d-first" ? CameraMode.FirstPerson : CameraMode.ThirdPerson);

            WorldScene world = Scene3DBuilder.Build(snap, myId, new CyclePalette(), arena, wall);
            return (world, cam.Pose(pos, dir));
        }

        // The same scripted world used by the 2D gameplay shot: a remote cycle that drives east,
        // turns up and stops on the top wall, plus the local cycle driving east near the bottom.
        private static (CycleSnapshot[] snap, int myId) DemoWorld(float topWall, bool ghost = false)
        {
            var w = new GameWorld();
            w.SetMyCycleId(5);
            w.UpdateRemoteCycle(9, new Vec2(10, 40),       new Vec2(1, 0), nowMs: 0,    alive: true,   speed: 30f);
            w.UpdateRemoteCycle(9, new Vec2(120, 40),      new Vec2(1, 0), nowMs: 1000, alive: true,   speed: 30f);
            w.UpdateRemoteCycle(9, new Vec2(120, 90),      new Vec2(0, 1), nowMs: 2000, alive: true,   speed: 30f);
            w.UpdateRemoteCycle(9, new Vec2(120, topWall), new Vec2(0, 1), nowMs: 3000, alive: ghost,  speed: 30f);
            w.MoveLocalCycle(5, new Vec2(40, 12), new Vec2(1, 0));
            w.MoveLocalCycle(5, new Vec2(90, 12), new Vec2(1, 0));
            return (w.Snapshot(nowMs: 9_999), w.MyCycleId);
        }

        private static List<Layer> BuildLayers(string scenario) => scenario switch
        {
            "font" => new List<Layer> { new Layer(FontProof(), 0, 0) },
            "connect" or "connecting" or "playing" or "paused" or "settings" or "servers"
                   => ScreenShot(scenario),
            _      => new List<Layer> { GameplayLayer(scenario) },
        };

        /// <summary>One translated scene layer (gameplay is letterboxed; the overlay is at 0,0).</summary>
        internal readonly struct Layer
        {
            public readonly Scene Scene; public readonly int Dx, Dy;
            public Layer(Scene scene, int dx, int dy) { Scene = scene; Dx = dx; Dy = dy; }
        }

        /// <summary>Render a UI screen via the real <see cref="AppShell"/> (gameplay behind it
        /// when applicable), so the PNG is exactly what the front-end would draw.</summary>
        private static List<Layer> ScreenShot(string name)
        {
            var client = new HarnessClient();
            // Desktop-accurate: no touch controls (arrow-key steering, no tap zones / no overlay).
            var shell = new AppShell(client, UiTheme.Default, "192.168.68.61", 4534, "AaBot", touchControls: false);
            long now = 800;

            var conn = Layouts.Connect(W, H).Connect;
            void StartPlaying()
            {
                shell.HandleTap(conn.CenterX, conn.CenterY, W, H);      // → Connecting
                client.Status = ConnectionStatus.Connected;
                shell.Tick(Array.Empty<CycleSnapshot>(), now);          // → Playing
            }

            switch (name)
            {
                case "connecting":
                    shell.HandleTap(conn.CenterX, conn.CenterY, W, H);
                    break;
                case "playing":
                    StartPlaying();
                    client.Events.Enqueue(MatchEvent.RoundStart);       // banner + toast
                    shell.Tick(Array.Empty<CycleSnapshot>(), 1_200);
                    now = 1_200;
                    break;
                case "paused":
                    StartPlaying(); shell.OnBack();
                    break;
                case "settings":
                    StartPlaying(); shell.OnBack();
                    var sb = Layouts.Menu(W, H, 3).Buttons[1];
                    shell.HandleTap(sb.CenterX, sb.CenterY, W, H);
                    break;
                case "servers":
                    var br = Layouts.Connect(W, H).Browse;
                    shell.HandleTap(br.CenterX, br.CenterY, W, H);
                    break;
            }

            var layers = new List<Layer>();
            if (shell.ShowsGameplay) layers.Add(GameplayLayer("freeze"));
            layers.Add(new Layer(shell.BuildOverlay(W, H, now), 0, 0));
            return layers;
        }

        // The arena is a centred square of the shorter edge, letterboxed into the landscape frame.
        private static Layer GameplayLayer(string scenario)
        {
            int side = Math.Min(W, H);
            return new Layer(Gameplay(scenario, side), (W - side) / 2, (H - side) / 2);
        }

        /// <summary>
        /// A remote cycle drives right, turns up, reaches the TOP WALL; the render is taken
        /// long after the final sync. "freeze" → final sync alive=false (head stops on the
        /// wall); "ghost" → treated as alive (head pokes through).
        /// </summary>
        private static Scene Gameplay(string scenario, int side)
        {
            const float topWall = 176.78f;
            (CycleSnapshot[] snap, int myId) = DemoWorld(topWall, ghost: scenario == "ghost");
            var view = new ArenaView(arenaSize: topWall, margin: 10f, viewSize: side);
            return SceneBuilder.BuildWithArt(snap, myId, view, new CyclePalette(), divisions: 8);
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
        public readonly Queue<MatchEvent> Events = new Queue<MatchEvent>();
        public IReadOnlyList<MatchEvent> DrainEvents()
        {
            var list = new List<MatchEvent>(Events);
            Events.Clear();
            return list;
        }
    }

    internal sealed class HarnessGame : Microsoft.Xna.Framework.Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private readonly List<Program.Layer> _layers;
        private readonly int _w, _h;
        private readonly string _outPath;

        private readonly WorldScene? _world3d;
        private readonly CameraPose _pose3d;

        private TextureStore _textures = null!;
        private TextRenderer _text = null!;
        private SceneRenderer _renderer = null!;
        private Scene3DRenderer _renderer3d = null!;
        private bool _captured;

        public HarnessGame(List<Program.Layer> layers, int w, int h, string outPath,
                           WorldScene? world3d = null, CameraPose pose3d = default)
        {
            _layers = layers;
            _w = w;
            _h = h;
            _outPath = outPath;
            _world3d = world3d;
            _pose3d = pose3d;
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = w,
                PreferredBackBufferHeight = h,
            };
        }

        protected override void LoadContent()
        {
            _textures = new TextureStore(GraphicsDevice);
            _text = new TextRenderer();
            _renderer = new SceneRenderer(GraphicsDevice, _textures, _text);
            _renderer3d = new Scene3DRenderer(GraphicsDevice, _textures);
        }

        protected override void Update(GameTime gameTime)
        {
            if (_captured) { Exit(); return; }

            var rt = new RenderTarget2D(GraphicsDevice, _w, _h, false,
                                        SurfaceFormat.Color, DepthFormat.Depth24);
            GraphicsDevice.SetRenderTarget(rt);
            GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1f, 0);

            if (_world3d != null)
                _renderer3d.Render(_world3d, _pose3d, _w, _h);

            foreach (Program.Layer layer in _layers)
                _renderer.Render(layer.Scene, layer.Dx, layer.Dy);

            GraphicsDevice.SetRenderTarget(null);

            using (var fs = File.Create(_outPath))
                rt.SaveAsPng(fs, _w, _h);
            Console.Error.WriteLine($"wrote {_outPath}");
            _captured = true;
        }

        protected override void UnloadContent()
        {
            _renderer3d?.Dispose();
            _renderer?.Dispose();
            _text?.Dispose();
            _textures?.Dispose();
            base.UnloadContent();
        }
    }
}
