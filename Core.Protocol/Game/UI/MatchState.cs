namespace Armagetron.Game.UI
{
    /// <summary>
    /// Client-side, observable match facts for the HUD — everything we can know honestly
    /// WITHOUT the not-yet-decoded score/standings messages (PROTOCOL.md open questions).
    /// The host feeds it round-lifecycle events and per-frame snapshots; the HUD reads it.
    ///
    /// Honest sourcing: <see cref="RoundNumber"/> and <see cref="ElapsedMs"/> come from the
    /// RoundStarted event + the local clock; <see cref="CycleCount"/> from the snapshot;
    /// <see cref="LocalAlive"/> from the local cycle's Died event. Real scores remain a
    /// PLACEHOLDER until the protocol decodes them.
    /// </summary>
    public sealed class MatchState
    {
        /// <summary>How many rounds have started this session (1-based once playing).</summary>
        public int RoundNumber { get; private set; }

        /// <summary>True between RoundStarted and RoundEnded.</summary>
        public bool RoundActive { get; private set; }

        /// <summary>True while the local cycle is alive this round.</summary>
        public bool LocalAlive { get; private set; } = true;

        /// <summary>Number of cycles currently in the snapshot (all players seen).</summary>
        public int CycleCount { get; private set; }

        private long _roundStartMs;

        public void OnRoundStart(long nowMs)
        {
            RoundNumber++;
            _roundStartMs = nowMs;
            RoundActive = true;
            LocalAlive = true;
        }

        public void OnRoundEnd() => RoundActive = false;

        public void OnLocalDied() => LocalAlive = false;

        public void SetCycleCount(int count) => CycleCount = count;

        /// <summary>Milliseconds since the round started (0 when no round is active).</summary>
        public long ElapsedMs(long nowMs)
        {
            if (!RoundActive) return 0;
            long d = nowMs - _roundStartMs;
            return d < 0 ? 0 : d;
        }

        /// <summary>The elapsed time as M:SS for the HUD timer.</summary>
        public string TimeLabel(long nowMs)
        {
            long total = ElapsedMs(nowMs) / 1000;
            long m = total / 60;
            long s = total % 60;
            return $"{m}:{s:00}";
        }
    }
}
