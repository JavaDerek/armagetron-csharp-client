using System;
using System.Diagnostics.CodeAnalysis;
using Armagetron.Game;      // WorldScene, WallQuad, CameraPose, CyclePalette
using Armagetron.Protocol;  // Vec3
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Armagetron.Game.Rendering
{
    /// <summary>
    /// The GPU draw loop for the 3D perspective views (third-person chase, first-person cockpit).
    /// Consumes a pure <see cref="WorldScene"/> plus a <see cref="CameraPose"/> exactly as
    /// <see cref="SceneRenderer"/> consumes a 2D <see cref="Scene"/>, so the geometry/camera math
    /// stays unit-tested in Core.Protocol and only the device calls live here. Draws a tiled
    /// arena floor, the four boundary walls, each cycle's light-wall trail (extruded quads, the
    /// neon glow coming from bright vertex colors on black), and a camera-facing billboard for
    /// each cycle head. This is the one I/O edge for 3D — excluded from coverage and proven by
    /// the render harness PNG and the live-server gate.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class Scene3DRenderer : IDisposable
    {
        private const float FovDegrees = 60f;
        private const float NearPlane = 0.4f;
        private const float FarPlane = 2000f;
        private const float BillboardSize = 7f;   // world units across for a cycle head
        private const int FloorTileWorld = 22;    // ~world units per arena-tile repeat

        private readonly GraphicsDevice _gd;
        private readonly TextureStore _textures;
        private readonly ModelStore _models;
        private readonly BasicEffect _effect;

        private static readonly RasterizerState NoCull = new RasterizerState
        {
            CullMode = CullMode.None,
            FillMode = FillMode.Solid,
        };

        public Scene3DRenderer(GraphicsDevice gd, TextureStore textures, string? mediaRoot = null)
        {
            _gd = gd;
            _textures = textures;
            _models = new ModelStore(gd, mediaRoot);
            _effect = new BasicEffect(gd) { LightingEnabled = false };
        }

        /// <summary>Render <paramref name="world"/> from <paramref name="pose"/> into a viewport of
        /// the given pixel size (used for the aspect ratio).</summary>
        public void Render(WorldScene world, CameraPose pose, int viewportW, int viewportH)
        {
            float aspect = viewportH <= 0 ? 1f : (float)viewportW / viewportH;

            _gd.DepthStencilState = DepthStencilState.Default;
            _gd.RasterizerState = NoCull;
            _gd.BlendState = BlendState.NonPremultiplied;
            _gd.SamplerStates[0] = SamplerState.LinearWrap;

            _effect.World = Matrix.Identity;
            _effect.View = Matrix.CreateLookAt(V(pose.Eye), V(pose.Target), V(pose.Up));
            _effect.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(FovDegrees), aspect, NearPlane, FarPlane);

            DrawFloor(world);
            DrawWalls(world);
            DrawCycles(world, pose);
        }

        // Tiled arena floor as one textured quad; falls back to a flat dark fill if the tile
        // texture is missing so the play surface is always visible.
        private void DrawFloor(WorldScene world)
        {
            float s = world.ArenaSize;
            Texture2D? tex = _textures.Get("ingame/arena");
            float reps = MathF.Max(1f, s / FloorTileWorld);

            if (tex != null)
            {
                _effect.TextureEnabled = true;
                _effect.VertexColorEnabled = false;
                _effect.Texture = tex;
                _effect.DiffuseColor = Vector3.One;
                var quad = new[]
                {
                    new VertexPositionTexture(new Vector3(0, 0, 0), new Vector2(0, 0)),
                    new VertexPositionTexture(new Vector3(s, 0, 0), new Vector2(reps, 0)),
                    new VertexPositionTexture(new Vector3(s, 0, s), new Vector2(reps, reps)),
                    new VertexPositionTexture(new Vector3(0, 0, 0), new Vector2(0, 0)),
                    new VertexPositionTexture(new Vector3(s, 0, s), new Vector2(reps, reps)),
                    new VertexPositionTexture(new Vector3(0, 0, s), new Vector2(0, reps)),
                };
                ApplyAndDraw(quad);
            }
            else
            {
                var dark = new Color(8, 14, 26);
                var quad = new[]
                {
                    new VertexPositionColor(new Vector3(0, 0, 0), dark),
                    new VertexPositionColor(new Vector3(s, 0, 0), dark),
                    new VertexPositionColor(new Vector3(s, 0, s), dark),
                    new VertexPositionColor(new Vector3(0, 0, 0), dark),
                    new VertexPositionColor(new Vector3(s, 0, s), dark),
                    new VertexPositionColor(new Vector3(0, 0, s), dark),
                };
                DrawColored(quad);
            }
        }

        private void DrawWalls(WorldScene world)
        {
            // Two triangles per cycle wall, plus the four arena boundary walls.
            int total = world.Walls.Count + 4;
            var verts = new VertexPositionColor[total * 6];
            int v = 0;

            foreach (WallQuad w in world.Walls)
                v = AppendQuad(verts, v, w.A, w.B, w.C, w.D, ToXna(w.Color));

            // Boundary: a dim cyan rail around the play area at full wall height.
            float s = world.ArenaSize, h = world.WallHeight;
            var edge = new Color(40, 90, 120, 255);
            v = AppendQuad(verts, v, new Vec3(0, 0, 0), new Vec3(s, 0, 0), new Vec3(s, h, 0), new Vec3(0, h, 0), edge);
            v = AppendQuad(verts, v, new Vec3(s, 0, 0), new Vec3(s, 0, s), new Vec3(s, h, s), new Vec3(s, h, 0), edge);
            v = AppendQuad(verts, v, new Vec3(s, 0, s), new Vec3(0, 0, s), new Vec3(0, h, s), new Vec3(s, h, s), edge);
            v = AppendQuad(verts, v, new Vec3(0, 0, s), new Vec3(0, 0, 0), new Vec3(0, h, 0), new Vec3(0, h, s), edge);

            if (v > 0) DrawColored(verts);
        }

        private void DrawCycles(WorldScene world, CameraPose pose)
        {
            // Prefer the designer's 3D lightcycle model once it's delivered; until then fall back to
            // the camera-facing billboard so the view always shows cycles.
            CycleModel? model = _models.Cycle;
            if (model != null) { DrawCycleModels(world, model); return; }

            Texture2D? tex = _textures.Get("ingame/cycle");
            if (tex == null) return;

            // Billboard basis: right/up perpendicular to the view direction.
            Vector3 viewDir = V(pose.Target) - V(pose.Eye);
            if (viewDir.LengthSquared() < 1e-6f) viewDir = Vector3.Forward;
            viewDir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.Up, viewDir);
            if (right.LengthSquared() < 1e-6f) right = Vector3.Right;
            right.Normalize();
            Vector3 up = Vector3.Cross(viewDir, right);

            _effect.TextureEnabled = true;
            _effect.VertexColorEnabled = false;
            _effect.Texture = tex;

            float half = BillboardSize / 2f;
            foreach (CycleMarker m in world.Cycles)
            {
                var center = new Vector3(m.Position.X, half, m.Position.Y);
                Vector3 r = right * half, u = up * half;
                var quad = new[]
                {
                    new VertexPositionTexture(center - r + u, new Vector2(0, 0)),
                    new VertexPositionTexture(center + r + u, new Vector2(1, 0)),
                    new VertexPositionTexture(center + r - u, new Vector2(1, 1)),
                    new VertexPositionTexture(center - r + u, new Vector2(0, 0)),
                    new VertexPositionTexture(center + r - u, new Vector2(1, 1)),
                    new VertexPositionTexture(center - r - u, new Vector2(0, 1)),
                };
                Color c = ToXna(m.Color);
                _effect.DiffuseColor = new Vector3(c.R / 255f, c.G / 255f, c.B / 255f);
                ApplyAndDraw(quad);
            }
            _effect.DiffuseColor = Vector3.One;
        }

        // Draw the lightcycle model once per cycle: tinted to the player colour (white-master model
        // modulated by DiffuseColor), placed at the head and yawed to face its heading. The model is
        // authored nose-+X at identity (DESIGN_BRIEF_3D.md), so the yaw rotates +X onto the heading.
        private void DrawCycleModels(WorldScene world, CycleModel model)
        {
            _gd.SetVertexBuffer(model.Vertices);
            _gd.Indices = model.Indices;
            _effect.TextureEnabled = false;
            _effect.VertexColorEnabled = false;

            foreach (CycleMarker m in world.Cycles)
            {
                float yaw = MathF.Atan2(-m.Direction.Y, m.Direction.X); // +X heading → 0
                _effect.World = Matrix.CreateRotationY(yaw)
                              * Matrix.CreateTranslation(m.Position.X, 0f, m.Position.Y);
                Color c = ToXna(m.Color);
                _effect.DiffuseColor = new Vector3(c.R / 255f, c.G / 255f, c.B / 255f);
                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, model.PrimitiveCount);
                }
            }

            _effect.World = Matrix.Identity;
            _effect.DiffuseColor = Vector3.One;
        }

        private int AppendQuad(VertexPositionColor[] buf, int i, Vec3 a, Vec3 b, Vec3 c, Vec3 d, Color color)
        {
            buf[i++] = new VertexPositionColor(V(a), color);
            buf[i++] = new VertexPositionColor(V(b), color);
            buf[i++] = new VertexPositionColor(V(c), color);
            buf[i++] = new VertexPositionColor(V(a), color);
            buf[i++] = new VertexPositionColor(V(c), color);
            buf[i++] = new VertexPositionColor(V(d), color);
            return i;
        }

        private void DrawColored(VertexPositionColor[] verts)
        {
            _effect.TextureEnabled = false;
            _effect.VertexColorEnabled = true;
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _gd.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, verts.Length / 3);
            }
            _effect.VertexColorEnabled = false;
        }

        private void ApplyAndDraw(VertexPositionTexture[] verts)
        {
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _gd.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, verts.Length / 3);
            }
        }

        private static Vector3 V(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
        private static Color ToXna(RenderColor c) => new Color(c.R, c.G, c.B, c.A);

        public void Dispose()
        {
            _models.Dispose();
            _effect.Dispose();
        }
    }
}
