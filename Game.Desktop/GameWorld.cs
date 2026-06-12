using System;
using System.Collections.Generic;
using Armagetron.Protocol;

namespace Armagetron.Game
{
    /// <summary>
    /// Thread-safe snapshot of the game world: all known cycle positions and trails.
    /// Written by the protocol thread, read by the render thread.
    /// </summary>
    public sealed class GameWorld
    {
        private readonly object _lock = new object();
        private readonly Dictionary<int, MutableCycleState> _cycles = new Dictionary<int, MutableCycleState>();

        public int MyCycleId { get; private set; } = -1;

        private sealed class MutableCycleState
        {
            public Vec2 Position;
            public Vec2 Direction;
            // Waypoints: spawn position + each subsequent turn point.
            // The active trail segment runs from Trail[^1] to Position.
            public readonly List<Vec2> Trail = new List<Vec2>();
        }

        public void SetMyCycleId(int id)
        {
            lock (_lock) { MyCycleId = id; }
        }

        public void ClearRound()
        {
            lock (_lock) { _cycles.Clear(); }
        }

        /// <summary>
        /// Update a cycle's position and direction.
        /// Adds a trail waypoint whenever direction changes (indicating a turn).
        /// </summary>
        public void UpdateCycle(int cycleId, Vec2 pos, Vec2 dir)
        {
            lock (_lock)
            {
                if (!_cycles.TryGetValue(cycleId, out var c))
                {
                    c = new MutableCycleState();
                    c.Trail.Add(pos); // spawn point is the first waypoint
                    _cycles[cycleId] = c;
                }
                else if (DirectionChanged(c.Direction, dir))
                {
                    c.Trail.Add(c.Position); // record position just before the turn
                }
                c.Position  = pos;
                c.Direction = dir;
            }
        }

        /// <summary>
        /// Snapshot all cycle states for the render thread. Allocates new arrays
        /// but holds the lock only briefly.
        /// </summary>
        public CycleSnapshot[] Snapshot()
        {
            lock (_lock)
            {
                var result = new CycleSnapshot[_cycles.Count];
                int i = 0;
                foreach (var kvp in _cycles)
                {
                    result[i++] = new CycleSnapshot
                    {
                        CycleId   = kvp.Key,
                        Position  = kvp.Value.Position,
                        Direction = kvp.Value.Direction,
                        Trail     = kvp.Value.Trail.ToArray(),
                    };
                }
                return result;
            }
        }

        private static bool DirectionChanged(Vec2 prev, Vec2 next) =>
            Math.Abs(prev.X - next.X) > 0.1f || Math.Abs(prev.Y - next.Y) > 0.1f;
    }

    public sealed class CycleSnapshot
    {
        public int CycleId;
        public Vec2 Position;
        public Vec2 Direction;
        public Vec2[] Trail = Array.Empty<Vec2>(); // waypoints (spawn + turn points)
    }
}
