# Live session plan — what the bot needs, and what's still unknown

Goal: a headless bot that connects to a stock 0.2.9.x listen server *as a human
player*, stays in the session, and emits turn commands — proving the whole live
protocol end-to-end. No rendering; even random turns are fine. Staying
**connected** across rounds is the test, not staying alive.

## Resolved from the capture ✅

- **Framing / REAL / strings / cycle command** — done and golden-tested in
  `Core.Protocol`.
- **Message ids:** one monotonic counter per side for *reliable* messages.
  **Acks (descriptor 1) carry message-id 0** and are never acked themselves.
  (Observed: client reliable ids 1746→1747→1748→… ; acks all id 0.)
- **Ack rule:** on receiving a reliable message (id ≠ 0), send a desc-1 Ack whose
  body lists that id (`u16` per id). The server acked each client reliable
  message this way. → implemented in `ReliableSession`.
- **Outgoing encode:** a `CycleDestinationSync` re-encodes byte-identical to a
  captured command, and frames to the exact captured packet. The write path works.

## Confirmed from static capture analysis ✅ (pre-live)

1. **Packet trailer semantics.** C→S trailer is `0x0000` during the pre-login
   handshake and `0x0001` in every post-login packet (5260 packets across the full
   session). S→C trailer is always `0x0000`. It is a constant "connected = 1"
   flag — not a server-assigned id. `ReliableSession.ConnectionId` is set to 1 on
   `LoginAccepted`. `[?]→✅`

2. **Login handshake (`desc 11`) body layout** (36 bytes = 18 words, confirmed
   across 13 Login messages from 4 separate pcap files):
   ```
   u16  0x0040        protocol version = 64 (constant)
   u16  0x0000
   u16  0x0000
   u16  0x0000
   u16  prev_player_id   0 for a new bot; reconnecting clients send their last id
   u16  0x0000
   String  player_name   AA byte-swapped, length-prefixed (e.g. len=4 for "Bot")
   bytes[16]  nonce      random; server echoes back verbatim in desc=5
   ```
   Pre-login sequence: `desc=52` (poll) → `desc=50` (version reply) → `desc=53`
   (info request) → `desc=51` (info reply, 72w) → `desc=7` (body=`0000`) × 4
   → `desc=11` (Login). All pre-login messages use mid=0 (unreliable). `[?]→✅`

3. **Reliable-id base.** The capture client used a persistent counter (1746…);
   the server accepted any value as long as it's monotonically increasing.
   A bot starting at `1` is expected to work — confirm live. `[?]→partial`

4. **Resend timing.** The real client retransmits by including each reliable
   message **twice in the same UDP datagram** (`[msg][ack-if-any][msg]`), not by
   sending a separate packet later. This "redundant transmission" pattern appeared
   on every post-login C→S reliable message in the capture. The bot implements
   `SendReliableDouble()` matching this pattern. `[?]→✅`

5. **Spawn trigger.** Server sends `desc=310` (CycleAlive) after login; the bot
   reads the cycle_id from the first u16 of its body and transitions to
   `State.Playing`. Need to confirm the body layout live. `[?]→partial`

## Fully confirmed live (2026-06-10) ✅ — all 5 original unknowns resolved

3b. **Reliable-id base** ✅ Server accepts client starting at id=1 (no complaint,
    full 45-second session with turns acked).

5b. **desc=310 CycleAlive body** ✅ First u16 is the cycle_id (confirmed: id=5,
    same id echoed in desc=321 CycleDestinationSync; server acked turns with that
    id throughout the session).

6.  **desc=21 body** ✅ Body=0 accepted without disconnect; the real client used
    body=40 but the server does not validate the value.

7.  **desc=28 (S→C, 4w)** ✅ Two REAL values — usually `(1.0, 0.0)`, occasionally
    `(4.0, 0.0)` at round boundaries. The second REAL is always ≈0.  Purpose still
    unknown (possibly a round-score or rubber counter), but the bot just acks it.

    **desc=25 (C→S, 0w)** ✅ "I want to play" / ready signal. Without it (plus
    the desc=28 below) the server parks the client as spectator and never calls
    desc=310.

    **desc=28 (C→S, 4w, REAL_0=40.0, REAL_1=0.0)** ✅ Initial cycle-state sync.
    Sending desc=25 + desc=28(40.0,0.0) right after desc=21 triggers:
      `desc=20 (empty)` → `desc=1 ack(desc=21,25,28)` → `desc=220 (cycle-create)`
      → `desc=310 (cycle-alive, cycle_id=5)` — spawn in < 100ms.

## Additional descriptors observed live (new knowledge)

- **desc=220** (S→C): cycle-create; body starts with nNetObject class id and then
  cycle_id at u16 offset 5 (len 15w or 18w depending on version).
- **desc=24** (S→C, 2w or 5w): cycle wall/position sync from server, appears
  at ≈ 1 Hz per active cycle after spawn; bot just acks it.
- **desc=210** (S→C, 6w): appeared once right after spawn; body unknown.
- **desc=330/331** (S→C, 22-24w): zone definitions; appear at round start.
- **desc=8** (S→C, 16-45w): MOTD / round-start announcement.
  Contains byte-swapped AA strings with embedded 0xRRGGBB color codes.
  Appears at each round start: "Go (round N of 1)".
- **desc=9** (S→C, 2w): round-end signal; appears ≈15 s before next "Go" message.

## Live session proof (2026-06-10)

Bot `AaBot` connected to 192.168.68.61:4534 (0.2.9.3.0 listen server):
- Login accepted in < 20 ms
- Cycle spawned (cycle_id=5) in < 200 ms after desc=25+28 sent
- 26 desc=321 turn commands sent over 45 s, every one acked by the server
- Session survived 2+ full rounds (desc=9 round-end × 2, "Go" round N × 2)
- No disconnection, no retransmission failures

## Live bring-up steps (when a local listen server is running)

1. **Send the pre-login + Login**, get `desc 5` LoginAccepted → learn the trailer
   and any assigned ids. (Iterate `desc 11` layout until accepted.)
2. **Maintain the session:** ack every reliable message, answer keepalives, keep
   the trailer the server expects. Confirm we stay connected for minutes.
3. **Spawn + send turns:** emit `CycleDestinationSync` (desc 321) on a timer
   (random direction). Watch them appear on a human's client.
4. Promote each ❓ above to ✅ in the protocol spec as the live runs confirm it.

## Test harness role

Once it connects, the bot is a permanent integration test: "does our stack still
complete a real session against a stock server?" — the strongest regression guard
we can have.
