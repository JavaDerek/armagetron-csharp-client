using System;
using System.Collections.Generic;
using Armagetron.Protocol;

namespace Armagetron.Game
{
    /// <summary>
    /// Engine-neutral RGBA color. Core.Protocol must not reference MonoGame (rule 4),
    /// so the render model speaks in this type; the desktop/Android front-ends map it
    /// to their own color type (e.g. <c>new Color(R, G, B, A)</c>) at draw time.
    /// </summary>
    public readonly struct RenderColor : IEquatable<RenderColor>
    {
        public readonly byte R, G, B, A;
        public RenderColor(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; }

        public static readonly RenderColor White = new RenderColor(255, 255, 255);

        public bool Equals(RenderColor o) => R == o.R && G == o.G && B == o.B && A == o.A;
        public override bool Equals(object? o) => o is RenderColor c && Equals(c);
        public override int GetHashCode() => (R << 24) | (G << 16) | (B << 8) | A;
        public override string ToString() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";
    }

    /// <summary>A line segment in SCREEN-pixel coordinates (already projected).</summary>
    public readonly struct RenderSegment
    {
        public readonly Vec2 From, To;
        public readonly RenderColor Color;
        public readonly float Thickness;
        public RenderSegment(Vec2 from, Vec2 to, RenderColor color, float thickness = 2f)
        {
            From = from; To = to; Color = color; Thickness = thickness;
        }
    }

    /// <summary>A filled rectangle in SCREEN-pixel coordinates (the cycle head marker).</summary>
    public readonly struct RenderRect
    {
        public readonly int X, Y, W, H;
        public readonly RenderColor Color;
        public RenderRect(int x, int y, int w, int h, RenderColor color)
        {
            X = x; Y = y; W = w; H = h; Color = color;
        }
    }

    /// <summary>How a sprite is composited over what's already drawn.</summary>
    public enum BlendKind
    {
        /// <summary>Standard source-over alpha blend (UI, icons, the cycle sprite).</summary>
        Alpha,
        /// <summary>Additive blend for glowing FX (explosion, trail bloom) per the tint recipe.</summary>
        Additive,
    }

    /// <summary>
    /// A textured-sprite draw command in SCREEN-pixel coordinates. The neutral layer references
    /// the bitmap by a stable <see cref="Key"/> (e.g. <c>"icon/gear"</c>, <c>"nine/panel"</c>,
    /// <c>"ingame/cycle"</c>); the front-end maps the key to a loaded GPU texture. White master
    /// sprites are <see cref="Tint"/>ed to a player/UI color (the design's one-white-sprite → 8
    /// colors recipe). When <see cref="NineSlice"/> is set the front-end stretches the texture as
    /// a 9-patch into the dest rect (corners fixed); a non-empty source sub-rect selects one cell
    /// of a sprite sheet (the explosion frames).
    /// </summary>
    public readonly struct RenderSprite
    {
        public readonly string Key;
        public readonly int X, Y, W, H;          // destination rect
        public readonly RenderColor Tint;
        public readonly BlendKind Blend;
        public readonly bool NineSlice;
        public readonly int SrcX, SrcY, SrcW, SrcH; // source sub-rect; W/H == 0 ⇒ whole texture
        public readonly float Rotation;          // radians, about the dest-rect center

        public RenderSprite(string key, int x, int y, int w, int h, RenderColor tint,
                            BlendKind blend = BlendKind.Alpha, bool nineSlice = false,
                            int srcX = 0, int srcY = 0, int srcW = 0, int srcH = 0,
                            float rotation = 0f)
        {
            Key = key; X = x; Y = y; W = w; H = h; Tint = tint; Blend = blend;
            NineSlice = nineSlice; SrcX = srcX; SrcY = srcY; SrcW = srcW; SrcH = srcH;
            Rotation = rotation;
        }

        /// <summary>True when a source sub-rect (sprite-sheet cell) is specified.</summary>
        public bool HasSource => SrcW > 0 && SrcH > 0;
    }

    /// <summary>
    /// Pure world→screen projection plus arena geometry. Holds no GPU state; safe to
    /// unit-test. Mirrors the constants the desktop client used inline.
    /// </summary>
    public sealed class ArenaView
    {
        public float ArenaSize { get; }
        public float Margin { get; }
        public int ViewSize { get; }

        public ArenaView(float arenaSize, float margin, int viewSize)
        {
            ArenaSize = arenaSize; Margin = margin; ViewSize = viewSize;
        }

        /// <summary>Pixels per world unit (uniform on both axes).</summary>
        public float Scale => (ViewSize - 2f * Margin) / ArenaSize;

        /// <summary>Project a world point to screen pixels (game Y+ is up → screen Y+ is down).</summary>
        public Vec2 ToScreen(Vec2 world)
        {
            float scale = (ViewSize - 2f * Margin) / ArenaSize;
            return new Vec2(
                Margin + world.X * scale,
                ViewSize - Margin - world.Y * scale);
        }

        /// <summary>The four arena-boundary segments, in screen coordinates.</summary>
        public RenderSegment[] ArenaBorder(RenderColor color)
        {
            float w = ArenaSize;
            Vec2 tl = ToScreen(new Vec2(0, w));
            Vec2 tr = ToScreen(new Vec2(w, w));
            Vec2 br = ToScreen(new Vec2(w, 0));
            Vec2 bl = ToScreen(new Vec2(0, 0));
            return new[]
            {
                new RenderSegment(tl, tr, color),
                new RenderSegment(tr, br, color),
                new RenderSegment(br, bl, color),
                new RenderSegment(bl, tl, color),
            };
        }

        /// <summary>
        /// PLACEHOLDER floor grid: <paramref name="divisions"/>−1 evenly-spaced lines on each
        /// axis across the arena, drawn thin. Stands in for the designer's arena-floor texture
        /// (DESIGN_BRIEF §6) — swapped for a tiled sprite later; the projection stays the same.
        /// </summary>
        public RenderSegment[] Grid(RenderColor color, int divisions)
        {
            var list = new List<RenderSegment>();
            for (int i = 1; i < divisions; i++)
            {
                float t = ArenaSize * i / divisions;
                list.Add(new RenderSegment(ToScreen(new Vec2(t, 0)), ToScreen(new Vec2(t, ArenaSize)), color, 1f));
                list.Add(new RenderSegment(ToScreen(new Vec2(0, t)), ToScreen(new Vec2(ArenaSize, t)), color, 1f));
            }
            return list.ToArray();
        }
    }

    /// <summary>Which typeface/weight role a line of text uses (mapped to a real OFL font by
    /// the front-end: Orbitron for display/title, Rajdhani for UI/body, Share Tech Mono for
    /// numerals/fields). The neutral layer only names the role; the head owns the TTFs.</summary>
    public enum FontRole
    {
        /// <summary>Orbitron 900 — countdown numerals (huge).</summary>
        Display,
        /// <summary>Orbitron 800 — screen titles, winner banner.</summary>
        Title,
        /// <summary>Orbitron 700 — PAUSED / splash-weight headings.</summary>
        Heading,
        /// <summary>Rajdhani Medium — default UI/body text.</summary>
        Body,
        /// <summary>Rajdhani SemiBold — button labels, emphasized small caps.</summary>
        Label,
        /// <summary>Share Tech Mono — host/port fields, ping, score numerals.</summary>
        Mono,
    }

    /// <summary>Horizontal anchoring of a <see cref="RenderText"/> about its (X, Y) point. The
    /// front-end resolves the final pixel origin using the REAL font's measured width, so the
    /// neutral layer never needs proportional metrics.</summary>
    public enum TextAlign { Left, Center, Right }

    /// <summary>
    /// A line of text in SCREEN-pixel coordinates anchored at (<see cref="X"/>, <see cref="Y"/>)
    /// per <see cref="Align"/> (Left ⇒ X is the left edge, Center ⇒ X is the horizontal center,
    /// Right ⇒ X is the right edge). When <see cref="Middle"/> is set, Y is the vertical CENTER;
    /// otherwise Y is the top. The front-end measures with the real <see cref="Role"/> font to
    /// resolve the origin. <see cref="Scale"/> is the legacy integer size knob (multiplies a base
    /// px size per role); kept so existing layouts read identically while the bitmap font is
    /// retired.
    /// </summary>
    public readonly struct RenderText
    {
        public readonly string Text;
        public readonly int X, Y;
        public readonly RenderColor Color;
        public readonly int Scale;
        public readonly FontRole Role;
        public readonly TextAlign Align;
        public readonly bool Middle;

        /// <summary>When set, the front-end draws a text caret immediately after the (real-font
        /// measured) end of this line — so a focused field's caret tracks the glyphs exactly,
        /// instead of the neutral layer guessing the width.</summary>
        public readonly bool Caret;

        public RenderText(string text, int x, int y, RenderColor color, int scale = 2)
            : this(text, x, y, color, scale, FontRole.Body, TextAlign.Left, false) { }

        public RenderText(string text, int x, int y, RenderColor color, int scale,
                          FontRole role, TextAlign align, bool middle, bool caret = false)
        {
            Text = text; X = x; Y = y; Color = color; Scale = scale;
            Role = role; Align = align; Middle = middle; Caret = caret;
        }
    }

    /// <summary>
    /// A frame's worth of draw commands in screen space — no GPU types. Commands are kept in a
    /// single INSERTION-ORDERED stream (<see cref="Commands"/>) so layered UI composites
    /// correctly (a nine-slice panel under its fields under its text), with the front-end drawing
    /// the stream front-to-back. The typed <see cref="Segments"/>/<see cref="Heads"/>/
    /// <see cref="Texts"/>/<see cref="Sprites"/> views are filtered projections kept for the
    /// pure unit tests and the gameplay builder.
    /// </summary>
    public sealed class Scene
    {
        /// <summary>Every draw command in front-end render order. Each element is a
        /// <see cref="RenderSegment"/>, <see cref="RenderRect"/>, <see cref="RenderSprite"/> or
        /// <see cref="RenderText"/> (boxed); the head type-switches over them.</summary>
        public IReadOnlyList<object> Commands { get; }

        public IReadOnlyList<RenderSegment> Segments { get; }
        public IReadOnlyList<RenderRect> Heads { get; }
        public IReadOnlyList<RenderSprite> Sprites { get; }

        /// <summary>UI/HUD text overlays.</summary>
        public IReadOnlyList<RenderText> Texts { get; }

        public Scene(IReadOnlyList<RenderSegment> segments, IReadOnlyList<RenderRect> heads)
            : this(segments, heads, System.Array.Empty<RenderText>()) { }

        public Scene(IReadOnlyList<RenderSegment> segments, IReadOnlyList<RenderRect> heads,
                     IReadOnlyList<RenderText> texts)
        {
            // Preserve the historical gameplay layer order: world segments, then heads, then text.
            var cmds = new List<object>(segments.Count + heads.Count + texts.Count);
            foreach (var s in segments) cmds.Add(s);
            foreach (var r in heads) cmds.Add(r);
            foreach (var t in texts) cmds.Add(t);
            Commands = cmds;
            Segments = segments; Heads = heads; Texts = texts;
            Sprites = System.Array.Empty<RenderSprite>();
        }

        /// <summary>Build from a pre-ordered command stream (used by <see cref="SceneBuf"/>).</summary>
        public Scene(IReadOnlyList<object> commands)
        {
            Commands = commands;
            var seg = new List<RenderSegment>();
            var rect = new List<RenderRect>();
            var spr = new List<RenderSprite>();
            var txt = new List<RenderText>();
            foreach (object c in commands)
            {
                switch (c)
                {
                    case RenderSegment s: seg.Add(s); break;
                    case RenderRect r: rect.Add(r); break;
                    case RenderSprite p: spr.Add(p); break;
                    case RenderText t: txt.Add(t); break;
                }
            }
            Segments = seg; Heads = rect; Sprites = spr; Texts = txt;
        }
    }

    /// <summary>
    /// Stable per-cycle color assignment. The local cycle is always <see cref="Mine"/>;
    /// remote cycles cycle through a fixed palette in first-seen order, so a given cycle
    /// keeps its color for the whole session. The 8 colors are the CVD-checked trail palette
    /// from the approved design (armagetron-advanced-design.html, vars --p1…--p8); the local
    /// cycle takes the cyan signature (--p1) and remotes use the other seven, cyan last.
    /// </summary>
    public sealed class CyclePalette
    {
        public static readonly RenderColor Mine = new RenderColor(0x1F, 0xE3, 0xFF); // --p1 cyan (signature)

        /// <summary>The 8 selectable signature colors (--p1…--p8), in the settings picker order.</summary>
        public static readonly RenderColor[] SignatureOptions =
        {
            new RenderColor(0x1F, 0xE3, 0xFF), new RenderColor(0xFF, 0x9A, 0x2E),
            new RenderColor(0xFF, 0x2F, 0xA4), new RenderColor(0x8D, 0xFF, 0x3A),
            new RenderColor(0x3D, 0x8B, 0xFF), new RenderColor(0xFF, 0x4D, 0x4D),
            new RenderColor(0xFF, 0xE0, 0x3A), new RenderColor(0xB9, 0x68, 0xFF),
        };

        private static readonly RenderColor[] Palette =
        {
            new RenderColor(0xFF, 0x9A, 0x2E), // --p2 orange
            new RenderColor(0xFF, 0x2F, 0xA4), // --p3 pink
            new RenderColor(0x8D, 0xFF, 0x3A), // --p4 green
            new RenderColor(0x3D, 0x8B, 0xFF), // --p5 blue
            new RenderColor(0xFF, 0x4D, 0x4D), // --p6 red
            new RenderColor(0xFF, 0xE0, 0x3A), // --p7 yellow
            new RenderColor(0xB9, 0x68, 0xFF), // --p8 purple
            new RenderColor(0x1F, 0xE3, 0xFF), // --p1 cyan
        };

        private readonly Dictionary<int, RenderColor> _assigned = new Dictionary<int, RenderColor>();
        private int _next;

        public RenderColor ColorFor(int cycleId, int myId)
        {
            if (cycleId == myId) return Mine;
            if (!_assigned.TryGetValue(cycleId, out RenderColor c))
            {
                c = Palette[_next % Palette.Length];
                _next++;
                _assigned[cycleId] = c;
            }
            return c;
        }
    }

    /// <summary>
    /// Builds a <see cref="Scene"/> from a world snapshot. This is the whole of the
    /// "what to draw" decision — projection, trail/active-segment construction, head
    /// markers, draw order (others first, the local cycle last so it renders on top),
    /// and color — with zero GPU dependency, so it is fully unit-testable.
    /// </summary>
    public static class SceneBuilder
    {
        public static Scene Build(CycleSnapshot[] cycles, int myId, ArenaView view, CyclePalette palette)
        {
            var segments = new List<RenderSegment>();
            var heads = new List<RenderRect>();

            segments.AddRange(view.ArenaBorder(RenderColor.White));

            // Others first, then the local cycle, so the local cycle draws on top.
            foreach (var c in cycles)
                if (c.CycleId != myId)
                    AppendCycle(c, palette.ColorFor(c.CycleId, myId), view, segments, heads);

            foreach (var c in cycles)
                if (c.CycleId == myId)
                    AppendCycle(c, CyclePalette.Mine, view, segments, heads);

            return new Scene(segments, heads);
        }

        /// <summary>
        /// Like <see cref="Build"/> but with the designer's in-game art layered in: the tileable
        /// <c>arena_tile</c> floor UNDER everything, the procedural arena border + trails over it,
        /// then a tinted, heading-rotated <c>cycle</c> sprite at each head (replacing the plain
        /// head square). Trails stay procedural line segments per the design ("trails are
        /// procedural — no sprite"). Kept separate from <see cref="Build"/> so the core geometry
        /// contract (and its tests) is untouched.
        /// </summary>
        public static Scene BuildWithArt(CycleSnapshot[] cycles, int myId, ArenaView view,
                                         CyclePalette palette, int divisions)
        {
            Scene baseScene = Build(cycles, myId, view, palette);

            var cmds = new List<object>();
            AddArenaFloor(view, divisions, cmds);            // floor first (drawn underneath)
            foreach (var s in baseScene.Segments) cmds.Add(s); // arena border + trails over it

            // Cycle sprite: ~9 world units across, tinted to the player color, rotated so the
            // nose-up master points along the cycle's screen heading.
            int sz = (int)System.Math.Round(9f * view.Scale);
            if (sz < 6) sz = 6;
            foreach (var c in cycles)
            {
                RenderColor color = c.CycleId == myId ? CyclePalette.Mine : palette.ColorFor(c.CycleId, myId);
                Vec2 head = view.ToScreen(c.Position);
                double mag = Math.Sqrt(c.Direction.X * c.Direction.X + c.Direction.Y * c.Direction.Y);
                float rot = mag < 1e-4 ? 0f
                          : (float)(Math.Atan2(-c.Direction.Y, c.Direction.X) + Math.PI / 2.0);
                cmds.Add(new RenderSprite("ingame/cycle", (int)head.X - sz / 2, (int)head.Y - sz / 2,
                                          sz, sz, color, BlendKind.Alpha, false, 0, 0, 0, 0, rot));
            }

            return new Scene(cmds);
        }

        // Tile the tintable arena_tile sprite across the arena square, `divisions` cells per axis.
        private static void AddArenaFloor(ArenaView view, int divisions, List<object> cmds)
        {
            if (divisions < 1) divisions = 1;
            int lo = (int)view.Margin;
            int hi = view.ViewSize - lo;
            float cell = (hi - lo) / (float)divisions;
            for (int gy = 0; gy < divisions; gy++)
            for (int gx = 0; gx < divisions; gx++)
            {
                int x = lo + (int)(gx * cell);
                int y = lo + (int)(gy * cell);
                int wCell = lo + (int)((gx + 1) * cell) - x;
                int hCell = lo + (int)((gy + 1) * cell) - y;
                cmds.Add(new RenderSprite("ingame/arena", x, y, wCell, hCell, RenderColor.White));
            }
        }

        private static void AppendCycle(CycleSnapshot cycle, RenderColor color, ArenaView view,
                                        List<RenderSegment> segments, List<RenderRect> heads)
        {
            // Trail: segments between consecutive waypoints.
            for (int i = 0; i + 1 < cycle.Trail.Length; i++)
                segments.Add(new RenderSegment(view.ToScreen(cycle.Trail[i]),
                                               view.ToScreen(cycle.Trail[i + 1]), color));

            // Active segment: last waypoint → current (possibly frozen) head position.
            if (cycle.Trail.Length > 0)
                segments.Add(new RenderSegment(view.ToScreen(cycle.Trail[cycle.Trail.Length - 1]),
                                               view.ToScreen(cycle.Position), color));

            // Head: small filled square centered on the cycle position.
            Vec2 head = view.ToScreen(cycle.Position);
            heads.Add(new RenderRect((int)head.X - 3, (int)head.Y - 3, 7, 7, color));
        }
    }
}
