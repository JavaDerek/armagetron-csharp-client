using System.Globalization;

namespace Armagetron.Game.UI
{
    /// <summary>The player's last-used connect target, persisted between app sessions.</summary>
    public readonly struct ConnectChoice
    {
        public string Host { get; }
        public int Port { get; }
        public string Name { get; }

        public ConnectChoice(string host, int port, string name)
        {
            Host = host;
            Port = port;
            Name = name;
        }
    }

    /// <summary>
    /// Persistence seam for the connect form. A pure interface so <see cref="AppShell"/> stays
    /// I/O-free and unit-testable; each native head supplies a platform implementation (a small
    /// file under the per-user app-data dir — see <c>FileConnectStore</c>). The shell seeds the
    /// form from <see cref="Load"/> at launch and calls <see cref="Save"/> only once a connection
    /// actually succeeds, so only choices that worked are remembered.
    /// </summary>
    public interface IConnectStore
    {
        /// <summary>The last saved choice, or null if nothing has been saved yet.</summary>
        ConnectChoice? Load();

        /// <summary>Persist the latest successful choice.</summary>
        void Save(ConnectChoice choice);
    }

    /// <summary>
    /// Pure (de)serialization of a <see cref="ConnectChoice"/> to a tiny text format, kept in
    /// Core.Protocol so it is unit-tested; a head's file store is just <c>ReadAllText</c>/
    /// <c>WriteAllText</c> around these. The format is three newline-separated lines:
    /// host, port, name. \r\n is tolerated on read so a hand-edited or Windows-written file
    /// round-trips cleanly.
    /// </summary>
    public static class ConnectChoiceFormat
    {
        public static string Serialize(ConnectChoice c) =>
            c.Host + "\n" + c.Port.ToString(CultureInfo.InvariantCulture) + "\n" + c.Name;

        public static bool TryParse(string? text, out ConnectChoice choice)
        {
            choice = default;
            if (string.IsNullOrEmpty(text)) return false;

            string[] lines = text!.Replace("\r\n", "\n").Split('\n');
            if (lines.Length < 3) return false;
            if (!int.TryParse(lines[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
                return false;

            choice = new ConnectChoice(lines[0], port, lines[2]);
            return true;
        }
    }
}
