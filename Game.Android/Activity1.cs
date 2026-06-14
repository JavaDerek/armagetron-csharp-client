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
        // Defaults pre-fill the connect screen so the proven-good 'AaBot' identity is one tap
        // away; the player can edit any field via the soft keyboard. (A brand-new name can trip
        // the server-side Cheater() gate — see registration_timing_race notes.)
        private const string Host = "192.168.68.61";
        private const int    Port = 4534;
        private const string Name = "AaBot";

        private ArmagetronGame? _game;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var client = new UiArmaClient();
            var shell  = new AppShell(client, UiTheme.Default, Host, Port, Name, touchControls: true);
            _game = new ArmagetronGame(client, new AndroidShellInput(), shell,
                                       "Armagetron", fullscreen: true);

            SetContentView((View)_game.Services.GetService(typeof(View)));
            _game.Run();
        }
    }
}
