using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Armagetron.Game;
using Microsoft.Xna.Framework.Input.Touch;

namespace Armagetron.Android
{
    /// <summary>
    /// Android <see cref="ITurnInput"/>: a fresh touch on the LEFT half of the screen turns
    /// left, on the RIGHT half turns right — the touch analogue of the desktop ← / → keys.
    /// Only newly-pressed touches count, so holding a finger down yields a single turn. This
    /// is an I/O edge over MonoGame's TouchPanel and is excluded from coverage; its sole
    /// piece of logic (which half was tapped) lives in the unit-tested
    /// <see cref="TapTurnDecider"/>. The hardware Back button is left to the Activity, so
    /// there is no in-game exit gesture.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class TouchTurnInput : ITurnInput
    {
        public IReadOnlyList<TurnDirection> Poll()
        {
            var turns = new List<TurnDirection>();
            float width = TouchPanel.DisplayWidth;

            foreach (TouchLocation t in TouchPanel.GetState())
            {
                if (t.State == TouchLocationState.Pressed)
                    turns.Add(TapTurnDecider.Decide(t.Position.X, width));
            }

            return turns;
        }

        // The hardware Back button (handled by the OS / Activity) is the way out on Android.
        public bool ExitRequested => false;
    }
}
