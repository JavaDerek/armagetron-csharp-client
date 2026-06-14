using Armagetron.Protocol;

namespace Armagetron.Game.UI
{
    /// <summary>
    /// Per-screen view builders: each appends its placeholder visuals to a <see cref="SceneBuf"/>
    /// from a pure layout + state. No GPU, no platform — fully unit-testable by asserting on the
    /// emitted draw commands. All colors come from <see cref="UiTheme"/> and all glyphs from the
    /// placeholder <see cref="PixelFont"/>, so the designer's assets drop in without touching these.
    /// </summary>
    public static class ConnectView
    {
        public static void Add(SceneBuf buf, UiTheme t, Layouts.ConnectL L,
                               UiTextField host, UiTextField port, UiTextField name,
                               string? error, bool formValid, int w, int h)
        {
            buf.Fill(new UiRect(0, 0, w, h), t.Background);
            buf.Fill(L.Panel, t.Panel);
            buf.Border(L.Panel, t.PanelBorder);

            buf.TextCenter("ARMAGETRON", L.Panel.CenterX, L.TitleY, t.Accent, L.TitleScale);
            buf.TextCenter("CONNECT TO SERVER", L.Panel.CenterX, L.SubY, t.TextMuted, L.TextScale);

            buf.DrawField(host, t, L.TextScale);
            buf.DrawField(port, t, L.TextScale);
            buf.DrawField(name, t, L.TextScale);

            buf.DrawButton(new UiButton("connect", L.Connect, "CONNECT") { Enabled = formValid },
                           t, L.TextScale);
            // Placeholder gear: a '*' until the designer's settings icon arrives (DESIGN_BRIEF §8).
            buf.DrawButton(new UiButton("settings", L.Settings, "*"), t, L.TextScale);

            if (!string.IsNullOrEmpty(error))
                buf.TextCenter(error, L.Panel.CenterX, L.ErrorY, t.Danger, L.TextScale);
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
                           t.Text, L.TextScale * 2);
            buf.TextCenter(host + ":" + port, w / 2, L.CenterY + 8, t.TextMuted, L.TextScale);
            buf.DrawButton(new UiButton("cancel", L.Cancel, "CANCEL"), t, L.TextScale);
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

            buf.TextLeft(playerName, m, m, nameColor, ts);
            buf.TextLeft("TIME " + match.TimeLabel(now), m, m + line, t.Text, ts);
            buf.TextLeft("ROUND " + match.RoundNumber + "   CYCLES " + match.CycleCount,
                         m, m + 2 * line, t.TextMuted, ts);

            // Connection indicator: a colored dot left of the pause button.
            RenderColor dot = status == ConnectionStatus.Connected ? t.Success
                            : status == ConnectionStatus.Connecting ? t.Accent
                                                                    : t.Danger;
            int d = PixelFont.Height(ts);
            buf.Fill(new UiRect(L.Pause.X - d - 12, L.Pause.CenterY - d / 2, d, d), dot);

            // Placeholder pause glyph "II" until the designer's pause icon arrives (DESIGN_BRIEF §8).
            buf.DrawButton(new UiButton("pause", L.Pause, "II"), t, ts);

            // Round banner: a big centred announcement at round start / round over. NB a true
            // pre-round 3·2·1 countdown needs the desc=24 negative game_time decode (PROTOCOL.md
            // open item); until then this is an honest round-start/over banner placeholder.
            int bannerY = h / 4;
            if (match.RoundActive && match.ElapsedMs(now) < 2_500)
                buf.TextCenter("ROUND " + match.RoundNumber, w / 2, bannerY, t.Accent, ts * 3);
            else if (!match.RoundActive && match.RoundNumber > 0)
                buf.TextCenter("ROUND OVER", w / 2, bannerY, t.Text, ts * 3);

            // Death / spectator overlay (design 5.7): ELIMINATED + a spectating line. The arena
            // keeps playing underneath at full brightness (placeholder for the design's dimmed
            // spectator camera, which needs per-cycle follow once cycles are individually tracked).
            if (!match.LocalAlive)
            {
                buf.TextCenter("ELIMINATED", w / 2, h / 2 - PixelFont.Height(ts * 3), t.Danger, ts * 3);
                buf.TextCenter("SPECTATING - " + match.CycleCount + " CYCLES",
                               w / 2, h / 2 + PixelFont.Height(ts), t.TextMuted, ts);
            }

            // Toast stack: newest at the bottom, older ones rising above it.
            int toastY = (int)(h * 0.72);
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                buf.TextCenter(toasts[i].Text, w / 2, toastY, toasts[i].Color, ts);
                toastY -= PixelFont.Height(ts) + 8;
            }
        }
    }

    public static class TouchOverlay
    {
        /// <summary>The tap-to-turn affordance (Android): a faint center divider, dim chevrons,
        /// and a first-run hint until the player has turned once.</summary>
        public static void Add(SceneBuf buf, UiTheme t, int w, int h, bool showHint)
        {
            var faint = new RenderColor(255, 255, 255, 45);
            buf.Line(new Vec2(w / 2f, 0), new Vec2(w / 2f, h), faint, 2f);

            int ts = Layouts.TextScale(h);
            var chev = new RenderColor(255, 255, 255, 70);
            int cy = h - PixelFont.Height(ts * 2) - 28;
            buf.TextCenter("<", w / 4, cy, chev, ts * 2);
            buf.TextCenter(">", w * 3 / 4, cy, chev, ts * 2);

            if (showHint)
                buf.TextCenter("TAP LEFT / RIGHT TO TURN", w / 2, h - PixelFont.Height(ts) - 12,
                               t.Accent, ts);
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
            buf.Fill(new UiRect(0, 0, w, h), new RenderColor(0, 0, 0, 170));
            buf.Fill(L.Panel, t.Panel);
            buf.Border(L.Panel, t.PanelBorder);
            buf.TextCenter("SETTINGS", L.Panel.CenterX, L.TitleY, t.Accent, L.TitleScale);

            // Player name (left) + signature swatches (right)
            buf.DrawField(name, t, ts);
            buf.TextLeft("SIGNATURE COLOR", L.Swatches[0].X, L.Swatches[0].Y - PixelFont.Height(ts) - 5, t.TextMuted, ts);
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
                buf.TextLeft(ToggleLabels[i], cell.X, cell.CenterY - PixelFont.Height(ts) / 2, t.Text, ts);
                var pill = new UiRect(cell.Right - 60, cell.Y, 60, cell.H);
                buf.DrawToggle(pill, on[i], t);
            }

            buf.DrawButton(new UiButton("back", L.Back, "BACK"), t, ts);
        }

        private static void Label(SceneBuf buf, UiTheme t, int ts, string text, string value, UiRect track)
        {
            int y = track.Y - PixelFont.Height(ts) - 10;
            buf.TextLeft(text, track.X, y, t.TextMuted, ts);
            buf.TextLeft(value, track.Right - PixelFont.MeasureWidth(value, ts), y, t.Accent, ts);
        }
    }

    public static class MenuView
    {
        /// <summary>A dimmed modal with a title (optional subtitle) and a vertical button stack.</summary>
        public static void Add(SceneBuf buf, UiTheme t, Layouts.MenuL L, string title,
                               string[] labels, int w, int h, string? subtitle = null)
        {
            buf.Fill(new UiRect(0, 0, w, h), new RenderColor(0, 0, 0, 170)); // dim
            buf.Fill(L.Panel, t.Panel);
            buf.Border(L.Panel, t.PanelBorder);
            buf.TextCenter(title, L.Panel.CenterX, L.TitleY, t.Accent, L.TitleScale);
            if (!string.IsNullOrEmpty(subtitle))
                buf.TextCenter(subtitle, L.Panel.CenterX, L.TitleY + PixelFont.Height(L.TitleScale) + 8,
                               t.TextMuted, L.TextScale);

            for (int i = 0; i < labels.Length && i < L.Buttons.Length; i++)
                buf.DrawButton(new UiButton("menu" + i, L.Buttons[i], labels[i]), t, L.TextScale);
        }
    }
}
