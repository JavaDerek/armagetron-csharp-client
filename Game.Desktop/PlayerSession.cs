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
            // Server-authoritative position sync for any cycle (including ours).
            // For our own cycle we also update via dead-reckoning; the server sync
            // acts as a correction if they differ.
            _world.UpdateCycle(cycleId, pos, dir);
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

            // Push the latest dead-reckoned position to the world for rendering.
            _world.UpdateCycle(_myCycleId, _pos, _dir);

            // Apply and send a queued player turn if one is waiting.
            if (!_pendingTurns.TryDequeue(out var turn)) return;

            _dir = turn == TurnDirection.Left
                ? new Vec2(-_dir.Y,  _dir.X)
                : new Vec2( _dir.Y, -_dir.X);
            _turns++;

            SendDesc321(_pos, _dir, _dist, _gameTime, _turns);
        }
    }
}
