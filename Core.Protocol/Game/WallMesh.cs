using System.Collections.Generic;
using Armagetron.Protocol;

namespace Armagetron.Game
{
    /// <summary>
    /// The pure, GPU-free tessellation of a <see cref="WorldScene"/>'s light walls into an indexed
    /// triangle mesh: per <see cref="WallQuad"/>, 4 vertices (A,B,C,D) and 6 indices forming two
    /// triangles wound (A,B,C) and (A,C,D), with a flat per-vertex color.
    ///
    /// This is engine-neutral on purpose. The triangulation — vertex order, triangle winding,
    /// per-vertex color — is exactly the part of the VR/3D wall rendering most likely to fail
    /// invisibly (a flipped index winds a wall the wrong way and back-face culling hides it; a
    /// mis-grouped color tints the wrong player). It used to live inside the Unity
    /// <c>WallMeshBuilder</c> where nothing could test it. Here it is unit-tested with no UnityEngine
    /// or GPU; a front-end (Unity, a desktop 3D renderer) just copies these arrays into its own mesh
    /// type via a thin <c>Vec3</c>/<c>RenderColor</c> bridge. The geometry decision (which walls
    /// exist, where, extruded to what height) stays in <see cref="Scene3DBuilder"/>.
    /// </summary>
    public sealed class WallMesh
    {
        // Unity meshes default to a 16-bit index buffer; past this many vertices the front-end must
        // switch to 32-bit (see NeedsWideIndices). 65000 is a deliberately conservative cushion
        // below the hard 65535 UInt16 limit — a Tron arena never approaches it, but the flag keeps
        // the (untestable-on-device) decision honest and pinned.
        private const int MaxUInt16Vertices = 65000;

        /// <summary>Vertices in A,B,C,D order per wall; 4 × wall count.</summary>
        public Vec3[] Vertices { get; }

        /// <summary>Triangle indices, 6 × wall count: (A,B,C),(A,C,D) per wall, each offset by 4·i.</summary>
        public int[] Indices { get; }

        /// <summary>Flat per-vertex color, parallel to <see cref="Vertices"/>; 4 entries per wall.</summary>
        public RenderColor[] Colors { get; }

        /// <summary>True once the vertex total exceeds the 16-bit index budget and the front-end
        /// must allocate a 32-bit index buffer for the upload.</summary>
        public bool NeedsWideIndices => Vertices.Length > MaxUInt16Vertices;

        private WallMesh(Vec3[] vertices, int[] indices, RenderColor[] colors)
        {
            Vertices = vertices;
            Indices = indices;
            Colors = colors;
        }

        /// <summary>Tessellate a whole scene's walls.</summary>
        public static WallMesh From(WorldScene world) => From(world.Walls);

        /// <summary>Tessellate a list of wall quads into the indexed triangle mesh.</summary>
        public static WallMesh From(IReadOnlyList<WallQuad> walls)
        {
            int n = walls.Count;
            var verts = new Vec3[n * 4];
            var colors = new RenderColor[n * 4];
            var tris = new int[n * 6];

            int vi = 0, ti = 0;
            for (int i = 0; i < n; i++)
            {
                WallQuad w = walls[i];
                verts[vi + 0] = w.A;
                verts[vi + 1] = w.B;
                verts[vi + 2] = w.C;
                verts[vi + 3] = w.D;

                colors[vi + 0] = colors[vi + 1] = colors[vi + 2] = colors[vi + 3] = w.Color;

                // Two triangles (A,B,C) and (A,C,D), referencing this wall's own four vertices.
                tris[ti + 0] = vi + 0; tris[ti + 1] = vi + 1; tris[ti + 2] = vi + 2;
                tris[ti + 3] = vi + 0; tris[ti + 4] = vi + 2; tris[ti + 5] = vi + 3;

                vi += 4; ti += 6;
            }

            return new WallMesh(verts, tris, colors);
        }
    }
}
