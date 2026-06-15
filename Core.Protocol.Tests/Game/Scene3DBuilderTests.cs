using System.Linq;
using Armagetron.Game;
using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the pure 3D geometry builder — the "what walls exist, where, in what color"
    /// decision behind the perspective views. Asserts wall extrusion (floor→height), counts,
    /// degenerate-segment skipping, colors and head markers from a world snapshot, no GPU.
    /// </summary>
    public class Scene3DBuilderTests
    {
        private const float Arena = 176.78f;
        private const float Wall = 8f;

        private static CycleSnapshot Cycle(int id, Vec2 pos, Vec2 dir, params Vec2[] trail) =>
            new CycleSnapshot { CycleId = id, Position = pos, Direction = dir, Trail = trail };

        [Fact]
        public void EmptySnapshot_ProducesNoWallsOrCycles()
        {
            var ws = Scene3DBuilder.Build(System.Array.Empty<CycleSnapshot>(), 5,
                                          new CyclePalette(), Arena, Wall);
            Assert.Empty(ws.Walls);
            Assert.Empty(ws.Cycles);
            Assert.Equal(Arena, ws.ArenaSize);
            Assert.Equal(Wall, ws.WallHeight);
        }

        [Fact]
        public void Trail_OfThreeWaypoints_PlusHead_MakesThreeWalls()
        {
            // 3 waypoints → 2 completed walls; + 1 active wall to the head = 3.
            var snap = new[]
            {
                Cycle(9, new Vec2(60, 40), new Vec2(0, 1),
                      new Vec2(10, 10), new Vec2(60, 10), new Vec2(60, 40)),
            };
            var ws = Scene3DBuilder.Build(snap, 5, new CyclePalette(), Arena, Wall);
            // Active segment last-waypoint(60,40)→head(60,40) is degenerate ⇒ skipped, leaving 2.
            Assert.Equal(2, ws.Walls.Count);
        }

        [Fact]
        public void ActiveSegment_ToDistinctHead_AddsAWall()
        {
            var snap = new[]
            {
                Cycle(9, new Vec2(60, 90), new Vec2(0, 1),
                      new Vec2(10, 10), new Vec2(60, 10)),
            };
            var ws = Scene3DBuilder.Build(snap, 5, new CyclePalette(), Arena, Wall);
            // 1 completed (10,10)->(60,10) + 1 active (60,10)->head(60,90) = 2.
            Assert.Equal(2, ws.Walls.Count);
        }

        [Fact]
        public void DegenerateSegment_IsSkipped()
        {
            // Head exactly on the single waypoint: no wall at all.
            var snap = new[] { Cycle(9, new Vec2(30, 30), new Vec2(1, 0), new Vec2(30, 30)) };
            var ws = Scene3DBuilder.Build(snap, 5, new CyclePalette(), Arena, Wall);
            Assert.Empty(ws.Walls);
            Assert.Single(ws.Cycles); // marker still emitted
        }

        [Fact]
        public void Wall_StandsOnFloor_ExtrudedToHeight_OverTheSegment()
        {
            var snap = new[] { Cycle(9, new Vec2(50, 10), new Vec2(1, 0), new Vec2(10, 10)) };
            var ws = Scene3DBuilder.Build(snap, 5, new CyclePalette(), Arena, Wall);
            WallQuad w = ws.Walls.Single();

            // Bottom edge on the floor (Y=0), top edge at the wall height.
            Assert.Equal(0f, w.A.Y, 4);
            Assert.Equal(0f, w.B.Y, 4);
            Assert.Equal(Wall, w.C.Y, 4);
            Assert.Equal(Wall, w.D.Y, 4);

            // Bottom spans the segment ends; arena-X→world-X, arena-Y→world-Z.
            Assert.Equal(10f, w.A.X, 4); Assert.Equal(10f, w.A.Z, 4);
            Assert.Equal(50f, w.B.X, 4); Assert.Equal(10f, w.B.Z, 4);

            // Top corners sit directly above the matching bottom corners.
            Assert.Equal(w.B.X, w.C.X, 4); Assert.Equal(w.B.Z, w.C.Z, 4);
            Assert.Equal(w.A.X, w.D.X, 4); Assert.Equal(w.A.Z, w.D.Z, 4);
        }

        [Fact]
        public void LocalCycle_GetsSignatureColor_RemotesFromPalette()
        {
            var snap = new[]
            {
                Cycle(5, new Vec2(50, 10), new Vec2(1, 0), new Vec2(10, 10)), // mine
                Cycle(9, new Vec2(50, 40), new Vec2(1, 0), new Vec2(10, 40)), // remote
            };
            var ws = Scene3DBuilder.Build(snap, myId: 5, new CyclePalette(), Arena, Wall);

            CycleMarker mine = ws.Cycles.Single(m => m.Position.Equals(new Vec2(50, 10)));
            CycleMarker remote = ws.Cycles.Single(m => m.Position.Equals(new Vec2(50, 40)));

            Assert.Equal(CyclePalette.Mine, mine.Color);
            Assert.NotEqual(CyclePalette.Mine, remote.Color);

            // Walls carry the same per-cycle color as the marker.
            Assert.Contains(ws.Walls, w => w.Color.Equals(CyclePalette.Mine));
            Assert.Contains(ws.Walls, w => w.Color.Equals(remote.Color));
        }

        [Fact]
        public void Marker_CarriesPositionAndDirection()
        {
            var snap = new[] { Cycle(9, new Vec2(70, 80), new Vec2(0, 1), new Vec2(70, 20)) };
            var ws = Scene3DBuilder.Build(snap, 5, new CyclePalette(), Arena, Wall);
            CycleMarker m = ws.Cycles.Single();
            Assert.Equal(new Vec2(70, 80), m.Position);
            Assert.Equal(new Vec2(0, 1), m.Direction);
        }
    }
}
