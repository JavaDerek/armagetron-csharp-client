using System;
using System.Collections.Generic;

namespace Armagetron.Lib
{
    /// <summary>
    /// Pure event-derivation layer for ArmaLib. A session feeds it the low-level protocol
    /// notifications it observes — our cycle was created, a position+alive sync arrived for
    /// some cycle, a round started or ended — and the tracker raises the small set of
    /// high-level events a GUI cares about:
    /// <list type="bullet">
    ///   <item><see cref="Spawned"/> — the first time a cycle is seen this round.</item>
    ///   <item><see cref="Died"/> — exactly once, on a cycle's alive→dead transition.</item>
    ///   <item><see cref="RoundStarted"/> / <see cref="RoundEnded"/>.</item>
    ///   <item><see cref="CyclesChanged"/> — on every position update, so the GUI can
    ///         re-snapshot and redraw.</item>
    /// </list>
    /// It holds no sockets, threads, or protocol primitives, so it is fully unit-testable
    /// from synthetic inputs — the testable heart of the otherwise I/O-bound facade.
    /// </summary>
    public sealed class GameEventTracker
    {
        // Cycles seen alive at least once since the last round reset — used to fire Spawned
        // exactly once per cycle per round.
        private readonly HashSet<int> _known = new HashSet<int>();
        // Cycles currently believed dead — used to fire Died exactly once per death and to
        // detect a respawn (dead → alive again).
        private readonly HashSet<int> _dead = new HashSet<int>();

        /// <summary>The locally-controlled cycle's id, or -1 before our cycle is created.</summary>
        public int MyCycleId { get; private set; } = -1;

        public event EventHandler? RoundStarted;
        public event EventHandler? RoundEnded;
        public event EventHandler? CyclesChanged;
        public event EventHandler<CycleEventArgs>? Spawned;
        public event EventHandler<CycleEventArgs>? Died;

        /// <summary>
        /// Our gCycle was created. Records its id (identity that survives round resets) and
        /// raises <see cref="Spawned"/> for it as mine.
        /// </summary>
        public void MyCycleCreated(int cycleId)
        {
            MyCycleId = cycleId;
            if (_known.Add(cycleId))
                Spawned?.Invoke(this, new CycleEventArgs(cycleId, isMine: true));
        }

        /// <summary>
        /// A position sync arrived for <paramref name="cycleId"/>. <paramref name="alive"/>
        /// is the cycle's alive flag (false on its final death sync). Raises
        /// <see cref="Spawned"/> on first sight, <see cref="Died"/> on an alive→dead edge,
        /// re-spawns on a dead→alive edge, and always raises <see cref="CyclesChanged"/>.
        /// </summary>
        public void PositionUpdate(int cycleId, bool alive)
        {
            if (alive && _dead.Remove(cycleId))
            {
                // Came back alive after a death sync (e.g. a fresh sync past a gap): treat
                // as a respawn so it is eligible to die again and the GUI repaints it.
                _known.Remove(cycleId);
            }

            if (_known.Add(cycleId))
                Spawned?.Invoke(this, new CycleEventArgs(cycleId, isMine: IsMine(cycleId)));

            if (!alive && _dead.Add(cycleId))
                Died?.Invoke(this, new CycleEventArgs(cycleId, isMine: IsMine(cycleId)));

            CyclesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// A new round has begun. Clears per-cycle spawn/death memory so the same ids spawn
        /// and die fresh, but preserves <see cref="MyCycleId"/> (our identity outlives the
        /// round). Raises <see cref="RoundStarted"/>.
        /// </summary>
        public void RoundStart()
        {
            _known.Clear();
            _dead.Clear();
            RoundStarted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>The round ended. Raises <see cref="RoundEnded"/>.</summary>
        public void RoundEnd() => RoundEnded?.Invoke(this, EventArgs.Empty);

        private bool IsMine(int cycleId) => cycleId == MyCycleId && MyCycleId >= 0;
    }
}
