using System;
using Armagetron.Protocol;

namespace Armagetron.Game
{
    /// <summary>
    /// Which viewpoint the desktop client renders the arena from. <see cref="TopDown"/> is the
    /// original 2D orthographic view (drawn by the SpriteBatch path); the other two are 3D
    /// perspective views built by <see cref="Camera3D"/>, matching the C++ client's in-cycle
    /// (first-person) and chase (third-person, orbit-able) cameras.
    /// </summary>
    public enum CameraMode
    {
        /// <summary>Classic 2D top-down (the existing render path).</summary>
        TopDown,
        /// <summary>Chase camera floating behind/above the local cycle; orbit-able with the mouse.</summary>
        ThirdPerson,
        /// <summary>In-cockpit view from the cycle, looking along its heading.</summary>
        FirstPerson,
    }

    /// <summary>
    /// A camera placement in 3D world space: where the eye is, what it looks at, and which way
    /// is up. The front-end turns this into MonoGame view/projection matrices
    /// (<c>Matrix.CreateLookAt</c>); keeping it as pure data makes the placement math testable
    /// without any GPU.
    /// </summary>
    public readonly struct CameraPose
    {
        public readonly Vec3 Eye;
        public readonly Vec3 Target;
        public readonly Vec3 Up;

        public CameraPose(Vec3 eye, Vec3 target, Vec3 up)
        {
            Eye = eye;
            Target = target;
            Up = up;
        }

        /// <summary>Normalized look direction from the eye toward the target.</summary>
        public Vec3 Forward => (Target - Eye).Normalized;
    }

    /// <summary>
    /// Tunable heights/offsets for the 3D camera, kept separate from the placement math so the
    /// latter is pure and can be unit-tested without baking magic constants into two places.
    /// All values are in world (arena) units.
    /// </summary>
    public readonly struct CameraSettings
    {
        /// <summary>First-person eye height above the floor (cockpit canopy).</summary>
        public readonly float EyeHeight;
        /// <summary>First-person forward offset so the eye sits just ahead of the player's own wall.</summary>
        public readonly float NoseOffset;
        /// <summary>Third-person look-at height above the floor (roughly mid-wall).</summary>
        public readonly float TargetHeight;

        public CameraSettings(float eyeHeight, float noseOffset, float targetHeight)
        {
            EyeHeight = eyeHeight;
            NoseOffset = noseOffset;
            TargetHeight = targetHeight;
        }

        /// <summary>Sensible defaults for a ~177-unit arena with ~8-unit walls.</summary>
        public static CameraSettings Default => new CameraSettings(eyeHeight: 3f, noseOffset: 1.5f, targetHeight: 4f);
    }

    /// <summary>
    /// Pure placement of the 3D camera from the local cycle's state plus orbit parameters.
    /// No GPU, no MonoGame — produces a <see cref="CameraPose"/> the front-end feeds to
    /// <c>Matrix.CreateLookAt</c>. This is the whole "where does the camera go" decision, so it
    /// is fully unit-testable from cycle position/direction alone.
    /// </summary>
    public static class Camera3D
    {
        /// <summary>World up axis (+Y).</summary>
        public static readonly Vec3 WorldUp = new Vec3(0, 1, 0);

        /// <summary>
        /// Compute the camera pose for <paramref name="mode"/> given the local cycle's arena
        /// <paramref name="pos"/> and travel <paramref name="dir"/>. <paramref name="distance"/>,
        /// <paramref name="pitchRad"/> and <paramref name="yawRad"/> are the third-person orbit
        /// controls (ignored in first-person): distance from the cycle, elevation above the
        /// horizon, and rotation around the cycle relative to its heading (yaw 0 ⇒ directly
        /// behind). A near-zero direction is treated as +X so the pose is always finite.
        /// </summary>
        public static CameraPose Compute(CameraMode mode, Vec2 pos, Vec2 dir,
                                         float distance, float pitchRad, float yawRad,
                                         CameraSettings s)
        {
            Vec3 forward = Heading(dir);

            if (mode == CameraMode.FirstPerson)
            {
                Vec3 eye = Vec3.FromArena(pos, s.EyeHeight) + forward * s.NoseOffset;
                Vec3 target = eye + forward * 10f;
                return new CameraPose(eye, target, WorldUp);
            }

            // ThirdPerson (also the fallback for TopDown, which actually uses the 2D path).
            Vec3 tgt = Vec3.FromArena(pos, s.TargetHeight);

            // Orbit: rotate the heading about the vertical axis by yaw, then place the eye that
            // far back and pitched up. cos²+sin² keeps |eye-target| == distance for any pitch.
            Vec3 viewDir = RotateAboutY(forward, yawRad);
            float cp = MathF.Cos(pitchRad), sp = MathF.Sin(pitchRad);
            Vec3 eye3 = tgt + viewDir * (-distance * cp) + WorldUp * (distance * sp);
            return new CameraPose(eye3, tgt, WorldUp);
        }

        /// <summary>The cycle's heading as a flat (Y=0) unit world vector; +X for a null direction.</summary>
        public static Vec3 Heading(Vec2 dir)
        {
            if (MathF.Abs(dir.X) < 1e-5f && MathF.Abs(dir.Y) < 1e-5f)
                return new Vec3(1, 0, 0);
            return new Vec3(dir.X, 0, dir.Y).Normalized;
        }

        // Rotate a flat vector about the world up axis by `rad`. Identity at rad == 0.
        private static Vec3 RotateAboutY(Vec3 v, float rad)
        {
            float c = MathF.Cos(rad), s = MathF.Sin(rad);
            return new Vec3(v.X * c - v.Z * s, v.Y, v.X * s + v.Z * c).Normalized;
        }
    }
}
