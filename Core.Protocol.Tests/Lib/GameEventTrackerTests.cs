using System.Collections.Generic;
using Armagetron.Lib;

namespace Armagetron.Protocol.Tests.Lib
{
    /// <summary>
    /// Tests for the pure event-derivation layer of ArmaLib. The tracker converts the
    /// low-level protocol notifications a session emits (cycle created, position+alive
    /// sync, round start/end) into the small set of high-level events a GUI subscribes to
    /// — <c>Spawned</c>, <c>Died</c>, <c>RoundStarted</c>, <c>RoundEnded</c>,
    /// <c>CyclesChanged</c> — with no sockets in sight, so it is fully unit-testable.
    /// </summary>
    public class GameEventTrackerTests
    {
        // Collects the events a tracker raises, in order, for assertions.
        private sealed class Recorder
        {
            public readonly List<string> Log = new List<string>();
            public Recorder(GameEventTracker t)
            {
                t.RoundStarted += (_, __) => Log.Add("RoundStarted");
                t.RoundEnded   += (_, __) => Log.Add("RoundEnded");
                t.CyclesChanged += (_, __) => Log.Add("CyclesChanged");
                t.Spawned += (_, e) => Log.Add($"Spawned:{e.CycleId}:{(e.IsMine ? "mine" : "other")}");
                t.Died    += (_, e) => Log.Add($"Died:{e.CycleId}:{(e.IsMine ? "mine" : "other")}");
            }
        }

        // ── Spawned ───────────────────────────────────────────────────────────

        [Fact]
        public void MyCycleCreated_RaisesSpawnedForMine()
        {
            var t = new GameEventTracker();
            var rec = new Recorder(t);

            t.MyCycleCreated(7);

            Assert.Contains("Spawned:7:mine", rec.Log);
            Assert.Equal(7, t.MyCycleId);
        }

        [Fact]
        public void FirstPositionUpdate_ForUnknownCycle_RaisesSpawnedAsOther()
        {
            var t = new GameEventTracker();
            var rec = new Recorder(t);

            t.PositionUpdate(42, alive: true);

            Assert.Contains("Spawned:42:other", rec.Log);
        }

        [Fact]
        public void PositionUpdate_ForMyCycle_AfterCreate_DoesNotSpawnTwice()
        {
            var t = new GameEventTracker();
            t.MyCycleCreated(7);
            var rec = new Recorder(t); // start recording AFTER the create

            t.PositionUpdate(7, alive: true);

            Assert.DoesNotContain("Spawned:7:mine", rec.Log);
        }

        [Fact]
        public void RepeatedPositionUpdates_SpawnExactlyOncePerCycle()
        {
            var t = new GameEventTracker();
            var rec = new Recorder(t);

            t.PositionUpdate(42, alive: true);
            t.PositionUpdate(42, alive: true);
            t.PositionUpdate(42, alive: true);

            Assert.Single(rec.Log.FindAll(s => s == "Spawned:42:other"));
        }

        // ── CyclesChanged ───────────────────────────────────────────────────────

        [Fact]
        public void EveryPositionUpdate_RaisesCyclesChanged()
        {
            var t = new GameEventTracker();
            var rec = new Recorder(t);

            t.PositionUpdate(1, alive: true);
            t.PositionUpdate(1, alive: true);

            Assert.Equal(2, rec.Log.FindAll(s => s == "CyclesChanged").Count);
        }

        // ── Died ──────────────────────────────────────────────────────────────

        [Fact]
        public void AliveToDead_RaisesDiedOnce()
        {
            var t = new GameEventTracker();
            t.PositionUpdate(42, alive: true);
            var rec = new Recorder(t);

            t.PositionUpdate(42, alive: false);

            Assert.Single(rec.Log.FindAll(s => s == "Died:42:other"));
        }

        [Fact]
        public void RepeatedDeathSyncs_RaiseDiedOnlyOnce()
        {
            var t = new GameEventTracker();
            t.PositionUpdate(42, alive: true);
            var rec = new Recorder(t);

            t.PositionUpdate(42, alive: false);
            t.PositionUpdate(42, alive: false); // server may re-send the final sync

            Assert.Single(rec.Log.FindAll(s => s == "Died:42:other"));
        }

        [Fact]
        public void DeathOfMyCycle_RaisesDiedAsMine()
        {
            var t = new GameEventTracker();
            t.MyCycleCreated(7);
            t.PositionUpdate(7, alive: true);
            var rec = new Recorder(t);

            t.PositionUpdate(7, alive: false);

            Assert.Contains("Died:7:mine", rec.Log);
        }

        [Fact]
        public void DeadCycle_SeenAliveAgain_RespawnsAndCanDieAgain()
        {
            // A reused cycle id that comes back alive (e.g. a fresh sync after a gap)
            // should re-spawn and be eligible to die again.
            var t = new GameEventTracker();
            t.PositionUpdate(42, alive: true);
            t.PositionUpdate(42, alive: false);
            var rec = new Recorder(t);

            t.PositionUpdate(42, alive: true);  // back alive → respawn
            t.PositionUpdate(42, alive: false); // and dies again

            Assert.Contains("Spawned:42:other", rec.Log);
            Assert.Single(rec.Log.FindAll(s => s == "Died:42:other"));
        }

        // ── Rounds ──────────────────────────────────────────────────────────────

        [Fact]
        public void RoundStarted_IsRaised()
        {
            var t = new GameEventTracker();
            var rec = new Recorder(t);

            t.RoundStart();

            Assert.Contains("RoundStarted", rec.Log);
        }

        [Fact]
        public void RoundEnded_IsRaised()
        {
            var t = new GameEventTracker();
            var rec = new Recorder(t);

            t.RoundEnd();

            Assert.Contains("RoundEnded", rec.Log);
        }

        [Fact]
        public void RoundStart_ResetsCycleState_SoSameIdSpawnsAndDiesAgain()
        {
            var t = new GameEventTracker();
            t.PositionUpdate(42, alive: true);
            t.PositionUpdate(42, alive: false);

            t.RoundStart(); // new round wipes per-cycle memory
            var rec = new Recorder(t);

            t.PositionUpdate(42, alive: true);  // spawns fresh
            t.PositionUpdate(42, alive: false); // dies fresh

            Assert.Contains("Spawned:42:other", rec.Log);
            Assert.Single(rec.Log.FindAll(s => s == "Died:42:other"));
        }

        [Fact]
        public void RoundStart_PreservesMyCycleIdForMineClassification()
        {
            // The local cycle id is identity that outlives a round reset; a post-round
            // spawn of that id must still be classified as mine.
            var t = new GameEventTracker();
            t.MyCycleCreated(7);

            t.RoundStart();
            var rec = new Recorder(t);
            t.PositionUpdate(7, alive: true);

            Assert.Contains("Spawned:7:mine", rec.Log);
        }
    }
}
