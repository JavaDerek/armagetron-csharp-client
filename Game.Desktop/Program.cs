using System;
using Armagetron.Game;

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

using var game = new ArmagetronGame(host, port, name);
game.Run();
