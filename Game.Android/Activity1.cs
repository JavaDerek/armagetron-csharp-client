using System.Threading;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Armagetron.Game;
using Armagetron.Lib;
using Microsoft.Xna.Framework;

namespace Armagetron.Android
{
    /// <summary>
    /// Android entry point. Hosts the shared <see cref="ArmagetronGame"/> exactly like the
    /// desktop <c>Program.cs</c> does, with two platform differences:
    ///   • Connection runs on a BACKGROUND thread — Android forbids socket I/O on the UI
    ///     thread (NetworkOnMainThreadException), and starting it async also means a down
    ///     server never blocks the window; the arena renders immediately and cycles appear
    ///     once registration completes (the game re-reads the facade snapshot every frame).
    ///   • Input is touch (<see cref="TouchTurnInput"/>) rather than the keyboard.
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
        // TODO: surface these as an in-app connect screen; hard-coded for the first head.
        // NB: the name must be one the server already knows — a brand-new name trips the
        // server-side Cheater() gate (desc=3 "…assumed you are cheating"). 'AaBot' is the
        // proven-good name used by Bot.Console; see registration_timing_race notes.
        private const string Host = "192.168.68.61";
        private const int    Port = 4534;
        private const string Name = "AaBot";

        private ArmagetronGame? _game;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var client = new ArmaClient();

            // Register off the UI thread; single attempt so a dead server fails fast instead
            // of retrying for minutes. On success the facade's background loop takes over and
            // the running game starts seeing cycles in its per-frame Snapshot().
            new Thread(() => client.Connect(Host, Port, Name, timeoutMs: 8_000, maxAttempts: 1))
                { IsBackground = true }.Start();

            _game = new ArmagetronGame(client, new TouchTurnInput(),
                                       $"Armagetron — {Host}:{Port}", fullscreen: true);

            SetContentView((View)_game.Services.GetService(typeof(View)));
            _game.Run();
        }
    }
}
