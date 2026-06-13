using System.Linq;
using Armagetron.Game;
using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the pure render model — the "what to draw" decisions (projection,
    /// segment construction, draw order, colors) extracted out of the MonoGame
    /// <c>Draw()</c> GPU calls. This is the layer that lets rendering correctness be
    /// asserted without a window or a GPU; the through-wall-ghost class of bug shows
    /// up here as "the active segment does not end at the cycle's position".
    /// </summary>
    public class SceneBuilderTests
    {
        // 800px view, 176.78 arena, 10px margin — same constants as the desktop client.
        private static ArenaView View() => new ArenaView(arenaSize: 176.78f, margin: 10f, viewSize: 800);

        // ── Projection ────────────────────────────────────────────────────────

        [Fact]
        public void ToScreen_MapsArenaOriginToBottomLeft_WithYFlip()
        {
            var v = View();
            Vec2 s = v.ToScreen(new Vec2(0, 0));
            Assert.Equal(10f, s.X, 3);          // left margin
            Assert.Equal(800f - 10f, s.Y, 3);   // bottom (Y flipped)
        }

        [Fact]
        public void ToScreen_MapsArenaFarCornerToTopRight()
        {
            var v = View();
            Vec2 s = v.ToScreen(new Vec2(176.78f, 176.78f));
            Assert.Equal(800f - 10f, s.X, 3);   // right edge
            Assert.Equal(10f, s.Y, 3);          // top (Y flipped)
        }

        [Fact]
        public void ArenaBorder_IsFourAxisAlignedSegments()
        {
            var v = View();
            var border = v.ArenaBorder(RenderColor.White);
            Assert.Equal(4, border.Length);
            foreach (var seg in border)
                Assert.True(seg.From.X == seg.To.X || seg.From.Y == seg.To.Y,
                    $"border segment {seg.From}->{seg.To} is diagonal");
        }

        // ── Segment construction ──────────────────────────────────────────────

        [Fact]
        public void Build_RemoteCycle_DrawsTrailSegmentsThenActiveSegmentToHead()
        {
            var v = View();
            var snap = new CycleSnapshot
            {
                CycleId = 9,
                Position = new Vec2(50, 0),
                Direction = new Vec2(1, 0),
                Trail = new[] { new Vec2(0, 0), new Vec2(30, 0) },
            };

            var scene = SceneBuilder.Build(new[] { snap }, myId: 5, v, new CyclePalette());

            // 1 trail segment (0,0)->(30,0) plus the active segment (30,0)->position.
            var cycleSegs = scene.Segments.Where(s => s.Color.Equals(new CyclePalette().ColorFor(9, 5))).ToArray();
            Assert.Equal(2, cycleSegs.Length);
            // The active (last) segment must END exactly at the cycle's head position.
            Assert.Equal(v.ToScreen(new Vec2(50, 0)), cycleSegs[^1].To);
        }

        [Fact]
        public void Build_ActiveSegmentEndsAtFrozenDeathPosition()
        {
            // The death-freeze, verified at the geometry layer: a dead cycle's Position is
            // pinned at the wall, so the active segment must terminate there — not extend past.
            var v = View();
            var dead = new CycleSnapshot
            {
                CycleId = 9,
                Position = new Vec2(50, 0),   // frozen death position
                Direction = new Vec2(1, 0),
                Trail = new[] { new Vec2(0, 0) },
            };

            var scene = SceneBuilder.Build(new[] { dead }, myId: 5, v, new CyclePalette());

            Assert.Equal(v.ToScreen(new Vec2(50, 0)), scene.Segments.Last().To);
        }

        [Fact]
        public void Build_EmptyTrail_DrawsNoCycleSegments()
        {
            var v = View();
            var snap = new CycleSnapshot { CycleId = 9, Position = new Vec2(5, 5), Trail = new Vec2[0] };

            var scene = SceneBuilder.Build(new[] { snap }, myId: 5, v, new CyclePalette());

            // Only the arena border (4 segments); no segment for a cycle with no waypoints.
            Assert.Equal(4, scene.Segments.Count);
        }

        // ── Head ──────────────────────────────────────────────────────────────

        [Fact]
        public void Build_HeadRect_IsCenteredOnCyclePosition()
        {
            var v = View();
            var snap = new CycleSnapshot { CycleId = 9, Position = new Vec2(88.39f, 88.39f), Trail = new[] { new Vec2(0, 0) } };

            var scene = SceneBuilder.Build(new[] { snap }, myId: 5, v, new CyclePalette());

            Vec2 head = v.ToScreen(new Vec2(88.39f, 88.39f));
            var rect = Assert.Single(scene.Heads);
            Assert.Equal((int)head.X - 3, rect.X);
            Assert.Equal((int)head.Y - 3, rect.Y);
            Assert.Equal(7, rect.W);
            Assert.Equal(7, rect.H);
        }

        // ── Draw order & color ────────────────────────────────────────────────

        [Fact]
        public void Build_MyCycle_IsDrawnLast_OnTop()
        {
            var v = View();
            var mine = new CycleSnapshot { CycleId = 5, Position = new Vec2(10, 0), Trail = new[] { new Vec2(0, 0) } };
            var other = new CycleSnapshot { CycleId = 9, Position = new Vec2(10, 20), Trail = new[] { new Vec2(0, 20) } };

            var scene = SceneBuilder.Build(new[] { mine, other }, myId: 5, v, new CyclePalette());

            // My head must be the last head drawn (rendered on top of everyone else).
            Assert.Equal(CyclePalette.Mine, scene.Heads.Last().Color);
        }

        [Fact]
        public void Build_MyCycle_IsGreen_OthersFromPalette()
        {
            var v = View();
            var mine = new CycleSnapshot { CycleId = 5, Position = new Vec2(10, 0), Trail = new[] { new Vec2(0, 0) } };

            var scene = SceneBuilder.Build(new[] { mine }, myId: 5, v, new CyclePalette());

            Assert.Equal(CyclePalette.Mine, scene.Heads.Single().Color);
        }

        [Fact]
        public void CyclePalette_AssignsStableColorPerCycle()
        {
            var p = new CyclePalette();
            RenderColor first = p.ColorFor(9, myId: 5);
            RenderColor again = p.ColorFor(9, myId: 5);
            Assert.Equal(first, again); // stable within a session
        }
    }
}
