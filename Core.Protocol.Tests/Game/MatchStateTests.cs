using Armagetron.Game.UI;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    public class MatchStateTests
    {
        [Fact]
        public void NewState_NoRound_ElapsedZero_LocalAlive()
        {
            var m = new MatchState();
            Assert.Equal(0, m.RoundNumber);
            Assert.False(m.RoundActive);
            Assert.True(m.LocalAlive);
            Assert.Equal(0, m.ElapsedMs(10_000));
            Assert.Equal("0:00", m.TimeLabel(10_000));
        }

        [Fact]
        public void OnRoundStart_IncrementsRound_SetsActiveAndAlive()
        {
            var m = new MatchState();
            m.OnLocalDied();
            m.OnRoundStart(1_000);
            Assert.Equal(1, m.RoundNumber);
            Assert.True(m.RoundActive);
            Assert.True(m.LocalAlive);          // reset on new round
            m.OnRoundStart(5_000);
            Assert.Equal(2, m.RoundNumber);
        }

        [Fact]
        public void ElapsedMs_CountsFromRoundStart_AndClampsNonNegative()
        {
            var m = new MatchState();
            m.OnRoundStart(1_000);
            Assert.Equal(2_000, m.ElapsedMs(3_000));
            Assert.Equal(0, m.ElapsedMs(500)); // clock skew → clamp to 0
        }

        [Fact]
        public void TimeLabel_FormatsMinutesAndZeroPaddedSeconds()
        {
            var m = new MatchState();
            m.OnRoundStart(0);
            Assert.Equal("0:05", m.TimeLabel(5_000));
            Assert.Equal("1:05", m.TimeLabel(65_000));
            Assert.Equal("2:00", m.TimeLabel(120_000));
        }

        [Fact]
        public void OnRoundEnd_StopsTimer()
        {
            var m = new MatchState();
            m.OnRoundStart(0);
            m.OnRoundEnd();
            Assert.False(m.RoundActive);
            Assert.Equal(0, m.ElapsedMs(9_000));
        }

        [Fact]
        public void OnLocalDied_ClearsAlive_SetCycleCount()
        {
            var m = new MatchState();
            m.OnRoundStart(0);
            m.OnLocalDied();
            Assert.False(m.LocalAlive);
            m.SetCycleCount(4);
            Assert.Equal(4, m.CycleCount);
        }
    }
}
