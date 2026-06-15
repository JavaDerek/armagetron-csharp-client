using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Armagetron.Game;       // SceneBuilder, Scene, ArenaView, CyclePalette, RenderSegment, RenderRect, RenderColor
using Armagetron.Game.UI;    // ConnectionStatus
using Armagetron.Lib;        // UiArmaClient
using Armagetron.Protocol;   // CycleSnapshot

// Server-rendered HTML5 client. Serves a thin <canvas> page and, per WebSocket connection, runs a
// real game session and streams the pure Scene (the same SceneBuilder output every other head
// draws) as JSON frames; the browser paints them and sends turns back.
//
// Usage:  dotnet run --project Web/Web.Host [--urls http://0.0.0.0:8080] [--selfcheck]
//         Browser: http://localhost:8080/?host=192.168.68.61&port=4534&name=Vlad

const int ViewSize = 720;            // canvas is ViewSize×ViewSize; Scene coords are already screen px
const float ArenaSize = 176.78f;
const int Fps = 30;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
var app = builder.Build();

app.Urls.Clear();
app.Urls.Add(Environment.GetEnvironmentVariable("ARMA_WEB_URL") ?? "http://0.0.0.0:8080");

app.UseWebSockets();
app.UseDefaultFiles();   // serve wwwroot/index.html at /
app.UseStaticFiles();

app.MapGet("/healthz", () => "ok");

app.Map("/play", async (HttpContext ctx, ILogger<Program> log) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }

    string host = ctx.Request.Query["host"].ToString();
    if (string.IsNullOrEmpty(host)) host = "192.168.68.61";
    int port = int.TryParse(ctx.Request.Query["port"], out int p) ? p : 4534;
    string name = ctx.Request.Query["name"].ToString();
    if (string.IsNullOrEmpty(name)) name = "Vlad";

    using WebSocket ws = await ctx.WebSockets.AcceptWebSocketAsync();
    log.LogInformation("play session -> {Host}:{Port} as {Name}", host, port, name);
    await RunSession(ws, host, port, name, ViewSize, ArenaSize, Fps, ctx.RequestAborted);
    log.LogInformation("play session closed");
});

// --selfcheck: start Kestrel, connect a WS client to our own /play against a live server, read a
// few frames, print a summary, and exit — headless verification that real geometry reaches a browser.
if (args.Contains("--selfcheck"))
{
    await app.StartAsync();
    string baseUrl = app.Urls.First().Replace("http://0.0.0.0", "http://localhost");
    int rc = await SelfCheck(baseUrl);
    await app.StopAsync();
    return rc;
}

app.Run();
return 0;

// ── session ─────────────────────────────────────────────────────────────────────────────────
static async Task RunSession(WebSocket ws, string host, int port, string name,
                             int viewSize, float arenaSize, int fps, CancellationToken ct)
{
    using var client = new UiArmaClient();
    client.BeginConnect(host, port, name);

    var view = new ArenaView(arenaSize, 10f, viewSize);
    var palette = new CyclePalette();
    var inputs = Task.Run(() => ReadInputs(ws, client, ct), ct);

    int delay = 1000 / Math.Max(1, fps);
    var opts = new JsonSerializerOptions();
    try
    {
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            CycleSnapshot[] snap = client.Snapshot();
            Scene scene = SceneBuilder.Build(snap, client.MyCycleId, view, palette);
            string json = JsonSerializer.Serialize(Frame.From(scene, client.Status, client.MyCycleId, viewSize), opts);
            await ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
            await Task.Delay(delay, ct);
        }
    }
    catch (OperationCanceledException) { /* client gone */ }
    catch (WebSocketException) { /* client gone */ }
    finally { try { await inputs; } catch { /* ignore */ } }
}

static async Task ReadInputs(WebSocket ws, UiArmaClient client, CancellationToken ct)
{
    var buf = new byte[256];
    while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
    {
        WebSocketReceiveResult r;
        try { r = await ws.ReceiveAsync(buf, ct); }
        catch { return; }
        if (r.MessageType == WebSocketMessageType.Close) return;

        foreach (char c in Encoding.UTF8.GetString(buf, 0, r.Count))
        {
            if (c == 'L') client.TurnLeft();
            else if (c == 'R') client.TurnRight();
        }
    }
}

// --selfcheck client
static async Task<int> SelfCheck(string baseUrl)
{
    string wsUrl = baseUrl.Replace("http://", "ws://").TrimEnd('/') +
                   "/play?host=192.168.68.61&port=4534&name=Vlad";
    Console.WriteLine($"[selfcheck] connecting {wsUrl}");
    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);

    var buf = new byte[64 * 1024];
    int maxLines = 0, maxRects = 0, frames = 0;
    string lastStatus = "?";
    var deadline = DateTime.UtcNow.AddSeconds(20);
    while (DateTime.UtcNow < deadline)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        WebSocketReceiveResult r;
        try { r = await ws.ReceiveAsync(buf, cts.Token); }
        catch (OperationCanceledException) { break; }
        if (r.MessageType == WebSocketMessageType.Close) break;

        string json = Encoding.UTF8.GetString(buf, 0, r.Count);
        using var doc = JsonDocument.Parse(json);
        frames++;
        lastStatus = doc.RootElement.GetProperty("status").GetString() ?? "?";
        int lines = doc.RootElement.GetProperty("lines").GetArrayLength();
        int rects = doc.RootElement.GetProperty("rects").GetArrayLength();
        maxLines = Math.Max(maxLines, lines);
        maxRects = Math.Max(maxRects, rects);
        if (maxLines > 4 && maxRects > 0) break; // saw real cycle geometry, not just the arena border
    }

    bool ok = frames > 0 && lastStatus == "Connected";
    Console.WriteLine($"[selfcheck] frames={frames} status={lastStatus} maxLines={maxLines} maxRects={maxRects}");
    Console.WriteLine(ok
        ? "[selfcheck] PASS — server-rendered Scene frames streamed from a live game to a WS client."
        : "[selfcheck] FAIL — no Connected frames; is the server up?");

    try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); }
    catch (WebSocketException) { /* server already tore down — fine, we have our verdict */ }
    return ok ? 0 : 1;
}

// ── frame DTO (compact arrays keep the per-frame payload small) ──────────────────────────────
record Frame(string status, int myId, int size, List<float[]> lines, List<float[]> rects)
{
    public static Frame From(Scene scene, ConnectionStatus status, int myId, int size)
    {
        var lines = new List<float[]>(scene.Segments.Count);
        foreach (RenderSegment s in scene.Segments)
            lines.Add(new[] { s.From.X, s.From.Y, s.To.X, s.To.Y, Packed(s.Color), s.Thickness });

        var rects = new List<float[]>(scene.Heads.Count);
        foreach (RenderRect h in scene.Heads)
            rects.Add(new[] { h.X, h.Y, h.W, h.H, Packed(h.Color) });

        return new Frame(status.ToString(), myId, size, lines, rects);
    }

    // Pack RGB into a single float (0xRRGGBB) so the browser can unpack to a CSS colour cheaply.
    private static float Packed(RenderColor c) => (c.R << 16) | (c.G << 8) | c.B;
}

public partial class Program { }
