using System.Collections.Generic;
using Armagetron.Protocol;

namespace Armagetron.Game.UI
{
    /// <summary>Which button chrome a <see cref="SceneBuf.DrawButton"/> draws (the four
    /// nine-slice states from <c>nine-slice.json</c>): a filled primary CTA or an outlined
    /// secondary/ghost button.</summary>
    public enum ButtonStyle { Primary, Secondary }

    /// <summary>
    /// A mutable accumulator that turns high-level UI primitives (filled rects, borders, sprites,
    /// nine-slice panels, aligned text, buttons, fields) into the flat ordered <see cref="Scene"/>
    /// command stream the front-end renders. Commands are kept in INSERTION ORDER so layered
    /// chrome composites correctly (panel under fields under text). This is the one place that
    /// knows how a widget LOOKS, so the per-screen view builders stay declarative ("draw this
    /// button here") and fully unit-testable — they assert on the emitted commands, no GPU.
    /// </summary>
    public sealed class SceneBuf
    {
        public const int Pad = 8;            // inner padding for fields/buttons

        private readonly List<object> _commands = new List<object>();

        // Filtered views, materialized on access — the unit tests assert against these.
        public IReadOnlyList<RenderRect> Rects => Filter<RenderRect>();
        public IReadOnlyList<RenderText> Texts => Filter<RenderText>();
        public IReadOnlyList<RenderSegment> Segments => Filter<RenderSegment>();
        public IReadOnlyList<RenderSprite> Sprites => Filter<RenderSprite>();

        private List<T> Filter<T>()
        {
            var list = new List<T>();
            foreach (object c in _commands) if (c is T t) list.Add(t);
            return list;
        }

        // ── Primitives ──────────────────────────────────────────────────────────

        /// <summary>Filled rectangle.</summary>
        public SceneBuf Fill(UiRect r, RenderColor color)
        {
            _commands.Add(new RenderRect(r.X, r.Y, r.W, r.H, color));
            return this;
        }

        /// <summary>A rectangular outline of the given pixel thickness (four filled edges).</summary>
        public SceneBuf Border(UiRect r, RenderColor color, int thickness = 2)
        {
            _commands.Add(new RenderRect(r.X, r.Y, r.W, thickness, color));                 // top
            _commands.Add(new RenderRect(r.X, r.Bottom - thickness, r.W, thickness, color)); // bottom
            _commands.Add(new RenderRect(r.X, r.Y, thickness, r.H, color));                 // left
            _commands.Add(new RenderRect(r.Right - thickness, r.Y, thickness, r.H, color)); // right
            return this;
        }

        /// <summary>A line segment (used by the touch overlay / dividers).</summary>
        public SceneBuf Line(Vec2 from, Vec2 to, RenderColor color, float thickness = 2f)
        {
            _commands.Add(new RenderSegment(from, to, color, thickness));
            return this;
        }

        // ── Sprites ──────────────────────────────────────────────────────────────

        /// <summary>Draw a (tintable) sprite stretched into <paramref name="dest"/>.</summary>
        public SceneBuf Sprite(string key, UiRect dest, RenderColor tint,
                               BlendKind blend = BlendKind.Alpha, float rotation = 0f)
        {
            _commands.Add(new RenderSprite(key, dest.X, dest.Y, dest.W, dest.H, tint,
                                           blend, false, 0, 0, 0, 0, rotation));
            return this;
        }

        /// <summary>Draw one cell of a sprite sheet (e.g. an explosion frame) into
        /// <paramref name="dest"/>, tinted and (typically) additively blended.</summary>
        public SceneBuf SpriteFrame(string key, UiRect dest, RenderColor tint,
                                    int srcX, int srcY, int srcW, int srcH,
                                    BlendKind blend = BlendKind.Alpha)
        {
            _commands.Add(new RenderSprite(key, dest.X, dest.Y, dest.W, dest.H, tint,
                                           blend, false, srcX, srcY, srcW, srcH, 0f));
            return this;
        }

        /// <summary>Draw a nine-slice (9-patch) panel/button: corners fixed, edges/center
        /// stretched into <paramref name="dest"/> by the front-end using the key's known insets.</summary>
        public SceneBuf NineSlice(string key, UiRect dest, RenderColor tint,
                                  BlendKind blend = BlendKind.Alpha)
        {
            _commands.Add(new RenderSprite(key, dest.X, dest.Y, dest.W, dest.H, tint,
                                           blend, true, 0, 0, 0, 0, 0f));
            return this;
        }

        /// <summary>The design's nine-slice surface panel (gradient fill + cyan border + glow).</summary>
        public SceneBuf Panel(UiRect dest) => NineSlice("nine/panel", dest, RenderColor.White);

        /// <summary>Center one of the design's UI icons (already drawn in its state color, so it
        /// is tinted white) within <paramref name="bounds"/>. <paramref name="state"/> is
        /// <c>default</c> / <c>pressed</c> / <c>disabled</c>.</summary>
        public SceneBuf Icon(string name, UiRect bounds, string state = "default", int pad = 0)
        {
            int size = (bounds.W < bounds.H ? bounds.W : bounds.H) - 2 * pad;
            if (size < 1) size = 1;
            var box = new UiRect(bounds.CenterX - size / 2, bounds.CenterY - size / 2, size, size);
            _commands.Add(new RenderSprite($"icon/{name}/{state}", box.X, box.Y, box.W, box.H,
                                           RenderColor.White, BlendKind.Alpha, false, 0, 0, 0, 0, 0f));
            return this;
        }

        // ── Text (alignment resolved by the head using the real font) ─────────────

        /// <summary>Text anchored at its top-left.</summary>
        public SceneBuf TextLeft(string s, int x, int y, RenderColor color, int scale,
                                 FontRole role = FontRole.Body)
        {
            _commands.Add(new RenderText(s, x, y, color, scale, role, TextAlign.Left, false));
            return this;
        }

        /// <summary>Text horizontally centered on <paramref name="centerX"/> (top at y).</summary>
        public SceneBuf TextCenter(string s, int centerX, int y, RenderColor color, int scale,
                                   FontRole role = FontRole.Body)
        {
            _commands.Add(new RenderText(s, centerX, y, color, scale, role, TextAlign.Center, false));
            return this;
        }

        /// <summary>Text anchored at its left edge, vertically centered on <paramref name="midY"/>.</summary>
        public SceneBuf TextLeftMid(string s, int x, int midY, RenderColor color, int scale,
                                    FontRole role = FontRole.Body)
        {
            _commands.Add(new RenderText(s, x, midY, color, scale, role, TextAlign.Left, true));
            return this;
        }

        /// <summary>Text right-aligned to <paramref name="rightX"/> (top at y).</summary>
        public SceneBuf TextRight(string s, int rightX, int y, RenderColor color, int scale,
                                  FontRole role = FontRole.Body)
        {
            _commands.Add(new RenderText(s, rightX, y, color, scale, role, TextAlign.Right, false));
            return this;
        }

        /// <summary>Text centered both axes within <paramref name="r"/>.</summary>
        public SceneBuf TextCentered(string s, UiRect r, RenderColor color, int scale,
                                     FontRole role = FontRole.Body)
        {
            _commands.Add(new RenderText(s, r.CenterX, r.CenterY, color, scale, role, TextAlign.Center, true));
            return this;
        }

        // ── Composite widgets ─────────────────────────────────────────────────────

        /// <summary>Draw a button as design nine-slice chrome (state/style-dependent) + a
        /// centered label in the state's text color.</summary>
        public SceneBuf DrawButton(UiButton b, UiTheme theme, int textScale,
                                   ButtonStyle style = ButtonStyle.Primary)
        {
            string key;
            RenderColor labelColor;
            if (!b.Enabled) { key = "btn/disabled"; labelColor = theme.TextMuted; }
            else if (style == ButtonStyle.Secondary) { key = "btn/secondary"; labelColor = theme.Accent; }
            else if (b.Pressed) { key = "btn/pressed"; labelColor = theme.ButtonText; }
            else { key = "btn/default"; labelColor = theme.ButtonText; }

            NineSlice(key, b.Bounds, RenderColor.White);
            TextCentered(b.Label, b.Bounds, labelColor, textScale, FontRole.Label);
            return this;
        }

        /// <summary>
        /// Draw a labeled text field: a small label above the box, then the box (focus-tinted),
        /// border, the current value left-aligned (mono), and a caret when focused.
        /// </summary>
        public SceneBuf DrawField(UiTextField f, UiTheme theme, int textScale)
        {
            if (!string.IsNullOrEmpty(f.Label))
                TextLeft(f.Label, f.Bounds.X, f.Bounds.Y - PixelFont.Height(textScale) - 4,
                         theme.TextMuted, textScale, FontRole.Label);

            Fill(f.Bounds, f.Focused ? theme.FieldFocused : theme.Field);
            Border(f.Bounds, f.Focused ? theme.Accent : theme.FieldBorder);

            int textX = f.Bounds.X + Pad;
            int textY = f.Bounds.CenterY;
            // Emit the value (mono, vertically centered) with a trailing caret when focused; the
            // head positions the caret from the REAL measured glyph width so it tracks the text.
            if (f.Value.Length > 0 || f.Focused)
                _commands.Add(new RenderText(f.Value, textX, textY,
                                             f.Focused ? theme.Accent : theme.Text, textScale,
                                             FontRole.Mono, TextAlign.Left, true, f.Focused));
            return this;
        }

        /// <summary>A horizontal slider: track, filled portion, and a square knob at value (0–1).</summary>
        public SceneBuf DrawSlider(UiRect track, float value01, UiTheme theme)
        {
            float v = value01 < 0f ? 0f : value01 > 1f ? 1f : value01;
            int th = 6, mid = track.CenterY;
            Fill(new UiRect(track.X, mid - th / 2, track.W, th), theme.FieldBorder);
            int fillW = (int)(track.W * v);
            Fill(new UiRect(track.X, mid - th / 2, fillW, th), theme.Accent);
            int k = 16;
            Fill(new UiRect(track.X + fillW - k / 2, mid - k / 2, k, k), theme.Accent);
            return this;
        }

        /// <summary>An on/off toggle pill with a sliding knob (accent when on).</summary>
        public SceneBuf DrawToggle(UiRect pill, bool on, UiTheme theme)
        {
            Fill(pill, on ? theme.Accent : theme.Field);
            Border(pill, on ? theme.Accent : theme.FieldBorder);
            int k = pill.H - 6;
            int kx = on ? pill.Right - k - 3 : pill.X + 3;
            Fill(new UiRect(kx, pill.Y + 3, k, k), on ? theme.Background : theme.TextMuted);
            return this;
        }

        /// <summary>Append an existing scene's command stream (e.g. gameplay under a HUD overlay).</summary>
        public SceneBuf Append(Scene scene)
        {
            _commands.AddRange(scene.Commands);
            return this;
        }

        /// <summary>Finalize into an immutable <see cref="Scene"/>.</summary>
        public Scene ToScene() => new Scene(_commands);
    }
}
