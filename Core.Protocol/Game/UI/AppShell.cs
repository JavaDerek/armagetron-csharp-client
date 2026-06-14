using Armagetron.Protocol;

namespace Armagetron.Game.UI
{
    /// <summary>Top-level screens the client can be showing.</summary>
    public enum AppScreen { Connect, Connecting, Playing, Paused, Settings, ConfirmLeave }

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
        public bool ExitRequested { get; private set; }
        public bool SoundEnabled { get; private set; } = true;

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
                        break;
                    case MatchEvent.RoundEnd:
                        Match.OnRoundEnd();
                        _toasts.Push("ROUND OVER", _theme.Text, nowMs);
                        break;
                    case MatchEvent.LocalDied:
                        Match.OnLocalDied();
                        _toasts.Push("YOU CRASHED", _theme.Danger, nowMs);
                        break;
                }
            }

            if (Screen == AppScreen.Connecting)
            {
                if (_client.Status == ConnectionStatus.Connected) { Screen = AppScreen.Playing; _error = null; }
                else if (_client.Status == ConnectionStatus.Failed)
                {
                    Screen = AppScreen.Connect;
                    _error = _client.LastError ?? "COULD NOT CONNECT";
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
                case AppScreen.ConfirmLeave: Screen = AppScreen.Paused; break;
                case AppScreen.Connecting: _client.Disconnect(); Screen = AppScreen.Connect; break;
                case AppScreen.Connect:    ExitRequested = true; break;
            }
        }

        /// <summary>A discrete turn (desktop arrow keys). Only acts while Playing.</summary>
        public void OnTurn(TurnDirection dir)
        {
            if (Screen != AppScreen.Playing) return;
            _hasTurned = true;
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
            }
        }

        private void TapConnect(int x, int y, int w, int h)
        {
            Layouts.ConnectL L = Layouts.Connect(w, h);
            _host.Bounds = L.Host; _port.Bounds = L.Port; _name.Bounds = L.Name;

            if (L.Settings.Contains(x, y)) { _settingsReturn = AppScreen.Connect; Screen = AppScreen.Settings; return; }

            _host.Focused = L.Host.Contains(x, y);
            _port.Focused = L.Port.Contains(x, y);
            _name.Focused = L.Name.Contains(x, y);

            if (L.Connect.Contains(x, y) && IsFormValid()) StartConnect();
        }

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
            if (Layouts.Play(w, h).Pause.Contains(x, y)) { Screen = AppScreen.Paused; return; }
            // Anywhere else is a tap-to-turn (Android steering).
            _hasTurned = true;
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

        private void TapSettings(int x, int y, int w, int h)
        {
            UiRect[] b = Layouts.Menu(w, h, 2).Buttons;
            if (b[0].Contains(x, y)) SoundEnabled = !SoundEnabled;
            else if (b[1].Contains(x, y)) Screen = _settingsReturn;
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
                    HudView.Add(buf, _theme, Layouts.Play(w, h), _name.Value, _client.Status, Match,
                                _toasts.Active(nowMs), nowMs, w, h);
                    if (_touchControls) TouchOverlay.Add(buf, _theme, w, h, showHint: !_hasTurned);
                    break;
                case AppScreen.Paused:
                    MenuView.Add(buf, _theme, Layouts.Menu(w, h, 3), "PAUSED",
                                 new[] { "RESUME", "SETTINGS", "DISCONNECT" }, w, h);
                    break;
                case AppScreen.Settings:
                    MenuView.Add(buf, _theme, Layouts.Menu(w, h, 2), "SETTINGS",
                                 new[] { "SOUND: " + (SoundEnabled ? "ON" : "OFF"), "BACK" }, w, h);
                    break;
                case AppScreen.ConfirmLeave:
                    MenuView.Add(buf, _theme, Layouts.Menu(w, h, 2), "LEAVE MATCH?",
                                 new[] { "CANCEL", "LEAVE" }, w, h,
                                 subtitle: "ROUND PROGRESS IS LOST.");
                    break;
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
            _client.BeginConnect(_host.Value.Trim(), port, _name.Value.Trim());
            Screen = AppScreen.Connecting;
        }
    }
}
