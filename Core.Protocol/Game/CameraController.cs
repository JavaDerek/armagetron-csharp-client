using System;
using Armagetron.Protocol;

namespace Armagetron.Game
{
    /// <summary>
    /// Holds the live camera state — current <see cref="CameraMode"/> plus the third-person
    /// orbit (distance / pitch / yaw) — and turns per-frame input deltas into a clamped
    /// <see cref="CameraPose"/> via <see cref="Camera3D"/>. Pure (no GPU, no input device): the
    /// front-end feeds it edge-detected key presses and mouse/scroll deltas, so the mode-cycling
    /// and clamping rules are unit-testable. Switching mode resets the orbit to its default so a
    /// fresh view is never stuck at a weird angle inherited from the last one.
    /// </summary>
    public sealed class CameraController
    {
        // The order the on-screen toggle cycles through.
        private static readonly CameraMode[] Order =
        {
            CameraMode.TopDown, CameraMode.ThirdPerson, CameraMode.FirstPerson,
        };

        // Orbit clamps (world units / radians). Pitch stays off the floor and out of straight-down.
        private const float MinDistance = 8f;
        private const float MaxDistance = 90f;
        private const float MinPitch = 0.12f;  // ~7°
        private const float MaxPitch = 1.45f;  // ~83°

        private readonly CameraSettings _settings;
        private readonly float _defaultDistance;
        private readonly float _defaultPitch;

        public CameraMode Mode { get; private set; } = CameraMode.TopDown;
        public float Distance { get; private set; }
        public float Pitch { get; private set; }
        public float Yaw { get; private set; }

        public CameraController(CameraSettings settings, float defaultDistance = 32f, float defaultPitch = 0.5f)
        {
            _settings = settings;
            _defaultDistance = Clamp(defaultDistance, MinDistance, MaxDistance);
            _defaultPitch = Clamp(defaultPitch, MinPitch, MaxPitch);
            ResetOrbit();
        }

        public CameraSettings Settings => _settings;

        /// <summary>True when the active mode is a 3D view (so the front-end uses the 3D render path).</summary>
        public bool Is3D => Mode != CameraMode.TopDown;

        /// <summary>Advance to the next mode in the toggle order (wraps), resetting the orbit.</summary>
        public void NextMode() => SetMode(Order[(Array.IndexOf(Order, Mode) + 1) % Order.Length]);

        /// <summary>Jump straight to a mode, resetting the orbit.</summary>
        public void SetMode(CameraMode mode)
        {
            Mode = mode;
            ResetOrbit();
        }

        /// <summary>Restore the default chase distance/pitch and zero the yaw (camera behind cycle).</summary>
        public void ResetOrbit()
        {
            Distance = _defaultDistance;
            Pitch = _defaultPitch;
            Yaw = 0f;
        }

        /// <summary>Apply a mouse-drag orbit delta: yaw wraps, pitch is clamped to a sane band.</summary>
        public void Orbit(float deltaYaw, float deltaPitch)
        {
            Yaw = WrapAngle(Yaw + deltaYaw);
            Pitch = Clamp(Pitch + deltaPitch, MinPitch, MaxPitch);
        }

        /// <summary>Apply a zoom delta (negative = closer); clamped to the distance band.</summary>
        public void Zoom(float delta) => Distance = Clamp(Distance + delta, MinDistance, MaxDistance);

        /// <summary>Build the pose for the local cycle at <paramref name="pos"/> heading <paramref name="dir"/>.</summary>
        public CameraPose Pose(Vec2 pos, Vec2 dir) =>
            Camera3D.Compute(Mode, pos, dir, Distance, Pitch, Yaw, _settings);

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        // Keep yaw in (-π, π] so it never drifts unbounded over a long session.
        private static float WrapAngle(float a)
        {
            const float twoPi = MathF.PI * 2f;
            a %= twoPi;
            if (a > MathF.PI) a -= twoPi;
            else if (a <= -MathF.PI) a += twoPi;
            return a;
        }
    }
}
