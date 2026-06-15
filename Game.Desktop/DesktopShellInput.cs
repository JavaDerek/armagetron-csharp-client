using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Armagetron.Game.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Armagetron.Game
{
    /// <summary>
    /// Desktop <see cref="IShellInput"/>: mouse clicks are taps, ← / → are turn keys, Esc is
    /// Back, and live typing into the focused field comes from the window's TextInput event
    /// (printable chars + backspace). Edge-detected so each press counts once. This is the I/O
    /// edge over MonoGame's Mouse/Keyboard/Window — excluded from coverage; the routing it
    /// feeds is the unit-tested <see cref="AppShell"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class DesktopShellInput : IShellInput
    {
        private MouseState _prevMouse;
        private KeyboardState _prevKeys, _curKeys;
        private readonly List<char> _typed = new List<char>();
        private int _backspaces;

        // Separate baselines for camera input, so its edge/drag/scroll detection is independent
        // of the shell-key edge machinery (whose baseline is advanced by ApplyTextEditing).
        private KeyboardState _prevCamKeys;
        private MouseState _prevCamMouse;
        private bool _camInit;

        /// <summary>Subscribe to the window's text-input event (call after the game is created).</summary>
        public void Attach(GameWindow window) => window.TextInput += OnTextInput;

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            char c = e.Character;
            if (c == '\b') _backspaces++;
            else if (c >= ' ' && c <= '~') _typed.Add(c);
        }

        public IReadOnlyList<TapPoint> Taps()
        {
            MouseState m = Mouse.GetState();
            var taps = new List<TapPoint>();
            if (m.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
                taps.Add(new TapPoint(m.X, m.Y));
            _prevMouse = m;
            return taps;
        }

        public IReadOnlyList<TurnDirection> TurnKeys()
        {
            _curKeys = Keyboard.GetState();
            var turns = new List<TurnDirection>(2);
            if (Edge(Keys.Left))  turns.Add(TurnDirection.Left);
            if (Edge(Keys.Right)) turns.Add(TurnDirection.Right);
            return turns;
        }

        public bool BackPressed() => Edge(Keys.Escape);

        public void ApplyTextEditing(AppShell shell)
        {
            for (int i = 0; i < _backspaces; i++) shell.OnBackspace();
            foreach (char c in _typed) shell.OnText(c);
            _backspaces = 0;
            _typed.Clear();
            _prevKeys = _curKeys; // advance edge baseline once per frame (last call in the loop)
        }

        private bool Edge(Keys k) => _curKeys.IsKeyDown(k) && !_prevKeys.IsKeyDown(k);

        /// <summary>
        /// Camera controls: <c>C</c> cycles top-down → third-person → first-person, <c>R</c>
        /// recentres the chase orbit, dragging with the right mouse button orbits the chase cam,
        /// and the scroll wheel zooms it. Uses its own key/mouse baselines (see fields) so it is
        /// unaffected by the shell-input edge bookkeeping.
        /// </summary>
        public CameraInputState CameraInput()
        {
            KeyboardState k = Keyboard.GetState();
            MouseState m = Mouse.GetState();
            if (!_camInit) { _prevCamKeys = k; _prevCamMouse = m; _camInit = true; }

            bool cycle = k.IsKeyDown(Keys.C) && !_prevCamKeys.IsKeyDown(Keys.C);
            bool reset = k.IsKeyDown(Keys.R) && !_prevCamKeys.IsKeyDown(Keys.R);

            float yaw = 0f, pitch = 0f;
            if (m.RightButton == ButtonState.Pressed && _prevCamMouse.RightButton == ButtonState.Pressed)
            {
                const float sensitivity = 0.005f;
                yaw = (m.X - _prevCamMouse.X) * sensitivity;
                pitch = (m.Y - _prevCamMouse.Y) * sensitivity;
            }

            // One wheel notch = 120 units; ~3 world units of zoom per notch, scroll-up = closer.
            float zoom = -(m.ScrollWheelValue - _prevCamMouse.ScrollWheelValue) / 120f * 3f;

            _prevCamKeys = k;
            _prevCamMouse = m;
            return new CameraInputState(cycle, reset, yaw, pitch, zoom);
        }
    }
}
