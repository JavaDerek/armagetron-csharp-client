using System.Collections.Generic;

namespace Armagetron.Game
{
    /// <summary>A turn the player wants to make this frame.</summary>
    public enum TurnDirection
    {
        Left,
        Right,
    }

    /// <summary>
    /// Platform-agnostic source of turn input for <c>ArmagetronGame</c>. Each head supplies
    /// its own implementation — keyboard on desktop, touch on Android — so the shared game
    /// loop never references a platform-specific input API. Implementations live at the I/O
    /// edge (they poll MonoGame's Keyboard/TouchPanel) and are excluded from coverage; the
    /// only logic worth testing — which half of the screen a tap fell on — is factored out
    /// into the pure <see cref="TapTurnDecider"/>.
    /// </summary>
    public interface ITurnInput
    {
        /// <summary>
        /// Poll the underlying device and return any turns triggered since the last call,
        /// in the order they occurred. Returns an empty sequence when nothing happened.
        /// </summary>
        IReadOnlyList<TurnDirection> Poll();

        /// <summary>True once the player has asked to quit (e.g. Esc / back button).</summary>
        bool ExitRequested { get; }
    }

    /// <summary>
    /// Pure rule mapping a touch's x-coordinate to a turn: the left half of the screen turns
    /// left, the right half turns right (the midpoint and beyond count as right). This mirrors
    /// the desktop ← / → keys and is the entire decision behind Android touch steering.
    /// </summary>
    public static class TapTurnDecider
    {
        public static TurnDirection Decide(float tapX, float screenWidth) =>
            tapX < screenWidth / 2f ? TurnDirection.Left : TurnDirection.Right;
    }
}
