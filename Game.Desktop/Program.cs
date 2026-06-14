using System;
using Armagetron.Game;
using Armagetron.Lib;

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

// Everything below the wire — login, the desc=201 registration race, fresh-socket retry,
// and the background session loop — now lives inside ArmaClient. Connect() runs the
// timing-sensitive registration on THIS (uncontended) thread before the render loop opens,
// then hands the session to a background thread; see ArmaClient.Connect for why.
var client = new ArmaClient();
Console.WriteLine("[AaClient] Registering…");
if (!client.Connect(host, port, name))
{
    Console.WriteLine("[AaClient] Could not register. Exiting.");
    return;
}
Console.WriteLine("[AaClient] Registered — entering game.");

string title = $"Armagetron — {host}:{port}  [{name}]  ← → to turn  Esc to quit";
using var game = new ArmagetronGame(client, title);
game.Run();
