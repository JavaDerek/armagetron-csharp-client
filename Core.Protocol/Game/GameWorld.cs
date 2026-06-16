using System;
using System.Collections.Generic;
using Armagetron.Protocol;

namespace Armagetron.Game
{
    /// <summary>
    /// Thread-safe snapshot of the game world: all known cycle positions and trails.
    /// Written by the protocol thread, read by the render thread.
    ///
    /// Two motion models, deliberately separated to avoid the "garbled trail" bug:
    ///   • The <b>local</b> cycle (the player we control) is client-predicted. Its head
    ///     is moved by <see cref="MoveLocalCycle"/> every tick from dead-reckoning, and
    ///     trail corners are added <i>only</i> by <see cref="TurnLocalCycle"/> on a real
    ///     player turn — never inferred from a direction delta. This makes spurious
    ///     corners impossible even when a server position sync momentarily disagrees
    ///     with the predicted direction.
    ///   • <b>Remote</b> cycles are server-driven via <see cref="UpdateRemoteCycle"/>:
    ///     a single authoritative writer, so inferring a corner whenever the travel
    ///     direction changes is safe.
    /// </summary>
    public sealed class GameWorld
    {
        private readonly object _lock = new object();
        private readonly Dictionary<int, MutableCycleState> _cycles = new Dictionary<int, MutableCycleState>();

        // Round transitions are a DEFERRED clear, not an immediate one. ClearRound() only
        // arms this flag; the cycles are dropped lazily on the next write (the new round's
        // first sync). Between the two, Snapshot() keeps returning the last round's frame
        // (sample-and-hold) so a continuous renderer never blanks at a round boundary —
        // yet the prior round's trails are still flushed before the new round draws, so no
        // stale trail bleeds across (the bug ClearRound exists to prevent).
        private bool _clearPending;

        public int MyCycleId { get; private set; } = -1;

        // Fallback remote speed (units/sec) used only until a sync reports the cycle's
        // real speed (desc=24 27w word [11-12]). Mirrors ArmagetronSessionBase.CycleSpeed.
        private const float RemoteCycleSpeed   = 30f;
        // Predict at most this far past the last sync. This is now only a backstop for a
        // LIVING cycle whose syncs went briefly silent (packet loss / sparse straightaway
        // syncs) — it bounds the coast until the next sync. Death is handled precisely:
        // the server sends a final sync with alive=0 (word [13]), on which we freeze the
        // head outright (see PredictedHead), so a dead cycle never extrapolates at all.
        private const float MaxExtrapolationSec = 0.15f;

        private sealed class MutableCycleState
        {
            public Vec2 Position;     // local: dead-reckoned head; remote: last synced position
            public Vec2 Direction;
            public bool HasDirection; // false until the first direction is recorded
            public bool IsRemote;     // remote cycles get render-time dead-reckoning in Snapshot
            public long LastSyncMs;   // timestamp of the last remote sync, for extrapolation
            public float Speed = RemoteCycleSpeed; // server-reported speed, for dead-reckoning
            public bool Alive = true; // false after a death sync (alive=0): freezes the head
            // Waypoints: spawn position + each subsequent turn point.
            // The active trail segment runs from Trail[^1] to Position.
            public readonly List<Vec2> Trail = new List<Vec2>();
        }

        public void SetMyCycleId(int id)
        {
            lock (_lock) { MyCycleId = id; }
        }

        /// <summary>
        /// Arm a deferred end-of-round clear. The world is NOT emptied now — the current
        /// frame is held until the next round's first cycle write (<see cref="ApplyPendingClear"/>),
        /// so a continuous renderer keeps drawing the last frame instead of blanking in the
        /// gap before the new round's syncs arrive.
        /// </summary>
        public void ClearRound()
        {
            lock (_lock) { _clearPending = true; }
        }

        // Flush a pending end-of-round clear. Called at the start of every cycle write (all
        // under _lock): the first write of the new round drops the prior round's cycles so
        // their trails don't bleed in, then the write proceeds to seed the fresh round.
        private void ApplyPendingClear()
        {
            if (_clearPending)
            {
                _cycles.Clear();
                _clearPending = false;
            }
        }

        /// <summary>
        /// Move the local (client-predicted) cycle's head. Updates position and
        /// direction but never adds a trail corner — corners come only from
        /// <see cref="TurnLocalCycle"/>. The first call seeds the spawn waypoint.
        /// </summary>
        public void MoveLocalCycle(int cycleId, Vec2 pos, Vec2 dir)
        {
            lock (_lock)
            {
                var c = GetOrCreate(cycleId, pos);
                // A dead local cycle is frozen at its crash point (see KillLocalCycle): the
                // upstream predictor keeps dead-reckoning for a few ticks after the death sync,
                // and applying those would glide the head past the wall it just hit.
                if (!c.Alive) return;
                c.Position     = pos;
                c.Direction    = dir;
                c.HasDirection = true;
                c.IsRemote     = false;
            }
        }

        /// <summary>
        /// Mark the local (client-predicted) cycle dead and pin its head to the server's
        /// authoritative crash point <paramref name="deathPos"/>. After this, <see
        /// cref="MoveLocalCycle"/> is a no-op until the next <see cref="ClearRound"/>, so the
        /// head stops exactly at impact instead of coasting forward. This is the local-cycle
        /// counterpart to the remote death freeze in <see cref="UpdateRemoteCycle"/>.
        /// </summary>
        public void KillLocalCycle(int cycleId, Vec2 deathPos)
        {
            lock (_lock)
            {
                var c = GetOrCreate(cycleId, deathPos);
                c.Position = deathPos;
                c.IsRemote = false;
                c.Alive    = false;
            }
        }

        /// <summary>
        /// Record a real player turn on the local cycle: <paramref name="cornerPos"/>
        /// becomes a fixed waypoint and the head continues from there along
        /// <paramref name="newDir"/>.
        /// </summary>
        public void TurnLocalCycle(int cycleId, Vec2 cornerPos, Vec2 newDir)
        {
            lock (_lock)
            {
                var c = GetOrCreate(cycleId, cornerPos);
                c.Trail.Add(cornerPos);
                c.Position     = cornerPos;
                c.Direction    = newDir;
                c.HasDirection = true;
            }
        }

        /// <summary>
        /// Update a remote (server-driven) cycle. Adds a trail waypoint whenever the
        /// travel direction changes, marking the turn. Safe because the server is the
        /// sole writer for remote cycles.
        /// </summary>
        public void UpdateRemoteCycle(int cycleId, Vec2 pos, Vec2 dir) =>
            UpdateRemoteCycle(cycleId, pos, dir, nowMs: 0);

        public void UpdateRemoteCycle(int cycleId, Vec2 pos, Vec2 dir, long nowMs) =>
            UpdateRemoteCycle(cycleId, pos, dir, nowMs, alive: true, speed: RemoteCycleSpeed);

        /// <summary>
        /// Update a remote cycle with its server-reported <paramref name="alive"/> flag and
        /// <paramref name="speed"/> (desc=24 27w words [13] and [11-12]). When
        /// <paramref name="alive"/> is false this is the cycle's death sync: its head is
        /// pinned at <paramref name="pos"/> and never dead-reckoned again, so it stops at the
        /// wall it hit instead of ghosting through. <paramref name="speed"/> drives the
        /// dead-reckoning of a living cycle.
        /// </summary>
        public void UpdateRemoteCycle(int cycleId, Vec2 pos, Vec2 dir, long nowMs, bool alive, float speed)
        {
            lock (_lock)
            {
                ApplyPendingClear();
                if (!_cycles.TryGetValue(cycleId, out var c))
                {
                    c = new MutableCycleState();
                    c.Trail.Add(pos); // spawn point is the first waypoint
                    _cycles[cycleId] = c;
                }
                else if (alive && c.HasDirection && DirectionChanged(c.Direction, dir))
                {
                    // Server syncs are sparse: between c.Position and pos the cycle may have
                    // both moved and turned. Armagetron motion is axis-aligned, so the turn is
                    // an L-joint — reconstruct it rather than drawing a straight diagonal from
                    // the last sample to this one. Skipped on the death sync: a crash is not a
                    // turn, and the final position should anchor the head as-is.
                    c.Trail.Add(ReconstructCorner(c.Position, pos, dir));
                }
                c.Position     = pos;
                c.Direction    = dir;
                c.HasDirection = true;
                c.IsRemote     = true;
                c.LastSyncMs   = nowMs;
                c.Speed        = speed;
                c.Alive        = alive;
            }
        }

        /// <summary>
        /// Snapshot all cycle states for the render thread. Allocates new arrays
        /// but holds the lock only briefly.
        /// </summary>
        public CycleSnapshot[] Snapshot() => Snapshot(nowMs: 0);

        /// <summary>
        /// Snapshot all cycle states for the render thread. Remote cycles are dead-reckoned
        /// to <paramref name="nowMs"/> — their head is predicted forward along their heading
        /// since the last server sync — so they move smoothly instead of jumping between
        /// sparse syncs. The local cycle is already current (dead-reckoned upstream) and is
        /// not extrapolated again.
        /// </summary>
        public CycleSnapshot[] Snapshot(long nowMs)
        {
            lock (_lock)
            {
                var result = new CycleSnapshot[_cycles.Count];
                int i = 0;
                foreach (var kvp in _cycles)
                {
                    var c = kvp.Value;
                    result[i++] = new CycleSnapshot
                    {
                        CycleId   = kvp.Key,
                        Position  = PredictedHead(c, nowMs),
                        Direction = c.Direction,
                        Trail     = c.Trail.ToArray(),
                    };
                }
                return result;
            }
        }

        private static Vec2 PredictedHead(MutableCycleState c, long nowMs)
        {
            // A dead cycle is frozen at its death position — never extrapolate it, or it
            // would coast through the wall it just crashed into.
            if (!c.IsRemote || !c.HasDirection || !c.Alive) return c.Position;
            float dt = (nowMs - c.LastSyncMs) / 1000f;
            if (dt <= 0f) return c.Position;
            if (dt > MaxExtrapolationSec) dt = MaxExtrapolationSec;
            float d = c.Speed * dt;
            return new Vec2(c.Position.X + c.Direction.X * d, c.Position.Y + c.Direction.Y * d);
        }

        private MutableCycleState GetOrCreate(int cycleId, Vec2 seed)
        {
            ApplyPendingClear();
            if (!_cycles.TryGetValue(cycleId, out var c))
            {
                c = new MutableCycleState();
                c.Trail.Add(seed); // spawn point is the first waypoint
                _cycles[cycleId] = c;
            }
            return c;
        }

        private static bool DirectionChanged(Vec2 prev, Vec2 next) =>
            Math.Abs(prev.X - next.X) > 0.1f || Math.Abs(prev.Y - next.Y) > 0.1f;

        /// <summary>
        /// The axis-aligned corner where a cycle last seen at <paramref name="from"/> turned to
        /// head along <paramref name="newDir"/> and was next seen at <paramref name="to"/>.
        /// If it now moves vertically it must have moved horizontally before the turn, so the
        /// corner is (to.X, from.Y); the horizontal case mirrors to (from.X, to.Y).
        /// </summary>
        private static Vec2 ReconstructCorner(Vec2 from, Vec2 to, Vec2 newDir)
        {
            bool newDirVertical = Math.Abs(newDir.Y) > Math.Abs(newDir.X);
            return newDirVertical ? new Vec2(to.X, from.Y) : new Vec2(from.X, to.Y);
        }
    }

    public sealed class CycleSnapshot
    {
        public int CycleId;
        public Vec2 Position;
        public Vec2 Direction;
        public Vec2[] Trail = Array.Empty<Vec2>(); // waypoints (spawn + turn points)
    }
}
