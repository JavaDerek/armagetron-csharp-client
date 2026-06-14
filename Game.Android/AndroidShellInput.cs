using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Armagetron.Game;
using Armagetron.Game.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace Armagetron.Android
{
    /// <summary>
    /// Android <see cref="IShellInput"/>: touch presses are taps, there are no turn keys
    /// (steering is tap-to-turn, routed by the shell), the hardware Back button maps to
    /// Back, and tapping a text field pops the native soft keyboard via MonoGame's
    /// <see cref="KeyboardInput"/>. The I/O edge over TouchPanel/GamePad/KeyboardInput —
    /// excluded from coverage; the routing it feeds is the unit-tested <see cref="AppShell"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class AndroidShellInput : IShellInput
    {
        private GamePadState _prevPad;
        private string? _editingFieldId;
        private volatile bool _resultReady;
        private volatile string? _result;
        private bool _keyboardOpen;

        public IReadOnlyList<TapPoint> Taps()
        {
            var taps = new List<TapPoint>();
            foreach (TouchLocation t in TouchPanel.GetState())
                if (t.State == TouchLocationState.Pressed)
                    taps.Add(new TapPoint((int)t.Position.X, (int)t.Position.Y));
            return taps;
        }

        public IReadOnlyList<TurnDirection> TurnKeys() => Array.Empty<TurnDirection>();

        public bool BackPressed()
        {
            GamePadState pad = GamePad.GetState(PlayerIndex.One);
            bool edge = pad.Buttons.Back == ButtonState.Pressed
                        && _prevPad.Buttons.Back == ButtonState.Released;
            _prevPad = pad;
            return edge;
        }

        public void ApplyTextEditing(AppShell shell)
        {
            // Deliver a finished soft-keyboard edit back to the focused field.
            if (_resultReady)
            {
                _resultReady = false;
                _keyboardOpen = false;
                if (_result != null) shell.SetFocusedFieldValue(_result);
            }

            string? field = shell.FocusedFieldId;
            if (field == null) { _editingFieldId = null; return; }

            // A freshly-focused field opens the keyboard once.
            if (field != _editingFieldId && !_keyboardOpen)
            {
                _editingFieldId = field;
                _keyboardOpen = true;
                ShowKeyboard(field, shell.FocusedFieldValue ?? "");
            }
        }

        private async void ShowKeyboard(string fieldId, string current)
        {
            try
            {
                _result = await KeyboardInput.Show("EDIT " + fieldId.ToUpperInvariant(), "", current);
            }
            catch
            {
                _result = null;
            }
            _resultReady = true;
        }
    }
}
