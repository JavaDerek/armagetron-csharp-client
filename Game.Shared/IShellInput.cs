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
    /// Per-frame camera controls for the 3D views (desktop only). <see cref="CycleMode"/> toggles
    /// top-down → third-person → first-person; <see cref="ResetView"/> recentres the chase orbit;
    /// <see cref="OrbitYaw"/>/<see cref="OrbitPitch"/> are mouse-drag deltas (radians) and
    /// <see cref="Zoom"/> is a scroll delta (world units, negative = closer). Touch heads leave
    /// this at its zero default and stay top-down.
    /// </summary>
    public readonly struct CameraInputState
    {
        public readonly bool CycleMode;
        public readonly bool ResetView;
        public readonly float OrbitYaw;
        public readonly float OrbitPitch;
        public readonly float Zoom;

        public CameraInputState(bool cycleMode, bool resetView, float orbitYaw, float orbitPitch, float zoom)
        {
            CycleMode = cycleMode; ResetView = resetView;
            OrbitYaw = orbitYaw; OrbitPitch = orbitPitch; Zoom = zoom;
        }
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

        /// <summary>Camera controls for the 3D views this frame. Defaults to no input so touch
        /// heads (Android) need not implement it and simply stay in top-down mode.</summary>
        CameraInputState CameraInput() => default;
    }
}
