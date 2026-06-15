using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Armagetron.Game;
using Armagetron.Net;
using Armagetron.Protocol;

namespace Armagetron.Lib
{
    /// <summary>Which way to turn the local cycle. Internal to ArmaLib — the public facade
    /// exposes <see cref="ArmaClient.TurnLeft"/>/<see cref="ArmaClient.TurnRight"/> instead.</summary>
    internal enum TurnDir { Left, Right }

    /// <summary>
    /// The protocol session that backs <see cref="ArmaClient"/>. Extends the shared
    /// <see cref="ArmagetronSessionBase"/> with: a turn queue fed by the GUI thread,
    /// continuous client-predicted dead-reckoning of our cycle into <see cref="GameWorld"/>
    /// for rendering, and forwarding of every protocol notification into a
    /// <see cref="GameEventTracker"/> so the facade can raise high-level events.
    ///
    /// This mirrors the desktop client's old PlayerSession, but adds the event-tracker wiring
    /// and lives below the ArmaLib facade. Excluded from coverage for the same reason as the
    /// base class: session machinery is proven by the live-server gate, not unit tests. The
    /// pure event logic it delegates to (<see cref="GameEventTracker"/>) IS unit-tested.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal sealed class ArmaSession : ArmagetronSessionBase
    {
        private readonly GameWorld _world;
        private readonly GameEventTracker _events;
        private readonly ConcurrentQueue<TurnDir> _pendingTurns = new ConcurrentQueue<TurnDir>();

        private long _lastMoveTick;

        public ArmaSession(IUdpLink link, string name, GameWorld world, GameEventTracker events)
            : base(link, name)
        {
            _world  = world;
            _events = events;
        }

        /// <summary>Enqueue a turn to be applied and sent on the next protocol tick.</summary>
        public void QueueTurn(TurnDir dir) => _pendingTurns.Enqueue(dir);

        protected override void OnMyCycleCreated(int cycleId)
        {
            _world.SetMyCycleId(cycleId);
            _events.MyCycleCreated(cycleId);
            _lastMoveTick = Tick();
        }

        protected override void OnRoundStart()
        {
            _world.ClearRound();
            _events.RoundStart();
            _lastMoveTick = Tick();
        }

        protected override void OnRoundEnd() => _events.RoundEnd();

        protected override void OnCyclePositionUpdate(int cycleId, Vec2 pos, Vec2 dir,
                                                      bool alive, float speed)
        {
            // Events fire for ALL cycles (including ours) so the GUI hears spawn/death.
            _events.PositionUpdate(cycleId, alive);

            // Our OWN cycle is client-predicted (rendered from dead-reckoning in
            // MaybeSendCycleCommand), so we deliberately ignore server syncs for it in the
            // world model — feeding the server direction alongside the predicted one is what
            // produced the old garbled-trail bug. The ONE exception is the death sync: pin our
            // head to the server's crash point and freeze it, or client prediction coasts the
            // cycle a few units past the wall it just hit. Remote cycles are server-driven.
            if (cycleId == _myCycleId)
            {
                if (!alive) _world.KillLocalCycle(_myCycleId, pos);
                return;
            }
            _world.UpdateRemoteCycle(cycleId, pos, dir, MonoClock.NowMs(), alive, speed);
        }

        protected override void MaybeSendCycleCommand()
        {
            if (!_posInitialized) return;

            // Dead-reckon our head every tick so the render is smooth.
            long  now    = Tick();
            float dtMove = (now - _lastMoveTick) / 1000f;
            if (dtMove > 0f && dtMove < 1f)
            {
                _pos      = new Vec2(_pos.X + _dir.X * CycleSpeed * dtMove,
                                     _pos.Y + _dir.Y * CycleSpeed * dtMove);
                _dist    += CycleSpeed * dtMove;
                _gameTime += dtMove;
            }
            _lastMoveTick = now;

            // Push the latest predicted head for rendering (never adds a trail corner).
            _world.MoveLocalCycle(_myCycleId, _pos, _dir);

            if (!_pendingTurns.TryDequeue(out var turn)) return;

            _dir = turn == TurnDir.Left
                ? new Vec2(-_dir.Y,  _dir.X)
                : new Vec2( _dir.Y, -_dir.X);
            _turns++;

            // The corner is exactly the current predicted position; fix it as a waypoint.
            _world.TurnLocalCycle(_myCycleId, _pos, _dir);

            SendDesc321(_pos, _dir, _dist, _gameTime, _turns);
        }
    }
}
