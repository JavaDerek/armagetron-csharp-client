using System.Linq;
using Armagetron.Game;
using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the placeholder in-game art: the arena floor grid and the directional cycle
    /// nose layered in by <see cref="SceneBuilder.BuildWithArt"/>. The core
    /// <see cref="SceneBuilder.Build"/> geometry is covered by SceneBuilderTests and must stay
    /// unchanged, so these assert only the added layer.
    /// </summary>
    public class ArenaArtTests
    {
        private static ArenaView View() => new ArenaView(arenaSize: 176.78f, margin: 10f, viewSize: 800);
        private static readonly RenderColor Grid = new RenderColor(28, 28, 44);

        [Fact]
        public void Grid_Produces_TwoLinesPerInteriorDivision()
        {
            // 8 divisions → 7 interior lines per axis → 14 segments.
            Assert.Equal(14, View().Grid(Grid, 8).Length);
            Assert.All(View().Grid(Grid, 8), s => Assert.Equal(Grid, s.Color));
        }

        [Fact]
        public void Grid_OneDivision_IsEmpty()
        {
            Assert.Empty(View().Grid(Grid, 1));
        }

        [Fact]
        public void BuildWithArt_AddsGridUnderneath_AndKeepsHeads()
        {
            var cycles = new[]
            {
                new CycleSnapshot { CycleId = 5, Position = new Vec2(40, 40),
                                    Direction = new Vec2(1, 0), Trail = new[] { new Vec2(10, 40) } },
            };
            var v = View();
            Scene plain = SceneBuilder.Build(cycles, myId: 5, v, new CyclePalette());
            Scene art   = SceneBuilder.BuildWithArt(cycles, myId: 5, v, new CyclePalette(), Grid, 8);

            // Grid (14) + a directional nose (1) more segments than the plain build.
            Assert.Equal(plain.Segments.Count + 14 + 1, art.Segments.Count);
            // First segments are the grid (drawn underneath).
            Assert.Equal(Grid, art.Segments[0].Color);
            // Heads are preserved unchanged.
            Assert.Equal(plain.Heads.Count, art.Heads.Count);
        }

        [Fact]
        public void BuildWithArt_NoseExtendsFromHead_InDirection()
        {
            var cycles = new[]
            {
                new CycleSnapshot { CycleId = 5, Position = new Vec2(40, 40),
                                    Direction = new Vec2(1, 0), Trail = System.Array.Empty<Vec2>() },
            };
            var v = View();
            Scene art = SceneBuilder.BuildWithArt(cycles, myId: 5, v, new CyclePalette(), Grid, 8);

            // The nose is the local-color thick segment starting at the head's screen position.
            RenderSegment nose = art.Segments.Last();
            Assert.Equal(CyclePalette.Mine, nose.Color);
            Assert.Equal(v.ToScreen(new Vec2(40, 40)), nose.From);
            // Heading +X → tip is to the right of the head on screen.
            Assert.True(nose.To.X > nose.From.X);
        }

        [Fact]
        public void BuildWithArt_ZeroDirection_AddsNoNose()
        {
            var cycles = new[]
            {
                new CycleSnapshot { CycleId = 5, Position = new Vec2(40, 40),
                                    Direction = new Vec2(0, 0), Trail = System.Array.Empty<Vec2>() },
            };
            var v = View();
            Scene art   = SceneBuilder.BuildWithArt(cycles, myId: 5, v, new CyclePalette(), Grid, 8);
            // Only the 14 grid lines added over the plain build (no nose for a still cycle).
            Scene plain = SceneBuilder.Build(cycles, myId: 5, v, new CyclePalette());
            Assert.Equal(plain.Segments.Count + 14, art.Segments.Count);
        }
    }
}
