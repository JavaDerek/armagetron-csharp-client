using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests
{
    /// <summary>
    /// Tests for the engine-neutral <see cref="Vec3"/> — the basis for all 3D camera and
    /// world-geometry math. Pure value type, so every operation is asserted directly.
    /// </summary>
    public class Vec3Tests
    {
        [Fact]
        public void Add_Sub_Scale_ComponentWise()
        {
            var a = new Vec3(1, 2, 3);
            var b = new Vec3(10, 20, 30);

            Assert.Equal(new Vec3(11, 22, 33), a + b);
            Assert.Equal(new Vec3(9, 18, 27), b - a);
            Assert.Equal(new Vec3(2, 4, 6), a * 2f);
        }

        [Fact]
        public void Length_IsEuclidean()
        {
            Assert.Equal(5f, new Vec3(3, 4, 0).Length, 4);
            Assert.Equal(13f, new Vec3(0, 5, 12).Length, 4);
        }

        [Fact]
        public void Normalized_IsUnitLength()
        {
            Vec3 n = new Vec3(0, 0, 7).Normalized;
            Assert.Equal(1f, n.Length, 4);
            Assert.Equal(new Vec3(0, 0, 1), n);
        }

        [Fact]
        public void Normalized_Zero_StaysZero()
        {
            Vec3 n = new Vec3(0, 0, 0).Normalized;
            Assert.Equal(new Vec3(0, 0, 0), n);
        }

        [Fact]
        public void Dot_OfPerpendicular_IsZero_OfParallel_IsProduct()
        {
            Assert.Equal(0f, Vec3.Dot(new Vec3(1, 0, 0), new Vec3(0, 1, 0)), 4);
            Assert.Equal(6f, Vec3.Dot(new Vec3(0, 2, 0), new Vec3(0, 3, 0)), 4);
        }

        [Fact]
        public void Cross_OfXandY_IsZ_RightHanded()
        {
            Vec3 c = Vec3.Cross(new Vec3(1, 0, 0), new Vec3(0, 1, 0));
            Assert.Equal(new Vec3(0, 0, 1), c);
        }

        [Fact]
        public void FromArena_LiftsXtoX_YtoZ_AtGivenHeight()
        {
            Vec3 w = Vec3.FromArena(new Vec2(12, 34), 5f);
            Assert.Equal(12f, w.X, 4);
            Assert.Equal(5f, w.Y, 4);   // height on the up-axis
            Assert.Equal(34f, w.Z, 4);  // arena-Y becomes world-Z
        }

        [Fact]
        public void Equality_And_ToString()
        {
            Assert.True(new Vec3(1, 2, 3).Equals(new Vec3(1, 2, 3)));
            Assert.False(new Vec3(1, 2, 3).Equals(new Vec3(1, 2, 4)));
            Assert.Equal(new Vec3(1, 2, 3).GetHashCode(), new Vec3(1, 2, 3).GetHashCode());
            Assert.Equal("(1,2,3)", new Vec3(1, 2, 3).ToString());
            Assert.True(new Vec3(1, 2, 3).Equals((object)new Vec3(1, 2, 3)));
            Assert.False(new Vec3(1, 2, 3).Equals((object?)null));
        }
    }
}
