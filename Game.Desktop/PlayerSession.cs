using System.Collections.Concurrent;
using Armagetron.Net;
using Armagetron.Protocol;

namespace Armagetron.Game
{
    public enum TurnDirection { Left, Right }

    /// <summary>
    /// Player-controlled session. Extends the protocol base class with a turn queue
    /// fed by keyboard input from the MonoGame thread, and continuously pushes
    /// dead-reckoned position updates into <see cref="GameWorld"/> for rendering.
    /// </summary>
    public sealed class PlayerSession : ArmagetronSessionBase
    {
        private readonly GameWorld _world;
        private readonly ConcurrentQueue<TurnDirection> _pendingTurns = new ConcurrentQueue<TurnDirection>();

        private long _lastMoveTick = 0;

        public PlayerSession(IUdpLink link, string name, GameWorld world)
            : base(link, name)
        {
            _world = world;
        }

        /// <summary>Enqueue a turn to be sent on the next protocol tick.</summary>
        public void QueueTurn(TurnDirection dir) => _pendingTurns.Enqueue(dir);

        protected override void OnMyCycleCreated(int cycleId)
        {
            _world.SetMyCycleId(cycleId);
            _lastMoveTick = Tick();
        }

        protected override void OnRoundStart()
        {
            _world.ClearRound();
            _lastMoveTick = Tick();
        }

        protected override void OnCyclePositionUpdate(int cycleId, Vec2 pos, Vec2 dir)
        {
            // Remote cycles are server-driven. Our OWN cycle is client-predicted
            // (rendered purely from dead-reckoning in MaybeSendCycleCommand), so we
            // deliberately ignore server syncs for it here: feeding the server's
            // direction into the trail alongside the predicted one is exactly what
            // produced the intermittent "garbled trail" — two writers disagreeing
            // around turns, each disagreement spawning a spurious waypoint.
            if (cycleId == _myCycleId) return;
            _world.UpdateRemoteCycle(cycleId, pos, dir, System.Environment.TickCount64);
        }

        protected override void MaybeSendCycleCommand()
        {
            if (!_posInitialized) return;

            // Dead-reckon our position on every tick so the render is smooth.
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

            // Push the latest dead-reckoned head position to the world for rendering.
            // This never adds a trail corner — corners come only from the turn below.
            _world.MoveLocalCycle(_myCycleId, _pos, _dir);

            // Apply and send a queued player turn if one is waiting.
            if (!_pendingTurns.TryDequeue(out var turn)) return;

            _dir = turn == TurnDirection.Left
                ? new Vec2(-_dir.Y,  _dir.X)
                : new Vec2( _dir.Y, -_dir.X);
            _turns++;

            // The corner is exactly the current dead-reckoned position; fix it as a
            // waypoint so the trail bends cleanly there.
            _world.TurnLocalCycle(_myCycleId, _pos, _dir);

            SendDesc321(_pos, _dir, _dist, _gameTime, _turns);
        }
    }
}
