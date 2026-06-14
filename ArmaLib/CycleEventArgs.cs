using System;

namespace Armagetron.Lib
{
    /// <summary>
    /// Payload for the per-cycle <see cref="ArmaClient.Spawned"/> and
    /// <see cref="ArmaClient.Died"/> events. Carries only the cycle identity and whether
    /// it is the locally-controlled cycle — no protocol detail leaks through.
    /// </summary>
    public sealed class CycleEventArgs : EventArgs
    {
        /// <summary>The game-level id of the cycle this event concerns.</summary>
        public int CycleId { get; }

        /// <summary>True when this is the locally-controlled ("our") cycle.</summary>
        public bool IsMine { get; }

        public CycleEventArgs(int cycleId, bool isMine)
        {
            CycleId = cycleId;
            IsMine  = isMine;
        }
    }
}
