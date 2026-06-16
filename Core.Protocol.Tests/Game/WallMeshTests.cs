using System.Collections.Generic;
using System.Linq;
using Armagetron.Game;
using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the pure, engine-neutral wall-mesh tessellation that the VR/3D heads upload to the
    /// GPU. The triangulation — 4 vertices + 6 indices per <see cref="WallQuad"/>, two triangles
    /// wound (A,B,C) and (A,C,D), flat per-vertex color — used to live untested inside the Unity
    /// <c>WallMeshBuilder</c>; winding/index bugs there are invisible until you are in a headset.
    /// Pinning it here (no UnityEngine, no GPU) makes those failure modes catchable in CI.
    /// </summary>
    public class WallMeshTests
    {
        private static readonly RenderColor Red = new RenderColor(255, 0, 0, 255);
        private static readonly RenderColor Blue = new RenderColor(0, 0, 255, 255);

        // A wall whose four corners are all distinct, so vertex order is unambiguous.
        private static WallQuad Quad(RenderColor color) => new WallQuad(
            new Vec3(1, 0, 2),   // A bottom-from
            new Vec3(3, 0, 4),   // B bottom-to
            new Vec3(3, 8, 4),   // C top-to
            new Vec3(1, 8, 2),   // D top-from
            color);

        [Fact]
        public void Empty_ProducesEmptyArrays_AndNarrowIndices()
        {
            WallMesh m = WallMesh.From(new List<WallQuad>());
            Assert.Empty(m.Vertices);
            Assert.Empty(m.Indices);
            Assert.Empty(m.Colors);
            Assert.False(m.NeedsWideIndices);
        }

        [Fact]
        public void OneWall_HasFourVertsSixIndices_InCornerOrder()
        {
            WallQuad q = Quad(Red);
            WallMesh m = WallMesh.From(new[] { q });

            Assert.Equal(4, m.Vertices.Length);
            Assert.Equal(6, m.Indices.Length);
            // Vertices are emitted in A,B,C,D order so the front-end's winding matches.
            Assert.Equal(q.A, m.Vertices[0]);
            Assert.Equal(q.B, m.Vertices[1]);
            Assert.Equal(q.C, m.Vertices[2]);
            Assert.Equal(q.D, m.Vertices[3]);
        }

        [Fact]
        public void OneWall_WindsTwoTriangles_ABC_and_ACD()
        {
            WallMesh m = WallMesh.From(new[] { Quad(Red) });
            // (A,B,C) then (A,C,D): a consistently wound quad. A flipped index here is exactly the
            // bug that makes a wall vanish under back-face culling.
            Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, m.Indices);
        }

        [Fact]
        public void EveryVertex_CarriesItsWallColor()
        {
            WallMesh m = WallMesh.From(new[] { Quad(Red) });
            Assert.Equal(4, m.Colors.Length);
            Assert.All(m.Colors, c => Assert.Equal(Red, c));
        }

        [Fact]
        public void TwoWalls_SecondTriangleSetIsOffsetByFour_AndColorsAreGrouped()
        {
            WallMesh m = WallMesh.From(new[] { Quad(Red), Quad(Blue) });

            Assert.Equal(8, m.Vertices.Length);
            Assert.Equal(12, m.Indices.Length);
            // Second quad's indices reference its own 4 verts (base 4), same winding.
            Assert.Equal(new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 }, m.Indices);
            // Colors stay grouped per wall (first four Red, next four Blue).
            Assert.All(m.Colors.Take(4), c => Assert.Equal(Red, c));
            Assert.All(m.Colors.Skip(4), c => Assert.Equal(Blue, c));
        }

        [Fact]
        public void From_WorldScene_TessellatesItsWalls()
        {
            // End-to-end with the real geometry builder: a 2-waypoint trail + head → 2 walls → 8 verts.
            var snap = new[]
            {
                new CycleSnapshot
                {
                    CycleId = 9, Position = new Vec2(60, 90), Direction = new Vec2(0, 1),
                    Trail = new[] { new Vec2(10, 10), new Vec2(60, 10) },
                },
            };
            WorldScene world = Scene3DBuilder.Build(snap, myId: 5, new CyclePalette(), 176.78f, 8f);
            WallMesh m = WallMesh.From(world);

            Assert.Equal(world.Walls.Count * 4, m.Vertices.Length);
            Assert.Equal(world.Walls.Count * 6, m.Indices.Length);
        }

        [Fact]
        public void NeedsWideIndices_FlipsTrue_PastTheSixteenBitVertexBudget()
        {
            // Unity meshes default to 16-bit indices; the front-end must switch to 32-bit once the
            // vertex total exceeds the budget. 16250 walls = 65000 verts (at the cushion, still
            // narrow); one more wall crosses it.
            var atBudget = Enumerable.Range(0, 16250).Select(_ => Quad(Red)).ToList();
            var overBudget = Enumerable.Range(0, 16251).Select(_ => Quad(Red)).ToList();

            Assert.False(WallMesh.From(atBudget).NeedsWideIndices);
            Assert.True(WallMesh.From(overBudget).NeedsWideIndices);
        }
    }
}
