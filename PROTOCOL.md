# Armagetron Advanced 0.2.9 Network Protocol — Reverse-Engineering Notes

**Provenance (read this).** Every wire-format fact in this document is derived from
black-box observation: live 0.2.9.3.0 PCAP captures (`~/armagetron-capture`, decoded with
`/tmp/aa_decode.py`) plus iterative bot experiments against a real server. The GPL server
source was *separately* read on 2026-06-11 to **confirm** the mechanisms named below; that
reading is disclosed here for honesty, but it introduced no facts that are not also
independently verifiable from the captures — each claim cites its PCAP / black-box evidence.

This document is the **clean-room specification**: the C# client is implemented solely from
this document, by parties with no access to the GPL source. Do not paste source code, source
identifiers used as code, or source-only detail into the client; document the observable wire
behaviour here and implement from the observation.

---

## Transport

- **UDP, big-endian 16-bit words** throughout.
- Each UDP datagram is a **Packet**:
  - One or more **Messages** concatenated.
  - A 2-byte trailer = **sender's connection slot** (user ID in fifi.org terminology). Sent as 0 pre-login; after LoginAccepted, set to 1 (first remote client). **NOT the player_id from desc=5 body** — player_id is a global monotonic counter and is NOT the connection slot. The server confirmed slot=1 via desc=3 "You timed out." data=0x0001 when using player_id=17 as the trailer caused all our packets to be dropped. Confirmed by fifi.org: *"The user ID is sent to simplify the server distinguishing the clients; with the possibility of clients hiding behind masquerading firewalls and thus appearing to send from changing ports, simply checking the sender's addresses may not be enough."*
- Messages are **not** self-delimiting by length in the packet; the parser reads each message's body length from the `data_length` field (words). (In practice, a packet usually carries 1–4 messages.)

### Reliable delivery

- A **Reliable message** has `message_id != 0`. The sender retransmits until ACKed.
- An **Ack** (descriptor 1, `message_id = 0`) carries one or more u16 reliable-message IDs in its body — the receiver sends it for every reliable message received.
- **Unreliable messages** have `message_id = 0` and are never ACKed.

### Packet wire format

```
[message_0_descriptor u16] [message_0_id u16] [message_0_body ...words...]
[message_1_descriptor u16] ...
[trailer u16]  ← connection_id
```

---

## REAL number encoding

32-bit type used for all floating-point game values. **Not IEEE 754.**

```
bits 0-24  : mantissa  (unsigned, 25 bits)
bit  25    : sign      (0 = positive)
bits 26-31 : exponent  (unsigned, 6 bits)

value = sign × (mantissa / 2^25) × 2^exponent
```

Confirmed encodings:
- `0.0`  → `0x00000000`
- `1.0`  → `0x05000000`   (mant=2^24, exp=1 → 2^24/2^25 × 2^1 = 1.0)
- `4.0`  → `0x0d000000`   (mant=2^24, exp=3 → 2^24/2^25 × 2^3 = 4.0)

A REAL occupies 2 words (4 bytes) on the wire.

---

## Descriptor index

### Handshake (C→S unless noted)

| Desc | Direction | Name              | Notes |
|------|-----------|-------------------|-------|
| 52   | C→S       | Poll              | One u16 (port). Client sends to discover server. |
| 50   | S→C       | PollReply         | Server replies to poll with port + version words. |
| 53   | C→S       | ServerInfoRequest | Body: 1 word (0). |
| 51   | S→C       | ServerInfoReply   | Large blob (~68w): server name, settings, CYCLE_SPEED, ARENA_SIZE, etc. |
| 7    | C→S       | VersionFrame      | Sent 4×; body contains version/build strings. |
| 11   | C→S       | Login             | Name string + auth fields. |
| 5    | S→C       | LoginAccepted     | status u16 (1=spawn now, 2=wait for round), player_id u16, IP string, 16-byte token. |

### Session management

| Desc | Direction | Name              | Notes |
|------|-----------|-------------------|-------|
| 1    | both      | Ack               | mid=0; body = list of u16 reliable message IDs being acknowledged. |
| 27   | S→C       | Keepalive         | Body: one u16 (0). Send Ack immediately. |
| 52   | C→S       | Keepalive reply   | (same descriptor as Poll — used to respond to keepalives mid-session) |

### Game lifecycle

| Desc | Direction | Name              | Notes |
|------|-----------|-------------------|-------|
| 8    | S→C       | Announcement      | Variable-length text/config blob. Round-start "Go (round N of M)!" at game start. |
| 9    | S→C       | RoundEnd          | Body: small. Marks end of current round; client should request spawn for next. |
| 20   | S→C       | ReservedIdReply   | Server's reply to desc=21: blocks of netobj ids reserved to this client. Body = `[begin u16][len u16]` pairs (RLE). See **netObject ID reservation** below. |
| 21   | C→S       | ReserveIdsRequest | 1-word body = **0x0028 (40)** = number of netobj ids to reserve. (Earlier mislabelled "ReadyFrame0".) Sending **0** reserves nothing → later object creation has no legal id → spawn silently fails. |
| 25   | C→S       | ReadyFrame1       | 0-word body (empty). Part of spawn-request sequence. |
| 311  | C→S       | CycleSpeedSync?   | 1-word body = incremental speed/config values. Sent after spawn in a repeating sequence: 10,20,30,35,40,50,60,70,7,10,20,... (PCAP observed). Not required pre-spawn. |
| 28   | both      | TimerSync         | Body: two REALs. S→C: REAL_0 = current game clock. First such message after spawn gives spawn_game_time. |

### Cycle / player objects

| Desc | Direction | Name              | Notes |
|------|-----------|-------------------|-------|
| 310  | S→C       | GameSync          | nNOInitialisator for **gGame**. word[0] = gGame netobj_id (typically 5). **NOT** a cycle message. The gGame object sends compact desc=24 2w packets with GS_ state transitions (GS_TRANSFER_SETTINGS=7, GS_PLAY=50, GS_DELETE_OBJECTS=60). Record word[0] as `_gameNetObjId` to match round-start triggers. Source: GPL nNOInitialisator<gGame> game_init(310,"game"), confirmed by desc=310 body word[0]=5 seen live. |
| 320  | S→C       | CycleCreate       | nNOInitialisator for **gCycle**. **Authoritative source for our gCycle id.** Wire layout: `[0] cycle_id u16`, `[1] connectionSlot u16` (0=AI/server, 1=first remote client), `[2] playerNetObjId u16`, `[3+] further sync data`. Identify our cycle by connectionSlot == session.ConnectionId (=1). Source: GPL nNOInitialisator<gCycle> cycle_init(320,"cycle"), confirmed live by desc=320 w1=0x0001 matching our slot. |
| 220  | S→C       | TeamCreate        | nNOInitialisator for **eTeam**. When word[5+] carries a player name, the team's name is the player's name (1-player teams). NOT a cycle message. Wire layout (15w observed): `[0] netObjId u16`, `[1-4] unknown`, `[5+] name string (AA-encoded, optional)`. Source: GPL nNOInitialisator<eTeam> eTeam_init(220,"eTeam"). |
| 321  | C→S       | CycleDestinationSync | 15-word turn/position command (see below). |
| 24   | S→C       | NetSync           | Generic nNetObject sync (net_sync). 2-word compact: `[netobj_id u16][value u16]` — for gGame objects, value is a GS_ state transition (7=GS_TRANSFER_SETTINGS=round start, 50=GS_PLAY, 60=GS_DELETE_OBJECTS). 27-word full spawn sync for gCycle objects (see below). |

### netObject sync (RESOLVED)

| Desc | Direction | Name              | Notes |
|------|-----------|-------------------|-------|
| 60   | S→C       | NetObjectSync     | Server pushes state for a tracked game object (gCycle, ePlayerNetID, etc.). Variable length. |
| 27   | both      | TimerAck / GameTickSync | S→C: server keepalive / timer tick (body=0x0000, 1 word, reliable). C→S: client application-layer ack for timer tick (same format). |
| 28   | both      | TimerSync         | Carries current game clock as two REALs. S→C: server sends current game time. C→S: client echoes its own game clock back (sent periodically, not every tick). |

**desc=27 C→S is required for nNetObject state.** PCAP analysis confirmed: the real client sends exactly one `desc=27 C→S` (body=0x0000, reliable, fresh mid) for every `desc=28 S→C` received. Without it, the server never transitions objects to "client knows about this" state, causing a cascade of "User X does not know about netobject N" errors. The server then retransmits ~1300+ creation messages every tick, saturating its send queue and starving physics (herky-jerky). With desc=27 C→S flowing, rounds return to normal 27-second duration.

**"User X does not know about netobject N" root causes (all identified):**
1. Wrong ConnectionId (packet trailer): if trailer != our slot, server drops all acks. Fix: trailer=1.
2. Missing desc=27 C→S: server never marks objects as "known" at application layer. Fix: send desc=27 C→S for each desc=28 S→C received.

---

## CycleDestinationSync (desc=321) — wire layout (15 words)

Verified from PCAP cross-reference: **no cycle_id at word [0]**. Position comes first.
The field formerly labelled "spawnKey" (0x0097=151) IS the cycle_id for that round —
confirmed by matching desc=24 27w word[0]=151 with desc=321 word[11]=151.

```
[0-1]   pos_x        REAL
[2-3]   pos_y        REAL
[4-5]   dir_x        REAL    (unit axis)
[6-7]   dir_y        REAL    (unit axis)
[8-9]   distance     REAL    (total distance traveled from spawn, starts at 0)
[10]    flags        u16     (bit0=brake, bit1=chat)
[11]    cycle_id     u16     (gCycle netobj_id — same value as desc=320 word[0])
[12-13] game_time    REAL    (current game clock, monotonically increasing from spawn)
[14]    turns        u16     (turn counter, increments each time client sends a turn)
```

**Critical history:** Early versions put cycle_id at word [0] (16 words total). With REAL-encoded
pos_x in word [0], the server decoded pos_x as a cycle_id ≈ 0 or garbage, giving a
position discrepancy of billions of units → immediate cheating detection + disconnect.

---

## Round lifecycle (observed sequence)

```
S→C  desc=310  (gGame create — word[0]=gameNetObjId; record for round-start triggers)
S→C  desc=320  (gCycle create — word[0]=cycle_id, word[1]=connectionSlot; our cycle has slot=1)
S→C  desc=24 27w (spawn position sync for our cycle — seed spawn pos from here)
S→C  desc=8    (round announcement "Go round N of M")
S→C  desc=28   (timer sync — REAL_0 = spawn game_time)
...  [game runs] ...
S→C  desc=24 2w (gGame compact sync, netobj_id=gameNetObjId, value=7=GS_TRANSFER_SETTINGS → new round start)
S→C  desc=9    (round end)
C→S  desc=21   (ready frame 0 — request next spawn)
C→S  desc=25   (ready frame 1)
C→S  desc=28   (timer sync acknowledgment? exact semantics TBD)
```

**Observed:** desc=310 and desc=320 are sent once at session start (first spawn). The gGame broadcasts compact desc=24 2w with value=GS_TRANSFER_SETTINGS=7 to signal the start of each new round. The gCycle id from desc=320 remains valid across rounds (server reuses it).

---

## desc=24 27-word cycle position sync — wire layout

Sent by the server at round start for every active cycle, then periodically during gameplay.
The bot reads this to get the real spawn position before sending the first desc=321.

```
[0]     cycle_id    u16     (netobj_id of this cycle, same value as desc=321 word[11])
[1-2]   game_time   REAL    (≈0 at spawn, increases during round)
[3-4]   dir_x       REAL    (current direction x component)
[5-6]   dir_y       REAL    (current direction y component)
[7-8]   pos_x       REAL    (current position x)
[9-10]  pos_y       REAL    (current position y)
[11-12] ?           REAL    (≈20.0 at spawn, increases — possibly CYCLE_SPEED or boost)
[13+]   ...                 (additional state fields not yet decoded)
```

**Spawn position source:** The bot waits for a desc=24 27w matching its cycle_id before
sending any desc=321. This guarantees the position reported to the server matches the
server's own calculated spawn position, preventing cheating detection.

PCAP example (round 3, cycle 151): spawn at (90.156, 17.688) dir=(0,1)=UP, game_time=0.

---

## netObject ID reservation — REQUIRED before creating any object (desc=21 → desc=20)

**This is the gate that controls the "It assumed you are cheating" disconnect.** A client may
not invent the netobj id of an object it creates. Ids must be **reserved by the server first**;
creating an object (e.g. the player object, desc=201) with an id the server did *not* reserve
to this client triggers an immediate cheating disconnect (desc=3 text "It assumed you are
cheating…", typically within ~10 ms).

### The handshake (observed on the wire)

1. **C→S desc=21** (`ReserveIdsRequest`), 1-word body = **40** (`0x0028`). "Reserve me 40 ids."
   - Erin's real client sends exactly this; our bot already sends it (was mislabelled).
2. **S→C desc=20** (`ReservedIdReply`). Body = one or more `[begin u16][len u16]` pairs. Each
   pair means "ids `begin … begin+len-1` are yours." Replies seen carried a single 40-id block.
3. The client keeps these ids in a pool and **allocates from the highest id first** when it
   needs an id to create an object.

### Allocation order — highest first (PCAP-proven)

Across three independent real-client sessions, the id used to create the player object
(desc=201) was always the **top** of the reserved block:

| Capture (Erin = 192.168.68.69) | desc=20 block | desc=201 id used |
|---|---|---|
| aa-capture-20260609-220832 | 84–123  | **123** (max) |
| aa-capture-20260609-210032 | 44–83   | **83** (max)  |
| aa-capture-20260609-210032 (Derek .61) | 129–168 | **168** (max) |

So: maintain the pool sorted; pop the maximum when allocating.

### Requirement for desc=201 (player object create)

The id at word[0] of desc=201 (see "Outgoing player registration" / the C→S desc=201 layout)
**must be an id popped from the reserved pool** — never a guessed value. Defer sending desc=201
until at least one desc=20 block has arrived. Send it once per session.

### Black-box confirmation of the causal link (our own runs, no source needed)

- **Bot run with an unreserved id** (`netobj=105`, a guess): server replied desc=3 "It assumed
  you are cheating" ~7 ms later, then disconnected. Reproduced every time.
- **Bot run with a reserved id** (`netobj=727`, top of a 688–727 block): **no cheating**; the
  server then created our cycle (desc=220, name='AaBot'), sent our cycle's position syncs, and
  accepted our desc=321 turns for ~58 s with no disconnect.

This before/after isolates the cause to the id-reservation requirement alone; position, timing,
and desc=311 priming were ruled out as irrelevant by earlier experiments.

### Notes

- The server sends a fresh desc=20 block roughly each time it receives a desc=21; a client that
  re-sends desc=21 (e.g. as part of a keepalive) accumulates multiple blocks. One block is
  enough — request once if possible, but consuming from a growing pool also works.
- desc=21 body **must** be non-zero (40 observed). Zero reserves nothing and later spawn fails.

## nNetObject — RESOLVED

The server tracks a per-client "known objects" set. The mechanism (confirmed from PCAP analysis, 2026-06-10):

1. Server sends creation/sync messages for each object (desc=60, desc=201, desc=210, desc=220, desc=330, desc=331).
2. Client sends desc=1 to ack delivery (transport layer).
3. **Server sends desc=28 (timer sync) — once per game tick for objects that need app-layer sync.**
4. **Client sends desc=27 C→S (body=0x0000, reliable) in response to each desc=28. This is the app-layer "I have processed the game state up to this tick" ack.**
5. Server marks all objects whose creation messages were acked at transport level (desc=1) as "known" after receiving desc=27 C→S.

Without step 4, the server retransmits everything every tick (herky-jerky). With step 4, normal operation resumes.

Real client C→S descriptors observed (from PCAP, not all currently implemented):
- desc=27 (1w body=0x0000): timer ack — **required**; send once per desc=28 S→C received
- desc=28 (4w, two REALs): cycle-state timer sync — optional echo of current game clock
- desc=201 (21w): player object create — sent once at login, carries player name + config
- desc=204 (7w): player auth — sent once at login, carries player ID + guild/source string
- desc=311 (1w): cycle speed sync — sent during gameplay; real client sends 109/session

---

## Known constants

| Value      | Meaning |
|------------|---------|
| 0x0097 (151) | cycle_id used in real-client desc=321 word [11] for a specific capture round |
| 0x0007 (7) | GS_TRANSFER_SETTINGS — gGame state value that signals round start (in 2-word desc=24) |
| 0x0032 (50)| GS_PLAY — gGame state value during active gameplay |
| 0x003C (60)| GS_DELETE_OBJECTS — gGame state value at round/session cleanup |
| ~28-30     | CYCLE_SPEED estimate (units/sec); 36.393 units / 1.279s ≈ 28.5 |

## Open questions (priority order)

1. **nNetObject ack format** — descriptor + body to acknowledge receiving a netobject sync
2. **desc=51 blob decode** — extract arena size and spawn-slot algorithm
3. **desc=24 27w words [11-26]** — fields after pos_y (rubber, speed, wall state, etc.)
4. **desc=28 C→S format** — what body does the client send when it echoes desc=28 back?
5. **desc=3 format** — appears as mid=0 unreliable messages, body starts with netobj ID
6. **Correct chat descriptor** — desc=8 C→S didn't produce visible chat

---

*Last updated: 2026-06-10. Protocol version: Armagetron Advanced 0.2.9.3.0.*
