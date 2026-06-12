using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Armagetron.Net;
using Armagetron.Protocol;
using Armagetron.Protocol.Game;

namespace Armagetron.Bot
{
    /// <summary>
    /// Headless bot that connects to a 0.2.9.x Armagetron listen server, completes the
    /// login handshake, stays in the session (acking every reliable message + answering
    /// keepalives), and sends random desc-321 CycleDestinationSync turn commands once a
    /// cycle is spawned for it.
    ///
    /// Protocol findings confirmed live here feed back into Core.Net/NETPLAN.md.
    /// </summary>
    public sealed class BotSession : IDisposable
    {
        // ── Descriptor constants (from capture analysis) ──────────────────────
        private const int DescPollRequest      = 52; // C→S  empty (unreliable)
        private const int DescPollReply        = 50; // S→C  [port u16, 0 0 0 0]
        private const int DescInfoRequest      = 53; // C→S  empty (unreliable)
        private const int DescInfoReply        = 51; // S→C  full server info blob
        private const int DescVersion         =  7; // C→S  [0000] × 4 (unreliable)
        private const int DescLogin            = 11; // C→S  login body (unreliable)
        private const int DescLoginAccepted    =  5; // S→C  login-accepted body
        private const int DescAck             =  1; // both  ack (mid=0, unreliable)
        private const int DescKeepalive        = 27; // S→C  [0000] heartbeat (reliable)
        private const int DescNetObjectSync    = 60; // S→C  nNetObject sync
        private const int DescCycleSync        = 300; // S→C  cycle position sync
        private const int DescGameSync         = 310; // S→C  gGame create/sync (GS_ state transitions)
        private const int DescCycleCreate      = 320; // S→C  gCycle create (gives us our cycle id)
        private const int DescCycleTurn        = 321; // C→S  CycleDestinationSync

        // ── State machine ─────────────────────────────────────────────────────
        private enum State
        {
            Polling,          // sending desc=52; waiting for desc=50
            InfoRequesting,   // sent desc=53; waiting for desc=51
            LoggingIn,        // sent desc=7×4 + desc=11; waiting for desc=5
            Connected,        // logged in; acking + watching for spawn
            Playing,          // cycle spawned; sending desc=321 turns
        }

        private readonly IUdpLink _link;
        private readonly ReliableSession _session;
        private readonly string _name;
        private readonly byte[] _token;      // 16-byte random nonce
        private readonly Random _rng = new Random();

        private State _state = State.Polling;
        private int _loginStatus = 0;
        private int _pollRetries = 0;
        private const int MaxPollRetries = 30;

        // Turn state
        private int _myCycleId    = -1;  // gCycle netobj_id (set from desc=320 when slot==ours)
        private int _gameNetObjId = -1;  // gGame netobj_id (set from desc=310; used for round-start trigger)
        private Vec2 _pos      = new Vec2(0, 0);
        private Vec2 _dir      = new Vec2(1, 0);
        private float _dist    = 0f;
        private float _gameTime = 0f;     // current game clock (increments at ~CYCLE_SPEED)
        private float _spawnGameTime = 0f; // game clock at spawn (from desc=28 REAL_0)
        private int _turns     = 0;
        private long _lastTurnTick = 0;
        private long _lastMoveTick = 0;   // for dead-reckoning position updates
        private const int TurnIntervalMs    = 2000;
        private const int KeepaliveMs       = 8_000; // resend ReadySync if server goes quiet
        // Arena speed roughly measured from a real session: ~37 units/sec.
        // We use a conservative 30 to avoid overshooting walls.
        private const float CycleSpeed = 30f;

        // Connection-slot probe state (find our actual server slot via retransmit detection)
        private int _loginMid = -1;          // mid of the desc=5 we last saw
        private int _slotProbe = 1;          // current ConnectionId candidate
        private long _lastSlotProbeTick = 0; // rate-limit probing to every 500ms

        // Player object registration (desc=201+204 C→S)
        private int _myPlayerNetObjId = -1;  // server-reserved netobj_id for our player object
        private bool _playerObjectSent = false; // only send once per session

        // netObject ID reservation (desc=21 → desc=20). The server reserves blocks of
        // netobj ids to this client; an object (desc=201) MUST use a reserved id or the
        // server disconnects us for cheating. desc=20 body = [begin u16][len u16] pairs.
        // We allocate the HIGHEST reserved id first (PCAP-proven: the real client always
        // uses the top of the block).
        private readonly SortedSet<int> _reservedIds = new SortedSet<int>();

        // desc=311 CycleSpeedSync sequence — sent before desc=201 (mirrors real client behaviour)
        // Real client sends values 10,20,30,35,40,50 at ~100ms intervals before registering.
        private static readonly int[] SpeedSyncValues = { 10, 20, 30, 35, 40, 50 };
        private int  _speedSyncStep = -1;    // -1=idle, 0-5=which value to send next, 6=done
        private long _nextSpeedSyncTick = 0;

        // Spawn position tracking — set from desc=24 27w server sync before first desc=321
        private bool _posInitialized = false;

        // Phase timing
        private long _lastSendTick = 0;
        private const int RetryIntervalMs = 500;

        public BotSession(IUdpLink link, string name)
        {
            _link = link;
            _session = new ReliableSession(firstReliableId: 1, connectionId: 0);
            _name = name;
            _token = new byte[16];
            RandomNumberGenerator.Fill(_token);
        }

        public void Run()
        {
            SendPoll();

            while (true)
            {
                byte[]? data = _link.Receive(timeoutMillis: 20);
                if (data != null)
                    HandlePacket(data);
                else
                    HandleIdle();

                // Run speed-sync priming whenever it is in progress (may start in Connected state
                // before desc=320 has arrived). Only send turns once in Playing state.
                if (_speedSyncStep >= 0) MaybeSpeedSync();
                if (_state == State.Playing) MaybeSendTurn();
            }
        }

        // ── Packet receive path ───────────────────────────────────────────────

        private void HandlePacket(byte[] data)
        {
            Packet pkt;
            try { pkt = StreamCodec.Parse(data); }
            catch (Exception ex)
            {
                Log($"WARN parse error: {ex.Message}");
                return;
            }

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
                case DescPollReply:          // desc=50: server port/version ping
                    if (_state == State.Polling)
                    {
                        _state = State.InfoRequesting;
                        SendServerInfoRequest();
                    }
                    break;

                case DescInfoReply:          // desc=51: full server info blob
                    if (_state == State.InfoRequesting)
                    {
                        _state = State.LoggingIn;
                        SendVersionAndLogin();
                    }
                    break;

                case DescLoginAccepted:      // desc=5 — handle in any state so retransmits trigger slot probe
                    OnLoginAccepted(msg);
                    break;

                case 8:                      // probably MOTD/chat
                    LogBody("desc=8  (MOTD?)", msg);
                    break;

                case 10:                     // player-join announcement
                    LogBody("desc=10 (player-join?)", msg);
                    break;

                case 28:                     // S→C timer/sync; decode REALs
                    OnDesc28(msg);
                    break;

                case 20:                     // desc=20: ReservedIdReply — blocks of netobj ids reserved to us
                    OnReservedIdReply(msg);
                    break;

                case DescGameSync:           // desc=310: gGame create/sync (GS_ state transitions)
                    OnGameStateSync(msg);
                    break;

                case DescCycleCreate:        // desc=320: gCycle creation — identifies our cycle
                    OnCycleCreate(msg);
                    break;

                case 220:
                    OnDesc220(msg);
                    break;

                case 330: case 331:
                    LogBody($"desc={msg.DescriptorId} (zone?)", msg);
                    break;

                case 9:                      // round-end
                    Log($"  desc=9 (round-end) mid={msg.MessageId} cycle={_myCycleId}");
                    // Do NOT reset _myCycleId — server reuses same ID across rounds.
                    // Clear posInitialized so the next round's desc=24 27w will re-seed
                    // the spawn position before we resume desc=321.
                    _posInitialized = false;
                    if (_myCycleId < 0)
                        _state = State.Connected;
                    SendReadySync();
                    break;

                case 24:                     // compact rubber/status update
                    OnDesc24(msg);
                    break;

                case 3:
                    OnDesc3(msg);
                    break;

                case DescNetObjectSync:  // desc=60: log word[0]=netobj_id, word[1]=creation flag
                {
                    if (msg.DataLengthWords >= 2)
                    {
                        int netId = ((msg.Body[0] & 0xFF) << 8) | (msg.Body[1] & 0xFF);
                        int flag  = ((msg.Body[2] & 0xFF) << 8) | (msg.Body[3] & 0xFF);
                        bool isCreate = (flag == 0xFFFF);
                        Log($"  desc=60 netobj={netId} flag={flag:X4}{(isCreate ? " CREATE" : "")} mid={msg.MessageId} ({msg.DataLengthWords}w)");
                    }
                    else
                    {
                        LogBody("desc=60 (short?)", msg);
                    }
                    break;
                }

                default:
                    if (msg.DescriptorId != DescAck && msg.DescriptorId != DescKeepalive)
                    {
                        // Log first 2 words to spot creation messages for high netobj IDs
                        int w0 = msg.DataLengthWords >= 1
                            ? ((msg.Body[0] & 0xFF) << 8) | (msg.Body[1] & 0xFF) : -1;
                        int w1 = msg.DataLengthWords >= 2
                            ? ((msg.Body[2] & 0xFF) << 8) | (msg.Body[3] & 0xFF) : -1;
                        LogBody($"desc={msg.DescriptorId} w0={w0} w1={w1:X4}", msg);
                    }
                    break;
            }
        }

        // ── Login accepted ────────────────────────────────────────────────────

        private void OnLoginAccepted(NetMessage msg)
        {
            if (msg.DataLengthWords < 4)
            {
                LogBody("desc=5 (LoginAccepted too short — ignoring)", msg);
                return;
            }
            var r = msg.Reader();
            int status   = r.ReadUInt16();
            int w1r      = r.ReadUInt16();
            int w2r      = r.ReadUInt16();
            int playerId = r.ReadUInt16();

            if (msg.MessageId == _loginMid)
            {
                // Retransmission: server hasn't received our ack — our ConnectionId is
                // routing acks to the wrong session (a zombie from a prior run).
                // Probe the next slot, rate-limited to every 500 ms.
                long now = Tick();
                if (now - _lastSlotProbeTick >= 500)
                {
                    _slotProbe++;
                    _session.ConnectionId = _slotProbe;
                    _lastSlotProbeTick = now;
                    Log($"  desc=5 retransmit (mid={msg.MessageId}) → probing ConnectionId={_slotProbe}");
                    SendReadySync(); // re-send with new ConnectionId so the ack reaches the server
                }
                return;
            }

            // Fresh LoginAccepted (new mid).
            string bodyHex = BitConverter.ToString(msg.Body).Replace("-", " ");
            Log($"  desc=5 raw body ({msg.DataLengthWords}w): {bodyHex}");
            Log($"✓ LoginAccepted: status={status} w1={w1r} w2={w2r} player_id={playerId}");
            _loginMid    = msg.MessageId;
            _slotProbe   = 1;
            _loginStatus = status;

            // Start at ConnectionId=1; will advance if desc=5 keeps being retransmitted.
            _session.ConnectionId = 1;
            _lastSlotProbeTick = Tick();

            if (_state == State.LoggingIn)
            {
                _state = State.Connected;
                SendReadySync();
                if (status != 1)
                    Log($"  Mid-round join (status={status}), waiting for desc=310 spawn…");
            }
        }

        // ── Cycle alive / spawn ───────────────────────────────────────────────

        private void OnDesc24(NetMessage msg)
        {
            if (msg.DataLengthWords == 2)
            {
                // 2-word compact: [cycle_id][value] — rubber/status update
                var r = msg.Reader();
                int cid   = r.ReadUInt16();
                int value = r.ReadUInt16();
                Log($"  desc=24 compact cycle={cid} value={value}");

                if (cid == _gameNetObjId && _gameNetObjId >= 0 && value == 7 &&
                    (_state == State.Playing || _state == State.Connected))
                {
                    // GS_TRANSFER_SETTINGS (7) on the gGame netobj == server starting a new round.
                    // Fire in both Connected and Playing: in Connected we haven't registered yet
                    // and need this trigger to start desc=311 priming → desc=201 → desc=320.
                    // Clear position state and wait for desc=24 27w to give us the real spawn coords.
                    _dist          = 0f;
                    _gameTime      = 0f;
                    _spawnGameTime = 0f;
                    _turns         = 0;
                    _posInitialized = false;
                    _lastMoveTick  = Tick();
                    _lastTurnTick  = Tick();
                    Log("  → new round detected (rubber reset) — awaiting spawn pos from desc=24 27w");

                    // START desc=311 priming here — at round start the gCycle is freshly placed
                    // at the spawn position, so when desc=201 arrives after priming (~600ms later)
                    // the server's velocity check sees: displacement = 18 units / 600ms = 30 u/s.
                    // Mid-round we'd have undefined p0 / t0 making velocity appear infinite.
                    if (!_playerObjectSent && _speedSyncStep < 0)
                    {
                        _speedSyncStep = 0;
                        _nextSpeedSyncTick = Tick() + 100;
                        Log($"  → starting desc=311 speed-sync priming for desc=201 (round-start trigger, cycle={_myCycleId})");
                    }
                }
            }
            else if (CycleStateSync.TryDecodeFull(msg, out var sync))
            {
                // A genuine 27-word gCycle position sync. desc=24 is the generic
                // nNetObject sync: shorter ≥10w variants are player/team objects
                // (they carry names), and decoding those as positions seeds garbage.
                // Only this full cycle sync may set our spawn position.
                bool ours = (sync.CycleId == _myCycleId && _myCycleId >= 0);
                Log($"  desc=24 27w cid={sync.CycleId}{(ours ? " ★OUR" : "")} " +
                    $"pos=({sync.Position.X:0.##},{sync.Position.Y:0.##}) " +
                    $"dir=({sync.Direction.X:0.##},{sync.Direction.Y:0.##}) gt={sync.GameTime:0.###}");
                if (ours && !_posInitialized)
                {
                    _dir = sync.Direction;
                    _pos = sync.Position;
                    _dist = 0f;
                    _gameTime = sync.GameTime;
                    _posInitialized = true;
                    _lastMoveTick = Tick();
                    _lastTurnTick = Tick();
                    Log($"★ Spawn pos from desc=24 27w: pos=({_pos.X:0.##},{_pos.Y:0.##}) " +
                        $"dir=({_dir.X:0.##},{_dir.Y:0.##}) gt={_gameTime:0.###}");
                }
            }
            else if (msg.DataLengthWords >= 10 && msg.Reader().ReadUInt16() == _myCycleId && _myCycleId >= 0)
            {
                // A ≥10w desc=24 carrying OUR cycle id but NOT the 27w full form —
                // an as-yet-undecoded gCycle sync variant (19/24/29w seen on the wire).
                // Do NOT seed position from it; dump raw words so a live capture can
                // reveal the short-variant layout (CLAUDE.md TDD step: gather vectors).
                LogBody($"desc=24 {msg.DataLengthWords}w UNCHARTED our-cycle sync (not seeding)", msg);
            }
            else
            {
                // desc=24 with 3-9 words (game timer or short object sync) — just log
                if (msg.DataLengthWords == 5)
                {
                    var r = msg.Reader();
                    int w0 = r.ReadUInt16();
                    float gt = r.ReadReal();
                    if (w0 == 6)
                        Log($"  desc=24 5w game_time={gt:0.###}");
                }
                else
                {
                    LogBody($"desc=24 {msg.DataLengthWords}w", msg);
                }
            }
        }

        private void OnDesc3(NetMessage msg)
        {
            // Unreliable broadcast — body starts with an AA-encoded string, then data.
            try
            {
                var r = msg.Reader();
                string text = r.ReadString();
                LogBody($"desc=3 text='{text}'", msg);
            }
            catch { LogBody("desc=3 (??)", msg); }
        }

        private void OnDesc28(NetMessage msg)
        {
            if (msg.DataLengthWords >= 4)
            {
                var r = msg.Reader();
                float a = r.ReadReal();
                float b = r.ReadReal();
                string hex28 = BitConverter.ToString(msg.Body).Replace("-", "").ToLower();
                Log($"  desc=28 (timer/sync?) REAL_0={a:0.###}  REAL_1={b:0.###}  hex={hex28}");
                // First desc=28 after spawn gives the game clock at spawn time.
                if (_state == State.Playing && _spawnGameTime == 0f && a > 0f)
                {
                    _spawnGameTime = a;
                    _gameTime = a;
                    Log($"  → spawn_game_time captured: {a:0.###}");
                }
            }
            else
            {
                LogBody("desc=28", msg);
            }

            // Respond with desc=27 C→S (application-layer timer ack, body=0x0000).
            // PCAP analysis confirms: the real client sends exactly one desc=27 C→S for
            // every desc=28 S→C received. Without this, the server never transitions
            // objects to "client knows about this" state, causing the cascade of
            // "User X does not know about netobject N" errors and the herky-jerky
            // retransmission storm that freezes physics.
            SendTimerAck();
        }

        private void SendTimerAck()
        {
            var w = new MessageWriter();
            w.WriteUInt16(0x0000);
            int mid = _session.NextReliableId();
            var msg = new NetMessage(27, mid, w.ToArray());
            Log($"→ desc=27 (timer-ack) mid={mid}");
            SendReliable(msg);
        }

        // Send a reliable message once (with pending acks bundled, but NOT doubled).
        // Use for high-frequency protocol messages where doubling wastes bandwidth.
        private void SendReliable(NetMessage msg)
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

        // desc=20 ReservedIdReply: body = one or more [begin u16][len u16] pairs.
        // Each pair reserves ids begin..begin+len-1 to this client. We add them to a
        // pool and later allocate the highest id first when creating desc=201.
        private void OnReservedIdReply(NetMessage msg)
        {
            var r = msg.Reader();
            int blocks = 0;
            while (r.HasMore)
            {
                int begin = r.ReadUInt16();
                if (!r.HasMore) break;            // malformed trailing word — ignore
                int len = r.ReadUInt16();
                for (int i = 0; i < len; i++)
                    _reservedIds.Add(begin + i);
                blocks++;
                Log($"  desc=20 reserved ids {begin}..{begin + len - 1} ({len})");
            }
            Log($"  desc=20 pool now {_reservedIds.Count} ids ({blocks} block(s))");
        }

        // Allocate a reserved netobj id, taking the HIGHEST first (PCAP-proven: the real
        // client always uses the top of the reserved block). Returns -1 if the pool is empty.
        private int AllocateReservedId()
        {
            if (_reservedIds.Count == 0) return -1;
            int id = _reservedIds.Max;
            _reservedIds.Remove(id);
            return id;
        }

        // desc=310 = nNOInitialisator<gGame>. Word[0] is the gGame's server-assigned netobj_id.
        // The gGame sends compact desc=24 2w packets to broadcast GS_ state transitions
        // (GS_TRANSFER_SETTINGS=7, GS_PLAY=50, GS_DELETE_OBJECTS=60). We record its netobj_id
        // here so the round-start trigger in OnDesc24 can match cid==_gameNetObjId correctly.
        // This is NOT a gCycle message — do not set _myCycleId here.
        private void OnGameStateSync(NetMessage msg)
        {
            if (msg.DataLengthWords < 1)
            {
                LogBody("desc=310 (gGame) too short", msg);
                return;
            }
            int gameId = msg.Reader().ReadUInt16();
            _gameNetObjId = gameId;
            LogBody($"desc=310 (gGame) netobj={gameId}", msg);
        }

        // desc=320 = nNOInitialisator<gCycle>. This is the authoritative source of our gCycle id.
        // word[1] = connectionSlot: 1 for the first remote client (us), 0 for server/AI cycles.
        // We set _myCycleId only for our own cycle and transition to State.Playing.
        private void OnCycleCreate(NetMessage msg)
        {
            if (!CycleCreateMessage.TryDecode(msg, out var cc))
            {
                LogBody("desc=320 (gCycle) undecodeable", msg);
                return;
            }
            bool ours = cc.ConnectionSlot == _session.ConnectionId;
            Log($"  desc=320 (gCycle) cycleId={cc.CycleId} slot={cc.ConnectionSlot} player={cc.PlayerNetObjId}{(ours ? " ★OURS" : "")}");
            if (!ours) return;

            _myCycleId = cc.CycleId;
            _turns = 0;
            _gameTime = 0f;
            _spawnGameTime = 0f;
            _dist = 0f;
            _pos = new Vec2(0, 0);   // placeholder — overwritten by desc=24 27w
            _dir = new Vec2(1, 0);
            _posInitialized = false; // hold desc=321 until desc=24 27w gives real spawn pos
            _lastMoveTick = Tick();
            _lastTurnTick = Tick();  // delay first turn by TurnIntervalMs
            Log($"★ Our gCycle identified: cycle_id={_myCycleId}  → State.Playing (waiting for spawn pos)");
            _state = State.Playing;
            // desc=201 will be sent at the next round-start rubber reset (desc=24 2w GS_TRANSFER_SETTINGS=7),
            // NOT here mid-round. Sending desc=201 mid-round means the gCycle has been running
            // for hundreds of ms with no client position reports → velocity check → cheating.
        }

        private void OnDesc220(NetMessage msg)
        {
            if (!Desc220Message.TryDecode(msg, out var payload))
            {
                LogBody("desc=220 (undecodeable)", msg);
                return;
            }
            // desc=220 with a player name is an ePlayerNetID object, NOT our gCycle.
            // Our gCycle id is set authoritatively by desc=310 (CycleAlive). Never
            // update _myCycleId here — doing so would overwrite the correct value.
            string nameTag = payload.HasName ? $" name='{payload.Name}'" : "";
            Log($"  desc=220 netobj={payload.NetObjId}{nameTag} ({msg.DataLengthWords}w)");
        }

        // ── Outgoing messages ─────────────────────────────────────────────────

        // desc=201 C→S: register our player object with the server.
        // desc=204 C→S: send auth info (empty = unauthenticated).
        //
        // Wire layout (21 words) confirmed from real C→S capture (Erin's client):
        //   [0]     client-chosen player netobj_id
        //   [1]     1       (C→S flag — differs from S→C which uses 0)
        //   [2]     15
        //   [3]     7       (C→S constant — differs from S→C which uses 15)
        //   [4]     0
        //   [5]     1000    (C→S constant — differs from S→C which uses 100)
        //   [6+]    name (length-prefixed byte-swapped; 4 words for ≤6-char names)
        //   [10-17] 0, 0, 0, 0, 0, 0, 0, 0   ← all zeros (NOT 0xfffe/0xffff like S→C)
        //   [18]    cycle_id  ← CRITICAL: our current cycle (differs from S→C where it's at [16-17])
        //   [19-20] 0, 0
        private void SendPlayerObjectCreate()
        {
            var w201 = new MessageWriter();
            w201.WriteUInt16(_myPlayerNetObjId);   // w[0]
            w201.WriteUInt16(1);                    // w[1] = 1 for C→S
            w201.WriteUInt16(15);                   // w[2]
            w201.WriteUInt16(7);                    // w[3] = 7 for C→S
            w201.WriteUInt16(0);                    // w[4]
            w201.WriteUInt16(1000);                 // w[5] = 1000 for C→S
            w201.WriteString(_name);                // w[6..9] = name (4 words for ≤6-char names)

            // Pad from current offset to w[17] with zeros
            byte[] partial201 = w201.ToArray();
            int wordsWritten = partial201.Length / 2;
            while (wordsWritten < 18) { w201.WriteUInt16(0); wordsWritten++; }

            w201.WriteUInt16(_myCycleId);           // w[18] = our cycle_id (FIXED from hardcoded 2)
            w201.WriteUInt16(0);                    // w[19]
            w201.WriteUInt16(0);                    // w[20]

            int mid201 = _session.NextReliableId();
            byte[] body201 = w201.ToArray();
            var msg201 = new NetMessage(201, mid201, body201);
            string hex201 = BitConverter.ToString(body201).Replace("-", "").ToLower();
            Log($"→ desc=201 (PlayerObjectCreate) netobj={_myPlayerNetObjId} name='{_name}' mid={mid201} body({body201.Length / 2}w)={hex201}");
            SendReliableDouble(msg201);

            // desc=204 C→S: auth info (length=0 = no auth server)
            var w204 = new MessageWriter();
            w204.WriteUInt16(_myPlayerNetObjId);
            w204.WriteUInt16(0);
            w204.WriteUInt16(0);   // auth string length = 0 (unauthenticated)

            int mid204 = _session.NextReliableId();
            var msg204 = new NetMessage(204, mid204, w204.ToArray());
            Log($"→ desc=204 (PlayerAuth empty) netobj={_myPlayerNetObjId} mid={mid204}");
            SendReliableDouble(msg204);
        }

        private void SendPoll()
        {
            Log("→ desc=52 (poll)");
            SendUnreliable(new NetMessage(DescPollRequest, 0, Array.Empty<byte>()));
            _lastSendTick = Tick();
        }

        private void SendServerInfoRequest()
        {
            Log("→ desc=53 (server info request)");
            SendUnreliable(new NetMessage(DescInfoRequest, 0, Array.Empty<byte>()));
            _lastSendTick = Tick();
        }

        private void SendVersionAndLogin()
        {
            // desc=7 × 4: version / descriptor negotiation (body=0000 each)
            for (int i = 0; i < 4; i++)
            {
                Log($"→ desc=7  (version frame {i})");
                SendUnreliable(new NetMessage(DescVersion, 0, new byte[] { 0, 0 }));
            }

            // desc=11 Login (unreliable: mid=0)
            byte[] loginBody = EncodeLoginBody();
            Log($"→ desc=11 (Login) name='{_name}'");
            SendUnreliable(new NetMessage(DescLogin, 0, loginBody));
            _lastSendTick = Tick();
        }

        // desc=21 "ready" sync + desc=25 "I want to play" + desc=28 initial cycle state.
        // In the capture the client sent these three immediately after the initial
        // nNetObject flood, and the server responded with desc=220+310 (cycle spawn)
        // in the very next packet.  Without desc=25+28 the server parks us as spectator.
        private void SendReadySync()
        {
            // desc=21 (1w body=0x0028=40). PCAP shows real client ALWAYS sends 40;
            // sending 0 prevents spawn entirely.
            var w21 = new MessageWriter();
            w21.WriteUInt16(0x0028);
            int mid21 = _session.NextReliableId();
            var msg21 = new NetMessage(21, mid21, w21.ToArray());
            Log($"→ desc=21 (ready sync) mid={mid21}");
            SendReliableDouble(msg21);

            // desc=25 (0w empty) — "I want to play / ready" signal observed in capture
            int mid25 = _session.NextReliableId();
            var msg25 = new NetMessage(25, mid25, Array.Empty<byte>());
            Log($"→ desc=25 (want-to-play) mid={mid25}");
            SendReliableDouble(msg25);

            // desc=28 (2 REALs: 40.0, 0.0) — initial cycle state sync observed in capture
            var w28 = new MessageWriter();
            w28.WriteReal(40f);
            w28.WriteReal(0f);
            int mid28 = _session.NextReliableId();
            var msg28 = new NetMessage(28, mid28, w28.ToArray());
            Log($"→ desc=28 (cycle-state 40.0/0.0) mid={mid28}");
            SendReliableDouble(msg28);
        }

        private void SendChat(string text)
        {
            var w = new MessageWriter();
            w.WriteString(text);
            int mid = _session.NextReliableId();
            var msg = new NetMessage(8, mid, w.ToArray());
            Log($"→ desc=8  (chat) mid={mid} text='{text}'");
            SendReliableDouble(msg);
        }

        private void MaybeSpeedSync()
        {
            if (_speedSyncStep < 0 || _speedSyncStep >= SpeedSyncValues.Length) return;
            if (Tick() < _nextSpeedSyncTick) return;

            int value = SpeedSyncValues[_speedSyncStep];
            var w = new MessageWriter();
            w.WriteUInt16(value);
            int mid = _session.NextReliableId();
            var msg = new NetMessage(311, mid, w.ToArray());
            Log($"→ desc=311 (CycleSpeedSync) value={value} mid={mid}");
            SendReliableDouble(msg);

            _speedSyncStep++;
            _nextSpeedSyncTick = Tick() + 100;

            if (_speedSyncStep >= SpeedSyncValues.Length && !_playerObjectSent)
            {
                // Allocate the player object's id from the server-reserved pool (highest
                // first). Never guess — an unreserved id triggers a cheating disconnect.
                // If no desc=20 block has arrived yet, defer: hold at the final step and
                // retry on the next tick once the pool is populated.
                int reserved = AllocateReservedId();
                if (reserved < 0)
                {
                    Log("  → speed-sync priming complete but no reserved id yet (desc=20 pending) — deferring desc=201");
                    _nextSpeedSyncTick = Tick() + 100;
                    return;
                }

                Log($"  → speed-sync priming complete — sending desc=201+204 (reserved netobj={reserved})");
                _myPlayerNetObjId = reserved;
                SendPlayerObjectCreate();
                _playerObjectSent = true;
                _speedSyncStep = -1; // done
            }
        }

        private void MaybeSendTurn()
        {
            if (!_posInitialized) return;   // hold until server confirms spawn position

            long now = Tick();

            // Dead-reckon position on every loop tick regardless of turns
            float dtMove = (now - _lastMoveTick) / 1000f;
            if (dtMove > 0f && dtMove < 1f)  // sanity clamp
            {
                _pos = new Vec2(_pos.X + _dir.X * CycleSpeed * dtMove,
                                _pos.Y + _dir.Y * CycleSpeed * dtMove);
                _dist += CycleSpeed * dtMove;
                _gameTime += dtMove;
            }
            _lastMoveTick = now;

            if (now - _lastTurnTick < TurnIntervalMs) return;
            _lastTurnTick = now;

            // Rotate direction 90° at each turn (left or right randomly)
            bool turnLeft = _rng.Next(2) == 0;
            if (turnLeft)
                _dir = new Vec2(-_dir.Y, _dir.X);
            else
                _dir = new Vec2(_dir.Y, -_dir.X);

            _turns++;

            var sync = new CycleDestinationSync(
                position:  _pos,
                direction: _dir,
                distance:  _dist,
                flags:     0,
                cycleId:   _myCycleId,
                gameTime:  _gameTime,
                turns:     _turns);

            int mid = _session.NextReliableId();
            var msg = sync.ToMessage(mid);
            Log($"→ desc=321 (turn) mid={mid} cycle={_myCycleId} pos=({_pos.X:0.#},{_pos.Y:0.#}) dir={_dir} dist={_dist:0.#} t={_gameTime:0.##} turns={_turns}");
            SendReliableDouble(msg);
        }

        // ── Send helpers ──────────────────────────────────────────────────────

        // Send a single unreliable message (pre-login: trailer=0).
        private void SendUnreliable(NetMessage msg)
        {
            var bytes = StreamCodec.Serialize(new Packet(new[] { msg }, 0));
            _link.Send(bytes);
        }

        // Send a reliable message doubled in the same datagram, as observed in the
        // capture: [msg] [ack-if-any] [msg]. This mirrors the redundant-transmission
        // pattern used by the real client.
        private void SendReliableDouble(NetMessage msg)
        {
            byte[]? ack = _session.DrainAckPacket();
            var msgs = new List<NetMessage> { msg };
            if (ack != null)
            {
                // Inline-parse the ack packet to extract the ack message itself.
                var ackPkt = StreamCodec.Parse(ack);
                foreach (var m in ackPkt.Messages) msgs.Add(m);
            }
            msgs.Add(msg); // second copy
            _link.Send(_session.Assemble(msgs));
        }

        private void FlushAcks()
        {
            var (ack, ids) = _session.DrainAckPacketWithIds();
            if (ack != null)
            {
                string idStr = string.Join(",", ids!);
                Log($"→ desc=1  (ack) ids=[{idStr}]");
                _link.Send(ack);
            }
        }

        // ── Idle / retry ──────────────────────────────────────────────────────

        private void HandleIdle()
        {
            long now = Tick();
            int interval = _state == State.Connected ? KeepaliveMs : RetryIntervalMs;
            if (now - _lastSendTick < interval) return;

            switch (_state)
            {
                case State.Polling:
                    if (++_pollRetries >= MaxPollRetries)
                    {
                        Log($"ERROR: no response from server after {MaxPollRetries} polls. Start the server!");
                        Environment.Exit(1);
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
                    // Server times out silent clients in ~30s. Resend ReadySync as heartbeat
                    // and to re-request spawn if the previous request was lost.
                    Log("→ keepalive (resending ReadySync)");
                    SendReadySync();
                    _lastSendTick = now;
                    break;
            }
        }

        // ── Login body encoder ────────────────────────────────────────────────

        // desc=11 Login body layout (confirmed from all captures):
        //   u16 0x0040        protocol version = 64 (constant across all sessions)
        //   u16 0x0000
        //   u16 0x0000
        //   u16 0x0000
        //   u16 0x0000        prev_player_id (0 = new connection; server echoes in desc=5)
        //   u16 0x0000
        //   String name       AA byte-swapped, length-prefixed
        //   bytes[16] token   random nonce; server echoes back in LoginAccepted
        private byte[] EncodeLoginBody()
        {
            var w = new MessageWriter();
            w.WriteUInt16(0x0040); // protocol version
            w.WriteUInt16(0);
            w.WriteUInt16(0);
            w.WriteUInt16(0);
            w.WriteUInt16(0);     // prev_player_id = 0
            w.WriteUInt16(0);
            w.WriteString(_name);
            // 16-byte token as 8 big-endian u16 words
            for (int i = 0; i < 16; i += 2)
                w.WriteUInt16((_token[i] << 8) | _token[i + 1]);
            return w.ToArray();
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private static void LogBody(string label, NetMessage msg)
        {
            byte[] body = msg.Body;
            string hex = body.Length == 0 ? "(empty)" : BitConverter.ToString(body).Replace("-", "").ToLower();
            Log($"  {label} mid={msg.MessageId} body={hex}");
        }

        private static long Tick() =>
            System.Diagnostics.Stopwatch.GetTimestamp() * 1000L /
            System.Diagnostics.Stopwatch.Frequency;

        private static void Log(string msg) =>
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");

        public void Dispose() => _link.Dispose();
    }
}
