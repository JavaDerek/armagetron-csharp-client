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
        private const int DescCycleAlive       = 310; // S→C  cycle alive (spawn)
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
        private int _pollRetries = 0;
        private const int MaxPollRetries = 30;

        // Turn state
        private int _myCycleId = -1;
        private Vec2 _pos     = new Vec2(0, 0);
        private Vec2 _dir     = new Vec2(1, 0);
        private float _dist   = 0f;
        private float _gameTime = 0f;
        private int _turns     = 0;
        private long _lastTurnTick = 0;
        private const int TurnIntervalMs = 2000;

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

                if (_state == State.Playing)
                    MaybeSendTurn();
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

                case DescLoginAccepted:      // desc=5
                    if (_state == State.LoggingIn)
                    {
                        OnLoginAccepted(msg);
                    }
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

                case 20:                     // unknown
                    LogBody("desc=20", msg);
                    break;

                case DescCycleAlive:         // desc=310: server spawned a cycle
                    OnCycleAlive(msg);
                    break;

                case 220:
                    LogBody("desc=220 (cycle-create?)", msg);
                    break;

                case 330: case 331:
                    LogBody($"desc={msg.DescriptorId} (zone?)", msg);
                    break;
            }
        }

        // ── Login accepted ────────────────────────────────────────────────────

        private void OnLoginAccepted(NetMessage msg)
        {
            // body: u16 status, u16, u16, u16 player_id, u16, string(addr), 16B token
            var r = msg.Reader();
            int status   = r.ReadUInt16();
            r.ReadUInt16(); r.ReadUInt16();
            int playerId = r.ReadUInt16();
            Log($"✓ LoginAccepted: status={status} player_id={playerId}");

            // Set connection id to 1 — confirmed from every C→S packet post-login
            _session.ConnectionId = 1;
            _state = State.Connected;

            // Send the initial "ready" sync (desc=21 body = count of reliable msgs
            // received so far; observed as 40 in the capture; we'll use the count
            // we've actually received and iterate if the server objects).
            SendReadySync();
        }

        // ── Cycle alive / spawn ───────────────────────────────────────────────

        private void OnDesc28(NetMessage msg)
        {
            // 4-word body: two REAL values. Purpose still [?] — log both.
            if (msg.DataLengthWords >= 4)
            {
                var r = msg.Reader();
                float a = r.ReadReal();
                float b = r.ReadReal();
                string hex28 = BitConverter.ToString(msg.Body).Replace("-", "").ToLower();
                Log($"  desc=28 (timer/sync?) REAL_0={a:0.###}  REAL_1={b:0.###}  hex={hex28}");
            }
            else
            {
                LogBody("desc=28", msg);
            }
        }

        private void OnCycleAlive(NetMessage msg)
        {
            // desc=310: body starts with a u16 nNetObject id that appears to be the
            // cycle id. The exact layout is still [?] — we record the id and start
            // turning. NETPLAN item 5.
            if (msg.DataLengthWords >= 1)
            {
                int cycleId = msg.Reader().ReadUInt16();
                if (_myCycleId < 0)
                {
                    _myCycleId = cycleId;
                    Log($"★ Cycle spawned: cycle_id={cycleId}  → State.Playing");
                    _state = State.Playing;
                }
            }
        }

        // ── Outgoing messages ─────────────────────────────────────────────────

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
            // desc=21 (1w body=0, semantics still [?])
            var w21 = new MessageWriter();
            w21.WriteUInt16(0);
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

        private void MaybeSendTurn()
        {
            long now = Tick();
            if (now - _lastTurnTick < TurnIntervalMs) return;
            _lastTurnTick = now;

            // Rotate direction 90° at each turn (left or right randomly)
            bool turnLeft = _rng.Next(2) == 0;
            if (turnLeft)
                _dir = new Vec2(-_dir.Y, _dir.X);
            else
                _dir = new Vec2(_dir.Y, -_dir.X);

            _turns++;
            _gameTime += TurnIntervalMs / 1000f;

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
            Log($"→ desc=321 (turn) mid={mid} dir={_dir} cycle={_myCycleId}");
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
            byte[]? ack = _session.DrainAckPacket();
            if (ack != null)
            {
                Log("→ desc=1  (ack)");
                _link.Send(ack);
            }
        }

        // ── Idle / retry ──────────────────────────────────────────────────────

        private void HandleIdle()
        {
            long now = Tick();
            if (now - _lastSendTick < RetryIntervalMs) return;

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
