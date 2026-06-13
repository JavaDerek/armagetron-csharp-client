using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Armagetron.Protocol;
using Armagetron.Protocol.Game;

namespace Armagetron.Net
{
    /// <summary>
    /// Shared protocol machinery for Armagetron 0.2.9.x sessions.
    /// Handles connection, login, acking, netobject ID reservation, and spawn detection.
    /// Subclasses supply cycle commands via <see cref="MaybeSendCycleCommand"/> and
    /// receive game-state notifications via the virtual On* hooks.
    /// Excluded from coverage: session machinery is verified by the live-server gate,
    /// not by unit tests (same reasoning as UdpLink).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public abstract class ArmagetronSessionBase : IDisposable
    {
        // ── Descriptor constants ──────────────────────────────────────────────
        protected const int DescPollRequest   = 52;
        protected const int DescPollReply     = 50;
        protected const int DescInfoRequest   = 53;
        protected const int DescInfoReply     = 51;
        protected const int DescVersion       =  7;
        protected const int DescLogin         = 11;
        protected const int DescLoginAccepted =  5;
        protected const int DescAck           =  1;
        protected const int DescKeepalive     = 27;
        protected const int DescNetObjectSync = 60;
        protected const int DescGameSync      = 310;
        protected const int DescCycleCreate   = 320;
        protected const int DescCycleTurn     = 321;

        protected const float CycleSpeed = 30f;

        // ── State machine ─────────────────────────────────────────────────────
        protected enum State { Polling, InfoRequesting, LoggingIn, Connected, Playing }

        protected readonly IUdpLink _link;
        protected readonly ReliableSession _session;
        protected readonly string _name;
        private   readonly byte[] _token;

        protected State _state = State.Polling;

        // Cycle identity
        protected int _myCycleId    = -1;
        protected int _gameNetObjId = -1;

        // Cycle motion state — dead-reckoned, seeded from desc=24 27w at spawn
        protected Vec2  _pos            = new Vec2(0, 0);
        protected Vec2  _dir            = new Vec2(1, 0);
        protected float _dist           = 0f;
        protected float _gameTime       = 0f;
        protected float _spawnGameTime  = 0f;
        protected int   _turns          = 0;
        protected bool  _posInitialized = false;

        // Login / connection
        private int  _loginStatus       = 0;
        private int  _pollRetries       = 0;
        private int  _loginMid          = -1;
        private int  _slotProbe         = 1;
        private long _lastSlotProbeTick = 0;
        private long _lastSendTick      = 0;

        private const int MaxPollRetries = 30;
        private const int RetryIntervalMs = 500;
        private const int KeepaliveMs    = 8_000;

        // Player object registration
        private bool _playerObjectSent     = false;
        private bool _registrationRejected = false; // server sent desc=3 "cheating" after our desc=201
        private int  _myPlayerNetObjId     = -1;

        // netObject ID reservation pool (desc=20 reply → desc=201 allocation)
        private readonly SortedSet<int> _reservedIds = new SortedSet<int>();

        // desc=311 speed-sync priming sequence (mirrors real client before desc=201)
        private static readonly int[] SpeedSyncValues = { 10, 20, 30, 35, 40, 50 };
        private int  _speedSyncStep     = -1;
        private long _nextSpeedSyncTick = 0;

        protected volatile bool _stopRequested;

        protected ArmagetronSessionBase(IUdpLink link, string name)
        {
            _link    = link;
            _session = new ReliableSession(firstReliableId: 1, connectionId: 0);
            _name    = name;
            _token   = new byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(_token);
        }

        // ── Extension points ──────────────────────────────────────────────────

        /// Called each tick when in Playing state. Subclass sends turn commands here.
        protected abstract void MaybeSendCycleCommand();

        /// Called when the server sends a 27-word position sync for any cycle.
        protected virtual void OnCyclePositionUpdate(int cycleId, Vec2 pos, Vec2 dir) { }

        /// Called when desc=320 identifies our gCycle (before Playing state is set).
        protected virtual void OnMyCycleCreated(int cycleId) { }

        /// Called when GS_TRANSFER_SETTINGS=7 fires (new round starting).
        protected virtual void OnRoundStart() { }

        /// Called when desc=9 (round-end) arrives.
        protected virtual void OnRoundEnd() { }

        // ── Main loop ─────────────────────────────────────────────────────────

        public void Run()
        {
            SendPoll();
            while (!_stopRequested) Pump();
        }

        /// <summary>
        /// One iteration of the session loop: receive+dispatch (or idle), advance the
        /// priming sequence, and let the subclass send cycle commands when Playing.
        /// </summary>
        private void Pump()
        {
            byte[]? data = _link.Receive(timeoutMillis: 20);
            if (data != null) HandlePacket(data);
            else              HandleIdle();

            if (_speedSyncStep >= 0) MaybeSpeedSync();
            if (_state == State.Playing) MaybeSendCycleCommand();
        }

        /// <summary>
        /// Drive the connect→login→register handshake on the CALLER's thread until our
        /// cycle is created (State.Playing) or <paramref name="timeoutMs"/> elapses.
        /// Registration is a one-shot, timing-sensitive race against the server; running
        /// it on an uncontended thread (not a render-starved background thread) is what
        /// makes desc=201 land inside the server's valid window. Returns true on success.
        /// </summary>
        public bool RunUntilPlaying(int timeoutMs)
        {
            SendPoll();
            long deadline = Tick() + timeoutMs;
            while (!_stopRequested && _state != State.Playing
                   && !_registrationRejected && Tick() < deadline)
                Pump();
            return _state == State.Playing;
        }

        /// <summary>
        /// Continue pumping the session loop WITHOUT re-sending the initial poll — used to
        /// hand a session already advanced by <see cref="RunUntilPlaying"/> off to a
        /// background thread for the rendering phase.
        /// </summary>
        public void RunLoop()
        {
            while (!_stopRequested) Pump();
        }

        public void RequestStop() => _stopRequested = true;

        /// <summary>True once our gCycle has been created (registration passed the cheating gate).</summary>
        public bool IsPlaying => _state == State.Playing;

        // ── Packet receive ────────────────────────────────────────────────────

        private void HandlePacket(byte[] data)
        {
            Packet pkt;
            try { pkt = StreamCodec.Parse(data); }
            catch (Exception ex) { Log($"WARN parse error: {ex.Message}"); return; }

            _session.OnReceived(pkt);
            foreach (var msg in pkt.Messages)
            {
                Log($"← desc={msg.DescriptorId,4} mid={msg.MessageId,5} ({msg.DataLengthWords}w)");
                DispatchIncoming(msg);
            }
            FlushAcks();
        }

        private void DispatchIncoming(NetMessage msg)
        {
            switch (msg.DescriptorId)
            {
                case DescPollReply:
                    if (_state == State.Polling)
                    { _state = State.InfoRequesting; SendServerInfoRequest(); }
                    break;

                case DescInfoReply:
                    if (_state == State.InfoRequesting)
                    { _state = State.LoggingIn; SendVersionAndLogin(); }
                    break;

                case DescLoginAccepted:
                    OnLoginAccepted(msg);
                    break;

                case 8:
                    LogBody("desc=8  (MOTD?)", msg);
                    break;

                case 10:
                    LogBody("desc=10 (player-join?)", msg);
                    break;

                case 28:
                    OnDesc28(msg);
                    break;

                case 20:
                    OnReservedIdReply(msg);
                    break;

                case DescGameSync:
                    OnGameStateSync(msg);
                    break;

                case DescCycleCreate:
                    OnCycleCreate(msg);
                    break;

                case 220:
                    OnDesc220(msg);
                    break;

                case 330: case 331:
                    LogBody($"desc={msg.DescriptorId} (zone?)", msg);
                    break;

                case 9:
                    Log($"  desc=9 (round-end) mid={msg.MessageId} cycle={_myCycleId}");
                    _posInitialized = false;
                    if (_myCycleId < 0) _state = State.Connected;
                    OnRoundEnd();
                    SendReadySync();
                    break;

                case 24:
                    OnDesc24(msg);
                    break;

                case 3:
                    OnDesc3(msg);
                    break;

                case DescNetObjectSync:
                {
                    if (msg.DataLengthWords >= 2)
                    {
                        int netId     = ((msg.Body[0] & 0xFF) << 8) | (msg.Body[1] & 0xFF);
                        int flag      = ((msg.Body[2] & 0xFF) << 8) | (msg.Body[3] & 0xFF);
                        bool isCreate = flag == 0xFFFF;
                        Log($"  desc=60 netobj={netId} flag={flag:X4}{(isCreate ? " CREATE" : "")}");
                    }
                    else LogBody("desc=60 (short?)", msg);
                    break;
                }

                default:
                    if (msg.DescriptorId != DescAck && msg.DescriptorId != DescKeepalive)
                    {
                        int w0 = msg.DataLengthWords >= 1
                            ? ((msg.Body[0] & 0xFF) << 8) | (msg.Body[1] & 0xFF) : -1;
                        int w1 = msg.DataLengthWords >= 2
                            ? ((msg.Body[2] & 0xFF) << 8) | (msg.Body[3] & 0xFF) : -1;
                        LogBody($"desc={msg.DescriptorId} w0={w0} w1={w1:X4}", msg);
                    }
                    break;
            }
        }

        // ── Protocol handlers ─────────────────────────────────────────────────

        private void OnLoginAccepted(NetMessage msg)
        {
            if (msg.DataLengthWords < 4) { LogBody("desc=5 (too short)", msg); return; }
            var r        = msg.Reader();
            int status   = r.ReadUInt16();
            int w1r      = r.ReadUInt16();
            int w2r      = r.ReadUInt16();
            int playerId = r.ReadUInt16();

            if (msg.MessageId == _loginMid)
            {
                long now = Tick();
                if (now - _lastSlotProbeTick >= 500)
                {
                    _slotProbe++;
                    _session.ConnectionId = _slotProbe;
                    _lastSlotProbeTick    = now;
                    Log($"  desc=5 retransmit → probing ConnectionId={_slotProbe}");
                    SendReadySync();
                }
                return;
            }

            string bodyHex = BitConverter.ToString(msg.Body).Replace("-", " ");
            Log($"  desc=5 raw body ({msg.DataLengthWords}w): {bodyHex}");
            Log($"✓ LoginAccepted: status={status} w1={w1r} w2={w2r} player_id={playerId}");
            _loginMid             = msg.MessageId;
            _slotProbe            = 1;
            _loginStatus          = status;
            _session.ConnectionId = 1;
            _lastSlotProbeTick    = Tick();

            if (_state == State.LoggingIn)
            {
                _state = State.Connected;
                SendReadySync();
                if (status != 1) Log($"  Mid-round join (status={status}), waiting for spawn…");
            }
        }

        private void OnDesc24(NetMessage msg)
        {
            if (msg.DataLengthWords == 2)
            {
                var r     = msg.Reader();
                int cid   = r.ReadUInt16();
                int value = r.ReadUInt16();
                Log($"  desc=24 compact cycle={cid} value={value}");

                if (cid == _gameNetObjId && _gameNetObjId >= 0 && value == 7 &&
                    (_state == State.Playing || _state == State.Connected))
                {
                    _dist = 0f; _gameTime = 0f; _spawnGameTime = 0f;
                    _turns = 0; _posInitialized = false;
                    Log("  → new round detected — awaiting spawn pos from desc=24 27w");
                    OnRoundStart();

                    if (!_playerObjectSent && _speedSyncStep < 0)
                    {
                        _speedSyncStep     = 0;
                        _nextSpeedSyncTick = Tick() + 100;
                        Log($"  → starting desc=311 priming (cycle={_myCycleId})");
                    }
                }
            }
            else if (CycleStateSync.TryDecodeFull(msg, out var sync))
            {
                bool ours = sync.CycleId == _myCycleId && _myCycleId >= 0;
                Log($"  desc=24 27w cid={sync.CycleId}{(ours ? " ★OUR" : "")} " +
                    $"pos=({sync.Position.X:0.##},{sync.Position.Y:0.##}) " +
                    $"dir=({sync.Direction.X:0.##},{sync.Direction.Y:0.##}) gt={sync.GameTime:0.###}");

                OnCyclePositionUpdate(sync.CycleId, sync.Position, sync.Direction);

                if (ours && !_posInitialized)
                {
                    _dir            = sync.Direction;
                    _pos            = sync.Position;
                    _dist           = 0f;
                    _gameTime       = sync.GameTime;
                    _posInitialized = true;
                    Log($"★ Spawn pos: pos=({_pos.X:0.##},{_pos.Y:0.##}) dir=({_dir.X:0.##},{_dir.Y:0.##})");
                }
            }
            else if (msg.DataLengthWords >= 10 && msg.Reader().ReadUInt16() == _myCycleId && _myCycleId >= 0)
            {
                LogBody($"desc=24 {msg.DataLengthWords}w UNCHARTED our-cycle sync (not seeding)", msg);
            }
            else
            {
                if (msg.DataLengthWords == 5)
                {
                    var r    = msg.Reader();
                    int w0   = r.ReadUInt16();
                    float gt = r.ReadReal();
                    if (w0 == 6) Log($"  desc=24 5w game_time={gt:0.###}");
                }
                else LogBody($"desc=24 {msg.DataLengthWords}w", msg);
            }
        }

        private void OnDesc3(NetMessage msg)
        {
            string text;
            try { text = msg.Reader().ReadString(); }
            catch { LogBody("desc=3 (??)", msg); return; }

            LogBody($"desc=3 text='{text}'", msg);

            // After desc=201, a "cheating" notice means our registration was rejected and
            // the server stops talking to this connection. Flag it so RunUntilPlaying can
            // bail immediately and the caller can reconnect on a FRESH socket instead of
            // blocking until timeout on a dead connection.
            if (_playerObjectSent && text.IndexOf("cheating", StringComparison.OrdinalIgnoreCase) >= 0)
                _registrationRejected = true;
        }

        private void OnDesc28(NetMessage msg)
        {
            if (msg.DataLengthWords >= 4)
            {
                var r         = msg.Reader();
                float a       = r.ReadReal();
                float b       = r.ReadReal();
                string hex28  = BitConverter.ToString(msg.Body).Replace("-", "").ToLower();
                Log($"  desc=28 REAL_0={a:0.###}  REAL_1={b:0.###}  hex={hex28}");
                if (_state == State.Playing && _spawnGameTime == 0f && a > 0f)
                {
                    _spawnGameTime = a;
                    _gameTime      = a;
                    Log($"  → spawn_game_time={a:0.###}");
                }
            }
            else LogBody("desc=28", msg);

            SendTimerAck();
        }

        private void OnReservedIdReply(NetMessage msg)
        {
            var r      = msg.Reader();
            int blocks = 0;
            while (r.HasMore)
            {
                int begin = r.ReadUInt16();
                if (!r.HasMore) break;
                int len = r.ReadUInt16();
                for (int i = 0; i < len; i++) _reservedIds.Add(begin + i);
                blocks++;
                Log($"  desc=20 reserved {begin}..{begin + len - 1} ({len})");
            }
            Log($"  desc=20 pool={_reservedIds.Count} ids ({blocks} block(s))");
        }

        private void OnGameStateSync(NetMessage msg)
        {
            if (msg.DataLengthWords < 1) { LogBody("desc=310 too short", msg); return; }
            int gameId    = msg.Reader().ReadUInt16();
            _gameNetObjId = gameId;
            LogBody($"desc=310 (gGame) netobj={gameId}", msg);
        }

        private void OnCycleCreate(NetMessage msg)
        {
            if (!CycleCreateMessage.TryDecode(msg, out var cc))
            { LogBody("desc=320 undecodeable", msg); return; }

            bool ours = cc.ConnectionSlot == _session.ConnectionId;
            Log($"  desc=320 cycleId={cc.CycleId} slot={cc.ConnectionSlot} player={cc.PlayerNetObjId}{(ours ? " ★OURS" : "")}");
            if (!ours) return;

            _myCycleId      = cc.CycleId;
            _turns          = 0;
            _gameTime       = 0f;
            _spawnGameTime  = 0f;
            _dist           = 0f;
            _pos            = new Vec2(0, 0);
            _dir            = new Vec2(1, 0);
            _posInitialized = false;
            Log($"★ gCycle id={_myCycleId} → Playing");
            OnMyCycleCreated(_myCycleId);
            _state = State.Playing;
        }

        private void OnDesc220(NetMessage msg)
        {
            if (!Desc220Message.TryDecode(msg, out var payload))
            { LogBody("desc=220 (undecodeable)", msg); return; }
            string nameTag = payload.HasName ? $" name='{payload.Name}'" : "";
            Log($"  desc=220 netobj={payload.NetObjId}{nameTag} ({msg.DataLengthWords}w)");
        }

        // ── Outgoing helpers ──────────────────────────────────────────────────

        protected void SendDesc321(Vec2 pos, Vec2 dir, float dist, float gt, int turns)
        {
            var sync = new CycleDestinationSync(
                position:  pos,
                direction: dir,
                distance:  dist,
                flags:     0,
                cycleId:   _myCycleId,
                gameTime:  gt,
                turns:     turns);
            int mid = _session.NextReliableId();
            Log($"→ desc=321 mid={mid} cycle={_myCycleId} pos=({pos.X:0.#},{pos.Y:0.#}) dir=({dir.X:0.#},{dir.Y:0.#}) turns={turns}");
            SendReliableDouble(sync.ToMessage(mid));
        }

        private void SendTimerAck()
        {
            var w = new MessageWriter();
            w.WriteUInt16(0x0000);
            int mid = _session.NextReliableId();
            Log($"→ desc=27 (timer-ack) mid={mid}");
            SendReliable(new NetMessage(27, mid, w.ToArray()));
        }

        private void MaybeSpeedSync()
        {
            if (_speedSyncStep < 0 || _speedSyncStep >= SpeedSyncValues.Length) return;
            if (Tick() < _nextSpeedSyncTick) return;

            int value = SpeedSyncValues[_speedSyncStep];
            var w     = new MessageWriter();
            w.WriteUInt16(value);
            int mid = _session.NextReliableId();
            Log($"→ desc=311 value={value} mid={mid}");
            SendReliableDouble(new NetMessage(311, mid, w.ToArray()));

            _speedSyncStep++;
            _nextSpeedSyncTick = Tick() + 100;

            if (_speedSyncStep >= SpeedSyncValues.Length && !_playerObjectSent)
            {
                int reserved = AllocateReservedId();
                if (reserved < 0)
                {
                    Log("  → priming done, no reserved id yet — deferring desc=201");
                    _nextSpeedSyncTick = Tick() + 100;
                    return;
                }
                Log($"  → priming done — desc=201+204 (reserved netobj={reserved})");
                _myPlayerNetObjId = reserved;
                SendPlayerObjectCreate();
                _playerObjectSent = true;
                _speedSyncStep    = -1;
            }
        }

        private int AllocateReservedId()
        {
            if (_reservedIds.Count == 0) return -1;
            int id = _reservedIds.Max;
            _reservedIds.Remove(id);
            return id;
        }

        private void SendPlayerObjectCreate()
        {
            var w201 = new MessageWriter();
            w201.WriteUInt16(_myPlayerNetObjId);
            w201.WriteUInt16(1);
            w201.WriteUInt16(15);
            w201.WriteUInt16(7);
            w201.WriteUInt16(0);
            w201.WriteUInt16(1000);
            w201.WriteString(_name);

            byte[] partial      = w201.ToArray();
            int    wordsWritten = partial.Length / 2;
            while (wordsWritten < 18) { w201.WriteUInt16(0); wordsWritten++; }
            w201.WriteUInt16(_myCycleId);
            w201.WriteUInt16(0);
            w201.WriteUInt16(0);

            int    mid201  = _session.NextReliableId();
            byte[] body201 = w201.ToArray();
            Log($"→ desc=201 netobj={_myPlayerNetObjId} name='{_name}' mid={mid201}");
            SendReliableDouble(new NetMessage(201, mid201, body201));

            var w204 = new MessageWriter();
            w204.WriteUInt16(_myPlayerNetObjId);
            w204.WriteUInt16(0);
            w204.WriteUInt16(0);
            int mid204 = _session.NextReliableId();
            Log($"→ desc=204 netobj={_myPlayerNetObjId} mid={mid204}");
            SendReliableDouble(new NetMessage(204, mid204, w204.ToArray()));
        }

        private void SendPoll()
        {
            Log("→ desc=52 (poll)");
            SendUnreliable(new NetMessage(DescPollRequest, 0, Array.Empty<byte>()));
            _lastSendTick = Tick();
        }

        private void SendServerInfoRequest()
        {
            Log("→ desc=53 (info request)");
            SendUnreliable(new NetMessage(DescInfoRequest, 0, Array.Empty<byte>()));
            _lastSendTick = Tick();
        }

        private void SendVersionAndLogin()
        {
            for (int i = 0; i < 4; i++)
            {
                Log($"→ desc=7  (version frame {i})");
                SendUnreliable(new NetMessage(DescVersion, 0, new byte[] { 0, 0 }));
            }
            byte[] loginBody = EncodeLoginBody();
            Log($"→ desc=11 (Login) name='{_name}'");
            SendUnreliable(new NetMessage(DescLogin, 0, loginBody));
            _lastSendTick = Tick();
        }

        protected void SendReadySync()
        {
            var w21 = new MessageWriter();
            w21.WriteUInt16(0x0028);
            int mid21 = _session.NextReliableId();
            SendReliableDouble(new NetMessage(21, mid21, w21.ToArray()));
            Log($"→ desc=21 mid={mid21}");

            int mid25 = _session.NextReliableId();
            SendReliableDouble(new NetMessage(25, mid25, Array.Empty<byte>()));
            Log($"→ desc=25 mid={mid25}");

            var w28 = new MessageWriter();
            w28.WriteReal(40f);
            w28.WriteReal(0f);
            int mid28 = _session.NextReliableId();
            SendReliableDouble(new NetMessage(28, mid28, w28.ToArray()));
            Log($"→ desc=28 mid={mid28}");
        }

        protected void SendReliable(NetMessage msg)
        {
            byte[]? ack = _session.DrainAckPacket();
            var msgs = new List<NetMessage> { msg };
            if (ack != null)
            {
                var ackPkt = StreamCodec.Parse(ack);
                foreach (var m in ackPkt.Messages) msgs.Add(m);
            }
            _link.Send(_session.Assemble(msgs));
        }

        protected void SendReliableDouble(NetMessage msg)
        {
            byte[]? ack = _session.DrainAckPacket();
            var msgs = new List<NetMessage> { msg };
            if (ack != null)
            {
                var ackPkt = StreamCodec.Parse(ack);
                foreach (var m in ackPkt.Messages) msgs.Add(m);
            }
            msgs.Add(msg);
            _link.Send(_session.Assemble(msgs));
        }

        private void SendUnreliable(NetMessage msg) =>
            _link.Send(StreamCodec.Serialize(new Packet(new[] { msg }, 0)));

        private void FlushAcks()
        {
            var (ack, ids) = _session.DrainAckPacketWithIds();
            if (ack != null)
            {
                Log($"→ desc=1  (ack) ids=[{string.Join(",", ids!)}]");
                _link.Send(ack);
            }
        }

        private void HandleIdle()
        {
            long now      = Tick();
            int  interval = _state == State.Connected ? KeepaliveMs : RetryIntervalMs;
            if (now - _lastSendTick < interval) return;

            switch (_state)
            {
                case State.Polling:
                    if (++_pollRetries >= MaxPollRetries)
                    {
                        Log($"ERROR: no response after {MaxPollRetries} polls.");
                        System.Environment.Exit(1);
                    }
                    SendPoll();
                    break;
                case State.InfoRequesting:
                    SendServerInfoRequest();
                    break;
                case State.LoggingIn:
                    SendVersionAndLogin();
                    break;
                case State.Connected:
                    Log("→ keepalive (resend ReadySync)");
                    SendReadySync();
                    _lastSendTick = now;
                    break;
            }
        }

        private byte[] EncodeLoginBody()
        {
            var w = new MessageWriter();
            w.WriteUInt16(0x0040);
            w.WriteUInt16(0); w.WriteUInt16(0); w.WriteUInt16(0);
            w.WriteUInt16(0); w.WriteUInt16(0);
            w.WriteString(_name);
            for (int i = 0; i < 16; i += 2)
                w.WriteUInt16((_token[i] << 8) | _token[i + 1]);
            return w.ToArray();
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        protected static void LogBody(string label, NetMessage msg)
        {
            string hex = msg.Body.Length == 0
                ? "(empty)"
                : BitConverter.ToString(msg.Body).Replace("-", "").ToLower();
            Log($"  {label} mid={msg.MessageId} body={hex}");
        }

        protected static long Tick() =>
            System.Diagnostics.Stopwatch.GetTimestamp() * 1000L /
            System.Diagnostics.Stopwatch.Frequency;

        protected static void Log(string text) =>
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {text}");

        public virtual void Dispose() => _link.Dispose();
    }
}
