using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Armagetron.Game.UI;

namespace Armagetron.Game
{
    /// <summary>
    /// Remembers the player's last-used server/port/name in a small text file under the per-user
    /// app-data dir, so every native head (desktop, Android, iOS) pre-fills the connect form with
    /// it next launch instead of a baked-in default. The (de)serialization is the pure, unit-tested
    /// <see cref="ConnectChoiceFormat"/>; this is just the thin file I/O around it, hence
    /// <see cref="ExcludeFromCodeCoverageAttribute"/> like the other platform edges. Read/write
    /// failures degrade silently to "no memory" (logged, never thrown) — a missing or corrupt file
    /// must never stop the app from launching or connecting.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class FileConnectStore : IConnectStore
    {
        private readonly string _path;

        /// <summary>Use the default per-user path, or an explicit one (tests / custom installs).</summary>
        public FileConnectStore(string? path = null) => _path = path ?? DefaultPath();

        private static string DefaultPath()
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(dir, "Armagetron", "connect.txt");
        }

        public ConnectChoice? Load()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                return ConnectChoiceFormat.TryParse(File.ReadAllText(_path), out ConnectChoice c)
                    ? c : (ConnectChoice?)null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[connect-store] load failed ({_path}): {ex.Message}");
                return null;
            }
        }

        public void Save(ConnectChoice choice)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, ConnectChoiceFormat.Serialize(choice));
            }
            catch (Exception ex)
            {
                // Best-effort: remembering the last server is a convenience, not a correctness need.
                Console.Error.WriteLine($"[connect-store] save failed ({_path}): {ex.Message}");
            }
        }
    }
}
