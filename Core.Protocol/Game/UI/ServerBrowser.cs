using System.Collections.Generic;

namespace Armagetron.Game.UI
{
    /// <summary>One row in the server browser.</summary>
    public readonly struct ServerEntry
    {
        public readonly string Name, Region, Host;
        public readonly int Port, Players, MaxPlayers, Ping;
        public readonly bool Joinable;

        public ServerEntry(string name, string region, string host, int port,
                           int players, int maxPlayers, int ping, bool joinable)
        {
            Name = name; Region = region; Host = host; Port = port;
            Players = players; MaxPlayers = maxPlayers; Ping = ping; Joinable = joinable;
        }

        public string PlayersLabel => Players + "/" + MaxPlayers;
        public bool Full => Players >= MaxPlayers;
    }

    /// <summary>
    /// PLACEHOLDER server list. There is no master-server protocol yet, so row 0 is the
    /// configured server (really joinable) and the rest are illustrative examples from the
    /// design comp (not joinable). When a server-query protocol lands, this is replaced by a
    /// live fetch; the browser view/shell above it are unchanged.
    /// </summary>
    public static class ServerList
    {
        public static ServerEntry[] Placeholder(string host, int port) => new[]
        {
            new ServerEntry("LOCAL SERVER",     "LAN",     host, port, 2, 8,  12, true),
            new ServerEntry("GRID ZERO RANKED", "EU-WEST", "",   0,    6, 8,  24, false),
            new ServerEntry("NEON SPEEDWAY",    "US-EAST", "",   0,    4, 12, 58, false),
            new ServerEntry("TRONWALL CLASSIC", "ASIA",    "",   0,    8, 8,  142, false),
        };

        /// <summary>Ping bar color: &lt;50 green, 50–100 amber, &gt;100 red (design spec).</summary>
        public static RenderColor PingColor(int ping, UiTheme t) =>
            ping < 50 ? t.Success : ping <= 100 ? t.Warn : t.Danger;
    }

    public static class ServerBrowserView
    {
        public static void Add(SceneBuf buf, UiTheme t, Layouts.ServerL L,
                               IReadOnlyList<ServerEntry> servers, int w, int h)
        {
            int ts = L.TextScale;
            buf.Fill(new UiRect(0, 0, w, h), t.Background);
            buf.Panel(L.Panel);
            buf.TextLeft("SERVERS", L.Panel.X + 24, L.TitleY, t.Accent, L.TitleScale, FontRole.Title);
            buf.TextLeft("PLACEHOLDER - LIVE LIST PENDING",
                         L.Panel.X + 24, L.TitleY + PixelFont.Height(L.TitleScale) + 6, t.TextMuted, ts, FontRole.Label);

            for (int i = 0; i < L.Rows.Length && i < servers.Count; i++)
            {
                ServerEntry e = servers[i];
                UiRect row = L.Rows[i];
                if (i % 2 == 0) buf.Fill(row, t.Field);

                // Columns are laid out to clear the JOIN button on the right; on a landscape
                // phone there is ample width. Long names are clipped to their column.
                int cy = row.CenterY;
                int nameMax = (int)(row.W * 0.34) / PixelFont.Advance / ts;
                buf.TextLeftMid(Clip(e.Name, nameMax), row.X + 12, cy, t.Text, ts, FontRole.Label);
                buf.TextLeftMid(e.Region, row.X + (int)(row.W * 0.38), cy, t.TextMuted, ts, FontRole.Label);
                buf.TextLeftMid(e.PlayersLabel, row.X + (int)(row.W * 0.58), cy, t.Text, ts, FontRole.Mono);
                buf.TextLeftMid(e.Ping + "MS", row.X + (int)(row.W * 0.70), cy, ServerList.PingColor(e.Ping, t), ts, FontRole.Mono);

                var join = new UiButton("join" + i, L.JoinButtons[i], e.Full ? "FULL" : "JOIN")
                { Enabled = e.Joinable && !e.Full };
                buf.DrawButton(join, t, ts);
            }

            buf.DrawButton(new UiButton("direct", L.Direct, "DIRECT CONNECT"), t, ts, ButtonStyle.Secondary);
            buf.DrawButton(new UiButton("back", L.Back, "BACK"), t, ts, ButtonStyle.Secondary);
        }

        private static string Clip(string s, int max) =>
            max > 0 && s.Length > max ? s.Substring(0, max) : s;
    }
}
