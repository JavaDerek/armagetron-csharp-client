using System;
using Armagetron.Game;
using Armagetron.Net;

string host = "192.168.68.61";
int    port = 4534;
string name = "Player1";

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--host" && i + 1 < args.Length) host = args[++i];
    else if (args[i] == "--port" && i + 1 < args.Length) port = int.Parse(args[++i]);
    else if (args[i] == "--name" && i + 1 < args.Length) name = args[++i];
}

Console.WriteLine($"[AaClient] Connecting to {host}:{port} as '{name}'");

// Registration (desc=201) is a one-shot, timing-sensitive race against the server. It
// must run on an uncontended thread BEFORE the MonoGame render loop starts — a render-
// starved background thread loses the race and draws an "It assumed you are cheating"
// disconnect (verified live: bot on main thread 2/2 ok, on a busy background thread 2/3
// rejected). So we connect+register here, on the main thread, before opening the window.
// On timeout we reconnect with a FRESH socket, which also escapes the server's
// post-rejection mute (a new connection gets a new slot).
var world = new GameWorld();
PlayerSession? session = null;

const int maxAttempts = 10;
for (int attempt = 1; attempt <= maxAttempts; attempt++)
{
    var link = new UdpLink(host, port);
    var candidate = new PlayerSession(link, name, world);
    Console.WriteLine($"[AaClient] Registering… (attempt {attempt}/{maxAttempts})");
    if (candidate.RunUntilPlaying(timeoutMs: 45_000))
    {
        session = candidate;
        Console.WriteLine("[AaClient] Registered — entering game.");
        break;
    }
    Console.WriteLine("[AaClient] Registration timed out/rejected; reconnecting with a fresh socket…");
    candidate.Dispose();
}

if (session == null)
{
    Console.WriteLine($"[AaClient] Could not register after {maxAttempts} attempts. Exiting.");
    return;
}

string title = $"Armagetron — {host}:{port}  [{name}]  ← → to turn  Esc to quit";
using var game = new ArmagetronGame(session, world, title);
game.Run();
