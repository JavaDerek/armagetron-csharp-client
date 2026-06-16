using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Armagetron.Game.Rendering
{
    /// <summary>
    /// A GPU lightcycle model: the designer's <c>.glb</c> parsed by the unit-tested
    /// <see cref="GlbModelLoader"/> and uploaded to vertex/index buffers. Vertices are white so the
    /// renderer tints the whole model to the player colour via <c>BasicEffect.DiffuseColor</c> (the
    /// one-white-master-eight-colours trick). Normals/UVs are parsed and available on the neutral
    /// <c>ModelMesh3D</c> for a future lit/textured pass; v1 draws it flat-tinted to match the
    /// existing unlit neon look.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class CycleModel : IDisposable
    {
        public VertexBuffer Vertices { get; }
        public IndexBuffer Indices { get; }
        public int PrimitiveCount { get; }

        public CycleModel(GraphicsDevice gd, ModelMesh3D mesh)
        {
            var verts = new VertexPositionColor[mesh.VertexCount];
            for (int i = 0; i < verts.Length; i++)
            {
                Armagetron.Protocol.Vec3 p = mesh.Positions[i];
                verts[i] = new VertexPositionColor(new Vector3(p.X, p.Y, p.Z), Color.White);
            }
            Vertices = new VertexBuffer(gd, typeof(VertexPositionColor), verts.Length, BufferUsage.WriteOnly);
            Vertices.SetData(verts);

            Indices = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, mesh.Indices.Length, BufferUsage.WriteOnly);
            Indices.SetData(mesh.Indices);
            PrimitiveCount = mesh.TriangleCount;
        }

        public void Dispose()
        {
            Vertices.Dispose();
            Indices.Dispose();
        }
    }

    /// <summary>
    /// Loads 3D models from the copied <c>media/models/</c> tree at runtime, mirroring
    /// <see cref="TextureStore"/>: lazy, cached, and a missing file degrades to <c>null</c> so the
    /// 3D renderer falls back to its billboard rather than crashing. This is what makes the head
    /// "render the cycle the moment the designer delivers it" — drop <c>cycle.glb</c> into
    /// <c>media/models/</c> and it appears; until then the billboard shows.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class ModelStore : IDisposable
    {
        private readonly GraphicsDevice _gd;
        private readonly string _mediaRoot;
        private bool _cycleLoaded;
        private CycleModel? _cycle;

        public ModelStore(GraphicsDevice gd, string? mediaRoot = null)
        {
            _gd = gd;
            _mediaRoot = mediaRoot ?? Path.Combine(AppContext.BaseDirectory, "media");
        }

        /// <summary>The lightcycle model, or null if <c>media/models/cycle.glb</c> isn't present
        /// (the current state until the Phase-2 asset lands → the renderer uses its billboard).</summary>
        public CycleModel? Cycle
        {
            get
            {
                if (_cycleLoaded) return _cycle;
                _cycleLoaded = true;

                string path = Path.Combine(_mediaRoot, "models", "cycle.glb");
                if (File.Exists(path))
                {
                    try
                    {
                        _cycle = new CycleModel(_gd, GlbModelLoader.ReadFile(path));
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[ModelStore] failed to load {path}: {ex.Message}");
                    }
                }
                return _cycle;
            }
        }

        public void Dispose() => _cycle?.Dispose();
    }
}
