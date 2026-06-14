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

    /// <summary>
    /// A line of text in SCREEN-pixel coordinates, anchored at its TOP-LEFT (alignment is
    /// resolved by the view builder using <see cref="PixelFont"/>, so the front-end only has
    /// to draw glyphs left-to-right from this point). <see cref="Scale"/> multiplies the
    /// font's native 5×7 cell, e.g. Scale=3 draws 15×21px glyphs.
    /// </summary>
    public readonly struct RenderText
    {
        public readonly string Text;
        public readonly int X, Y;
        public readonly RenderColor Color;
        public readonly int Scale;
        public RenderText(string text, int x, int y, RenderColor color, int scale = 2)
        {
            Text = text; X = x; Y = y; Color = color; Scale = scale;
        }
    }

    /// <summary>A frame's worth of draw commands in screen space — no GPU types.</summary>
    public sealed class Scene
    {
        private static readonly RenderText[] NoTexts = System.Array.Empty<RenderText>();

        public IReadOnlyList<RenderSegment> Segments { get; }
        public IReadOnlyList<RenderRect> Heads { get; }

        /// <summary>UI/HUD text overlays, drawn on top of segments and heads.</summary>
        public IReadOnlyList<RenderText> Texts { get; }

        public Scene(IReadOnlyList<RenderSegment> segments, IReadOnlyList<RenderRect> heads)
            : this(segments, heads, NoTexts) { }

        public Scene(IReadOnlyList<RenderSegment> segments, IReadOnlyList<RenderRect> heads,
                     IReadOnlyList<RenderText> texts)
        {
            Segments = segments; Heads = heads; Texts = texts;
        }
    }

    /// <summary>
    /// Stable per-cycle color assignment. The local cycle is always <see cref="Mine"/>;
    /// remote cycles cycle through a fixed palette in first-seen order, so a given cycle
    /// keeps its color for the whole session.
    /// </summary>
    public sealed class CyclePalette
    {
        public static readonly RenderColor Mine = new RenderColor(124, 252, 0); // LawnGreen

        private static readonly RenderColor[] Palette =
        {
            new RenderColor(255, 0, 0),     // Red
            new RenderColor(0, 255, 255),   // Cyan
            new RenderColor(255, 255, 0),   // Yellow
            new RenderColor(255, 0, 255),   // Magenta
            new RenderColor(255, 165, 0),   // Orange
            new RenderColor(255, 105, 180), // HotPink
            new RenderColor(30, 144, 255),  // DodgerBlue
            new RenderColor(0, 255, 0),     // Lime
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
        /// Like <see cref="Build"/> but with PLACEHOLDER in-game art layered in: a faint floor
        /// grid UNDER everything and a short directional "nose" on each cycle head so heading is
        /// visible. Stands in for the designer's arena-floor texture + cycle sprite
        /// (DESIGN_BRIEF §6); when those land this becomes a sprite draw and the geometry here
        /// is retired. Kept separate from <see cref="Build"/> so the core geometry contract (and
        /// its tests) is untouched.
        /// </summary>
        public static Scene BuildWithArt(CycleSnapshot[] cycles, int myId, ArenaView view,
                                         CyclePalette palette, RenderColor gridColor, int divisions)
        {
            Scene baseScene = Build(cycles, myId, view, palette);

            var segments = new List<RenderSegment>();
            segments.AddRange(view.Grid(gridColor, divisions));   // floor first (drawn underneath)
            segments.AddRange(baseScene.Segments);

            const float noseLen = 5f; // world units
            foreach (var c in cycles)
            {
                double mag = Math.Sqrt(c.Direction.X * c.Direction.X + c.Direction.Y * c.Direction.Y);
                if (mag < 1e-4) continue;
                RenderColor color = c.CycleId == myId ? CyclePalette.Mine : palette.ColorFor(c.CycleId, myId);
                var tip = new Vec2(
                    c.Position.X + (float)(c.Direction.X / mag) * noseLen,
                    c.Position.Y + (float)(c.Direction.Y / mag) * noseLen);
                segments.Add(new RenderSegment(view.ToScreen(c.Position), view.ToScreen(tip), color, 3f));
            }

            return new Scene(segments, baseScene.Heads, baseScene.Texts);
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
