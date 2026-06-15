using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

// WebSocket <-> UDP relay for the browser client.
//
//   browser (Game.Web)  --ws-->  THIS RELAY  --udp-->  Armagetron server
//
// A browser connects to ws://<relay>/?host=<server>&port=<udpport>. The relay opens a dedicated
// UDP socket to that server and pumps datagrams both ways: one WS BINARY message == one UDP
// datagram (matching WebSocketUdpLink on the client). Run it on any machine that can reach the
// game server over UDP (the dev box, or the server host itself).
//
// Usage:  dotnet run --project Web/WsRelay [--urls http://0.0.0.0:8765]
//         (allowed targets can be locked down with ARMA_RELAY_ALLOW="192.168.68.61:4534,...")

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
var app = builder.Build();

app.Urls.Clear();
app.Urls.Add(Environment.GetEnvironmentVariable("ARMA_RELAY_URL") ?? "http://0.0.0.0:8765");

// Optional allow-list: comma-separated host:port the relay may bridge to. Empty = allow any
// (fine for a LAN dev relay; set it if the relay is ever exposed beyond localhost).
var allow = (Environment.GetEnvironmentVariable("ARMA_RELAY_ALLOW") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

app.UseWebSockets();

app.MapGet("/healthz", () => "ok");

app.Map("/", async (HttpContext ctx, ILogger<Program> log) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync("Armagetron WS<->UDP relay. Connect a WebSocket with ?host=&port=.");
        return;
    }

    string host = ctx.Request.Query["host"].ToString();
    if (!int.TryParse(ctx.Request.Query["port"], out int port) || string.IsNullOrEmpty(host))
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    if (allow.Length > 0 && Array.IndexOf(allow, $"{host}:{port}") < 0)
    {
        log.LogWarning("rejected target {Host}:{Port} (not in ARMA_RELAY_ALLOW)", host, port);
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    using WebSocket ws = await ctx.WebSockets.AcceptWebSocketAsync();
    using var udp = new UdpClient();
    udp.Connect(host, port);
    log.LogInformation("bridge open -> {Host}:{Port}", host, port);

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
    try
    {
        await Task.WhenAny(WsToUdp(ws, udp, cts.Token), UdpToWs(udp, ws, cts.Token));
    }
    catch (Exception ex) { log.LogDebug("bridge ended: {Msg}", ex.Message); }
    finally { cts.Cancel(); log.LogInformation("bridge closed -> {Host}:{Port}", host, port); }
});

app.Run();

// ── pumps ───────────────────────────────────────────────────────────────────────────────────
// Each complete WS binary message is forwarded as one UDP datagram.
static async Task WsToUdp(WebSocket ws, UdpClient udp, CancellationToken ct)
{
    var buf = new byte[8192];
    while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
    {
        using var ms = new MemoryStream();
        WebSocketReceiveResult r;
        do
        {
            r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
            if (r.MessageType == WebSocketMessageType.Close) return;
            ms.Write(buf, 0, r.Count);
        }
        while (!r.EndOfMessage);

        if (ms.Length > 0)
        {
            byte[] datagram = ms.ToArray();
            await udp.SendAsync(datagram, datagram.Length);
        }
    }
}

// Each UDP datagram from the server is forwarded as one WS binary message.
static async Task UdpToWs(UdpClient udp, WebSocket ws, CancellationToken ct)
{
    while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
    {
        UdpReceiveResult res = await udp.ReceiveAsync(ct);
        await ws.SendAsync(new ArraySegment<byte>(res.Buffer), WebSocketMessageType.Binary,
                           endOfMessage: true, ct);
    }
}

// Exposed so WebApplicationFactory / ILogger<Program> has a public entry type.
public partial class Program { }
