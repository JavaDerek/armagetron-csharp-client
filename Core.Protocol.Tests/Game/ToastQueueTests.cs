using Armagetron.Game;
using Armagetron.Game.UI;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    public class ToastQueueTests
    {
        [Fact]
        public void Push_AddsActiveToast()
        {
            var q = new ToastQueue();
            q.Push("HI", RenderColor.White, nowMs: 0, ttlMs: 1_000);
            Assert.Equal(1, q.Count);
            Assert.Single(q.Active(500));
        }

        [Fact]
        public void Active_DropsExpired()
        {
            var q = new ToastQueue();
            q.Push("A", RenderColor.White, nowMs: 0, ttlMs: 1_000);
            q.Push("B", RenderColor.White, nowMs: 0, ttlMs: 5_000);
            var still = q.Active(2_000);   // A expired, B alive
            Assert.Single(still);
            Assert.Equal("B", still[0].Text);
            Assert.Equal(1, q.Count);
        }

        [Fact]
        public void Active_PreservesPushOrder()
        {
            var q = new ToastQueue();
            q.Push("FIRST", RenderColor.White, 0);
            q.Push("SECOND", RenderColor.White, 0);
            var a = q.Active(100);
            Assert.Equal("FIRST", a[0].Text);
            Assert.Equal("SECOND", a[1].Text);
        }
    }
}
