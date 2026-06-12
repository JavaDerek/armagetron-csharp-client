using System;
using Armagetron.Net;
using Armagetron.Protocol;

namespace Armagetron.Bot
{
    /// <summary>
    /// Headless bot that connects to a 0.2.9.x listen server and sends random 90-degree
    /// turns on a timer. Extends <see cref="ArmagetronSessionBase"/> with bot-specific
    /// turn logic; all protocol machinery lives in the base class.
    /// </summary>
    public sealed class BotSession : ArmagetronSessionBase
    {
        private readonly Random _rng = new Random();
        private long _lastTurnTick = 0;
        private long _lastMoveTick = 0;
        private const int TurnIntervalMs = 2000;

        public BotSession(IUdpLink link, string name) : base(link, name) { }

        protected override void OnMyCycleCreated(int cycleId)
        {
            _lastMoveTick = Tick();
            _lastTurnTick = Tick();
        }

        protected override void OnRoundStart()
        {
            _lastMoveTick = Tick();
            _lastTurnTick = Tick();
        }

        protected override void MaybeSendCycleCommand()
        {
            if (!_posInitialized) return;

            long  now    = Tick();
            float dtMove = (now - _lastMoveTick) / 1000f;
            if (dtMove > 0f && dtMove < 1f)
            {
                _pos   = new Vec2(_pos.X + _dir.X * CycleSpeed * dtMove,
                                  _pos.Y + _dir.Y * CycleSpeed * dtMove);
                _dist  += CycleSpeed * dtMove;
                _gameTime += dtMove;
            }
            _lastMoveTick = now;

            if (now - _lastTurnTick < TurnIntervalMs) return;
            _lastTurnTick = now;

            bool turnLeft = _rng.Next(2) == 0;
            _dir = turnLeft
                ? new Vec2(-_dir.Y,  _dir.X)
                : new Vec2( _dir.Y, -_dir.X);
            _turns++;

            SendDesc321(_pos, _dir, _dist, _gameTime, _turns);
        }
    }
}
