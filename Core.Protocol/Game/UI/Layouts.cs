using System;

namespace Armagetron.Game.UI
{
    /// <summary>
    /// Pure, resolution-relative layout math for every screen. A layout is computed once from
    /// the viewport (w, h) and used by BOTH the view builder (to draw) and the <see cref="AppShell"/>
    /// input router (to hit-test), so the picture and the touch targets always agree. Sizes
    /// scale with screen height so the same code letterboxes sensibly from an 800² harness to a
    /// 2400×1080 phone. Exact pixel positions are placeholder and tuned via the render harness.
    /// </summary>
    public static class Layouts
    {
        public static int TextScale(int h)  => Clamp(h / 240, 2, 6);
        public static int TitleScale(int h) => Clamp(h / 130, 3, 9);
        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        // ── Connect ───────────────────────────────────────────────────────────────

        public struct ConnectL
        {
            public UiRect Panel, Brand, Host, Port, Name, Status, Connect, Browse, Settings;
            public int TextScale, TitleScale;
        }

        // Redline 5.1: a left brand lockup (left 6%, V-center, max-width 38%) and a right form
        // panel (right 6%, V-center, width 42%) holding host / port+name row / status / CONNECT.
        public static ConnectL Connect(int w, int h)
        {
            int ts = TextScale(h), tts = TitleScale(h);
            int margin = (int)(w * 0.06);
            int pw = (int)(w * 0.42);
            int px = w - margin - pw;

            int inset = 30, fieldH = 46;
            int labelH = PixelFont.Height(ts) + 10;
            // Wrap the panel tightly around its content (top inset → host label+field →
            // port/name row label+field → status → CONNECT → SERVER BROWSER → bottom inset).
            int ph = inset + (labelH + fieldH) + (16 + labelH + fieldH) + 18
                     + 36 + 16 + 52 + 12 + fieldH + inset;
            int py = (h - ph) / 2;

            int fx = px + inset, fw = pw - 2 * inset;

            int y = py + inset + labelH;
            var host = new UiRect(fx, y, fw, fieldH);
            y += fieldH + 16 + labelH;

            int gap = 12;
            int portW = (fw - gap) / 3;            // port flex 1
            int nameW = fw - gap - portW;          // name flex 2
            var port = new UiRect(fx, y, portW, fieldH);
            var name = new UiRect(fx + portW + gap, y, nameW, fieldH);
            y += fieldH + 18;

            var status = new UiRect(fx, y, fw, 36);
            y += 36 + 16;

            var connect = new UiRect(fx, y, fw, 52);
            y += 52 + 12;
            var browse = new UiRect(fx, y, fw, fieldH);

            int sset = PixelFont.Height(ts) * 2 + 12;
            var settings = new UiRect(w - sset - 22, 22, sset, sset);

            var brand = new UiRect(margin, py, (int)(w * 0.38), ph);

            return new ConnectL
            {
                Panel = new UiRect(px, py, pw, ph),
                Brand = brand, Host = host, Port = port, Name = name, Status = status,
                Connect = connect, Browse = browse, Settings = settings,
                TextScale = ts, TitleScale = tts,
            };
        }

        // ── Connecting ──────────────────────────────────────────────────────────

        public struct ConnectingL { public UiRect Cancel; public int CenterY, TextScale; }

        public static ConnectingL Connecting(int w, int h)
        {
            int ts = TextScale(h);
            int bw = Math.Min((int)(w * 0.4), 360);
            int bh = PixelFont.Height(ts) + 2 * SceneBuf.Pad + 10;
            return new ConnectingL
            {
                CenterY = h / 2,
                Cancel = new UiRect((w - bw) / 2, h / 2 + 80, bw, bh),
                TextScale = ts,
            };
        }

        // ── Playing (HUD + touch) ────────────────────────────────────────────────

        public struct PlayL
        {
            public UiRect Pause, Standings, Ping, Timer, LocalChip;
            public int TextScale;
        }

        // Redline 5.2: timer top-center, standings top-left (left 6.5%), ping/connection chip and
        // pause top-right (right 6.5%), local-player chip bottom-center (bottom 5%).
        public static PlayL Play(int w, int h)
        {
            int ts = TextScale(h);
            int topY = (int)(h * 0.05);
            int leftX = (int)(w * 0.065);
            int rightX = w - (int)(w * 0.065);
            int sset = Clamp(h / 15, 32, 52);

            var pause = new UiRect(rightX - sset, topY, sset, sset);

            int standW = Math.Max(260, (int)(w * 0.22));
            int standH = PixelFont.Height(ts) * 2 + 52;
            var standings = new UiRect(leftX, topY, standW, standH);

            int pingW = Math.Max(180, (int)(w * 0.13));
            var ping = new UiRect(pause.X - pingW - 14, topY, pingW, sset);

            int timerW = Math.Max(180, (int)(w * 0.16));
            int timerH = PixelFont.Height(ts * 4) + PixelFont.Height(ts) + 14;
            var timer = new UiRect(w / 2 - timerW / 2, (int)(h * 0.04), timerW, timerH);

            int chipW = Math.Max(300, (int)(w * 0.24));
            int chipH = sset + 8;
            var localChip = new UiRect(w / 2 - chipW / 2, h - (int)(h * 0.05) - chipH, chipW, chipH);

            return new PlayL
            {
                Pause = pause, Standings = standings, Ping = ping, Timer = timer,
                LocalChip = localChip, TextScale = ts,
            };
        }

        // ── Full settings (two-column, matching the design comp) ─────────────────

        public struct SettingsL
        {
            public UiRect Panel, Name, Back;
            public UiRect[] Swatches;          // 8 signature-color squares
            public UiRect TurnZone, Sens;      // slider tracks
            public UiRect[] Toggles;           // cells: 0 sound, 1 music, 2 haptics, 3 hints
            public int TitleY, TextScale, TitleScale;
        }

        public static SettingsL Settings(int w, int h, int swatchCount)
        {
            int ts = TextScale(h), tts = TitleScale(h);
            int pw = Math.Min((int)(w * 0.70), 700);
            int ph = Math.Min((int)(h * 0.82), 600);
            int px = (w - pw) / 2, py = (h - ph) / 2;

            int lx = px + 36, cw = pw - 72, colGap = 28;
            int colW = (cw - colGap) / 2, rx = lx + colW + colGap;
            int fieldH = PixelFont.Height(ts) + 2 * SceneBuf.Pad + 6;
            int labelH = PixelFont.Height(ts) + 6;

            int y = py + PixelFont.Height(tts) + 30;

            // Row 1: name (left) · signature swatches (right)
            var name = new UiRect(lx, y + labelH, colW, fieldH);
            int sw = (colW - 7 * 6) / swatchCount;             // 6px gaps
            var swatches = new UiRect[swatchCount];
            for (int i = 0; i < swatchCount; i++)
                swatches[i] = new UiRect(rx + i * (sw + 6), y + labelH, sw, fieldH);

            y += labelH + fieldH + 26;

            // Row 2: two slider tracks
            var turn = new UiRect(lx, y + labelH + fieldH / 2 - 2, colW, 4);
            var sens = new UiRect(rx, y + labelH + fieldH / 2 - 2, colW, 4);
            y += labelH + fieldH + 26;

            // Rows 3–4: four toggles in a 2×2 grid
            int pillH = PixelFont.Height(ts) + 12;
            var toggles = new UiRect[4];
            for (int i = 0; i < 4; i++)
            {
                int col = i % 2, row = i / 2;
                int cellX = col == 0 ? lx : rx;
                int cellY = y + row * (pillH + 18);
                toggles[i] = new UiRect(cellX, cellY, colW, pillH);
            }
            y += 2 * (pillH + 18) + 14;

            var back = new UiRect(lx, py + ph - fieldH - 24, cw, fieldH + 6);

            return new SettingsL
            {
                Panel = new UiRect(px, py, pw, ph),
                Name = name, Swatches = swatches, TurnZone = turn, Sens = sens,
                Toggles = toggles, Back = back,
                TitleY = py + 22, TextScale = ts, TitleScale = tts,
            };
        }

        // ── Server browser ───────────────────────────────────────────────────────

        public struct ServerL
        {
            public UiRect Panel, Direct, Back;
            public UiRect[] Rows, JoinButtons;
            public int TitleY, TextScale, TitleScale;
        }

        public static ServerL Server(int w, int h, int rowCount)
        {
            int ts = TextScale(h), tts = TitleScale(h);
            int pw = Math.Min((int)(w * 0.8), 900);
            int ph = Math.Min((int)(h * 0.86), 720);
            int px = (w - pw) / 2, py = (h - ph) / 2;

            int rowH = PixelFont.Height(ts) + 2 * SceneBuf.Pad + 10;
            int lx = px + 24, cw = pw - 48;
            int firstY = py + PixelFont.Height(tts) + PixelFont.Height(ts) + 40;

            var rows = new UiRect[rowCount];
            var joins = new UiRect[rowCount];
            int joinW = PixelFont.MeasureWidth("DIRECT", ts) + 2 * SceneBuf.Pad;
            for (int i = 0; i < rowCount; i++)
            {
                rows[i] = new UiRect(lx, firstY + i * (rowH + 8), cw, rowH);
                joins[i] = new UiRect(rows[i].Right - joinW - 6, rows[i].Y + 4, joinW, rowH - 8);
            }

            int btnH = rowH;
            var direct = new UiRect(lx, py + ph - 2 * btnH - 20, cw, btnH);
            var back = new UiRect(lx, py + ph - btnH - 12, cw, btnH);

            return new ServerL
            {
                Panel = new UiRect(px, py, pw, ph),
                Rows = rows, JoinButtons = joins, Direct = direct, Back = back,
                TitleY = py + 20, TextScale = ts, TitleScale = tts,
            };
        }

        // ── Pause / Settings menus (shared vertical button stack) ────────────────

        public struct MenuL { public UiRect Panel; public UiRect[] Buttons; public int TitleY, TextScale, TitleScale; }

        public static MenuL Menu(int w, int h, int buttonCount)
        {
            int ts = TextScale(h), tts = TitleScale(h);
            int pw = Math.Min((int)(w * 0.5), 520);
            int bh = PixelFont.Height(ts) + 2 * SceneBuf.Pad + 12;
            int gap = 18;
            int contentH = PixelFont.Height(tts) + 40 + buttonCount * (bh + gap);
            int ph = contentH + 56;
            int px = (w - pw) / 2, py = (h - ph) / 2;

            int bx = px + 40, bw = pw - 80;
            int firstY = py + PixelFont.Height(tts) + 48;
            var buttons = new UiRect[buttonCount];
            for (int i = 0; i < buttonCount; i++)
                buttons[i] = new UiRect(bx, firstY + i * (bh + gap), bw, bh);

            return new MenuL
            {
                Panel = new UiRect(px, py, pw, ph),
                Buttons = buttons,
                TitleY = py + 24,
                TextScale = ts, TitleScale = tts,
            };
        }
    }
}
