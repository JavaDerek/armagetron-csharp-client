using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Armagetron.Game;
using Microsoft.Xna.Framework.Input;

namespace Armagetron.Game
{
    /// <summary>
    /// Desktop <see cref="ITurnInput"/>: ← / → keys turn, Esc quits. Edge-detected so each
    /// key press yields exactly one turn. This is an I/O edge over MonoGame's Keyboard —
    /// it carries no logic worth unit-testing, so it is excluded from coverage; the live
    /// game window is its proof.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class KeyboardTurnInput : ITurnInput
    {
        private KeyboardState _prev;

        public IReadOnlyList<TurnDirection> Poll()
        {
            var keys = Keyboard.GetState();
            var turns = new List<TurnDirection>(2);

            if (IsNewPress(keys, _prev, Keys.Left))  turns.Add(TurnDirection.Left);
            if (IsNewPress(keys, _prev, Keys.Right)) turns.Add(TurnDirection.Right);

            ExitRequested = keys.IsKeyDown(Keys.Escape);
            _prev = keys;
            return turns;
        }

        public bool ExitRequested { get; private set; }

        private static bool IsNewPress(KeyboardState cur, KeyboardState prev, Keys key) =>
            cur.IsKeyDown(key) && !prev.IsKeyDown(key);
    }
}
