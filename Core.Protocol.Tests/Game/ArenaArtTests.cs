using System.Linq;
using Armagetron.Game;
using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the designer's in-game art layered in by <see cref="SceneBuilder.BuildWithArt"/>:
    /// the tiled <c>arena_tile</c> floor under everything, the preserved procedural border/trail
    /// segments, and a tinted, heading-rotated <c>cycle</c> sprite per head. The core
    /// <see cref="SceneBuilder.Build"/> geometry is covered by SceneBuilderTests and stays
    /// unchanged. (<see cref="ArenaView.Grid"/> remains a tested geometry helper in its own right.)
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
        public void BuildWithArt_TilesFloorUnderneath_AndPreservesTrailSegments()
        {
            var cycles = new[]
            {
                new CycleSnapshot { CycleId = 5, Position = new Vec2(40, 40),
                                    Direction = new Vec2(1, 0), Trail = new[] { new Vec2(10, 40) } },
            };
            var v = View();
            Scene plain = SceneBuilder.Build(cycles, myId: 5, v, new CyclePalette());
            Scene art   = SceneBuilder.BuildWithArt(cycles, myId: 5, v, new CyclePalette(), 8);

            // 8×8 arena_tile floor sprites + one cycle sprite.
            Assert.Equal(64 + 1, art.Sprites.Count);
            Assert.Equal(64, art.Sprites.Count(s => s.Key == "ingame/arena"));
            Assert.Single(art.Sprites, s => s.Key == "ingame/cycle");
            // The border + trail line segments are preserved unchanged.
            Assert.Equal(plain.Segments.Count, art.Segments.Count);
            // First command is a floor tile (drawn underneath everything).
            Assert.IsType<RenderSprite>(art.Commands[0]);
            Assert.Equal("ingame/arena", ((RenderSprite)art.Commands[0]).Key);
        }

        [Fact]
        public void BuildWithArt_CycleSprite_TintedAndHeadingRotated()
        {
            var cycles = new[]
            {
                new CycleSnapshot { CycleId = 5, Position = new Vec2(40, 40),
                                    Direction = new Vec2(1, 0), Trail = System.Array.Empty<Vec2>() },
            };
            var v = View();
            Scene art = SceneBuilder.BuildWithArt(cycles, myId: 5, v, new CyclePalette(), 8);

            RenderSprite cycle = art.Sprites.Single(s => s.Key == "ingame/cycle");
            Assert.Equal(CyclePalette.Mine, cycle.Tint);          // local cycle takes the signature color
            // Heading +X (nose-up master) → +90° rotation.
            Assert.Equal((float)(System.Math.PI / 2.0), cycle.Rotation, 3);
            // Centered on the head's screen position.
            Vec2 head = v.ToScreen(new Vec2(40, 40));
            Assert.Equal((int)head.X, cycle.X + cycle.W / 2);
            Assert.Equal((int)head.Y, cycle.Y + cycle.H / 2);
        }

        [Fact]
        public void BuildWithArt_ZeroDirection_CycleNotRotated()
        {
            var cycles = new[]
            {
                new CycleSnapshot { CycleId = 5, Position = new Vec2(40, 40),
                                    Direction = new Vec2(0, 0), Trail = System.Array.Empty<Vec2>() },
            };
            var v = View();
            Scene art = SceneBuilder.BuildWithArt(cycles, myId: 5, v, new CyclePalette(), 8);
            RenderSprite cycle = art.Sprites.Single(s => s.Key == "ingame/cycle");
            Assert.Equal(0f, cycle.Rotation);
        }
    }
}
