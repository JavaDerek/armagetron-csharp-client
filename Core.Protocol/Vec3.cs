using System;

namespace Armagetron.Protocol
{
    /// <summary>
    /// Engine-neutral 3D vector (rule 4): the sibling of <see cref="Vec2"/> used by the 3D
    /// camera and world-geometry layer. The world lifts an arena point (x, y) to (x, height, y)
    /// — the floor is the plane Y=0 and "up" is +Y — so a top-down arena coordinate keeps its
    /// X on X and its Y on Z. Map to MonoGame's Vector3 in the front-end; never reference it here.
    /// </summary>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>Unit vector in the same direction; the zero vector maps to itself.</summary>
        public Vec3 Normalized
        {
            get
            {
                float l = Length;
                return l < 1e-6f ? new Vec3(0, 0, 0) : new Vec3(X / l, Y / l, Z / l);
            }
        }

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>Right-handed cross product a × b.</summary>
        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        /// <summary>Lift an arena point (x, y) to a 3D world point at height <paramref name="height"/>: (x, height, y).</summary>
        public static Vec3 FromArena(Vec2 p, float height) => new Vec3(p.X, height, p.Y);

        public bool Equals(Vec3 o) => X == o.X && Y == o.Y && Z == o.Z;
        public override bool Equals(object? o) => o is Vec3 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X:0.##},{Y:0.##},{Z:0.##})";
    }
}
