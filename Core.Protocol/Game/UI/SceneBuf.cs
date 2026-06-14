using System.Collections.Generic;
using Armagetron.Protocol;

namespace Armagetron.Game.UI
{
    /// <summary>
    /// A mutable accumulator that turns high-level UI primitives (filled rects, borders,
    /// aligned text, buttons, fields) into the flat <see cref="Scene"/> draw lists the
    /// front-end renders. This is the one place that knows how a placeholder widget LOOKS, so
    /// the per-screen view builders stay declarative ("draw this button here") and fully
    /// unit-testable — they assert on the emitted commands, no GPU required.
    /// </summary>
    public sealed class SceneBuf
    {
        public const int Pad = 8;            // inner padding for fields/buttons
        public const int CaretWidth = 2;     // placeholder text caret thickness (pre-scale)

        private readonly List<RenderSegment> _segments = new List<RenderSegment>();
        private readonly List<RenderRect> _rects = new List<RenderRect>();
        private readonly List<RenderText> _texts = new List<RenderText>();

        public IReadOnlyList<RenderRect> Rects => _rects;
        public IReadOnlyList<RenderText> Texts => _texts;
        public IReadOnlyList<RenderSegment> Segments => _segments;

        // ── Primitives ──────────────────────────────────────────────────────────

        /// <summary>Filled rectangle.</summary>
        public SceneBuf Fill(UiRect r, RenderColor color)
        {
            _rects.Add(new RenderRect(r.X, r.Y, r.W, r.H, color));
            return this;
        }

        /// <summary>A rectangular outline of the given pixel thickness (four filled edges).</summary>
        public SceneBuf Border(UiRect r, RenderColor color, int thickness = 2)
        {
            _rects.Add(new RenderRect(r.X, r.Y, r.W, thickness, color));                 // top
            _rects.Add(new RenderRect(r.X, r.Bottom - thickness, r.W, thickness, color)); // bottom
            _rects.Add(new RenderRect(r.X, r.Y, thickness, r.H, color));                 // left
            _rects.Add(new RenderRect(r.Right - thickness, r.Y, thickness, r.H, color)); // right
            return this;
        }

        /// <summary>A line segment (used by the touch overlay / dividers).</summary>
        public SceneBuf Line(Vec2 from, Vec2 to, RenderColor color, float thickness = 2f)
        {
            _segments.Add(new RenderSegment(from, to, color, thickness));
            return this;
        }

        /// <summary>Text anchored at its top-left.</summary>
        public SceneBuf TextLeft(string s, int x, int y, RenderColor color, int scale)
        {
            _texts.Add(new RenderText(s, x, y, color, scale));
            return this;
        }

        /// <summary>Text horizontally centered on <paramref name="centerX"/>.</summary>
        public SceneBuf TextCenter(string s, int centerX, int y, RenderColor color, int scale)
        {
            int x = centerX - PixelFont.MeasureWidth(s, scale) / 2;
            _texts.Add(new RenderText(s, x, y, color, scale));
            return this;
        }

        /// <summary>Text centered both axes within <paramref name="r"/>.</summary>
        public SceneBuf TextCentered(string s, UiRect r, RenderColor color, int scale)
        {
            int y = r.CenterY - PixelFont.Height(scale) / 2;
            return TextCenter(s, r.CenterX, y, color, scale);
        }

        // ── Composite widgets (placeholder look) ─────────────────────────────────

        /// <summary>Draw a button: fill (state-dependent), border, centered label.</summary>
        public SceneBuf DrawButton(UiButton b, UiTheme theme, int textScale)
        {
            RenderColor fill = !b.Enabled ? theme.ButtonDisabled
                              : b.Pressed ? theme.ButtonPressed
                                          : theme.Button;
            Fill(b.Bounds, fill);
            Border(b.Bounds, theme.PanelBorder);
            TextCentered(b.Label, b.Bounds, b.Enabled ? theme.Text : theme.TextMuted, textScale);
            return this;
        }

        /// <summary>
        /// Draw a labeled text field: a small label above the box, then the box (focus-tinted),
        /// border, the current value left-aligned, and a caret when focused.
        /// </summary>
        public SceneBuf DrawField(UiTextField f, UiTheme theme, int textScale)
        {
            if (!string.IsNullOrEmpty(f.Label))
                TextLeft(f.Label, f.Bounds.X, f.Bounds.Y - PixelFont.Height(textScale) - 4,
                         theme.TextMuted, textScale);

            Fill(f.Bounds, f.Focused ? theme.FieldFocused : theme.Field);
            Border(f.Bounds, f.Focused ? theme.Accent : theme.FieldBorder);

            int textX = f.Bounds.X + Pad;
            int textY = f.Bounds.CenterY - PixelFont.Height(textScale) / 2;
            if (f.Value.Length > 0)
                TextLeft(f.Value, textX, textY, theme.Text, textScale);

            if (f.Focused)
            {
                int caretX = textX + PixelFont.MeasureWidth(f.Value, textScale)
                             + (f.Value.Length > 0 ? PixelFont.Spacing * textScale : 0);
                Fill(new UiRect(caretX, textY, CaretWidth * textScale, PixelFont.Height(textScale)),
                     theme.Accent);
            }
            return this;
        }

        /// <summary>Append an existing scene's draw lists (e.g. gameplay under a HUD overlay).</summary>
        public SceneBuf Append(Scene scene)
        {
            _segments.AddRange(scene.Segments);
            _rects.AddRange(scene.Heads);
            _texts.AddRange(scene.Texts);
            return this;
        }

        /// <summary>Finalize into an immutable <see cref="Scene"/>.</summary>
        public Scene ToScene() => new Scene(_segments, _rects, _texts);
    }
}
