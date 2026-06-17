using System;
using System.IO;
using Foundation;
using UIKit;
using Armagetron.Game;
using Armagetron.Game.UI;
using Armagetron.Lib;

namespace Armagetron.iOS
{
    /// <summary>
    /// iOS entry point + app delegate. Hosts the shared screen-driven <see cref="ArmagetronGame"/>
    /// exactly like the desktop <c>Program.cs</c> and the Android <c>Activity1</c>. The client
    /// starts DISCONNECTED: the in-app connect screen lets the player enter server/port/name and
    /// tap CONNECT, which drives <see cref="UiArmaClient"/> to register on a background thread
    /// (kept off the UI thread). Input is touch via <see cref="IosShellInput"/>.
    /// </summary>
    [Register("AppDelegate")]
    public class Program : UIApplicationDelegate
    {
        // Defaults pre-fill the connect screen; the player can edit any field via the soft
        // keyboard. Name is 'Vlad': the server currently rejects 'AaBot' with a Cheater() flag
        // while 'Vlad' registers cleanly — same stopgap as the desktop & Android heads. See the
        // registration_timing_race / registration_auth_research notes.
        private const string Host = "192.168.68.61";
        private const int    Port = 4534;
        private const string Name = "Vlad";

        private ArmagetronGame? _game;

        public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
        {
            // An iOS .app bundle is a real, readable filesystem directory, and the designer media
            // ships inside it as BundleResource under media/… (see the .csproj). So — unlike
            // Android, which must unpack assets out of the APK — the head simply points the shared
            // loaders at the bundle's media folder. No copy step.
            string mediaRoot = Path.Combine(NSBundle.MainBundle.BundlePath, "media");

            var client = new UiArmaClient();
            var shell  = new AppShell(client, UiTheme.Default, Host, Port, Name, touchControls: true);

            // Live-server gate harness: with AA_AUTOCONNECT=1 (passed via
            // SIMCTL_CHILD_AA_AUTOCONNECT on the simulator), skip the connect screen and register
            // immediately, so register/render can be verified without synthesizing a tap. Normal
            // launches (no env var) show the interactive connect screen unchanged.
            if (Environment.GetEnvironmentVariable("AA_AUTOCONNECT") == "1")
                shell.RequestConnect();

            _game = new ArmagetronGame(client, new IosShellInput(), shell,
                                       "Armagetron", fullscreen: true, mediaRoot: mediaRoot);
            _game.Run();
            return true;
        }

        private static void Main(string[] args)
        {
            UIApplication.Main(args, null, typeof(Program));
        }
    }
}
