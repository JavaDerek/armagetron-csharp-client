using Armagetron.Game;
using Armagetron.Protocol;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the platform-agnostic game-world state model.
    ///
    /// The headline regression is the "1-in-5 garbled trail": our own (local)
    /// cycle used to be written by two competing sources — client-side
    /// dead-reckoning and server position syncs — whose directions disagreed
    /// transiently around turns. A "corner on every direction change" heuristic
    /// then sprayed spurious zig-zag waypoints. The fix splits local
    /// (client-predicted: corners only on explicit turns) from remote
    /// (server-driven: corners inferred from direction change).
    /// </summary>
    public class GameWorldTests
    {
        private static GameWorld NewWorldOwning(int myId)
        {
            var w = new GameWorld();
            w.SetMyCycleId(myId);
            return w;
        }

        private static CycleSnapshot SnapOf(GameWorld w, int cycleId)
        {
            foreach (var c in w.Snapshot())
                if (c.CycleId == cycleId)
                    return c;
            throw new Xunit.Sdk.XunitException($"cycle {cycleId} not in snapshot");
        }

        // ── The regression: local cycle never grows spurious corners ──────────

        [Fact]
        public void LocalCycle_MovingWithChangingDirection_NeverAddsCorners()
        {
            // Reproduces the dual-writer flip-flop: feed the local cycle a sequence
            // of head moves whose directions alternate (as the old code did when the
            // server's direction briefly disagreed with the predicted one). The local
            // path must remain a single straight segment — no inferred corners.
            var w = NewWorldOwning(5);

            w.MoveLocalCycle(5, new Vec2(10, 10), new Vec2(1, 0));
            w.MoveLocalCycle(5, new Vec2(12, 10), new Vec2(0, 1)); // server dir disagrees
            w.MoveLocalCycle(5, new Vec2(14, 10), new Vec2(1, 0)); // predicted dir again
            w.MoveLocalCycle(5, new Vec2(16, 10), new Vec2(0, 1));

            var snap = SnapOf(w, 5);
            // Only the spawn waypoint — the head is drawn from it to Position.
            Assert.Single(snap.Trail);
            Assert.Equal(new Vec2(10, 10), snap.Trail[0]);
            Assert.Equal(new Vec2(16, 10), snap.Position);
        }

        [Fact]
        public void LocalCycle_Turn_AddsExactlyOneCornerAtTurnPoint()
        {
            var w = NewWorldOwning(5);

            w.MoveLocalCycle(5, new Vec2(10, 10), new Vec2(1, 0)); // spawn + drive right
            w.MoveLocalCycle(5, new Vec2(20, 10), new Vec2(1, 0));
            w.TurnLocalCycle(5, new Vec2(20, 10), new Vec2(0, 1));  // turn up at (20,10)
            w.MoveLocalCycle(5, new Vec2(20, 18), new Vec2(0, 1));  // drive up

            var snap = SnapOf(w, 5);
            Assert.Equal(2, snap.Trail.Length);          // spawn + the one corner
            Assert.Equal(new Vec2(10, 10), snap.Trail[0]);
            Assert.Equal(new Vec2(20, 10), snap.Trail[1]);
            Assert.Equal(new Vec2(20, 18), snap.Position);
            Assert.Equal(new Vec2(0, 1), snap.Direction);
        }

        [Fact]
        public void TurnLocalCycle_OnUnseenCycle_SeedsTrailThenAddsCorner()
        {
            // Exercises the create-on-first-call branch of TurnLocalCycle.
            var w = NewWorldOwning(7);
            w.TurnLocalCycle(7, new Vec2(5, 5), new Vec2(0, 1));

            var snap = SnapOf(w, 7);
            // Seed waypoint + the corner happen to coincide; both are recorded.
            Assert.Equal(new Vec2(5, 5), snap.Position);
            Assert.Equal(new Vec2(0, 1), snap.Direction);
            Assert.NotEmpty(snap.Trail);
        }

        // ── Remote cycles: server-driven corner inference still works ─────────

        [Fact]
        public void RemoteCycle_StraightLine_AddsNoCorners()
        {
            var w = NewWorldOwning(5);

            w.UpdateRemoteCycle(9, new Vec2(0, 0), new Vec2(1, 0));
            w.UpdateRemoteCycle(9, new Vec2(10, 0), new Vec2(1, 0));
            w.UpdateRemoteCycle(9, new Vec2(20, 0), new Vec2(1, 0));

            var snap = SnapOf(w, 9);
            Assert.Single(snap.Trail);
            Assert.Equal(new Vec2(0, 0), snap.Trail[0]);
            Assert.Equal(new Vec2(20, 0), snap.Position);
        }

        [Fact]
        public void RemoteCycle_DirectionChange_AddsCornerAtPreTurnPosition()
        {
            var w = NewWorldOwning(5);

            w.UpdateRemoteCycle(9, new Vec2(0, 0), new Vec2(1, 0));
            w.UpdateRemoteCycle(9, new Vec2(30, 0), new Vec2(1, 0)); // still right
            w.UpdateRemoteCycle(9, new Vec2(30, 5), new Vec2(0, 1)); // turned up (no x movement here)

            var snap = SnapOf(w, 9);
            Assert.Equal(2, snap.Trail.Length);
            Assert.Equal(new Vec2(0, 0), snap.Trail[0]);
            Assert.Equal(new Vec2(30, 0), snap.Trail[1]); // corner
            Assert.Equal(new Vec2(30, 5), snap.Position);
        }

        [Fact]
        public void RemoteCycle_SparseSyncAcrossTurn_ReconstructsLCorner_NotDiagonal()
        {
            // The real bug: server syncs are sparse, so between two samples a remote cycle
            // both moved AND turned. Connecting the two samples directly draws an impossible
            // diagonal. The corner must be reconstructed as an axis-aligned L-joint.
            var w = NewWorldOwning(5);

            w.UpdateRemoteCycle(9, new Vec2(0, 0), new Vec2(1, 0));   // heading right
            w.UpdateRemoteCycle(9, new Vec2(30, 0), new Vec2(1, 0));  // still right at (30,0)
            // Next sample: now at (50,20) heading UP — it ran right to x=50, then turned up to y=20.
            w.UpdateRemoteCycle(9, new Vec2(50, 20), new Vec2(0, 1));

            var snap = SnapOf(w, 9);
            // Corner must be (50,0): the L-joint. NOT (30,0) (which leaves a (30,0)->(50,20) diagonal).
            Assert.Equal(new Vec2(50, 0), snap.Trail[^1]);
            Assert.Equal(new Vec2(50, 20), snap.Position);

            // Every drawn segment (waypoints + active) must be axis-aligned (no diagonals).
            var pts = new System.Collections.Generic.List<Vec2>(snap.Trail) { snap.Position };
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                bool axisAligned = pts[i].X == pts[i + 1].X || pts[i].Y == pts[i + 1].Y;
                Assert.True(axisAligned, $"segment {pts[i]}->{pts[i + 1]} is diagonal");
            }
        }

        [Fact]
        public void RemoteCycle_TurnToHorizontal_ReconstructsCornerOnNewAxis()
        {
            // Mirror case: turning to a HORIZONTAL heading reconstructs corner = (from.X, to.Y).
            var w = NewWorldOwning(5);

            w.UpdateRemoteCycle(9, new Vec2(0, 0), new Vec2(0, 1));   // heading up
            w.UpdateRemoteCycle(9, new Vec2(0, 30), new Vec2(0, 1));  // still up at (0,30)
            w.UpdateRemoteCycle(9, new Vec2(20, 50), new Vec2(1, 0)); // now right at (20,50)

            var snap = SnapOf(w, 9);
            Assert.Equal(new Vec2(0, 50), snap.Trail[^1]); // L-joint: (from.X, to.Y)
        }

        // ── Remote dead-reckoning (smooth motion between sparse syncs) ────────

        [Fact]
        public void RemoteCycle_DeadReckonsHeadBetweenSyncs()
        {
            var w = NewWorldOwning(5);
            w.UpdateRemoteCycle(9, new Vec2(0, 0), new Vec2(1, 0), nowMs: 1000);

            // 100ms later, no new sync: head should be predicted forward along its heading.
            var snap = SnapOf(w.Snapshot(nowMs: 1100), 9);
            Assert.Equal(3f, snap.Position.X, 3); // 30 u/s * 0.1 s
            Assert.Equal(0f, snap.Position.Y, 3);
        }

        [Fact]
        public void RemoteCycle_ExtrapolationIsCapped_ForStaleCycles()
        {
            var w = NewWorldOwning(5);
            w.UpdateRemoteCycle(9, new Vec2(0, 0), new Vec2(1, 0), nowMs: 1000);

            // 5s with no sync (cycle died/left): prediction must cap, not fly off forever.
            var snap = SnapOf(w.Snapshot(nowMs: 6000), 9);
            Assert.Equal(4.5f, snap.Position.X, 3); // capped to 0.15s * 30 u/s
        }

        [Fact]
        public void RemoteCycle_DeadReckonedHead_StaysAxisAligned()
        {
            var w = NewWorldOwning(5);
            w.UpdateRemoteCycle(9, new Vec2(0, 0), new Vec2(1, 0), nowMs: 1000);
            w.UpdateRemoteCycle(9, new Vec2(30, 0), new Vec2(0, 1), nowMs: 1100); // turned up at (30,0)

            var snap = SnapOf(w.Snapshot(nowMs: 1200), 9); // 100ms after the turn sync
            // Head predicted up from (30,0): (30, 3). Active segment from corner (30,0) is vertical.
            Assert.Equal(30f, snap.Position.X, 3);
            Assert.Equal(3f, snap.Position.Y, 3);
        }

        [Fact]
        public void LocalCycle_IsNotDeadReckonedBySnapshot()
        {
            // The local cycle is already dead-reckoned by the protocol thread (MoveLocalCycle);
            // Snapshot must NOT extrapolate it again, or it would double-count.
            var w = NewWorldOwning(5);
            w.MoveLocalCycle(5, new Vec2(10, 10), new Vec2(1, 0));

            var snap = SnapOf(w.Snapshot(nowMs: 99_999), 5);
            Assert.Equal(new Vec2(10, 10), snap.Position);
        }

        private static CycleSnapshot SnapOf(CycleSnapshot[] snaps, int cycleId)
        {
            foreach (var c in snaps)
                if (c.CycleId == cycleId)
                    return c;
            throw new Xunit.Sdk.XunitException($"cycle {cycleId} not in snapshot");
        }

        // ── World bookkeeping ─────────────────────────────────────────────────

        [Fact]
        public void SetMyCycleId_IsReflectedInProperty()
        {
            var w = new GameWorld();
            Assert.Equal(-1, w.MyCycleId);
            w.SetMyCycleId(42);
            Assert.Equal(42, w.MyCycleId);
        }

        [Fact]
        public void ClearRound_RemovesAllCycles()
        {
            var w = NewWorldOwning(5);
            w.MoveLocalCycle(5, new Vec2(1, 1), new Vec2(1, 0));
            w.UpdateRemoteCycle(9, new Vec2(2, 2), new Vec2(0, 1));
            Assert.Equal(2, w.Snapshot().Length);

            w.ClearRound();
            Assert.Empty(w.Snapshot());
        }

        [Fact]
        public void Snapshot_CopiesTrail_SoLaterMutationDoesNotLeak()
        {
            var w = NewWorldOwning(5);
            w.MoveLocalCycle(5, new Vec2(0, 0), new Vec2(1, 0));
            var before = SnapOf(w, 5);
            int trailLenBefore = before.Trail.Length;

            // A later turn must not retroactively grow the already-taken snapshot.
            w.TurnLocalCycle(5, new Vec2(0, 0), new Vec2(0, 1));
            Assert.Equal(trailLenBefore, before.Trail.Length);
        }
    }
}
