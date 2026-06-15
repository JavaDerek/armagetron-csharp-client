using UnityEngine;
using Armagetron.Game;       // WorldScene, WallQuad

namespace Armagetron.Oculus
{
    /// <summary>
    /// Rebuilds a single combined <see cref="Mesh"/> from a <see cref="WorldScene"/>'s light-wall
    /// quads each frame — two triangles per wall, vertex-coloured per player. The geometry decision
    /// (which walls exist, where, extruded to what height) is made by the engine-neutral, unit-tested
    /// <c>Scene3DBuilder</c> in Core.Protocol; this just uploads it to the GPU as a Unity mesh. One
    /// draw call for every wall in the arena.
    /// </summary>
    public static class WallMeshBuilder
    {
        public static void Build(Mesh mesh, WorldScene world)
        {
            int n = world.Walls.Count;
            var verts = new Vector3[n * 4];
            var colors = new Color[n * 4];
            var tris = new int[n * 6];

            int vi = 0, ti = 0;
            foreach (WallQuad w in world.Walls)
            {
                verts[vi + 0] = VrConvert.ToUnity(w.A);
                verts[vi + 1] = VrConvert.ToUnity(w.B);
                verts[vi + 2] = VrConvert.ToUnity(w.C);
                verts[vi + 3] = VrConvert.ToUnity(w.D);

                Color c = VrConvert.ToUnity(w.Color);
                colors[vi + 0] = colors[vi + 1] = colors[vi + 2] = colors[vi + 3] = c;

                // Two triangles (A,B,C) and (A,C,D). Doubled-sided look comes from the material
                // (Cull Off) so walls show from both faces, like the C++ light walls.
                tris[ti + 0] = vi + 0; tris[ti + 1] = vi + 1; tris[ti + 2] = vi + 2;
                tris[ti + 3] = vi + 0; tris[ti + 4] = vi + 2; tris[ti + 5] = vi + 3;

                vi += 4; ti += 6;
            }

            mesh.Clear();
            mesh.indexFormat = n * 4 > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }
    }
}
