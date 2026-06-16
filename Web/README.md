# Web (HTML5) clients

Two ways to put Armagetron in a browser, both reusing the engine-neutral C# core. The shared
problem: **browsers can't open raw UDP sockets**, but the Armagetron `0.2.9.x` server speaks only
UDP. Each approach solves that differently.

```
                       ┌─────────────────────────────────────────────────────────┐
   APPROACH A          │  browser <canvas>  ──ws (Scene JSON)──►  Web.Host (C#)    │
   server-rendered     │                    ◄──turns────────────  runs ArmaClient  │──udp──► server
   (WORKS TODAY)       └─────────────────────────────────────────────────────────┘

                       ┌──────────────────────────────────────────────────────────────────┐
   APPROACH B          │  browser: Blazor WASM (Core.Protocol + WebArmaClient)              │
   pure in-browser     │     └─ WebSocketUdpLink ──ws (raw datagrams)──► WsRelay (C#) ──udp─┼─► server
   (FOUNDATION READY)  └──────────────────────────────────────────────────────────────────┘
```

## Approach A — `Web.Host` (server-rendered) — the working client

The browser is a thin `<canvas>` (`wwwroot/index.html` + `game.js`). The **game session runs
server-side**: per WebSocket connection `Web.Host` runs a real `UiArmaClient`/`ArmaClient` (normal
UDP), builds the **same pure `Scene`** every other head renders (`SceneBuilder` in Core.Protocol),
and streams it as compact JSON frames; the canvas paints lines + cycle heads and sends turns back.

Works in **any** browser — no WASM, no special headers, no threading caveats. Zero game logic in JS.

```bash
dotnet run --project Web/Web.Host          # listens on http://0.0.0.0:8080 (ARMA_WEB_URL to change)
# then open: http://localhost:8080/?host=192.168.68.61&port=4534&name=Vlad
```

**Verified live** (`--selfcheck`): connected to `192.168.68.61:4534`, reached `status=Connected`
with 8 wall segments + 3 cycle heads (the live players) — real geometry reaching a browser-style
WS client headlessly. The session now joins as a **spectator** a second or two after the socket
opens (it no longer blocks on our own cycle spawn; see the join-at-joinable fix in ArmaLib), so
the live arena streams immediately and our bike appears at the next round. The only unverified bit
is the in-page canvas paint (~80 lines of standard `game.js`); the data + frame path is proven.

Open work: the full nine-slice HUD/menus (it currently auto-connects and renders gameplay + a status
line); designer sprite art on the canvas; the in-app connect form.

## Approach B — pure in-browser (Blazor WASM) — foundation built & verified

Run the **entire** C# client in the browser via Blazor WebAssembly, reusing Core.Protocol + ArmaLib
unchanged, with only the transport swapped. The pieces for this are built and **verified against the
live server**:

- **`Web.Shared/WebSocketUdpLink.cs`** — `IUdpLink` over `ClientWebSocket` (one WS binary message ==
  one datagram). The single seam that frees the rest of the stack from raw sockets.
- **`Web.Shared/WebArmaClient.cs`** — `ArmaClient` with `CreateLink` overridden to the WS link.
- **`WsRelay`** — cross-platform (Kestrel) `ws://relay/?host=&port=` → dedicated UDP socket per
  connection, pumping both ways.
- **`WebProbe`** — headless end-to-end test. **Verified:** `WebProbe → WsRelay → 192.168.68.61:4534`
  reached `Status=Connected` (MyCycleId assigned), login accepted, turns accepted → PASS.

```bash
dotnet run --project Web/WsRelay                                   # ws://0.0.0.0:8765
dotnet run --project Web/WebProbe -- ws://localhost:8765/ 192.168.68.61 4534 Vlad   # prints PASS/FAIL
```

What remains to ship the actual Blazor page:
1. **WASM threading.** `ArmaClient` uses an OS thread for the session loop + a blocking connect, so
   the Blazor app must build with `<WasmEnableThreads>true</WasmEnableThreads>` (needs the
   `wasm-tools` workload — `sudo dotnet workload install wasm-tools` — and the page must be served
   **cross-origin isolated**, i.e. with `COOP: same-origin` + `COEP: require-corp` headers). The
   alternative is an async, threadless session driver over the same `IUdpLink` — a Core refactor.
2. A Blazor `Game.Web` project: `WebArmaClient(relayUrl)` → `UiArmaClient(client)` → the existing
   shell, with a canvas renderer (the same Scene-to-canvas mapping as `Web.Host/wwwroot/game.js`).
3. Serve with the COOP/COEP headers (a `staticwebapp.config.json` or dev-server middleware).

Approach A is the pragmatic "play it now" client; Approach B is the "no server-side session" client
once the threading/headers are sorted. Both share the canvas renderer and the Scene model.

## Not in `ArmagetronClient.sln`

These projects are kept out of the main solution so `build-and-test.sh` needs no web/WASM workloads.
Build/run them explicitly as above. `Web.Shared`, `WsRelay`, `WebProbe`, and `Web.Host` all build
with the plain .NET 9 SDK (ASP.NET Core runtime is already present).
