using System;
using Armagetron.Game;
using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the stateful camera controller — mode cycling, orbit/zoom clamping, and the
    /// reset-on-mode-change rule. Pure state machine, so input deltas in → state out is asserted
    /// directly with no GPU or input device.
    /// </summary>
    public class CameraControllerTests
    {
        private static CameraController New() => new CameraController(CameraSettings.Default);

        [Fact]
        public void StartsTopDown_NotIn3D()
        {
            var c = New();
            Assert.Equal(CameraMode.TopDown, c.Mode);
            Assert.False(c.Is3D);
        }

        [Fact]
        public void NextMode_CyclesTopDown_Third_First_Wrap()
        {
            var c = New();
            c.NextMode(); Assert.Equal(CameraMode.ThirdPerson, c.Mode);
            Assert.True(c.Is3D);
            c.NextMode(); Assert.Equal(CameraMode.FirstPerson, c.Mode);
            c.NextMode(); Assert.Equal(CameraMode.TopDown, c.Mode);
        }

        [Fact]
        public void SetMode_JumpsDirectly()
        {
            var c = New();
            c.SetMode(CameraMode.FirstPerson);
            Assert.Equal(CameraMode.FirstPerson, c.Mode);
        }

        [Fact]
        public void Zoom_ClampsToDistanceBand()
        {
            var c = New();
            c.Zoom(-1000f);
            float min = c.Distance;
            c.Zoom(-50f);
            Assert.Equal(min, c.Distance); // already at the floor, no further

            c.Zoom(100000f);
            float max = c.Distance;
            c.Zoom(50f);
            Assert.Equal(max, c.Distance); // pinned at the ceiling

            Assert.True(max > min);
        }

        [Fact]
        public void Orbit_ClampsPitch_WrapsYaw()
        {
            var c = New();

            c.Orbit(0f, +100f);          // far past the top clamp
            float topPitch = c.Pitch;
            c.Orbit(0f, +10f);
            Assert.Equal(topPitch, c.Pitch); // pinned

            c.Orbit(0f, -100f);          // far past the bottom clamp
            float botPitch = c.Pitch;
            Assert.True(botPitch < topPitch);

            // Yaw wraps into (-π, π].
            c.Orbit(MathF.PI * 4f + 0.5f, 0f);
            Assert.InRange(c.Yaw, -MathF.PI, MathF.PI);
        }

        [Fact]
        public void ChangingMode_ResetsOrbitToDefault()
        {
            var c = New();
            c.SetMode(CameraMode.ThirdPerson);
            float defaultDist = c.Distance;
            float defaultPitch = c.Pitch;

            c.Zoom(20f);
            c.Orbit(0.7f, 0.3f);
            Assert.NotEqual(defaultDist, c.Distance);
            Assert.NotEqual(0f, c.Yaw);

            c.SetMode(CameraMode.FirstPerson); // any mode change resets
            Assert.Equal(defaultDist, c.Distance);
            Assert.Equal(defaultPitch, c.Pitch);
            Assert.Equal(0f, c.Yaw);
        }

        [Fact]
        public void Pose_ThirdPerson_ReflectsCurrentZoom()
        {
            var c = New();
            c.SetMode(CameraMode.ThirdPerson);
            CameraPose p1 = c.Pose(new Vec2(80, 80), new Vec2(1, 0));
            c.Zoom(20f);
            CameraPose p2 = c.Pose(new Vec2(80, 80), new Vec2(1, 0));
            Assert.True((p2.Eye - p2.Target).Length > (p1.Eye - p1.Target).Length);
        }

        [Fact]
        public void Constructor_ClampsOutOfRangeDefaults()
        {
            var c = new CameraController(CameraSettings.Default, defaultDistance: 5000f, defaultPitch: 99f);
            c.SetMode(CameraMode.ThirdPerson);
            // Distance/pitch were clamped into band at construction; pose stays finite & bounded.
            CameraPose p = c.Pose(new Vec2(80, 80), new Vec2(1, 0));
            Assert.True((p.Eye - p.Target).Length <= 90f + 0.01f);
            Assert.True(p.Eye.Y > p.Target.Y);
        }
    }
}
