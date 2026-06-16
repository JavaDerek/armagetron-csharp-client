using System;
using Armagetron.Protocol;

namespace Armagetron.Game
{
    /// <summary>
    /// A pure, GPU-free indexed triangle mesh loaded from a 3D model file (a designer's lightcycle
    /// <c>.glb</c>). Engine-neutral, like <see cref="WorldScene"/> / <see cref="WallMesh"/>: a
    /// front-end copies these arrays into its own vertex/index buffers. Positions are required;
    /// normals and texture coordinates are optional (empty when the source omits them). The mesh is
    /// the concatenation of every primitive in the model, with node transforms already baked into the
    /// positions/normals, so a front-end can upload it as a single buffer and tint it per player.
    /// </summary>
    public sealed class ModelMesh3D
    {
        /// <summary>Vertex positions in model space (arena units; Y-up, floor at Y=0).</summary>
        public Vec3[] Positions { get; }

        /// <summary>Per-vertex normals, parallel to <see cref="Positions"/>; empty if the model had none.</summary>
        public Vec3[] Normals { get; }

        /// <summary>Per-vertex UVs, parallel to <see cref="Positions"/>; empty if the model had none.</summary>
        public Vec2[] TexCoords { get; }

        /// <summary>Triangle indices into the vertex arrays (3 per triangle).</summary>
        public int[] Indices { get; }

        /// <summary>The material base-colour factor (a fallback tint; the head usually overrides it
        /// with the player colour, since the model ships as a white/greyscale master).</summary>
        public RenderColor BaseColor { get; }

        public ModelMesh3D(Vec3[] positions, Vec3[] normals, Vec2[] texCoords, int[] indices,
                           RenderColor baseColor)
        {
            Positions = positions ?? throw new ArgumentNullException(nameof(positions));
            Normals = normals ?? Array.Empty<Vec3>();
            TexCoords = texCoords ?? Array.Empty<Vec2>();
            Indices = indices ?? throw new ArgumentNullException(nameof(indices));
            BaseColor = baseColor;
        }

        /// <summary>True when a normal is present for every vertex (so the head can light it).</summary>
        public bool HasNormals => Positions.Length > 0 && Normals.Length == Positions.Length;

        /// <summary>True when a UV is present for every vertex (so the head can texture it).</summary>
        public bool HasTexCoords => Positions.Length > 0 && TexCoords.Length == Positions.Length;

        public int VertexCount => Positions.Length;
        public int TriangleCount => Indices.Length / 3;
    }
}
