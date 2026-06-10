# armagetron-csharp-client

A clean-room, native **C#** client for the Armagetron Advanced 0.2.9 network
protocol — one portable core, many front-ends: flat desktop/mobile via
**MonoGame** (incl. iPhone) and **Quest VR** via **Unity**.

See [ARCHITECTURE.md](ARCHITECTURE.md) for the layered design, the six rules
that keep the core engine-agnostic and Unity-droppable, and the clean-room /
licensing posture.

## Layout

```
Core.Protocol         netstandard2.1, pure C#, zero engine deps
  ├── MessageReader / MessageWriter   u16 words, REAL float, byte-swapped strings
  ├── NetMessage / Packet / StreamCodec   [desc][mid][len words][body] + 2-byte trailer
  └── Game/CycleDestinationSync       decoded cycle turn command
Core.Protocol.Tests   xUnit (net9.0), golden vectors from a real 0.2.9.3.0 capture
```

## Build & test

```bash
dotnet test
```

(Verified with .NET SDK 9. The core targets `netstandard2.1`; the test project
targets `net9.0`.)

## Status

Port of the verified protocol core from the Java prototype, validated against
the same real-capture golden byte vectors (**35 tests green**):
- legacy stream framing (descriptor-first, word-counted, 2-byte trailer),
- the custom `REAL` float format,
- byte-swapped / NUL-terminated / word-padded strings,
- `desc 321` cycle turn command (position, direction, distance, cycle, time, turns).

The protocol findings live in the companion spec:
`JavaDerek/armagetronad` branch `protocol-spec`, `docs/protocol/`.

**Next:** `desc 300` server→client cycle sync (needs the nNetObject envelope),
then the connection handshake, then the first MonoGame front-end.

## License

**TBD.** This is a clean-room reimplementation built from a specification of
facts (protocol spec + packet captures), not from upstream source, and it does
not copy their `.proto` files — so it is not bound by the upstream GPL (see
ARCHITECTURE.md). A license will be chosen before any distribution. *Not legal
advice.*
