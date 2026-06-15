using System;
using System.Collections.Generic;
using Armagetron.Protocol;

namespace Armagetron.Game
{
    /// <summary>
    /// A single light-wall panel in 3D world space: a vertical quad standing on the floor,
    /// extruded from a trail segment. Corners are wound A→B→C→D: A,B are the bottom edge
    /// (floor, Y=0) running from the segment start to its end; D,C are directly above them at
    /// <see cref="Top"/>'s height. The front-end emits two triangles (A,B,C) and (A,C,D).
    /// </summary>
    public readonly struct WallQuad
    {
        public readonly Vec3 A, B, C, D;   // A bottom-from, B bottom-to, C top-to, D top-from
        public readonly RenderColor Color;

        public WallQuad(Vec3 a, Vec3 b, Vec3 c, Vec3 d, RenderColor color)
        {
            A = a; B = b; C = c; D = d; Color = color;
        }
    }

    /// <summary>
    /// A cycle's body to be drawn in 3D (a billboard/model at the head). Position and direction
    /// are arena (2D) coordinates; the front-end lifts them onto the floor and orients the model.
    /// </summary>
    public readonly struct CycleMarker
    {
        public readonly Vec2 Position;
        public readonly Vec2 Direction;
        public readonly RenderColor Color;

        public CycleMarker(Vec2 position, Vec2 direction, RenderColor color)
        {
            Position = position; Direction = direction; Color = color;
        }
    }

    /// <summary>
    /// The pure, GPU-free description of one frame in 3D: the arena floor extent, the height the
    /// light walls are extruded to, the wall panels, and each cycle's head marker. The 3D
    /// renderer consumes this exactly as the 2D renderer consumes <see cref="Scene"/>, so the
    /// geometry decision (what walls exist, where, in which colors) is unit-testable from a
    /// world snapshot without a window.
    /// </summary>
    public sealed class WorldScene
    {
        public float ArenaSize { get; }
        public float WallHeight { get; }
        public IReadOnlyList<WallQuad> Walls { get; }
        public IReadOnlyList<CycleMarker> Cycles { get; }

        public WorldScene(float arenaSize, float wallHeight,
                          IReadOnlyList<WallQuad> walls, IReadOnlyList<CycleMarker> cycles)
        {
            ArenaSize = arenaSize;
            WallHeight = wallHeight;
            Walls = walls;
            Cycles = cycles;
        }
    }

    /// <summary>
    /// Builds a <see cref="WorldScene"/> from a world snapshot: extrudes every cycle's trail
    /// (and the active segment up to its head) into vertical light-wall quads in the cycle's
    /// color, and emits a head marker per cycle. The 3D analogue of <see cref="SceneBuilder"/> —
    /// same inputs, same color rules — kept pure so it tests from captured positions alone.
    /// </summary>
    public static class Scene3DBuilder
    {
        // Segments shorter than this (in world units) are degenerate (head sitting on its last
        // waypoint) and produce no wall — drawing a zero-area quad just risks GPU artifacts.
        private const float MinSegment = 1e-3f;

        public static WorldScene Build(CycleSnapshot[] cycles, int myId, CyclePalette palette,
                                       float arenaSize, float wallHeight)
        {
            var walls = new List<WallQuad>();
            var markers = new List<CycleMarker>();

            foreach (var c in cycles)
            {
                RenderColor color = c.CycleId == myId
                    ? CyclePalette.Mine
                    : palette.ColorFor(c.CycleId, myId);

                // Completed trail: wall between consecutive waypoints.
                for (int i = 0; i + 1 < c.Trail.Length; i++)
                    AddWall(walls, c.Trail[i], c.Trail[i + 1], wallHeight, color);

                // Active segment: last waypoint → current head.
                if (c.Trail.Length > 0)
                    AddWall(walls, c.Trail[c.Trail.Length - 1], c.Position, wallHeight, color);

                markers.Add(new CycleMarker(c.Position, c.Direction, color));
            }

            return new WorldScene(arenaSize, wallHeight, walls, markers);
        }

        private static void AddWall(List<WallQuad> walls, Vec2 from, Vec2 to, float height, RenderColor color)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            if (dx * dx + dy * dy < MinSegment * MinSegment) return; // skip degenerate

            Vec3 a = Vec3.FromArena(from, 0f);
            Vec3 b = Vec3.FromArena(to, 0f);
            Vec3 c = Vec3.FromArena(to, height);
            Vec3 d = Vec3.FromArena(from, height);
            walls.Add(new WallQuad(a, b, c, d, color));
        }
    }
}
