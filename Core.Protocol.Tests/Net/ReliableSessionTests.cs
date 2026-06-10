using Armagetron.Net;
using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests
{
    /// <summary>
    /// Offline tests for the session logic that the bot (and the eventual client)
    /// will drive. Encodes the sequencing/ack rules observed in the capture; the
    /// trailer/login specifics are confirmed live (see Core.Net/NETPLAN.md).
    /// </summary>
    public class ReliableSessionTests
    {
        [Fact]
        public void ReliableIdsAreMonotonic()
        {
            var s = new ReliableSession(firstReliableId: 100);
            Assert.Equal(100, s.NextReliableId());
            Assert.Equal(101, s.NextReliableId());
            NetMessage m = s.Reliable(11, new byte[] { 0, 0 });
            Assert.Equal(11, m.DescriptorId);
            Assert.Equal(102, m.MessageId);
        }

        [Fact]
        public void AckHasMessageIdZeroAndListsTheIds()
        {
            NetMessage ack = ReliableSession.BuildAck(new[] { 1746 });
            Assert.Equal(1, ack.DescriptorId);
            Assert.Equal(0, ack.MessageId);        // acks are unreliable
            Assert.Equal("06d2", Hex.Encode(ack.Body)); // 1746 = 0x06d2
        }

        [Fact]
        public void OnReceivedQueuesReliableIdsThenDrainAcksThem()
        {
            var s = new ReliableSession(connectionId: 1);
            var received = new Packet(new[]
            {
                new NetMessage(60, 1746, new byte[] { 0, 0 }), // reliable -> must ack
                new NetMessage(1, 0, new byte[0]),             // an Ack -> never acked
            }, 1);

            s.OnReceived(received);
            Assert.Equal(1, s.PendingAckCount);

            byte[]? ackBytes = s.DrainAckPacket();
            Assert.NotNull(ackBytes);
            Packet p = StreamCodec.Parse(ackBytes!);
            Assert.Equal(1, p.Trailer);                  // connection-id trailer
            Assert.Single(p.Messages);
            Assert.Equal(1, p.Messages[0].DescriptorId);
            Assert.Equal(0, p.Messages[0].MessageId);
            Assert.Equal("06d2", Hex.Encode(p.Messages[0].Body));

            Assert.Equal(0, s.PendingAckCount);          // cleared
            Assert.Null(s.DrainAckPacket());             // nothing pending now
        }

        [Fact]
        public void AssembleUsesConnectionIdAsTrailer()
        {
            var s = new ReliableSession(connectionId: 0x1234);
            byte[] bytes = s.Assemble(new[] { new NetMessage(7, 0, new byte[] { 0, 0 }) });
            Assert.Equal(0x1234, StreamCodec.Parse(bytes).Trailer);
        }
    }
}
