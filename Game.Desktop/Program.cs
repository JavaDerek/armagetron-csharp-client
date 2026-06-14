using Armagetron.Game;
using Armagetron.Game.UI;
using Armagetron.Lib;

// Defaults are pre-filled into the connect screen; --host/--port/--name override them.
string host = "192.168.68.61";
int    port = 4534;
string name = "AaBot";

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--host" && i + 1 < args.Length) host = args[++i];
    else if (args[i] == "--port" && i + 1 < args.Length) port = int.Parse(args[++i]);
    else if (args[i] == "--name" && i + 1 < args.Length) name = args[++i];
}

// Headless self-test (--selftest): exercise the real UiArmaClient connect path against a live
// server WITHOUT opening a window, so the new adapter can be verified from a terminal (CLAUDE.md
// step 4). Drives BeginConnect, polls Status like the shell does, reports, and exits.
if (System.Array.IndexOf(args, "--selftest") >= 0)
{
    using var probe = new UiArmaClient();
    System.Console.WriteLine($"[selftest] BeginConnect {host}:{port} as '{name}'");
    probe.BeginConnect(host, port, name);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    while (probe.Status == ConnectionStatus.Connecting && sw.Elapsed.TotalSeconds < 50)
        System.Threading.Thread.Sleep(100);
    System.Console.WriteLine($"[selftest] Status={probe.Status} MyCycleId={probe.MyCycleId} " +
                             $"cycles={probe.Snapshot().Length} err={probe.LastError ?? "none"}");
    if (probe.Status == ConnectionStatus.Connected)
    {
        probe.TurnLeft(); probe.TurnRight();
        System.Threading.Thread.Sleep(1500);
        System.Console.WriteLine($"[selftest] after turns: cycles={probe.Snapshot().Length} " +
                                 $"events={probe.DrainEvents().Count}");
    }
    probe.Disconnect();
    return;
}

// The client starts DISCONNECTED: the in-app connect screen drives BeginConnect now, so the
// window opens immediately at the connect form (no blocking pre-connect). Login, the desc=201
// registration race, fresh-socket retry and the session loop all live inside UiArmaClient.
var client = new UiArmaClient();
var shell  = new AppShell(client, UiTheme.Default, host, port, name, touchControls: false);
var input  = new DesktopShellInput();

using var game = new ArmagetronGame(client, input, shell,
    title: "Armagetron — arrows / mouse to play, Esc to pause/quit");
input.Attach(game.Window);
game.Run();
