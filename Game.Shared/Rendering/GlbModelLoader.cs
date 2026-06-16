using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Armagetron.Game;       // ModelMesh3D
using Armagetron.Protocol;   // Vec2, Vec3, RenderColor

namespace Armagetron.Game.Rendering
{
    /// <summary>
    /// Pure parser for binary glTF 2.0 (<c>.glb</c>) → an engine-neutral <see cref="ModelMesh3D"/>.
    /// Deliberately MonoGame-free (only System.Text.Json + System.Numerics + the neutral core
    /// types) so it is unit-testable without a GPU and reusable across the MonoGame heads; the GPU
    /// upload lives in <c>ModelStore</c>. Unity has its own glTF import, so it does not use this.
    ///
    /// Supported subset (matches DESIGN_BRIEF_3D.md's export spec — single low-poly model, Y-up):
    /// the .glb container (JSON + BIN chunks), one buffer (the BIN chunk), all primitives across the
    /// scene's node graph concatenated into one mesh, POSITION (required) + NORMAL + TEXCOORD_0
    /// attributes (float), triangle indices (unsigned byte/short/int, or sequential if absent), node
    /// transforms (matrix or T/R/S, composed down the hierarchy and baked into the vertices), and the
    /// first material's baseColorFactor. Non-triangle primitive modes and sparse accessors are not
    /// supported and throw. glTF stores little-endian; all our targets are little-endian.
    /// </summary>
    public static class GlbModelLoader
    {
        private const uint Magic = 0x46546C67;     // "glTF"
        private const uint ChunkJson = 0x4E4F534A; // "JSON"
        private const uint ChunkBin = 0x004E4942;  // "BIN\0"

        // glTF component types
        private const int CompByte = 5121;   // UNSIGNED_BYTE
        private const int CompShort = 5123;  // UNSIGNED_SHORT
        private const int CompInt = 5125;    // UNSIGNED_INT
        private const int CompFloat = 5126;

        /// <summary>Read a .glb file from disk.</summary>
        public static ModelMesh3D ReadFile(string path) => Read(File.ReadAllBytes(path));

        /// <summary>Parse a .glb byte blob into a neutral mesh.</summary>
        public static ModelMesh3D Read(byte[] glb)
        {
            if (glb == null) throw new ArgumentNullException(nameof(glb));
            if (glb.Length < 12 || BitConverter.ToUInt32(glb, 0) != Magic)
                throw new InvalidDataException("Not a binary glTF (.glb): bad magic.");

            // Chunks: [uint length][uint type][bytes]. We need the JSON chunk and the BIN chunk.
            byte[]? json = null, bin = null;
            int p = 12;
            while (p + 8 <= glb.Length)
            {
                uint len = BitConverter.ToUInt32(glb, p);
                uint type = BitConverter.ToUInt32(glb, p + 4);
                int dataStart = p + 8;
                if (dataStart + (int)len > glb.Length)
                    throw new InvalidDataException("Truncated .glb chunk.");
                var data = new byte[len];
                Array.Copy(glb, dataStart, data, 0, (int)len);
                if (type == ChunkJson) json = data;
                else if (type == ChunkBin) bin = data;
                p = dataStart + (int)len;
            }
            if (json == null) throw new InvalidDataException(".glb has no JSON chunk.");
            bin ??= Array.Empty<byte>();

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            JsonElement accessors = Arr(root, "accessors");
            JsonElement bufferViews = Arr(root, "bufferViews");
            JsonElement meshes = Arr(root, "meshes");
            JsonElement nodes = Arr(root, "nodes");

            var positions = new List<Vec3>();
            var normals = new List<Vec3>();
            var texCoords = new List<Vec2>();
            var indices = new List<int>();
            RenderColor baseColor = new RenderColor(255, 255, 255, 255);
            bool baseColorSet = false;

            void EmitMesh(int meshIndex, Matrix4x4 world)
            {
                if (meshIndex < 0 || meshIndex >= meshes.GetArrayLength()) return;
                JsonElement prims = Arr(meshes[meshIndex], "primitives");
                foreach (JsonElement prim in prims.EnumerateArray())
                {
                    if (prim.TryGetProperty("mode", out JsonElement mode) && mode.GetInt32() != 4)
                        throw new NotSupportedException("Only triangle (mode 4) primitives are supported.");

                    if (!prim.TryGetProperty("attributes", out JsonElement attrs) ||
                        !attrs.TryGetProperty("POSITION", out JsonElement posAcc))
                        throw new InvalidDataException("Primitive has no POSITION attribute.");

                    int baseVertex = positions.Count;
                    var localPos = ReadVec3Accessor(accessors[posAcc.GetInt32()], bufferViews, bin);
                    foreach (Vec3 v in localPos)
                    {
                        Vector3 t = Vector3.Transform(new Vector3(v.X, v.Y, v.Z), world);
                        positions.Add(new Vec3(t.X, t.Y, t.Z));
                    }

                    if (attrs.TryGetProperty("NORMAL", out JsonElement nAcc))
                        foreach (Vec3 n in ReadVec3Accessor(accessors[nAcc.GetInt32()], bufferViews, bin))
                        {
                            Vector3 tn = Vector3.Normalize(Vector3.TransformNormal(new Vector3(n.X, n.Y, n.Z), world));
                            normals.Add(new Vec3(tn.X, tn.Y, tn.Z));
                        }

                    if (attrs.TryGetProperty("TEXCOORD_0", out JsonElement uvAcc))
                        texCoords.AddRange(ReadVec2Accessor(accessors[uvAcc.GetInt32()], bufferViews, bin));

                    if (prim.TryGetProperty("indices", out JsonElement iAcc))
                        foreach (int idx in ReadIndexAccessor(accessors[iAcc.GetInt32()], bufferViews, bin))
                            indices.Add(baseVertex + idx);
                    else
                        for (int i = 0; i < localPos.Count; i++) indices.Add(baseVertex + i);

                    if (!baseColorSet && prim.TryGetProperty("material", out JsonElement matIdx))
                    {
                        baseColor = ReadBaseColor(root, matIdx.GetInt32()) ?? baseColor;
                        baseColorSet = true;
                    }
                }
            }

            void Recurse(int nodeIndex, Matrix4x4 parent)
            {
                if (nodeIndex < 0 || nodeIndex >= nodes.GetArrayLength()) return;
                JsonElement node = nodes[nodeIndex];
                Matrix4x4 world = LocalMatrix(node) * parent;
                if (node.TryGetProperty("mesh", out JsonElement m)) EmitMesh(m.GetInt32(), world);
                if (node.TryGetProperty("children", out JsonElement kids))
                    foreach (JsonElement c in kids.EnumerateArray()) Recurse(c.GetInt32(), world);
            }

            // Walk the scene graph if present (so node transforms apply); otherwise emit every mesh
            // at identity as a fallback for asset-only files with no scene.
            if (TryGetScene(root, nodes, out JsonElement sceneNodes))
                foreach (JsonElement n in sceneNodes.EnumerateArray()) Recurse(n.GetInt32(), Matrix4x4.Identity);
            else
                for (int mi = 0; mi < meshes.GetArrayLength(); mi++) EmitMesh(mi, Matrix4x4.Identity);

            return new ModelMesh3D(
                positions.ToArray(),
                normals.Count == positions.Count ? normals.ToArray() : Array.Empty<Vec3>(),
                texCoords.Count == positions.Count ? texCoords.ToArray() : Array.Empty<Vec2>(),
                indices.ToArray(),
                baseColor);
        }

        // ── scene / node helpers ──────────────────────────────────────────────────

        private static bool TryGetScene(JsonElement root, JsonElement nodes, out JsonElement sceneNodes)
        {
            sceneNodes = default;
            if (nodes.ValueKind != JsonValueKind.Array || nodes.GetArrayLength() == 0) return false;
            if (root.TryGetProperty("scenes", out JsonElement scenes) && scenes.GetArrayLength() > 0)
            {
                int sceneIdx = root.TryGetProperty("scene", out JsonElement s) ? s.GetInt32() : 0;
                if (sceneIdx < 0 || sceneIdx >= scenes.GetArrayLength()) sceneIdx = 0;
                if (scenes[sceneIdx].TryGetProperty("nodes", out sceneNodes)) return true;
            }
            return false;
        }

        private static Matrix4x4 LocalMatrix(JsonElement node)
        {
            if (node.TryGetProperty("matrix", out JsonElement m) && m.GetArrayLength() == 16)
            {
                float[] g = ReadFloats(m);
                // glTF stores column-major; System.Numerics uses the row-vector convention, which is
                // the transpose — so the column-major array fills the row-major constructor in order.
                return new Matrix4x4(g[0], g[1], g[2], g[3], g[4], g[5], g[6], g[7],
                                     g[8], g[9], g[10], g[11], g[12], g[13], g[14], g[15]);
            }

            Matrix4x4 t = node.TryGetProperty("translation", out JsonElement tr)
                ? Matrix4x4.CreateTranslation(ReadFloats(tr)[0], ReadFloats(tr)[1], ReadFloats(tr)[2])
                : Matrix4x4.Identity;
            Matrix4x4 r = Matrix4x4.Identity;
            if (node.TryGetProperty("rotation", out JsonElement rot))
            {
                float[] q = ReadFloats(rot);
                r = Matrix4x4.CreateFromQuaternion(new Quaternion(q[0], q[1], q[2], q[3]));
            }
            Matrix4x4 sc = node.TryGetProperty("scale", out JsonElement scl)
                ? Matrix4x4.CreateScale(ReadFloats(scl)[0], ReadFloats(scl)[1], ReadFloats(scl)[2])
                : Matrix4x4.Identity;

            // Row-vector convention: v * (S * R * T) applies scale, then rotation, then translation.
            return sc * r * t;
        }

        private static RenderColor? ReadBaseColor(JsonElement root, int materialIndex)
        {
            if (!root.TryGetProperty("materials", out JsonElement mats) ||
                materialIndex < 0 || materialIndex >= mats.GetArrayLength())
                return null;
            if (!mats[materialIndex].TryGetProperty("pbrMetallicRoughness", out JsonElement pbr) ||
                !pbr.TryGetProperty("baseColorFactor", out JsonElement f))
                return null;
            float[] c = ReadFloats(f);
            byte B(float v) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v * 255f)));
            return new RenderColor(B(c[0]), B(c[1]), B(c[2]), c.Length > 3 ? B(c[3]) : (byte)255);
        }

        // ── accessor readers ──────────────────────────────────────────────────────

        private static List<Vec3> ReadVec3Accessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
        {
            (int baseOff, int stride, int count, int comp) = AccessorLayout(accessor, bufferViews, defaultStride: 12);
            if (comp != CompFloat) throw new NotSupportedException("VEC3 accessor must be float.");
            var list = new List<Vec3>(count);
            for (int i = 0; i < count; i++)
            {
                int o = baseOff + i * stride;
                list.Add(new Vec3(BitConverter.ToSingle(bin, o), BitConverter.ToSingle(bin, o + 4),
                                  BitConverter.ToSingle(bin, o + 8)));
            }
            return list;
        }

        private static List<Vec2> ReadVec2Accessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
        {
            (int baseOff, int stride, int count, int comp) = AccessorLayout(accessor, bufferViews, defaultStride: 8);
            if (comp != CompFloat) throw new NotSupportedException("VEC2 accessor must be float.");
            var list = new List<Vec2>(count);
            for (int i = 0; i < count; i++)
            {
                int o = baseOff + i * stride;
                list.Add(new Vec2(BitConverter.ToSingle(bin, o), BitConverter.ToSingle(bin, o + 4)));
            }
            return list;
        }

        private static IEnumerable<int> ReadIndexAccessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
        {
            int comp = accessor.GetProperty("componentType").GetInt32();
            int size = comp switch
            {
                CompByte => 1, CompShort => 2, CompInt => 4,
                _ => throw new NotSupportedException($"Unsupported index component type {comp}."),
            };
            (int baseOff, int stride, int count, _) = AccessorLayout(accessor, bufferViews, defaultStride: size);
            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                int o = baseOff + i * stride;
                result[i] = comp switch
                {
                    CompByte => bin[o],
                    CompShort => BitConverter.ToUInt16(bin, o),
                    _ => (int)BitConverter.ToUInt32(bin, o),
                };
            }
            return result;
        }

        // Resolve an accessor to (absolute byte offset in bin, byte stride, element count, componentType).
        private static (int baseOff, int stride, int count, int comp) AccessorLayout(
            JsonElement accessor, JsonElement bufferViews, int defaultStride)
        {
            if (accessor.TryGetProperty("sparse", out _))
                throw new NotSupportedException("Sparse accessors are not supported.");

            int count = accessor.GetProperty("count").GetInt32();
            int comp = accessor.TryGetProperty("componentType", out JsonElement ct) ? ct.GetInt32() : CompFloat;
            int accOffset = accessor.TryGetProperty("byteOffset", out JsonElement ao) ? ao.GetInt32() : 0;
            int bvIndex = accessor.GetProperty("bufferView").GetInt32();

            JsonElement bv = bufferViews[bvIndex];
            int bvOffset = bv.TryGetProperty("byteOffset", out JsonElement bo) ? bo.GetInt32() : 0;
            int stride = bv.TryGetProperty("byteStride", out JsonElement bs) && bs.GetInt32() > 0
                ? bs.GetInt32() : defaultStride;

            return (bvOffset + accOffset, stride, count, comp);
        }

        // ── small json helpers ────────────────────────────────────────────────────

        private static JsonElement Arr(JsonElement parent, string name) =>
            parent.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.Array
                ? e : default;

        private static float[] ReadFloats(JsonElement arr)
        {
            var f = new float[arr.GetArrayLength()];
            int i = 0;
            foreach (JsonElement e in arr.EnumerateArray()) f[i++] = e.GetSingle();
            return f;
        }
    }
}
