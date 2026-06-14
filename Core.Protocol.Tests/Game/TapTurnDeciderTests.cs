using Armagetron.Game;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for <see cref="TapTurnDecider"/> — the pure rule that maps an Android touch's
    /// x-coordinate to a turn direction. Tapping the LEFT half of the screen turns left;
    /// tapping the RIGHT half turns right. This mirrors the desktop ← / → keyboard model
    /// and is the only part of the touch input path that carries logic, so it is unit-tested
    /// here; the MonoGame TouchPanel polling that feeds it is an I/O edge (ExcludeFromCodeCoverage).
    /// </summary>
    public class TapTurnDeciderTests
    {
        [Theory]
        [InlineData(0f)]      // far left edge
        [InlineData(100f)]    // left of centre
        [InlineData(399f)]    // just left of the 800-wide midpoint
        public void Decide_LeftHalf_TurnsLeft(float tapX)
        {
            Assert.Equal(TurnDirection.Left, TapTurnDecider.Decide(tapX, screenWidth: 800f));
        }

        [Theory]
        [InlineData(400f)]    // exactly the midpoint counts as right
        [InlineData(401f)]    // just right of centre
        [InlineData(799f)]    // far right edge
        public void Decide_RightHalf_TurnsRight(float tapX)
        {
            Assert.Equal(TurnDirection.Right, TapTurnDecider.Decide(tapX, screenWidth: 800f));
        }

        [Fact]
        public void Decide_ScalesWithScreenWidth()
        {
            // A different width moves the dividing line: at width 200 the split is x=100.
            Assert.Equal(TurnDirection.Left,  TapTurnDecider.Decide(99f,  200f));
            Assert.Equal(TurnDirection.Right, TapTurnDecider.Decide(100f, 200f));
        }
    }
}
