# ArmaLib — a C# SDK for Armagetron Advanced

`DotNetDevotee.ArmaLib` is a small, beginner-friendly .NET SDK for connecting to
[Armagetron Advanced](https://www.armagetronad.org/) `0.2.9.x` game servers. It hides the wire
protocol (UDP framing, REAL number encoding, netobject-id reservation, the desc=311 priming
sequence, dead-reckoning) behind one class so you can join a game, steer a lightcycle, and read the
world in a few lines. Engine-agnostic (`netstandard2.1`) — it powers desktop (MonoGame), Android,
web, and VR front-ends from the same code.

This package is self-contained: it bundles its `Armagetron.Protocol` and `Armagetron.Net`
dependencies, so there is nothing else to install.

## Install

```
dotnet add package DotNetDevotee.ArmaLib
```

## Quick start

```csharp
using Armagetron.Lib;

using var client = new ArmaClient();
client.Spawned += (_, e) => Console.WriteLine($"cycle {e.CycleId} spawned (mine: {e.IsMine})");

// Connects, registers, and returns once the arena is joinable.
if (client.Connect("server.host", 4534, "MyName"))
{
    client.TurnLeft();
    client.TurnRight();

    // Render-ready snapshot of every cycle (head position, heading, trail), dead-reckoned to now.
    foreach (var cycle in client.Snapshot())
        Console.WriteLine($"{cycle.CycleId}: {cycle.Position.X:0.#},{cycle.Position.Y:0.#}");
}
client.Disconnect();
```

Events: `Spawned`, `Died`, `RoundStarted`, `RoundEnded`, `CyclesChanged`. For a non-blocking,
observable connection (UI apps), see `UiArmaClient`.

## Notes / current limitations

- Targets the Armagetron Advanced `0.2.9.x` protocol.
- Server name gate: some servers reject brand-new player names on first registration (a server-side
  trust check, not a client bug). Established names register reliably.
- Built clean-room from the wire protocol; contains no Armagetron Advanced (GPL) source.

## License

MIT — see the repository.
Source: https://gitlab.com/JavaDerek/armagetron-csharp-client
