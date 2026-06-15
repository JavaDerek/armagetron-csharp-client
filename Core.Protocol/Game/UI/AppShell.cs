using Armagetron.Protocol;

namespace Armagetron.Game.UI
{
    /// <summary>Top-level screens the client can be showing.</summary>
    public enum AppScreen { Connect, Connecting, Playing, Paused, Settings, ConfirmLeave, ServerBrowser }

    /// <summary>
    /// The pure brain of the front-end: a screen state machine + the single input router for
    /// every head. It owns the connect form, drives Connecting → Playing off the client's
    /// <see cref="ConnectionStatus"/>, routes taps/keys/back/text to the right action, tracks
    /// <see cref="MatchState"/>, and builds the UI overlay scene. Zero GPU/platform/threading
    /// dependency — the MonoGame host is a thin loop over this (gather input → Tick → draw),
    /// so all UX behavior is unit-tested against a fake <see cref="IUiClient"/>.
    ///
    /// Rendering split: the host draws gameplay (with its own letterbox offset); this builds
    /// only the screen-space overlay (menus, HUD, touch affordance, pause/settings modals).
    /// </summary>
    public sealed class AppShell
    {
        private readonly IUiClient _client;
        private readonly UiTheme _theme;
        private readonly bool _touchControls;

        private readonly UiTextField _host, _port, _name;
        private string? _error;
        private bool _hasTurned;
        private AppScreen _settingsReturn = AppScreen.Connect;

        private readonly ToastQueue _toasts = new ToastQueue();

        public AppScreen Screen { get; private set; } = AppScreen.Connect;
        public MatchState Match { get; } = new MatchState();
        public SettingsState Settings { get; } = new SettingsState();
        public bool ExitRequested { get; private set; }

        /// <summary>One-shot SFX cues pushed at game moments; the host drains and plays them
        /// once per frame (honoring <see cref="SettingsState.Sound"/>).</summary>
        public SfxCueQueue Sfx { get; } = new SfxCueQueue();

        /// <summary>True when the looping engine hum should sound: in a live match with the
        /// local cycle still alive. The host starts/stops the <see cref="SfxId.EngineLoop"/>
        /// instance from this each frame (like the music loop).</summary>
        public bool EngineRunning => Screen == AppScreen.Playing && Match.LocalAlive;

        /// <summary>The chosen player name (used by the HUD and as the connect identity).</summary>
        public string PlayerName => _name.Value;

        /// <summary>True when gameplay should be rendered behind the overlay (also under the
        /// pause/leave-confirm modals, which freeze the arena beneath them).</summary>
        public bool ShowsGameplay => Screen == AppScreen.Playing || Screen == AppScreen.Paused
                                  || Screen == AppScreen.ConfirmLeave;

        public AppShell(IUiClient client, UiTheme theme,
                        string host, int port, string name, bool touchControls = false)
        {
            _client = client;
            _theme = theme;
            _touchControls = touchControls;
            _host = new UiTextField("host", default, "SERVER ADDRESS") { Value = host, MaxLength = 40 };
            _port = new UiTextField("port", default, "PORT") { Value = port.ToString(), Numeric = true, MaxLength = 5 };
            _name = new UiTextField("name", default, "PLAYER NAME") { Value = name, MaxLength = 16 };
            _host.Focused = true;
        }

        // ── Per-frame ─────────────────────────────────────────────────────────────

        /// <summary>Advance state from the latest snapshot + clock. Call once per frame.</summary>
        public void Tick(CycleSnapshot[] snapshot, long nowMs)
        {
            Match.SetCycleCount(snapshot.Length);

            foreach (MatchEvent ev in _client.DrainEvents())
            {
                switch (ev)
                {
                    case MatchEvent.RoundStart:
                        Match.OnRoundStart(nowMs);
                        _toasts.Push("ROUND " + Match.RoundNumber, _theme.Accent, nowMs);
                        // "GO" cue resolves the start; the 3·2·1 countdown beeps are a timed
                        // pre-roll the protocol doesn't surface yet, so they stay a manual cue.
                        Sfx.Push(SfxId.Go);
                        break;
                    case MatchEvent.RoundEnd:
                        // Win/lose is read BEFORE OnRoundEnd off LocalAlive — survived = win.
                        // A LocalDied earlier in this same drain already flipped it to false.
                        Sfx.Push(Match.LocalAlive ? SfxId.Win : SfxId.Lose);
                        Match.OnRoundEnd();
                        _toasts.Push("ROUND OVER", _theme.Text, nowMs);
                        break;
                    case MatchEvent.LocalDied:
                        Match.OnLocalDied();
                        Sfx.Push(SfxId.Explosion);
                        _toasts.Push("YOU CRASHED", _theme.Danger, nowMs);
                        break;
                }
            }

            if (Screen == AppScreen.Connecting)
            {
                if (_client.Status == ConnectionStatus.Connected)
                {
                    Screen = AppScreen.Playing; _error = null;
                    Sfx.Push(SfxId.ConnectOk);
                }
                else if (_client.Status == ConnectionStatus.Failed)
                {
                    Screen = AppScreen.Connect;
                    _error = _client.LastError ?? "COULD NOT CONNECT";
                    Sfx.Push(SfxId.ConnectFail);
                }
            }
        }

        // ── Round lifecycle (host forwards client events here) ────────────────────

        public void OnRoundStart(long nowMs) => Match.OnRoundStart(nowMs);
        public void OnRoundEnd() => Match.OnRoundEnd();
        public void OnLocalDied() => Match.OnLocalDied();

        // ── Input ─────────────────────────────────────────────────────────────────

        /// <summary>Back / Esc / Android hardware-back.</summary>
        public void OnBack()
        {
            switch (Screen)
            {
                case AppScreen.Playing:    Screen = AppScreen.Paused; break;
                case AppScreen.Paused:     Screen = AppScreen.Playing; break;
                case AppScreen.Settings:   Screen = _settingsReturn; break;
                case AppScreen.ConfirmLeave:  Screen = AppScreen.Paused; break;
                case AppScreen.ServerBrowser: Screen = AppScreen.Connect; break;
                case AppScreen.Connecting: _client.Disconnect(); Screen = AppScreen.Connect; break;
                case AppScreen.Connect:    ExitRequested = true; break;
            }
        }

        /// <summary>A discrete turn (desktop arrow keys). Only acts while Playing.</summary>
        public void OnTurn(TurnDirection dir)
        {
            if (Screen != AppScreen.Playing) return;
            _hasTurned = true;
            Sfx.Push(SfxId.Turn);
            if (dir == TurnDirection.Left) _client.TurnLeft(); else _client.TurnRight();
        }

        /// <summary>A printable character typed into the focused field (Connect screen).</summary>
        public void OnText(char c) => Focused()?.Append(c);

        /// <summary>Backspace in the focused field.</summary>
        public void OnBackspace() => Focused()?.Backspace();

        /// <summary>Id of the focused field, or null (used by Android to open a soft keyboard).</summary>
        public string? FocusedFieldId => Focused()?.Id;

        /// <summary>Current value of the focused field, or null (prefills the soft keyboard).</summary>
        public string? FocusedFieldValue => Focused()?.Value;

        /// <summary>Replace the focused field's value wholesale (Android soft-keyboard result).</summary>
        public void SetFocusedFieldValue(string value)
        {
            UiTextField? f = Focused();
            if (f == null) return;
            f.Value = "";
            foreach (char c in value) f.Append(c);
        }

        /// <summary>Route a tap at screen pixel (x, y) for a viewport of (w, h).</summary>
        public void HandleTap(int x, int y, int w, int h)
        {
            switch (Screen)
            {
                case AppScreen.Connect:    TapConnect(x, y, w, h); break;
                case AppScreen.Connecting: TapConnecting(x, y, w, h); break;
                case AppScreen.Playing:    TapPlaying(x, y, w, h); break;
                case AppScreen.Paused:     TapPaused(x, y, w, h); break;
                case AppScreen.Settings:   TapSettings(x, y, w, h); break;
                case AppScreen.ConfirmLeave: TapConfirmLeave(x, y, w, h); break;
                case AppScreen.ServerBrowser: TapServerBrowser(x, y, w, h); break;
            }
        }

        private void TapConnect(int x, int y, int w, int h)
        {
            Layouts.ConnectL L = Layouts.Connect(w, h);
            _host.Bounds = L.Host; _port.Bounds = L.Port; _name.Bounds = L.Name;

            if (L.Settings.Contains(x, y)) { _settingsReturn = AppScreen.Connect; Screen = AppScreen.Settings; return; }
            if (L.Browse.Contains(x, y)) { Screen = AppScreen.ServerBrowser; return; }

            _host.Focused = L.Host.Contains(x, y);
            _port.Focused = L.Port.Contains(x, y);
            _name.Focused = L.Name.Contains(x, y);

            if (L.Connect.Contains(x, y) && IsFormValid()) StartConnect();
        }

        private void TapServerBrowser(int x, int y, int w, int h)
        {
            ServerEntry[] servers = ServerList.Placeholder(_host.Value, ParsePortOr(4534));
            Layouts.ServerL L = Layouts.Server(w, h, servers.Length);

            if (L.Back.Contains(x, y)) { Screen = AppScreen.Connect; return; }
            if (L.Direct.Contains(x, y)) { Screen = AppScreen.Connect; return; } // edit host/port on connect

            for (int i = 0; i < servers.Length; i++)
            {
                if (!servers[i].Joinable || servers[i].Full) continue;
                if (L.JoinButtons[i].Contains(x, y))
                {
                    _host.Value = servers[i].Host;
                    _port.Value = servers[i].Port.ToString();
                    if (IsFormValid()) StartConnect();
                    return;
                }
            }
        }

        private int ParsePortOr(int fallback) => int.TryParse(_port.Value, out int p) ? p : fallback;

        private void TapConnecting(int x, int y, int w, int h)
        {
            if (Layouts.Connecting(w, h).Cancel.Contains(x, y))
            {
                _client.Disconnect();
                Screen = AppScreen.Connect;
            }
        }

        private void TapPlaying(int x, int y, int w, int h)
        {
            if (Layouts.Play(w, h).Pause.Contains(x, y)) { Sfx.Push(SfxId.UiTap); Screen = AppScreen.Paused; return; }
            // Tap-to-turn is a TOUCH affordance only. On desktop (no touch controls) the arena is
            // steered with the arrow keys, so a mouse click in the play area does nothing — the
            // left/right zones aren't reserved and the space is reclaimed for the view.
            if (!_touchControls) return;
            _hasTurned = true;
            Sfx.Push(SfxId.Turn);
            if (TapTurnDecider.Decide(x, w) == TurnDirection.Left) _client.TurnLeft();
            else _client.TurnRight();
        }

        private void TapPaused(int x, int y, int w, int h)
        {
            UiRect[] b = Layouts.Menu(w, h, 3).Buttons;
            if (b[0].Contains(x, y)) Screen = AppScreen.Playing;                                  // RESUME
            else if (b[1].Contains(x, y)) { _settingsReturn = AppScreen.Paused; Screen = AppScreen.Settings; }
            else if (b[2].Contains(x, y)) Screen = AppScreen.ConfirmLeave;                        // DISCONNECT → confirm
        }

        private void TapConfirmLeave(int x, int y, int w, int h)
        {
            UiRect[] b = Layouts.Menu(w, h, 2).Buttons;
            if (b[0].Contains(x, y)) Screen = AppScreen.Paused;                                   // CANCEL
            else if (b[1].Contains(x, y)) { _client.Disconnect(); Screen = AppScreen.Connect; }   // LEAVE
        }

        private static readonly string[] ToggleIds = { "sound", "music", "haptics", "hints" };

        private void TapSettings(int x, int y, int w, int h)
        {
            Layouts.SettingsL L = Layouts.Settings(w, h, CyclePalette.SignatureOptions.Length);
            _name.Bounds = L.Name;

            if (L.Back.Contains(x, y)) { _name.Focused = false; Screen = _settingsReturn; return; }

            _host.Focused = false; _port.Focused = false;
            _name.Focused = L.Name.Contains(x, y);

            for (int i = 0; i < L.Swatches.Length; i++)
                if (L.Swatches[i].Contains(x, y)) Settings.SignatureColor = i;

            SliderHit(L.TurnZone, "turnzone", x, y);
            SliderHit(L.Sens, "sensitivity", x, y);

            for (int i = 0; i < L.Toggles.Length; i++)
                if (L.Toggles[i].Contains(x, y)) Settings.Toggle(ToggleIds[i]);
        }

        // The slider track is thin; accept taps in a taller band around it.
        private void SliderHit(UiRect track, string id, int x, int y)
        {
            if (x >= track.X && x < track.Right && y >= track.CenterY - 22 && y <= track.CenterY + 22)
                Settings.SetSlider(id, (float)(x - track.X) / track.W);
        }

        // ── View ────────────────────────────────────────────────────────────────

        /// <summary>Build the screen-space overlay scene (no gameplay; the host draws that).</summary>
        public Scene BuildOverlay(int w, int h, long nowMs)
        {
            var buf = new SceneBuf();
            switch (Screen)
            {
                case AppScreen.Connect:
                {
                    Layouts.ConnectL L = Layouts.Connect(w, h);
                    _host.Bounds = L.Host; _port.Bounds = L.Port; _name.Bounds = L.Name;
                    ConnectView.Add(buf, _theme, L, _host, _port, _name, _error, IsFormValid(), w, h);
                    break;
                }
                case AppScreen.Connecting:
                    ConnectingView.Add(buf, _theme, Layouts.Connecting(w, h), _host.Value, _port.Value, nowMs, w, h);
                    break;
                case AppScreen.Playing:
                    HudView.Add(buf, _theme, Layouts.Play(w, h), _name.Value,
                                CyclePalette.SignatureOptions[Settings.SignatureColor],
                                _client.Status, Match, _toasts.Active(nowMs), nowMs, w, h);
                    if (_touchControls) TouchOverlay.Add(buf, _theme, w, h, showHint: !_hasTurned && Settings.Hints);
                    break;
                case AppScreen.Paused:
                    MenuView.Add(buf, _theme, Layouts.Menu(w, h, 3), "PAUSED",
                                 new[] { "RESUME", "SETTINGS", "DISCONNECT" }, w, h);
                    break;
                case AppScreen.Settings:
                {
                    Layouts.SettingsL L = Layouts.Settings(w, h, CyclePalette.SignatureOptions.Length);
                    _name.Bounds = L.Name;
                    SettingsView.Add(buf, _theme, L, _name, Settings, CyclePalette.SignatureOptions, w, h);
                    break;
                }
                case AppScreen.ConfirmLeave:
                    MenuView.Add(buf, _theme, Layouts.Menu(w, h, 2), "LEAVE MATCH?",
                                 new[] { "CANCEL", "LEAVE" }, w, h,
                                 subtitle: "ROUND PROGRESS IS LOST.");
                    break;
                case AppScreen.ServerBrowser:
                {
                    ServerEntry[] servers = ServerList.Placeholder(_host.Value, ParsePortOr(4534));
                    ServerBrowserView.Add(buf, _theme, Layouts.Server(w, h, servers.Length), servers, w, h);
                    break;
                }
            }
            return buf.ToScene();
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private UiTextField? Focused() =>
            _host.Focused ? _host : _port.Focused ? _port : _name.Focused ? _name : null;

        /// <summary>The connect form is valid when host & name are non-empty and port is 1–65535.</summary>
        public bool IsFormValid() =>
            !string.IsNullOrWhiteSpace(_host.Value)
            && !string.IsNullOrWhiteSpace(_name.Value)
            && int.TryParse(_port.Value, out int p) && p >= 1 && p <= 65535;

        private void StartConnect()
        {
            int port = int.Parse(_port.Value);
            _error = null;
            Sfx.Push(SfxId.UiTap);
            _client.BeginConnect(_host.Value.Trim(), port, _name.Value.Trim());
            Screen = AppScreen.Connecting;
        }
    }
}
