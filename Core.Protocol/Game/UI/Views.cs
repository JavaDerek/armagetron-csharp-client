using Armagetron.Protocol;

namespace Armagetron.Game.UI
{
    /// <summary>
    /// Per-screen view builders: each appends its visuals to a <see cref="SceneBuf"/> from a pure
    /// layout + state. No GPU, no platform — fully unit-testable by asserting on the emitted draw
    /// commands. Colors come from <see cref="UiTheme"/>; chrome is the designer's nine-slice
    /// panels/buttons and icon sprites; text names a <see cref="FontRole"/> (Orbitron for
    /// titles/banners, Rajdhani for UI/body, Share Tech Mono for numerals) which the head renders
    /// with the real OFL fonts.
    /// </summary>
    public static class ConnectView
    {
        public static void Add(SceneBuf buf, UiTheme t, Layouts.ConnectL L,
                               UiTextField host, UiTextField port, UiTextField name,
                               string? error, bool formValid, int w, int h)
        {
            buf.Fill(new UiRect(0, 0, w, h), t.Background);
            buf.Panel(L.Panel);

            buf.TextCenter("ARMAGETRON", L.Panel.CenterX, L.TitleY, t.Accent, L.TitleScale, FontRole.Title);
            buf.TextCenter("CONNECT TO SERVER", L.Panel.CenterX, L.SubY, t.TextMuted, L.TextScale, FontRole.Label);

            buf.DrawField(host, t, L.TextScale);
            buf.DrawField(port, t, L.TextScale);
            buf.DrawField(name, t, L.TextScale);

            buf.DrawButton(new UiButton("connect", L.Connect, "CONNECT") { Enabled = formValid },
                           t, L.TextScale);
            buf.DrawButton(new UiButton("browse", L.Browse, "SERVER BROWSER"), t, L.TextScale,
                           ButtonStyle.Secondary);
            // Real settings gear icon (DESIGN_BRIEF §8) in place of the old '*' placeholder.
            buf.Icon("gear", L.Settings, pad: 6);

            if (!string.IsNullOrEmpty(error))
                buf.TextCenter(error, L.Panel.CenterX, L.ErrorY, t.Danger, L.TextScale, FontRole.Label);
        }
    }

    public static class ConnectingView
    {
        public static void Add(SceneBuf buf, UiTheme t, Layouts.ConnectingL L,
                               string host, string port, long now, int w, int h)
        {
            buf.Fill(new UiRect(0, 0, w, h), t.Background);
            int dots = (int)(now / 400 % 4);
            buf.TextCenter("CONNECTING" + new string('.', dots),
                           w / 2, L.CenterY - PixelFont.Height(L.TextScale * 2) - 24,
                           t.Text, L.TextScale * 2, FontRole.Title);
            buf.TextCenter(host + ":" + port, w / 2, L.CenterY + 8, t.TextMuted, L.TextScale, FontRole.Mono);
            buf.DrawButton(new UiButton("cancel", L.Cancel, "CANCEL"), t, L.TextScale, ButtonStyle.Secondary);
        }
    }

    public static class HudView
    {
        /// <summary>Append the in-game HUD (status corner, round banner, toasts) over the
        /// gameplay already in <paramref name="buf"/>.</summary>
        public static void Add(SceneBuf buf, UiTheme t, Layouts.PlayL L,
                               string playerName, RenderColor nameColor,
                               ConnectionStatus status, MatchState match,
                               System.Collections.Generic.IReadOnlyList<Toast> toasts,
                               long now, int w, int h)
        {
            int ts = L.TextScale, m = L.Margin, line = PixelFont.Height(ts) + 6;

            buf.TextLeft(playerName, m, m, nameColor, ts, FontRole.Label);
            buf.TextLeft("TIME " + match.TimeLabel(now), m, m + line, t.Text, ts, FontRole.Mono);
            buf.TextLeft("ROUND " + match.RoundNumber + "   CYCLES " + match.CycleCount,
                         m, m + 2 * line, t.TextMuted, ts, FontRole.Mono);

            // Connection indicator: a colored dot left of the pause button.
            RenderColor dot = status == ConnectionStatus.Connected ? t.Success
                            : status == ConnectionStatus.Connecting ? t.Accent
                                                                    : t.Danger;
            int d = PixelFont.Height(ts);
            buf.Fill(new UiRect(L.Pause.X - d - 12, L.Pause.CenterY - d / 2, d, d), dot);

            // Real pause icon in place of the old "II" placeholder.
            buf.Icon("pause", L.Pause, pad: 4);

            // Round banner: a big centred announcement at round start / round over. NB a true
            // pre-round 3·2·1 countdown needs the desc=24 negative game_time decode (PROTOCOL.md
            // open item); until then this is an honest round-start/over banner placeholder.
            int bannerY = h / 4;
            if (match.RoundActive && match.ElapsedMs(now) < 2_500)
                buf.TextCenter("ROUND " + match.RoundNumber, w / 2, bannerY, t.Accent, ts * 3, FontRole.Title);
            else if (!match.RoundActive && match.RoundNumber > 0)
                buf.TextCenter("ROUND OVER", w / 2, bannerY, t.Text, ts * 3, FontRole.Title);

            // Death / spectator overlay (design 5.7): ELIMINATED + a spectating line. The arena
            // keeps playing underneath (placeholder for the design's dimmed spectator camera).
            if (!match.LocalAlive)
            {
                buf.TextCenter("ELIMINATED", w / 2, h / 2 - PixelFont.Height(ts * 3), t.Danger, ts * 3, FontRole.Title);
                buf.TextCenter("SPECTATING - " + match.CycleCount + " CYCLES",
                               w / 2, h / 2 + PixelFont.Height(ts), t.TextMuted, ts, FontRole.Label);
            }

            // Toast stack: newest at the bottom, older ones rising above it.
            int toastY = (int)(h * 0.72);
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                buf.TextCenter(toasts[i].Text, w / 2, toastY, toasts[i].Color, ts, FontRole.Label);
                toastY -= PixelFont.Height(ts) + 8;
            }
        }
    }

    public static class TouchOverlay
    {
        /// <summary>The tap-to-turn affordance (Android): a faint center divider, dim chevron
        /// icons, and a first-run hint until the player has turned once.</summary>
        public static void Add(SceneBuf buf, UiTheme t, int w, int h, bool showHint)
        {
            var faint = new RenderColor(255, 255, 255, 45);
            buf.Line(new Vec2(w / 2f, 0), new Vec2(w / 2f, h), faint, 2f);

            int ts = Layouts.TextScale(h);
            int cs = PixelFont.Height(ts * 3);
            int cy = h - cs - 20;
            buf.Icon("chevron-left", new UiRect(w / 4 - cs / 2, cy, cs, cs));
            buf.Icon("chevron-right", new UiRect(w * 3 / 4 - cs / 2, cy, cs, cs));

            if (showHint)
                buf.TextCenter("TAP LEFT / RIGHT TO TURN", w / 2, h - PixelFont.Height(ts) - 12,
                               t.Accent, ts, FontRole.Label);
        }
    }

    public static class SettingsView
    {
        private static readonly string[] ToggleLabels = { "SOUND FX", "MUSIC", "HAPTICS", "HINTS" };

        public static void Add(SceneBuf buf, UiTheme t, Layouts.SettingsL L,
                               UiTextField name, SettingsState s,
                               System.Collections.Generic.IReadOnlyList<RenderColor> swatchColors,
                               int w, int h)
        {
            int ts = L.TextScale;
            buf.Fill(new UiRect(0, 0, w, h), new RenderColor(4, 6, 12, 140)); // scrim
            buf.Panel(L.Panel);
            buf.TextCenter("SETTINGS", L.Panel.CenterX, L.TitleY, t.Accent, L.TitleScale, FontRole.Title);

            // Player name (left) + signature swatches (right)
            buf.DrawField(name, t, ts);
            buf.TextLeft("SIGNATURE COLOR", L.Swatches[0].X, L.Swatches[0].Y - PixelFont.Height(ts) - 5,
                         t.TextMuted, ts, FontRole.Label);
            for (int i = 0; i < L.Swatches.Length; i++)
            {
                buf.Fill(L.Swatches[i], swatchColors[i % swatchColors.Count]);
                buf.Border(L.Swatches[i], i == s.SignatureColor ? t.Text : t.PanelBorder,
                           i == s.SignatureColor ? 3 : 1);
            }

            // Sliders with live value labels
            Label(buf, t, ts, "TURN ZONE", (int)(s.TurnZone * 100) + "%", L.TurnZone);
            buf.DrawSlider(L.TurnZone, s.TurnZone, t);
            Label(buf, t, ts, "SENS", s.Sensitivity.ToString("0.0"), L.Sens);
            buf.DrawSlider(L.Sens, s.Sensitivity, t);

            // Toggles (label left, pill right)
            bool[] on = { s.Sound, s.Music, s.Haptics, s.Hints };
            for (int i = 0; i < L.Toggles.Length; i++)
            {
                UiRect cell = L.Toggles[i];
                buf.TextLeft(ToggleLabels[i], cell.X, cell.CenterY - PixelFont.Height(ts) / 2, t.Text, ts, FontRole.Label);
                var pill = new UiRect(cell.Right - 60, cell.Y, 60, cell.H);
                buf.DrawToggle(pill, on[i], t);
            }

            buf.DrawButton(new UiButton("back", L.Back, "BACK"), t, ts, ButtonStyle.Secondary);
        }

        private static void Label(SceneBuf buf, UiTheme t, int ts, string text, string value, UiRect track)
        {
            int y = track.Y - PixelFont.Height(ts) - 10;
            buf.TextLeft(text, track.X, y, t.TextMuted, ts, FontRole.Label);
            buf.TextRight(value, track.Right, y, t.Accent, ts, FontRole.Mono);
        }
    }

    public static class MenuView
    {
        /// <summary>A scrimmed modal with a title (optional subtitle) and a vertical button stack.
        /// The first button is the primary CTA; the rest are outlined secondary buttons
        /// (design 5.7/10).</summary>
        public static void Add(SceneBuf buf, UiTheme t, Layouts.MenuL L, string title,
                               string[] labels, int w, int h, string? subtitle = null)
        {
            buf.Fill(new UiRect(0, 0, w, h), new RenderColor(4, 6, 12, 180)); // scrim
            buf.Panel(L.Panel);
            buf.TextCenter(title, L.Panel.CenterX, L.TitleY, t.Accent, L.TitleScale, FontRole.Title);
            if (!string.IsNullOrEmpty(subtitle))
                buf.TextCenter(subtitle, L.Panel.CenterX, L.TitleY + PixelFont.Height(L.TitleScale) + 8,
                               t.TextMuted, L.TextScale, FontRole.Label);

            for (int i = 0; i < labels.Length && i < L.Buttons.Length; i++)
            {
                ButtonStyle style = i == 0 ? ButtonStyle.Primary : ButtonStyle.Secondary;
                buf.DrawButton(new UiButton("menu" + i, L.Buttons[i], labels[i]), t, L.TextScale, style);
            }
        }
    }
}
