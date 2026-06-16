using UnityEngine;
using Armagetron.Game;       // WorldScene, WallMesh

namespace Armagetron.Oculus
{
    /// <summary>
    /// Uploads a <see cref="WorldScene"/>'s light walls to a single combined Unity <see cref="Mesh"/>
    /// each frame — one draw call for the whole arena, vertex-coloured per player.
    ///
    /// The tessellation itself (vertex order, triangle winding, per-vertex colour, and the 16- vs
    /// 32-bit index decision) lives in the engine-neutral, UNIT-TESTED <see cref="WallMesh"/> in
    /// Core.Protocol — so this method is now nothing but a type-bridge copy of those arrays into the
    /// Unity mesh via <see cref="VrConvert"/>. Keeping the geometry decision in the testable core is
    /// the same split the README describes for the rest of this head (Scene3DBuilder / Camera3D).
    /// </summary>
    public static class WallMeshBuilder
    {
        public static void Build(Mesh mesh, WorldScene world)
        {
            WallMesh m = WallMesh.From(world);

            var verts = new Vector3[m.Vertices.Length];
            for (int i = 0; i < verts.Length; i++) verts[i] = VrConvert.ToUnity(m.Vertices[i]);

            var colors = new Color[m.Colors.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = VrConvert.ToUnity(m.Colors[i]);

            mesh.Clear();
            mesh.indexFormat = m.NeedsWideIndices
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = m.Indices;
            mesh.RecalculateBounds();
        }
    }
}
