using System.Diagnostics;
using Armagetron.Game.UI;   // ConnectionStatus
using Armagetron.Lib;       // UiArmaClient
using Armagetron.Web;       // WebArmaClient

// Usage: dotnet run --project Web/WebProbe [relayUrl] [host] [port] [name]
string relay = args.Length > 0 ? args[0] : "ws://localhost:8765/";
string host  = args.Length > 1 ? args[1] : "192.168.68.61";
int    port  = args.Length > 2 ? int.Parse(args[2]) : 4534;
string name  = args.Length > 3 ? args[3] : "Vlad";

Console.WriteLine($"[webprobe] relay={relay} target={host}:{port} name='{name}'");

// The browser uses exactly this: a WebArmaClient (WebSocket transport) wrapped by the same
// UiArmaClient the desktop/Android/iOS shells drive.
var web = new WebArmaClient(relay);
using var client = new UiArmaClient(web);

client.BeginConnect(host, port, name);
var sw = Stopwatch.StartNew();
while (client.Status == ConnectionStatus.Connecting && sw.Elapsed.TotalSeconds < 55)
    Thread.Sleep(100);

Console.WriteLine($"[webprobe] Status={client.Status} MyCycleId={client.MyCycleId} " +
                  $"cycles={client.Snapshot().Length} err={client.LastError ?? "none"}");

int exit = 1;
if (client.Status == ConnectionStatus.Connected)
{
    client.TurnLeft();
    client.TurnRight();
    Thread.Sleep(2000);
    Console.WriteLine($"[webprobe] after turns: cycles={client.Snapshot().Length} " +
                      $"events={client.DrainEvents().Count}");
    Console.WriteLine("[webprobe] PASS — browser data path (WS->relay->UDP) reached a live game.");
    exit = 0;
}
else
{
    Console.WriteLine("[webprobe] FAIL — did not reach Connected. Is the relay running and the server up?");
}

client.Disconnect();
return exit;
