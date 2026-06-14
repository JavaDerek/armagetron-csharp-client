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

            // Left brand lockup (left 6%, V-centered): wordmark + ADVANCED sub + tagline.
            int by = L.Brand.CenterY;
            buf.TextLeft("ARMAGETRON", L.Brand.X, by - PixelFont.Height(L.TitleScale) - 6,
                         t.Accent, L.TitleScale, FontRole.Title);
            buf.TextLeft("ADVANCED", L.Brand.X + 2, by + 14, t.TextMuted, L.TextScale, FontRole.Heading);
            buf.TextLeft("NEON LIGHTCYCLE COMBAT", L.Brand.X + 2,
                         by + 14 + PixelFont.Height(L.TextScale) + 16, t.PanelBorder, L.TextScale, FontRole.Label);

            // Right form panel.
            buf.Panel(L.Panel);
            buf.DrawField(host, t, L.TextScale);
            buf.DrawField(port, t, L.TextScale);
            buf.DrawField(name, t, L.TextScale);

            // Status strip: error (danger) or a ready/idle hint (success/muted).
            if (!string.IsNullOrEmpty(error))
            {
                buf.Fill(L.Status, new RenderColor(0xFF, 0x4D, 0x5E, 0x22));
                buf.Border(L.Status, t.Danger, 1);
                buf.TextLeftMid(error, L.Status.X + 12, L.Status.CenterY, t.Danger, L.TextScale, FontRole.Label);
            }
            else
            {
                RenderColor c = formValid ? t.Success : t.TextMuted;
                buf.TextLeftMid(formValid ? "READY TO CONNECT" : "ENTER SERVER DETAILS",
                                L.Status.X + 2, L.Status.CenterY, c, L.TextScale, FontRole.Label);
            }

            buf.DrawButton(new UiButton("connect", L.Connect, "CONNECT") { Enabled = formValid },
                           t, L.TextScale);
            buf.DrawButton(new UiButton("browse", L.Browse, "SERVER BROWSER"), t, L.TextScale,
                           ButtonStyle.Secondary);
            // Real settings gear icon (DESIGN_BRIEF §8) in place of the old '*' placeholder.
            buf.Icon("gear", L.Settings, pad: 6);
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
            int ts = L.TextScale;

            // Round timer (top-center): big mono clock + "ROUND n / 5" caption.
            buf.TextCenter(match.TimeLabel(now), L.Timer.CenterX, L.Timer.Y, t.Text, ts * 4, FontRole.Mono);
            buf.TextCenter("ROUND " + match.RoundNumber + " / 5",
                           L.Timer.CenterX, L.Timer.Bottom - PixelFont.Height(ts), t.TextMuted, ts, FontRole.Label);

            // Standings panel (top-left).
            buf.Panel(L.Standings);
            buf.TextLeft("STANDINGS", L.Standings.X + 18, L.Standings.Y + 16, t.TextMuted, ts, FontRole.Label);
            int rowMid = L.Standings.Y + 20 + PixelFont.Height(ts) + 14;
            buf.Fill(new UiRect(L.Standings.X + 18, rowMid - 6, 12, 12), nameColor);
            buf.TextLeftMid(playerName, L.Standings.X + 18 + 18, rowMid, t.Text, ts, FontRole.Label);
            buf.TextRight("x" + match.CycleCount, L.Standings.Right - 22, rowMid - PixelFont.Height(ts) / 2,
                          t.TextMuted, ts, FontRole.Mono);

            // Connection / ping chip (top-right, left of pause).
            buf.Panel(L.Ping);
            RenderColor dot = status == ConnectionStatus.Connected ? t.Success
                            : status == ConnectionStatus.Connecting ? t.Accent
                                                                    : t.Danger;
            int d = PixelFont.Height(ts);
            buf.Fill(new UiRect(L.Ping.X + 12, L.Ping.CenterY - d / 2, d, d), dot);
            string conn = status == ConnectionStatus.Connected ? "LIVE"
                        : status == ConnectionStatus.Connecting ? "SYNC" : "DROP";
            buf.TextLeftMid(conn, L.Ping.X + 12 + d + 10, L.Ping.CenterY, dot, ts, FontRole.Mono);

            // Pause icon (top-right corner).
            buf.Icon("pause", L.Pause, pad: 4);

            // Round banner: a big centred announcement at round start / round over. (A true
            // pre-round 3·2·1 countdown needs the desc=24 negative game_time decode.)
            int bannerY = (int)(h * 0.30);
            if (match.RoundActive && match.ElapsedMs(now) < 2_500)
                buf.TextCenter("ROUND " + match.RoundNumber, w / 2, bannerY, t.Accent, ts * 3, FontRole.Title);
            else if (!match.RoundActive && match.RoundNumber > 0)
                buf.TextCenter("ROUND OVER", w / 2, bannerY, t.Text, ts * 3, FontRole.Title);

            // Local-player chip (bottom-center): cyan-bordered pill with the player's name.
            if (match.LocalAlive)
            {
                buf.Panel(L.LocalChip);
                buf.Fill(new UiRect(L.LocalChip.X + 16, L.LocalChip.CenterY - d / 2, d, d), nameColor);
                buf.TextLeftMid(playerName, L.LocalChip.X + 16 + d + 12, L.LocalChip.CenterY, t.Text, ts, FontRole.Label);
                buf.TextRight("YOU", L.LocalChip.Right - 16, L.LocalChip.CenterY - PixelFont.Height(ts) / 2,
                              t.Accent, ts, FontRole.Mono);
            }
            else
            {
                // Death / spectator overlay (design 5.7): ELIMINATED + a spectating line.
                buf.TextCenter("ELIMINATED", w / 2, h / 2 - PixelFont.Height(ts * 3), t.Danger, ts * 3, FontRole.Title);
                buf.TextCenter("SPECTATING - " + match.CycleCount + " CYCLES",
                               w / 2, h / 2 + PixelFont.Height(ts), t.TextMuted, ts, FontRole.Label);
            }

            // Toast stack (top-left region per design 11), under the standings panel.
            int toastY = L.Standings.Bottom + 16;
            for (int i = 0; i < toasts.Count; i++)
            {
                buf.TextLeft(toasts[i].Text, L.Standings.X + 4, toastY, toasts[i].Color, ts, FontRole.Label);
                toastY += PixelFont.Height(ts) + 8;
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
