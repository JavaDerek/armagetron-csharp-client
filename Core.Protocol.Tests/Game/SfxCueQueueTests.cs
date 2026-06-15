using Armagetron.Game.UI;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// The pure one-shot SFX cue queue: the shell pushes <see cref="SfxId"/> cues at game
    /// moments and the host drains them once per frame to play them. Drain must be
    /// exactly-once (no replay) and FIFO, so a sound fires the frame its trigger happened
    /// and never again.
    /// </summary>
    public class SfxCueQueueTests
    {
        [Fact]
        public void Drain_OnEmptyQueue_ReturnsNothing()
        {
            var q = new SfxCueQueue();
            Assert.Empty(q.Drain());
            Assert.Equal(0, q.Count);
        }

        [Fact]
        public void Drain_ReturnsPushedCues_InOrder()
        {
            var q = new SfxCueQueue();
            q.Push(SfxId.UiTap);
            q.Push(SfxId.Turn);
            q.Push(SfxId.Explosion);

            Assert.Equal(3, q.Count);
            Assert.Equal(new[] { SfxId.UiTap, SfxId.Turn, SfxId.Explosion }, q.Drain());
        }

        [Fact]
        public void Drain_ClearsTheQueue_NoReplayNextFrame()
        {
            var q = new SfxCueQueue();
            q.Push(SfxId.Go);
            Assert.Single(q.Drain());

            // Second drain (next frame) must be empty — a cue plays once, not every frame.
            Assert.Empty(q.Drain());
            Assert.Equal(0, q.Count);
        }
    }
}
