using System.Collections.Generic;
using Armagetron.Game.UI;

namespace Armagetron.Game
{
    /// <summary>A tap/click at a screen pixel.</summary>
    public readonly struct TapPoint
    {
        public readonly int X, Y;
        public TapPoint(int x, int y) { X = x; Y = y; }
    }

    /// <summary>
    /// Per-frame platform input for the screen-driven <see cref="ArmagetronGame"/> host. Each
    /// head implements it over its own devices: desktop uses mouse+keyboard+TextInput, Android
    /// uses the touch panel + a soft keyboard. The host stays platform-agnostic — it just
    /// forwards taps/turn-keys/back to the <see cref="AppShell"/> and lets the platform apply
    /// any text editing to the focused field. This is the thin I/O edge (excluded from
    /// coverage); the routing logic it feeds lives in the unit-tested AppShell.
    /// </summary>
    public interface IShellInput
    {
        /// <summary>New taps/clicks this frame.</summary>
        IReadOnlyList<TapPoint> Taps();

        /// <summary>Discrete turn keys pressed this frame (desktop ← / →; empty on touch).</summary>
        IReadOnlyList<TurnDirection> TurnKeys();

        /// <summary>True if Back/Esc was pressed this frame.</summary>
        bool BackPressed();

        /// <summary>Apply any pending text editing to the shell's focused field.</summary>
        void ApplyTextEditing(AppShell shell);
    }
}
