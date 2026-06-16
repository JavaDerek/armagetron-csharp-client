using System;
using Armagetron.Game.Audio;
using Armagetron.Game.Rendering;
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
        // Landscape window matching the design's 2400×1080 reference (16:9-ish) so the redline
        // layouts read correctly; the arena itself stays a centred square inside it.
        private const int   WindowW = 1280;
        private const int   WindowH = 720;

        // Arena floor tiling resolution (arena_tile cells per axis).
        private const int GridDivisions = 8;

        // How tall the light walls stand in the 3D views (world units). Empirical, tuned to the
        // arena scale; replaced by a server-decoded value alongside ArenaSize later.
        private const float WallHeight = 8f;

        private readonly GraphicsDeviceManager _graphics;
        private TextureStore    _textures  = null!;
        private TextRenderer    _text      = null!;
        private SceneRenderer   _renderer  = null!;
        private Scene3DRenderer _renderer3d = null!;
        private MusicController  _music     = null!;
        private SfxController    _sfx       = null!;
        private readonly CameraController _camera = new CameraController(CameraSettings.Default);

        private readonly string _title;
        private readonly string? _mediaRoot;
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
                              string title, bool fullscreen = false, string? mediaRoot = null)
        {
            _client = client;
            _input  = input;
            _shell  = shell;
            _title  = title;
            // Where the loaders read fonts/textures/audio. Desktop leaves this null (the loaders
            // default to "<binary>/media", populated by MediaContent.props). Android passes the
            // dir it unpacks its bundled assets into, since the APK has no such filesystem tree.
            _mediaRoot = mediaRoot;

            _graphics = new GraphicsDeviceManager(this);
            if (fullscreen)
            {
                _graphics.IsFullScreen = true;
            }
            else
            {
                _graphics.PreferredBackBufferWidth  = WindowW;
                _graphics.PreferredBackBufferHeight = WindowH;
            }
            Window.AllowUserResizing = true;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            Window.Title = _title;
            _textures = new TextureStore(GraphicsDevice, _mediaRoot);
            _text     = new TextRenderer(_mediaRoot);
            _renderer  = new SceneRenderer(GraphicsDevice, _textures, _text);
            _renderer3d = new Scene3DRenderer(GraphicsDevice, _textures, _mediaRoot);
            _music    = new MusicController(_mediaRoot);
            _sfx      = new SfxController(_mediaRoot);
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

            // Camera controls (desktop): toggle perspective, orbit/zoom the chase cam.
            CameraInputState cam = _input.CameraInput();
            if (cam.CycleMode) _camera.NextMode();
            if (cam.ResetView) _camera.ResetOrbit();
            if (cam.Zoom != 0f) _camera.Zoom(cam.Zoom);
            if (cam.OrbitYaw != 0f || cam.OrbitPitch != 0f) _camera.Orbit(cam.OrbitYaw, cam.OrbitPitch);

            _snapshot = _client.Snapshot();
            _shell.Tick(_snapshot, _nowMs);

            // Music: in-match loop while gameplay shows, menu loop otherwise; silent if toggled off.
            _music.Update(_shell.Settings.Music, _shell.ShowsGameplay);

            // SFX: play the one-shot cues the shell queued this frame, and run/stop the engine
            // hum from EngineRunning — both gated by the Sound toggle.
            _sfx.PlayCues(_shell.Sfx.Drain(), _shell.Settings.Sound);
            _sfx.SetEngine(_shell.EngineRunning, _shell.Settings.Sound);

            if (_shell.ExitRequested) Exit();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            // 3D perspective views (third-person chase / first-person cockpit): render the world
            // from the camera, then the same screen-space HUD overlay on top.
            if (_shell.ShowsGameplay && _camera.Is3D)
            {
                GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1f, 0);
                WorldScene world = Scene3DBuilder.Build(_snapshot, _client.MyCycleId, _palette,
                                                        ArenaSize, WallHeight);
                (Vec2 pos, Vec2 dir) = LocalCycle();
                _renderer3d.Render(world, _camera.Pose(pos, dir), _w, _h);
                _renderer.Render(_shell.BuildOverlay(_w, _h, _nowMs), 0, 0);
                base.Draw(gameTime);
                return;
            }

            GraphicsDevice.Clear(Color.Black);

            if (_shell.ShowsGameplay)
            {
                Scene game = SceneBuilder.BuildWithArt(_snapshot, _client.MyCycleId, _view, _palette,
                                                       GridDivisions);
                _renderer.Render(game, _offsetX, _offsetY);
            }

            _renderer.Render(_shell.BuildOverlay(_w, _h, _nowMs), 0, 0);

            base.Draw(gameTime);
        }

        // The local cycle's head position/heading for the camera to follow; falls back to the
        // arena centre looking east before the player's cycle exists (pre-spawn / spectator).
        private (Vec2 pos, Vec2 dir) LocalCycle()
        {
            int myId = _client.MyCycleId;
            foreach (CycleSnapshot c in _snapshot)
                if (c.CycleId == myId)
                    return (c.Position, c.Direction);
            return (new Vec2(ArenaSize / 2f, ArenaSize / 2f), new Vec2(1, 0));
        }

        protected override void UnloadContent()
        {
            _client.Disconnect();
            _sfx?.Dispose();
            _music?.Dispose();
            _renderer3d?.Dispose();
            _renderer?.Dispose();
            _text?.Dispose();
            _textures?.Dispose();
            base.UnloadContent();
        }
    }
}
