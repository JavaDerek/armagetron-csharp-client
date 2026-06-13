# Game.RenderHarness — offscreen render harness

Renders a **scripted** `GameWorld` (no socket, no live server) to an offscreen
`RenderTarget2D` and writes a PNG. This turns a rendering change into an artifact you
(or an agent) can inspect, without opening the game window or needing a human to watch it.

The scene routes through the same pure `SceneBuilder` the desktop client uses
([Core.Protocol/Game/Scene.cs](../Core.Protocol/Game/Scene.cs)), so the PNG is a faithful
render of production geometry — only the *world state* is scripted.

## Run

```
dotnet run --project Game.RenderHarness -- /tmp/aa_render.png
```

Writes an 800×800 RGBA PNG. The default scenario exercises the **death-freeze**: a remote
cycle drives right, turns up (axis-aligned L-corner), then dies at the wall (`alive=false`)
— its trail must stop at the wall and not coast past.

## Why this layer

Most "visual" bugs are geometry/state bugs (e.g. the through-wall ghost was a wrong
`GameWorld.Position`). Those are caught in `SceneBuilderTests` / `GameWorldTests` with no
pixels at all. This harness is the second line: when geometry assertions aren't enough,
render an actual frame and look at it.

> Requires a display/GL context (DesktopGL/SDL). It is a dev tool — it is **not** part of
> the `dotnet test` coverage gate.
