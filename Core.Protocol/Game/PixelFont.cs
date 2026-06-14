using System.Collections.Generic;

namespace Armagetron.Game
{
    /// <summary>
    /// One character cell of the <see cref="PixelFont"/>: 7 rows whose low 5 bits select lit
    /// columns (bit 4 = leftmost). A pure value with no GPU dependency — the front-end reads
    /// <see cref="IsLit"/> and draws a filled <see cref="RenderRect"/> per lit cell.
    /// </summary>
    public readonly struct Glyph
    {
        private readonly byte[] _rows;
        internal Glyph(byte[] rows) { _rows = rows; }

        /// <summary>True if the cell at (col 0–4, row 0–6) is part of the glyph.</summary>
        public bool IsLit(int col, int row) =>
            (_rows[row] & (1 << (PixelFont.GlyphWidth - 1 - col))) != 0;

        /// <summary>Number of lit cells — handy for tests and for the empty-space check.</summary>
        public int LitCount()
        {
            int n = 0;
            for (int r = 0; r < PixelFont.GlyphHeight; r++)
                for (int c = 0; c < PixelFont.GlyphWidth; c++)
                    if (IsLit(c, r)) n++;
            return n;
        }
    }

    /// <summary>
    /// PLACEHOLDER FONT. A compact, code-defined 5×7 bitmap font covering A–Z, 0–9 and the
    /// punctuation the UI needs. It exists so the screens, HUD and connect flow can render
    /// real text with NO art asset; when the designer delivers a licensed TTF/OTF (see
    /// DESIGN_BRIEF.md §3/§9) this whole class is swapped for a real SpriteFont and nothing
    /// above it changes — the front-end keeps calling <see cref="Get"/>/<see cref="MeasureWidth"/>.
    ///
    /// Lowercase maps to the uppercase glyph; unknown characters render as a hollow box so a
    /// missing glyph is visible rather than silent.
    /// </summary>
    public static class PixelFont
    {
        public const int GlyphWidth = 5;
        public const int GlyphHeight = 7;
        public const int Spacing = 1; // blank columns between adjacent glyphs

        /// <summary>Horizontal advance of one glyph (cell + inter-glyph spacing), unscaled.</summary>
        public static int Advance => GlyphWidth + Spacing;

        /// <summary>Pixel width of <paramref name="text"/> at the given integer scale.</summary>
        public static int MeasureWidth(string text, int scale)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (text.Length * Advance - Spacing) * scale;
        }

        /// <summary>Pixel height of a line at the given integer scale.</summary>
        public static int Height(int scale) => GlyphHeight * scale;

        /// <summary>The glyph for <paramref name="c"/> (uppercased; box if unknown).</summary>
        public static Glyph Get(char c)
        {
            if (c >= 'a' && c <= 'z') c = (char)(c - 32);
            return new Glyph(Glyphs.TryGetValue(c, out byte[]? rows) ? rows : Box);
        }

        // ── Glyph data ──────────────────────────────────────────────────────────

        private static byte[] R(params string[] rows)
        {
            var b = new byte[GlyphHeight];
            for (int row = 0; row < GlyphHeight; row++)
            {
                byte v = 0;
                for (int col = 0; col < GlyphWidth; col++)
                    if (rows[row][col] == '#') v |= (byte)(1 << (GlyphWidth - 1 - col));
                b[row] = v;
            }
            return b;
        }

        private static readonly byte[] Box = R(
            "#####",
            "#...#",
            "#...#",
            "#...#",
            "#...#",
            "#...#",
            "#####");

        private static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
        {
            [' '] = R(".....", ".....", ".....", ".....", ".....", ".....", "....."),
            ['A'] = R(".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"),
            ['B'] = R("####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."),
            ['C'] = R(".####", "#....", "#....", "#....", "#....", "#....", ".####"),
            ['D'] = R("####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."),
            ['E'] = R("#####", "#....", "#....", "####.", "#....", "#....", "#####"),
            ['F'] = R("#####", "#....", "#....", "####.", "#....", "#....", "#...."),
            ['G'] = R(".####", "#....", "#....", "#.###", "#...#", "#...#", ".####"),
            ['H'] = R("#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"),
            ['I'] = R("#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####"),
            ['J'] = R("..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.."),
            ['K'] = R("#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"),
            ['L'] = R("#....", "#....", "#....", "#....", "#....", "#....", "#####"),
            ['M'] = R("#...#", "##.##", "#.#.#", "#...#", "#...#", "#...#", "#...#"),
            ['N'] = R("#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"),
            ['O'] = R(".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."),
            ['P'] = R("####.", "#...#", "#...#", "####.", "#....", "#....", "#...."),
            ['Q'] = R(".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"),
            ['R'] = R("####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"),
            ['S'] = R(".####", "#....", "#....", ".###.", "....#", "....#", "####."),
            ['T'] = R("#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."),
            ['U'] = R("#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."),
            ['V'] = R("#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."),
            ['W'] = R("#...#", "#...#", "#...#", "#...#", "#.#.#", "##.##", "#...#"),
            ['X'] = R("#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"),
            ['Y'] = R("#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."),
            ['Z'] = R("#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"),
            ['0'] = R(".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."),
            ['1'] = R("..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."),
            ['2'] = R(".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"),
            ['3'] = R("#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###."),
            ['4'] = R("...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."),
            ['5'] = R("#####", "#....", "####.", "....#", "....#", "#...#", ".###."),
            ['6'] = R(".###.", "#....", "#....", "####.", "#...#", "#...#", ".###."),
            ['7'] = R("#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."),
            ['8'] = R(".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."),
            ['9'] = R(".###.", "#...#", "#...#", ".####", "....#", "....#", ".###."),
            ['.'] = R(".....", ".....", ".....", ".....", ".....", ".##..", ".##.."),
            [','] = R(".....", ".....", ".....", ".....", ".##..", ".##..", ".#..."),
            [':'] = R(".....", ".##..", ".##..", ".....", ".##..", ".##..", "....."),
            ['/'] = R("....#", "....#", "...#.", "..#..", ".#...", "#....", "#...."),
            ['-'] = R(".....", ".....", ".....", "#####", ".....", ".....", "....."),
            ['_'] = R(".....", ".....", ".....", ".....", ".....", ".....", "#####"),
            ['!'] = R("..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.."),
            ['?'] = R(".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.."),
            ['('] = R("..#..", ".#...", "#....", "#....", "#....", ".#...", "..#.."),
            [')'] = R("..#..", "...#.", "....#", "....#", "....#", "...#.", "..#.."),
            ['+'] = R(".....", "..#..", "..#..", "#####", "..#..", "..#..", "....."),
            ['='] = R(".....", ".....", "#####", ".....", "#####", ".....", "....."),
            ['<'] = R("...#.", "..#..", ".#...", "#....", ".#...", "..#..", "...#."),
            ['>'] = R(".#...", "..#..", "...#.", "....#", "...#.", "..#..", ".#..."),
            ['%'] = R("##..#", "##.#.", "...#.", "..#..", ".#...", ".#.##", "#..##"),
            ['*'] = R(".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", "....."),
        };
    }
}
