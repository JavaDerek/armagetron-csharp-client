using System;
using System.IO;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Armagetron.Game;
using Armagetron.Game.UI;
using Armagetron.Lib;
using Microsoft.Xna.Framework;

namespace Armagetron.Android
{
    /// <summary>
    /// Android entry point. Hosts the shared screen-driven <see cref="ArmagetronGame"/> exactly
    /// like the desktop <c>Program.cs</c> does. The client starts DISCONNECTED: the in-app
    /// connect screen (the former <c>Activity1.cs:33</c> TODO, now resolved) lets the player
    /// enter server/port/name and tap CONNECT, which drives <see cref="UiArmaClient"/> to
    /// register on a background thread (Android forbids socket I/O on the UI thread). Input is
    /// touch via <see cref="AndroidShellInput"/>; the hardware Back button reaches the shell as
    /// Back (pause / menu-back) through MonoGame's GamePad Back mapping.
    /// </summary>
    [Activity(
        Label = "Armagetron",
        MainLauncher = true,
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation
                             | ConfigChanges.ScreenSize
                             | ConfigChanges.Keyboard
                             | ConfigChanges.KeyboardHidden)]
    public class Activity1 : AndroidGameActivity
    {
        // The host ships BLANK (no baked-in server); a file store remembers the player's last
        // server across launches and pre-fills it. Port/name keep placeholders the player can edit
        // via the soft keyboard. Name is 'Vlad': the server currently rejects 'AaBot' with a
        // Cheater() flag while 'Vlad' registers cleanly — same stopgap as the desktop head. See the
        // registration_timing_race / registration_auth_research notes.
        private const string Host = "";
        private const int    Port = 4534;
        private const string Name = "Vlad";

        private ArmagetronGame? _game;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // The shared loaders (fonts/textures/audio) read from a filesystem media tree, which
            // doesn't exist inside an APK. Unpack our bundled assets/media/* to filesDir and point
            // the game there. Without this the TextRenderer has no font and the first text draw
            // crashes the app (FontStashSharp "no font source").
            string mediaRoot = Path.Combine(FilesDir!.AbsolutePath, "media");
            UnpackAssetDir("media", FilesDir.AbsolutePath);

            var client = new UiArmaClient();
            var shell  = new AppShell(client, UiTheme.Default, Host, Port, Name, touchControls: true,
                                      store: new FileConnectStore());
            _game = new ArmagetronGame(client, new AndroidShellInput(), shell,
                                       "Armagetron", fullscreen: true, mediaRoot: mediaRoot);

            SetContentView((View)_game.Services.GetService(typeof(View)));
            _game.Run();
        }

        // Recursively copy an asset directory (e.g. "media") from the APK to <paramref name="destRoot"/>.
        // Assets.List returns child names for a directory and an empty array for a file, so an empty
        // result is the recursion's leaf (copy it). Overwrites each launch so rebuilt assets refresh
        // even though filesDir survives reinstall. Per-file failures degrade (logged) rather than crash.
        private void UnpackAssetDir(string assetPath, string destRoot)
        {
            string[] entries;
            try { entries = Assets!.List(assetPath) ?? Array.Empty<string>(); }
            catch { entries = Array.Empty<string>(); }

            if (entries.Length == 0)
            {
                try
                {
                    string dest = Path.Combine(destRoot, assetPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    using Stream src = Assets!.Open(assetPath);
                    using FileStream outp = File.Create(dest);
                    src.CopyTo(outp);
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Warn("Armagetron", $"asset unpack skipped {assetPath}: {ex.Message}");
                }
                return;
            }

            foreach (string e in entries)
                UnpackAssetDir(assetPath + "/" + e, destRoot);
        }
    }
}
