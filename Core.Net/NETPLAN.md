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

## Still unknown — confirm live ❓

1. **Packet trailer (2-byte) semantics.** Observed `0` during the pre-login
   handshake, then a small constant (`1`) once logged in. Hypothesis: a
   connection/sender id assigned at login (modeled as `ReliableSession.ConnectionId`).
   Must confirm: is it constant per connection, a sender id, or a packet/ack seq?
   → capture/inspect server→client trailers and watch it across a longer session.
2. **Login handshake (`desc 11`).** Need the exact `Login` body layout and the
   16-byte token (the server echoes it back in `desc 5` LoginAccepted). Also the
   pre-login sequence seen: `desc 52/53` (server-info poll) → `desc 7` ×N →
   `desc 11` Login → `desc 5` Accepted. What does `desc 7` carry, and which steps
   are mandatory?
3. **How the reliable-id base is established.** The client's first reliable id was
   1746 (a global counter), not 1. Does the server care about the absolute value,
   or only monotonicity/uniqueness? (A bot starting at 1 probably works — verify.)
4. **Resend timing.** When does an unacked reliable message get resent, and how
   often? Needed so the bot is a well-behaved peer (and to not flood).
5. **Spawn.** After login, what makes the server spawn a cycle for us and assign a
   `cycle_id`? (Likely automatic once we're a non-spectator player; the bot must
   present as a player, not a spectator.)

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
