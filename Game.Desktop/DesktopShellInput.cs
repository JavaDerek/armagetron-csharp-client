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
    }
}
