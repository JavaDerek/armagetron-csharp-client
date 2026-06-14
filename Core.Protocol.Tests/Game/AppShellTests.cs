using System;
using System.Linq;
using Armagetron.Game;
using Armagetron.Game.UI;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// A scriptable <see cref="IUiClient"/> with no socket: records calls and lets a test set
    /// the connection status to drive the shell's transitions.
    /// </summary>
    internal sealed class FakeUiClient : IUiClient
    {
        public ConnectionStatus Status { get; set; } = ConnectionStatus.Idle;
        public string? LastError { get; set; }
        public int MyCycleId { get; set; } = -1;
        public CycleSnapshot[] Snap { get; set; } = Array.Empty<CycleSnapshot>();
        public CycleSnapshot[] Snapshot() => Snap;

        public int Lefts, Rights, BeginConnects;
        public bool Disconnected;
        public string? ConnHost, ConnName;
        public int ConnPort;

        public void BeginConnect(string host, int port, string name)
        {
            ConnHost = host; ConnPort = port; ConnName = name; BeginConnects++;
            Status = ConnectionStatus.Connecting;
        }
        public void Disconnect() { Disconnected = true; Status = ConnectionStatus.Idle; }
        public void TurnLeft() => Lefts++;
        public void TurnRight() => Rights++;

        public readonly System.Collections.Generic.Queue<MatchEvent> Events = new();
        public System.Collections.Generic.IReadOnlyList<MatchEvent> DrainEvents()
        {
            var list = new System.Collections.Generic.List<MatchEvent>(Events);
            Events.Clear();
            return list;
        }
    }

    public class AppShellTests
    {
        private const int W = 800, H = 800;

        private static AppShell Shell(FakeUiClient c, bool touch = false) =>
            new AppShell(c, UiTheme.Default, "192.168.68.61", 4534, "AaBot", touch);

        private static (int x, int y) Center(UiRect r) => (r.CenterX, r.CenterY);

        // ── Start / form ──────────────────────────────────────────────────────────

        [Fact]
        public void Starts_OnConnect_WithValidDefaultForm()
        {
            var s = Shell(new FakeUiClient());
            Assert.Equal(AppScreen.Connect, s.Screen);
            Assert.True(s.IsFormValid());
        }

        [Fact]
        public void TappingConnect_WithValidForm_BeginsConnect_AndGoesConnecting()
        {
            var c = new FakeUiClient();
            var s = Shell(c);
            var (x, y) = Center(Layouts.Connect(W, H).Connect);
            s.HandleTap(x, y, W, H);

            Assert.Equal(1, c.BeginConnects);
            Assert.Equal("192.168.68.61", c.ConnHost);
            Assert.Equal(4534, c.ConnPort);
            Assert.Equal("AaBot", c.ConnName);
            Assert.Equal(AppScreen.Connecting, s.Screen);
        }

        [Fact]
        public void TappingConnect_WithInvalidPort_DoesNothing()
        {
            var c = new FakeUiClient();
            var s = Shell(c);
            // Focus the port field then clear it → invalid.
            var (px, py) = Center(Layouts.Connect(W, H).Port);
            s.HandleTap(px, py, W, H);
            s.SetFocusedFieldValue("");
            Assert.False(s.IsFormValid());

            var (cx, cy) = Center(Layouts.Connect(W, H).Connect);
            s.HandleTap(cx, cy, W, H);
            Assert.Equal(0, c.BeginConnects);
            Assert.Equal(AppScreen.Connect, s.Screen);
        }

        [Fact]
        public void TypingIntoFocusedField_EditsIt()
        {
            var s = Shell(new FakeUiClient());
            var (nx, ny) = Center(Layouts.Connect(W, H).Name);
            s.HandleTap(nx, ny, W, H);   // focus name
            s.SetFocusedFieldValue("");
            s.OnText('V'); s.OnText('l'); s.OnText('a'); s.OnText('d');
            s.OnBackspace();
            // PlayerName reflects edits (lowercase kept).
            Assert.Equal("Vla", s.PlayerName);
        }

        // ── Connecting → Playing / failure ─────────────────────────────────────────

        [Fact]
        public void Tick_PromotesToPlaying_WhenConnected()
        {
            var c = new FakeUiClient();
            var s = Shell(c);
            s.HandleTap(Layouts.Connect(W, H).Connect.CenterX, Layouts.Connect(W, H).Connect.CenterY, W, H);
            c.Status = ConnectionStatus.Connected;
            s.Tick(Array.Empty<CycleSnapshot>(), 0);
            Assert.Equal(AppScreen.Playing, s.Screen);
        }

        [Fact]
        public void Tick_ReturnsToConnect_WithError_WhenFailed()
        {
            var c = new FakeUiClient();
            var s = Shell(c);
            s.HandleTap(Layouts.Connect(W, H).Connect.CenterX, Layouts.Connect(W, H).Connect.CenterY, W, H);
            c.Status = ConnectionStatus.Failed;
            c.LastError = "SERVER DOWN";
            s.Tick(Array.Empty<CycleSnapshot>(), 0);
            Assert.Equal(AppScreen.Connect, s.Screen);
            Scene scene = s.BuildOverlay(W, H, 0);
            Assert.Contains(scene.Texts, t => t.Text == "SERVER DOWN");
        }

        // ── Gameplay input ─────────────────────────────────────────────────────────

        private static AppShell Playing(FakeUiClient c, bool touch = false)
        {
            var s = Shell(c, touch);
            s.HandleTap(Layouts.Connect(W, H).Connect.CenterX, Layouts.Connect(W, H).Connect.CenterY, W, H);
            c.Status = ConnectionStatus.Connected;
            s.Tick(Array.Empty<CycleSnapshot>(), 0);
            return s;
        }

        [Fact]
        public void OnTurn_OnlyActsWhilePlaying()
        {
            var c = new FakeUiClient();
            var s = Shell(c);
            s.OnTurn(TurnDirection.Left);            // on Connect → ignored
            Assert.Equal(0, c.Lefts);

            var p = Playing(c);
            p.OnTurn(TurnDirection.Left);
            p.OnTurn(TurnDirection.Right);
            Assert.Equal(1, c.Lefts);
            Assert.Equal(1, c.Rights);
        }

        [Fact]
        public void TapPlaying_LeftHalfTurnsLeft_RightHalfTurnsRight()
        {
            var c = new FakeUiClient();
            var s = Playing(c);
            s.HandleTap(10, H / 2, W, H);            // far left
            s.HandleTap(W - 10, H / 2, W, H);        // far right
            Assert.Equal(1, c.Lefts);
            Assert.Equal(1, c.Rights);
        }

        [Fact]
        public void TapPauseButton_OpensPauseMenu_NotATurn()
        {
            var c = new FakeUiClient();
            var s = Playing(c);
            var (x, y) = Center(Layouts.Play(W, H).Pause);
            s.HandleTap(x, y, W, H);
            Assert.Equal(AppScreen.Paused, s.Screen);
            Assert.Equal(0, c.Lefts + c.Rights);
        }

        // ── Pause / settings navigation ─────────────────────────────────────────────

        [Fact]
        public void Pause_Resume_Disconnect()
        {
            var c = new FakeUiClient();
            var s = Playing(c);
            s.OnBack();                              // Playing → Paused
            Assert.Equal(AppScreen.Paused, s.Screen);

            UiRect[] b = Layouts.Menu(W, H, 3).Buttons;
            s.HandleTap(b[0].CenterX, b[0].CenterY, W, H);   // RESUME
            Assert.Equal(AppScreen.Playing, s.Screen);

            s.OnBack();                              // back to Paused
            s.HandleTap(b[2].CenterX, b[2].CenterY, W, H);   // DISCONNECT
            Assert.True(c.Disconnected);
            Assert.Equal(AppScreen.Connect, s.Screen);
        }

        [Fact]
        public void Settings_TogglesSound_AndBackReturns()
        {
            var c = new FakeUiClient();
            var s = Playing(c);
            s.OnBack();                                          // Paused
            UiRect[] pb = Layouts.Menu(W, H, 3).Buttons;
            s.HandleTap(pb[1].CenterX, pb[1].CenterY, W, H);     // SETTINGS
            Assert.Equal(AppScreen.Settings, s.Screen);
            Assert.True(s.SoundEnabled);

            UiRect[] sb = Layouts.Menu(W, H, 2).Buttons;
            s.HandleTap(sb[0].CenterX, sb[0].CenterY, W, H);     // SOUND toggle
            Assert.False(s.SoundEnabled);
            s.HandleTap(sb[1].CenterX, sb[1].CenterY, W, H);     // BACK → returns to Paused
            Assert.Equal(AppScreen.Paused, s.Screen);
        }

        [Fact]
        public void SettingsGear_FromConnect_OpensSettings_BackToConnect()
        {
            var s = Shell(new FakeUiClient());
            var (gx, gy) = Center(Layouts.Connect(W, H).Settings);
            s.HandleTap(gx, gy, W, H);
            Assert.Equal(AppScreen.Settings, s.Screen);
            s.OnBack();
            Assert.Equal(AppScreen.Connect, s.Screen);
        }

        [Fact]
        public void Connecting_Cancel_Disconnects()
        {
            var c = new FakeUiClient();
            var s = Shell(c);
            s.HandleTap(Layouts.Connect(W, H).Connect.CenterX, Layouts.Connect(W, H).Connect.CenterY, W, H);
            var (cx, cy) = Center(Layouts.Connecting(W, H).Cancel);
            s.HandleTap(cx, cy, W, H);
            Assert.True(c.Disconnected);
            Assert.Equal(AppScreen.Connect, s.Screen);
        }

        [Fact]
        public void Back_OnConnect_RequestsExit()
        {
            var s = Shell(new FakeUiClient());
            Assert.False(s.ExitRequested);
            s.OnBack();
            Assert.True(s.ExitRequested);
        }

        [Fact]
        public void Back_OnConnecting_CancelsToConnect()
        {
            var c = new FakeUiClient();
            var s = Shell(c);
            s.HandleTap(Layouts.Connect(W, H).Connect.CenterX, Layouts.Connect(W, H).Connect.CenterY, W, H);
            s.OnBack();
            Assert.True(c.Disconnected);
            Assert.Equal(AppScreen.Connect, s.Screen);
        }

        // ── Overlays / HUD content ──────────────────────────────────────────────────

        [Fact]
        public void ConnectOverlay_ShowsTitleAndFields()
        {
            var s = Shell(new FakeUiClient());
            Scene scene = s.BuildOverlay(W, H, 0);
            Assert.Contains(scene.Texts, t => t.Text == "ARMAGETRON");
            Assert.Contains(scene.Texts, t => t.Text == "192.168.68.61"); // host field value
        }

        [Fact]
        public void ConnectingOverlay_ShowsAnimatedDots_AndTarget()
        {
            var c = new FakeUiClient();
            var s = Shell(c);
            s.HandleTap(Layouts.Connect(W, H).Connect.CenterX, Layouts.Connect(W, H).Connect.CenterY, W, H);
            Scene scene = s.BuildOverlay(W, H, 800); // 800/400 % 4 = 2 dots
            Assert.Contains(scene.Texts, t => t.Text == "CONNECTING..");
            Assert.Contains(scene.Texts, t => t.Text == "192.168.68.61:4534");
        }

        [Fact]
        public void PlayingHud_ShowsName_Time_AndConnectionStatesGreen()
        {
            var c = new FakeUiClient();
            var s = Playing(c);
            s.OnRoundStart(0);
            Scene scene = s.BuildOverlay(W, H, 65_000);
            Assert.Contains(scene.Texts, t => t.Text == "AaBot");
            Assert.Contains(scene.Texts, t => t.Text == "TIME 1:05");
            // Connected → a success-colored indicator rect present.
            Assert.Contains(scene.Heads, r => r.Color.Equals(UiTheme.Default.Success));
        }

        [Fact]
        public void PlayingHud_WhenLocalDead_ShowsWaitingBanner()
        {
            var c = new FakeUiClient();
            var s = Playing(c);
            s.OnRoundStart(0);
            s.OnLocalDied();
            Scene scene = s.BuildOverlay(W, H, 0);
            Assert.Contains(scene.Texts, t => t.Text == "WAITING FOR NEXT ROUND");
        }

        [Fact]
        public void TouchControls_ShowHint_UntilFirstTurn()
        {
            var c = new FakeUiClient();
            var s = Playing(c, touch: true);
            Scene before = s.BuildOverlay(W, H, 0);
            Assert.Contains(before.Texts, t => t.Text == "TAP LEFT / RIGHT TO TURN");

            s.HandleTap(10, H / 2, W, H); // first turn
            Scene after = s.BuildOverlay(W, H, 0);
            Assert.DoesNotContain(after.Texts, t => t.Text == "TAP LEFT / RIGHT TO TURN");
        }

        [Fact]
        public void NoTouchControls_NoHint()
        {
            var c = new FakeUiClient();
            var s = Playing(c, touch: false);
            Scene scene = s.BuildOverlay(W, H, 0);
            Assert.DoesNotContain(scene.Texts, t => t.Text == "TAP LEFT / RIGHT TO TURN");
        }

        [Fact]
        public void PausedAndSettings_Overlays_ShowTitles()
        {
            var c = new FakeUiClient();
            var s = Playing(c);
            s.OnBack();
            Assert.Contains(s.BuildOverlay(W, H, 0).Texts, t => t.Text == "PAUSED");

            UiRect[] pb = Layouts.Menu(W, H, 3).Buttons;
            s.HandleTap(pb[1].CenterX, pb[1].CenterY, W, H);
            Scene settings = s.BuildOverlay(W, H, 0);
            Assert.Contains(settings.Texts, t => t.Text == "SETTINGS");
            Assert.Contains(settings.Texts, t => t.Text == "SOUND: ON");
        }

        [Fact]
        public void Tick_DrainsClientEvents_IntoMatchState()
        {
            var c = new FakeUiClient();
            var s = Playing(c);
            c.Events.Enqueue(MatchEvent.RoundStart);
            s.Tick(Array.Empty<CycleSnapshot>(), 1_000);
            Assert.Equal(1, s.Match.RoundNumber);
            Assert.True(s.Match.LocalAlive);

            c.Events.Enqueue(MatchEvent.LocalDied);
            s.Tick(Array.Empty<CycleSnapshot>(), 2_000);
            Assert.False(s.Match.LocalAlive);

            c.Events.Enqueue(MatchEvent.RoundEnd);
            s.Tick(Array.Empty<CycleSnapshot>(), 3_000);
            Assert.False(s.Match.RoundActive);
        }

        [Fact]
        public void ShowsGameplay_OnlyInPlayingOrPaused()
        {
            var c = new FakeUiClient();
            var s = Shell(c);
            Assert.False(s.ShowsGameplay);
            var p = Playing(c);
            Assert.True(p.ShowsGameplay);
            p.OnBack();
            Assert.True(p.ShowsGameplay); // paused still shows frozen gameplay
        }
    }
}
