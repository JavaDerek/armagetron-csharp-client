using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Armagetron.Game;
using Armagetron.Game.Rendering;
using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the pure glTF-binary (.glb) → <see cref="ModelMesh3D"/> parser that the MonoGame
    /// heads will use to load the designer's lightcycle model (DESIGN_BRIEF_3D.md). A real .glb
    /// can't be eyeballed until the asset lands and a window/GPU is available, so the parsing —
    /// container framing, accessor/bufferView indirection, index component types, and node-transform
    /// baking — is pinned here against synthetic .glb blobs built in-test. No MonoGame, no GPU.
    /// </summary>
    public class GlbModelLoaderTests
    {
        [Fact]
        public void Read_MinimalTriangle_ParsesPositionsAndIndices()
        {
            var pos = new[] { new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0) };
            byte[] glb = new GlbBuilder().AddTriangleMesh(pos).Build();

            ModelMesh3D m = GlbModelLoader.Read(glb);

            Assert.Equal(3, m.VertexCount);
            Assert.Equal(1, m.TriangleCount);
            Assert.Equal(new[] { 0, 1, 2 }, m.Indices);
            Assert.Equal(new Vec3(1, 0, 0), m.Positions[1]);
            Assert.False(m.HasNormals);
            Assert.False(m.HasTexCoords);
        }

        [Fact]
        public void Read_WithNormalsAndTexCoords_ParsesThem()
        {
            var pos = new[] { new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0) };
            var nrm = new[] { new Vec3(0, 1, 0), new Vec3(0, 1, 0), new Vec3(0, 1, 0) };
            var uv = new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(0, 1) };
            byte[] glb = new GlbBuilder().AddTriangleMesh(pos, nrm, uv).Build();

            ModelMesh3D m = GlbModelLoader.Read(glb);

            Assert.True(m.HasNormals);
            Assert.True(m.HasTexCoords);
            Assert.Equal(new Vec3(0, 1, 0), m.Normals[0]);
            Assert.Equal(new Vec2(1, 0), m.TexCoords[1]);
        }

        [Fact]
        public void Read_UIntIndices_AreParsed()
        {
            var pos = new[] { new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0) };
            byte[] glb = new GlbBuilder().AddTriangleMesh(pos, uintIndices: true).Build();

            ModelMesh3D m = GlbModelLoader.Read(glb);
            Assert.Equal(new[] { 0, 1, 2 }, m.Indices);
        }

        [Fact]
        public void Read_BaseColorFactor_MapsToRenderColor()
        {
            var pos = new[] { new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0) };
            byte[] glb = new GlbBuilder()
                .AddTriangleMesh(pos, baseColor: new[] { 1f, 0f, 0f, 1f })
                .Build();

            ModelMesh3D m = GlbModelLoader.Read(glb);
            Assert.Equal((byte)255, m.BaseColor.R);
            Assert.Equal((byte)0, m.BaseColor.G);
            Assert.Equal((byte)0, m.BaseColor.B);
        }

        [Fact]
        public void Read_NodeTranslation_IsBakedIntoPositions()
        {
            var pos = new[] { new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0) };
            byte[] glb = new GlbBuilder()
                .AddTriangleMesh(pos)
                .WithNodeTranslation(10f, 0f, 5f)
                .Build();

            ModelMesh3D m = GlbModelLoader.Read(glb);
            Assert.Equal(new Vec3(10, 0, 5), m.Positions[0]);
            Assert.Equal(new Vec3(11, 0, 5), m.Positions[1]);
        }

        [Fact]
        public void Read_NodeMatrix_RotationIsBakedIntoPositions()
        {
            // glTF column-major matrix for a +90° rotation about Y: maps (1,0,0) -> (0,0,-1).
            float[] rotY90 = { 0, 0, -1, 0,  0, 1, 0, 0,  1, 0, 0, 0,  0, 0, 0, 1 };
            var pos = new[] { new Vec3(1, 0, 0), new Vec3(0, 0, 0), new Vec3(0, 1, 0) };
            byte[] glb = new GlbBuilder().AddTriangleMesh(pos).WithNodeMatrix(rotY90).Build();

            ModelMesh3D m = GlbModelLoader.Read(glb);
            Assert.Equal(0f, m.Positions[0].X, 4);
            Assert.Equal(0f, m.Positions[0].Y, 4);
            Assert.Equal(-1f, m.Positions[0].Z, 4);
        }

        [Fact]
        public void Read_TwoPrimitives_AreConcatenatedWithIndexOffset()
        {
            var a = new[] { new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0) };
            var b = new[] { new Vec3(2, 0, 0), new Vec3(3, 0, 0), new Vec3(2, 1, 0) };
            byte[] glb = new GlbBuilder().AddTriangleMesh(a).AddTriangleMesh(b).Build();

            ModelMesh3D m = GlbModelLoader.Read(glb);
            Assert.Equal(6, m.VertexCount);
            Assert.Equal(2, m.TriangleCount);
            // Second primitive's indices reference its own three vertices (offset by 3).
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, m.Indices);
            Assert.Equal(new Vec3(2, 0, 0), m.Positions[3]);
        }

        [Fact]
        public void Read_BadMagic_Throws()
        {
            byte[] notGlb = Encoding.ASCII.GetBytes("this is not a glb file at all....");
            Assert.ThrowsAny<Exception>(() => GlbModelLoader.Read(notGlb));
        }

        // ── synthetic .glb builder ────────────────────────────────────────────────
        // Assembles a spec-valid binary glTF (one buffer in the BIN chunk, one bufferView +
        // accessor per attribute) so the parser can be exercised without a real model file.
        private sealed class GlbBuilder
        {
            private readonly List<byte> _bin = new List<byte>();
            private readonly List<object> _accessors = new List<object>();
            private readonly List<object> _bufferViews = new List<object>();
            private readonly List<object> _meshPrimitives = new List<object>();
            private readonly List<object> _materials = new List<object>();
            private Dictionary<string, object>? _nodeTransform;

            public GlbBuilder AddTriangleMesh(Vec3[] positions, Vec3[]? normals = null,
                Vec2[]? texCoords = null, bool uintIndices = false, float[]? baseColor = null)
            {
                var attrs = new Dictionary<string, object>
                {
                    ["POSITION"] = AddVec3Accessor(positions),
                };
                if (normals != null) attrs["NORMAL"] = AddVec3Accessor(normals);
                if (texCoords != null) attrs["TEXCOORD_0"] = AddVec2Accessor(texCoords);

                var idx = new int[positions.Length];
                for (int i = 0; i < idx.Length; i++) idx[i] = i;
                int idxAccessor = uintIndices ? AddUIntAccessor(idx) : AddUShortAccessor(idx);

                var prim = new Dictionary<string, object>
                {
                    ["attributes"] = attrs,
                    ["indices"] = idxAccessor,
                };
                if (baseColor != null)
                {
                    int mat = _materials.Count;
                    _materials.Add(new Dictionary<string, object>
                    {
                        ["pbrMetallicRoughness"] = new Dictionary<string, object>
                        {
                            ["baseColorFactor"] = baseColor,
                        },
                    });
                    prim["material"] = mat;
                }
                _meshPrimitives.Add(prim);
                return this;
            }

            public GlbBuilder WithNodeTranslation(float x, float y, float z)
            {
                _nodeTransform = new Dictionary<string, object> { ["translation"] = new[] { x, y, z } };
                return this;
            }

            public GlbBuilder WithNodeMatrix(float[] m16)
            {
                _nodeTransform = new Dictionary<string, object> { ["matrix"] = m16 };
                return this;
            }

            private int Align4()
            {
                while (_bin.Count % 4 != 0) _bin.Add(0);
                return _bin.Count;
            }

            private int AddVec3Accessor(Vec3[] v)
            {
                int offset = Align4();
                foreach (Vec3 p in v) { PutF(p.X); PutF(p.Y); PutF(p.Z); }
                return PushAccessor(offset, v.Length * 12, v.Length, 5126, "VEC3");
            }

            private int AddVec2Accessor(Vec2[] v)
            {
                int offset = Align4();
                foreach (Vec2 p in v) { PutF(p.X); PutF(p.Y); }
                return PushAccessor(offset, v.Length * 8, v.Length, 5126, "VEC2");
            }

            private int AddUShortAccessor(int[] v)
            {
                int offset = Align4();
                foreach (int i in v) { _bin.Add((byte)(i & 0xFF)); _bin.Add((byte)((i >> 8) & 0xFF)); }
                return PushAccessor(offset, v.Length * 2, v.Length, 5123, "SCALAR");
            }

            private int AddUIntAccessor(int[] v)
            {
                int offset = Align4();
                foreach (int i in v) PutU32((uint)i);
                return PushAccessor(offset, v.Length * 4, v.Length, 5125, "SCALAR");
            }

            private int PushAccessor(int byteOffset, int byteLength, int count, int componentType, string type)
            {
                int bv = _bufferViews.Count;
                _bufferViews.Add(new Dictionary<string, object>
                {
                    ["buffer"] = 0,
                    ["byteOffset"] = byteOffset,
                    ["byteLength"] = byteLength,
                });
                int acc = _accessors.Count;
                _accessors.Add(new Dictionary<string, object>
                {
                    ["bufferView"] = bv,
                    ["componentType"] = componentType,
                    ["count"] = count,
                    ["type"] = type,
                });
                return acc;
            }

            private void PutF(float f) => _bin.AddRange(BitConverter.GetBytes(f));
            private void PutU32(uint u) => _bin.AddRange(BitConverter.GetBytes(u));

            public byte[] Build()
            {
                var node = new Dictionary<string, object> { ["mesh"] = 0 };
                if (_nodeTransform != null)
                    foreach (var kv in _nodeTransform) node[kv.Key] = kv.Value;

                var root = new Dictionary<string, object>
                {
                    ["asset"] = new Dictionary<string, object> { ["version"] = "2.0" },
                    ["scene"] = 0,
                    ["scenes"] = new[] { new Dictionary<string, object> { ["nodes"] = new[] { 0 } } },
                    ["nodes"] = new[] { node },
                    ["meshes"] = new[] { new Dictionary<string, object> { ["primitives"] = _meshPrimitives } },
                    ["accessors"] = _accessors,
                    ["bufferViews"] = _bufferViews,
                    ["buffers"] = new[] { new Dictionary<string, object> { ["byteLength"] = _bin.Count } },
                };
                if (_materials.Count > 0) root["materials"] = _materials;

                byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(root));
                byte[] jsonChunk = Pad(json, 0x20);
                byte[] binChunk = Pad(_bin.ToArray(), 0x00);

                var outp = new List<byte>();
                // header
                outp.AddRange(Encoding.ASCII.GetBytes("glTF"));
                outp.AddRange(BitConverter.GetBytes((uint)2));
                int total = 12 + 8 + jsonChunk.Length + 8 + binChunk.Length;
                outp.AddRange(BitConverter.GetBytes((uint)total));
                // JSON chunk
                outp.AddRange(BitConverter.GetBytes((uint)jsonChunk.Length));
                outp.AddRange(BitConverter.GetBytes(0x4E4F534Au)); // "JSON"
                outp.AddRange(jsonChunk);
                // BIN chunk
                outp.AddRange(BitConverter.GetBytes((uint)binChunk.Length));
                outp.AddRange(BitConverter.GetBytes(0x004E4942u)); // "BIN\0"
                outp.AddRange(binChunk);
                return outp.ToArray();
            }

            private static byte[] Pad(byte[] data, byte padByte)
            {
                int rem = data.Length % 4;
                if (rem == 0) return data;
                var padded = new byte[data.Length + (4 - rem)];
                Array.Copy(data, padded, data.Length);
                for (int i = data.Length; i < padded.Length; i++) padded[i] = padByte;
                return padded;
            }
        }
    }
}
