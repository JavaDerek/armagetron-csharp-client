using System;
using Armagetron.Game;
using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the pure 3D camera placement — the "where does the eye go" decision behind the
    /// new third-person/first-person views. Asserts geometric relationships (above/behind, look
    /// direction, distance) rather than exact pixels, so the math is pinned without a GPU.
    /// </summary>
    public class Camera3DTests
    {
        private static readonly CameraSettings S = CameraSettings.Default;

        // A cycle at the arena middle heading along +X (arena-east).
        private static readonly Vec2 Pos = new Vec2(80, 90);
        private static readonly Vec2 DirEast = new Vec2(1, 0);

        // Flat (floor-plane) component of a world vector.
        private static Vec3 Flat(Vec3 v) => new Vec3(v.X, 0, v.Z);

        [Fact]
        public void Heading_NullDirection_FallsBackToEastUnit()
        {
            Vec3 h = Camera3D.Heading(new Vec2(0, 0));
            Assert.Equal(new Vec3(1, 0, 0), h);
        }

        [Fact]
        public void Heading_IsFlatAndUnitLength()
        {
            Vec3 h = Camera3D.Heading(new Vec2(3, 4));
            Assert.Equal(0f, h.Y, 5);
            Assert.Equal(1f, h.Length, 4);
        }

        [Fact]
        public void FirstPerson_EyeAtCockpitHeight_LooksAlongHeading()
        {
            CameraPose p = Camera3D.Compute(CameraMode.FirstPerson, Pos, DirEast, 0, 0, 0, S);

            Assert.Equal(S.EyeHeight, p.Eye.Y, 4);                 // at canopy height
            Assert.Equal(Pos.X + S.NoseOffset, p.Eye.X, 4);       // nudged forward past own wall
            Assert.Equal(Pos.Y, p.Eye.Z, 4);                       // arena-Y on world-Z
            // Looks along the heading (+X).
            Assert.True(Vec3.Dot(p.Forward, new Vec3(1, 0, 0)) > 0.99f);
        }

        [Fact]
        public void FirstPerson_ZeroDirection_StillProducesFiniteForward()
        {
            CameraPose p = Camera3D.Compute(CameraMode.FirstPerson, Pos, new Vec2(0, 0), 0, 0, 0, S);
            Assert.Equal(1f, p.Forward.Length, 4);
        }

        [Fact]
        public void ThirdPerson_EyeIsAboveAndBehindTheCycle()
        {
            CameraPose p = Camera3D.Compute(CameraMode.ThirdPerson, Pos, DirEast,
                                            distance: 30f, pitchRad: 0.5f, yawRad: 0f, S);

            // Target sits over the cycle at the configured height.
            Assert.Equal(Pos.X, p.Target.X, 4);
            Assert.Equal(S.TargetHeight, p.Target.Y, 4);
            Assert.Equal(Pos.Y, p.Target.Z, 4);

            // Above the look-at point...
            Assert.True(p.Eye.Y > p.Target.Y);
            // ...and behind it: the horizontal eye→target offset points opposite the heading.
            Vec3 behind = Flat(p.Eye - p.Target).Normalized;
            Assert.True(Vec3.Dot(behind, new Vec3(1, 0, 0)) < -0.5f);
        }

        [Fact]
        public void ThirdPerson_DistanceEqualsEyeToTargetMagnitude()
        {
            CameraPose p = Camera3D.Compute(CameraMode.ThirdPerson, Pos, DirEast,
                                            distance: 42f, pitchRad: 0.6f, yawRad: 0.3f, S);
            Assert.Equal(42f, (p.Eye - p.Target).Length, 3);
        }

        [Fact]
        public void ThirdPerson_LargerDistance_MovesEyeFartherOut()
        {
            CameraPose near = Camera3D.Compute(CameraMode.ThirdPerson, Pos, DirEast, 20f, 0.5f, 0f, S);
            CameraPose far = Camera3D.Compute(CameraMode.ThirdPerson, Pos, DirEast, 50f, 0.5f, 0f, S);
            Assert.True((far.Eye - far.Target).Length > (near.Eye - near.Target).Length);
        }

        [Fact]
        public void ThirdPerson_HigherPitch_RaisesTheEye()
        {
            CameraPose low = Camera3D.Compute(CameraMode.ThirdPerson, Pos, DirEast, 30f, 0.2f, 0f, S);
            CameraPose high = Camera3D.Compute(CameraMode.ThirdPerson, Pos, DirEast, 30f, 1.0f, 0f, S);
            Assert.True(high.Eye.Y > low.Eye.Y);
        }

        [Fact]
        public void ThirdPerson_YawHalfTurn_SwingsCameraInFrontOfTheCycle()
        {
            CameraPose behind = Camera3D.Compute(CameraMode.ThirdPerson, Pos, DirEast, 30f, 0.4f, 0f, S);
            CameraPose front = Camera3D.Compute(CameraMode.ThirdPerson, Pos, DirEast, 30f, 0.4f, MathF.PI, S);

            Vec3 behindOff = Flat(behind.Eye - behind.Target).Normalized;
            Vec3 frontOff = Flat(front.Eye - front.Target).Normalized;

            Assert.True(Vec3.Dot(behindOff, new Vec3(1, 0, 0)) < -0.5f); // default: behind
            Assert.True(Vec3.Dot(frontOff, new Vec3(1, 0, 0)) > 0.5f);   // half turn: in front
        }

        [Fact]
        public void ThirdPerson_AlwaysLooksAtTheCycle()
        {
            CameraPose p = Camera3D.Compute(CameraMode.ThirdPerson, Pos, new Vec2(0, 1), 25f, 0.5f, 1.1f, S);
            // Forward points from eye toward target (the cycle), regardless of orbit.
            Vec3 toTarget = (p.Target - p.Eye).Normalized;
            Assert.True(Vec3.Dot(p.Forward, toTarget) > 0.999f);
        }
    }
}
