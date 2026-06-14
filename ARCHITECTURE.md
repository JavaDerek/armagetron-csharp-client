# Architecture

A clean-room, native **C#** client for the Armagetron Advanced 0.2.9 network
protocol — built to talk to stock C++ servers, and structured so a single
portable core powers every front-end: flat desktop/mobile (MonoGame) **and**
Quest VR (Unity).

## Why C#

C# is the one language native to *both* of our intended front-ends:

- **MonoGame** (MIT, FOSS) — desktop (Win/macOS/Linux), Android, **iOS**.
- **Unity** — the practical engine for **Meta Quest VR** (Meta XR / OpenXR).

Java was a fine first cut, but the JVM has no real iOS story and cannot feed
Unity. C# reaches iPhone *and* lets the exact same core compile inside Unity.

## Layered design

```
Core.Protocol   (netstandard2.1, pure C#)  framing, REAL float, codec, message decoders   [shared by ALL]
Core.Net        (netstandard2.1, pure C#)  UDP link + reliable session + protocol state machine
ArmaLib         (netstandard2.1, pure C#)  beginner-friendly SDK facade over the two above
  ├── Client.MonoGame   flat client: desktop / Android / iPhone (1st & 3rd person)
  ├── Client.Unity      Quest VR: first-person, stereo (references the SAME core)
  └── (server / bot / tools, headless)
```

The hard-won, language-neutral knowledge (the protocol) is implemented **once**
in the core. Each presentation is a thin shell on top.

### ArmaLib — the front-end SDK facade

Front-ends should never touch a protocol primitive. **ArmaLib** (`Armagetron.Lib`)
is the single seam they program against, so the desktop, Android, universal, and
Oculus clients all compose on top of one foundation instead of re-implementing
protocol handling:

- **In:** `Connect(host, port, name)`, `TurnLeft()`, `TurnRight()`, `Disconnect()`.
- **Out:** `Snapshot()` (render-ready cycles, already dead-reckoned), `MyCycleId`,
  and events `Spawned`, `Died`, `RoundStarted`, `RoundEnded`, `CyclesChanged`.
- **Hidden below the line:** descriptor IDs, REAL encoding, netobject-id reservation,
  the desc=311 priming sequence, the desc=201 registration race + fresh-socket retry,
  and the background session-loop thread.

`ArmaClient` owns the I/O lifecycle (verified by the live-server gate, like `UdpLink`);
the pure event-derivation it sits on — `GameEventTracker` — is fully unit-tested.

## The six rules that keep the core "Unity-droppable"

Enforced from commit one. They cost nothing for protocol code and are simply
good design:

1. **Core targets `netstandard2.1`.** Lowest common denominator that both
   MonoGame and Unity (Mono/IL2CPP) consume cleanly. (Tests may target a
   current `net*` TFM — they never ship inside a front-end.)
2. **Zero engine/framework dependencies in the core.** Base class library only.
   No `MonoGame.*`, no `UnityEngine.*`, no third-party NuGet that isn't
   IL2CPP-safe.
3. **Pure data in, data out.** `byte[]` / spans → plain structs. UDP sockets,
   threading, and the frame loop live in per-engine adapters, because Unity
   owns its own loop.
4. **Our own neutral value types** (e.g. `Vec2`). Never `UnityEngine.Vector3`
   or MonoGame `Vector2` in the core; map to those in each front-end.
5. **Conservative language/runtime surface.** `LangVersion 9`; no reflection /
   `dynamic` / runtime codegen (IL2CPP strips it); avoid APIs newer than
   `netstandard2.1` (e.g. use `Math.Pow`, not `Math.ScaleB`; build Latin-1
   strings by code point, not `Encoding.Latin1`).
6. **Allocation-light and deterministic.** Good for IL2CPP and essential for
   Quest's 90 fps budget.

When the VR phase arrives, Unity ingests `Core.Protocol` / `Core.Game` either as
source or as a `netstandard2.1` DLL in `Plugins/`, and adds a VR rig on top.

## Clean-room / licensing posture

Armagetron Advanced is GPL-2.0-or-later. A network protocol / wire format is
functional and not copyrightable, so a from-scratch reimplementation built from
a **specification of facts** (our protocol spec + packet captures) is not a
derivative work and is not bound by their GPL. To keep that claim solid:

- **Do NOT copy their source files** — notably the `.proto` schemas. Re-express
  field layouts in our own code/spec. (The Java prototype copied the `.proto`s;
  this C# port deliberately does not.)
- Implement from the documented facts (the spec, captured bytes), not by
  transcribing their `.cpp` line-by-line. Algorithms (e.g. the REAL float math)
  are methods of operation and not themselves copyrightable.

This is general engineering reasoning, **not legal advice** — get a real opinion
before App Store / Quest Store distribution or any monetization.

## Target matrix

| Target | Engine | Status |
|--------|--------|--------|
| Desktop (Win/macOS/Linux) | MonoGame | planned (after core) |
| Android | MonoGame | planned |
| iPhone / iPad | MonoGame | planned (needs Xcode + Apple acct) |
| Meta Quest VR | Unity | future (first-person VR) |
| Protocol core | — | **in progress** (this repo) |
