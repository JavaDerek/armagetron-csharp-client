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
            public UiRect Panel, Host, Port, Name, Connect, Browse, Settings;
            public int TitleY, SubY, ErrorY, TextScale, TitleScale;
        }

        public static ConnectL Connect(int w, int h)
        {
            int ts = TextScale(h), tts = TitleScale(h);
            int pw = Math.Min((int)(w * 0.72), 760);
            int ph = Math.Min((int)(h * 0.86), 700);
            int px = (w - pw) / 2, py = (h - ph) / 2;

            int fieldX = px + 44, fieldW = pw - 88;
            int fieldH = PixelFont.Height(ts) + 2 * SceneBuf.Pad + 8;
            int labelH = PixelFont.Height(ts) + 6;

            int titleY = py + 28;
            int subY   = titleY + PixelFont.Height(tts) + 14;
            int firstY = subY + PixelFont.Height(ts) + 44 + labelH;
            int gap    = fieldH + labelH + 18;

            var host = new UiRect(fieldX, firstY, fieldW, fieldH);
            var port = new UiRect(fieldX, firstY + gap, fieldW, fieldH);
            var name = new UiRect(fieldX, firstY + 2 * gap, fieldW, fieldH);

            int by = firstY + 3 * gap + 8;
            var connect = new UiRect(fieldX, by, fieldW, fieldH + 10);
            int errorY = by + fieldH + 10 + 14;
            var browse = new UiRect(fieldX, errorY + PixelFont.Height(ts) + 12, fieldW, fieldH);

            int sset = PixelFont.Height(ts) * 2 + 8;
            var settings = new UiRect(w - sset - 16, 16, sset, sset);

            return new ConnectL
            {
                Panel = new UiRect(px, py, pw, ph),
                Host = host, Port = port, Name = name, Connect = connect, Browse = browse, Settings = settings,
                TitleY = titleY, SubY = subY, ErrorY = errorY, TextScale = ts, TitleScale = tts,
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

        public struct PlayL { public UiRect Pause; public int TextScale, Margin; }

        public static PlayL Play(int w, int h)
        {
            int ts = TextScale(h);
            int sset = PixelFont.Height(ts) * 2 + 8;
            return new PlayL
            {
                Pause = new UiRect(w - sset - 14, 14, sset, sset),
                TextScale = ts,
                Margin = 14,
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
