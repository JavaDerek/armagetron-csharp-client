using System;
using Armagetron.Bot;
using Armagetron.Net;

string host = "127.0.0.1";
int port = 4534;
string name = "AaBot";

int positional = 0;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--host" && i + 1 < args.Length) host = args[++i];
    else if (args[i] == "--port" && i + 1 < args.Length) port = int.Parse(args[++i]);
    else if (args[i] == "--name" && i + 1 < args.Length) name = args[++i];
    else if (!args[i].StartsWith("--"))
    {
        if (positional == 0) host = args[i];
        else if (positional == 1) port = int.Parse(args[i]);
        else if (positional == 2) name = args[i];
        positional++;
    }
}

Console.WriteLine($"[AaBot] Connecting to {host}:{port} as '{name}'");

using var link = new UdpLink(host, port);
var bot = new BotSession(link, name);
bot.Run();
