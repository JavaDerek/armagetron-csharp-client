using System.Collections.Generic;
using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests
{
    /// <summary>
    /// Golden framing tests using REAL, PII-free packet bytes from a 0.2.9.3.0
    /// LAN capture. parse → serialize must reproduce the exact wire bytes.
    /// </summary>
    public class StreamCodecTests
    {
        public const string Ack = "000100000011007c007d007e007f008000810082008300840085008600ca008700cb00cc00cd00880001";
        public const string Keepalive = "00070000000100000000";
        public const string Config = "003c00b4000d0014494d5f4e4c505941545f4d495f454f544154004c000000000000";
        public const string Input = "0141078f000f9fdc1d6891b019b0000005000000000025c61923000000977a40054700020001";
        public const string Turn = "01370701000100320001";
        public const string Sync = "00180127000500061e3011a9f41e05fc0000";

        public static IEnumerable<object[]> All() => new[]
        {
            new object[] { Ack }, new object[] { Keepalive }, new object[] { Config },
            new object[] { Input }, new object[] { Turn }, new object[] { Sync },
        };

        [Theory]
        [MemberData(nameof(All))]
        public void RealPacketsRoundTripExactly(string hex)
        {
            byte[] wire = Hex.Decode(hex);
            Packet p = StreamCodec.Parse(wire);
            byte[] back = StreamCodec.Serialize(p);
            Assert.Equal(hex, Hex.Encode(back));
        }

        [Theory]
        [InlineData(Ack, 1, 0, 17, 0x0001)]
        [InlineData(Keepalive, 7, 0, 1, 0x0000)]
        [InlineData(Config, 60, 180, 13, 0x0000)]
        [InlineData(Input, 321, 1935, 15, 0x0001)]
        [InlineData(Turn, 311, 1793, 1, 0x0001)]
        [InlineData(Sync, 24, 295, 5, 0x0000)]
        public void DecodedHeaderFieldsMatchCapture(string hex, int desc, int mid, int words, int trailer)
        {
            Packet p = StreamCodec.Parse(Hex.Decode(hex));
            Assert.Single(p.Messages);
            NetMessage m = p.Messages[0];
            Assert.Equal(desc, m.DescriptorId);
            Assert.Equal(mid, m.MessageId);
            Assert.Equal(words, m.DataLengthWords);
            Assert.Equal(trailer, p.Trailer);
        }

        [Fact]
        public void AckBodyDecodesToMessageIdList()
        {
            NetMessage ack = StreamCodec.Parse(Hex.Decode(Ack)).Messages[0];
            MessageReader r = ack.Reader();
            Assert.Equal(0x7c, r.ReadUInt16());
            Assert.True(r.HasMore);
        }

        [Fact]
        public void MultipleMessagesConcatenateAndRoundTrip()
        {
            var a = new NetMessage(7, 0, new byte[] { 0, 0 });
            var b = new NetMessage(1, 5, new byte[] { 0x00, 0x7c, 0x00, 0x7d });
            var built = new Packet(new List<NetMessage> { a, b }, 0x1234);

            byte[] wire = StreamCodec.Serialize(built);
            Packet parsed = StreamCodec.Parse(wire);

            Assert.Equal(2, parsed.Messages.Count);
            Assert.Equal(7, parsed.Messages[0].DescriptorId);
            Assert.Equal(1, parsed.Messages[1].DescriptorId);
            Assert.Equal(0x1234, parsed.Trailer);
            Assert.Equal(Hex.Encode(wire), Hex.Encode(StreamCodec.Serialize(parsed)));
        }

        [Fact]
        public void MissingTrailerIsRejected()
        {
            byte[] bad = Hex.Decode("0001000000010000"); // 1-word body, no trailer
            var ex = Assert.Throws<System.ArgumentException>(() => StreamCodec.Parse(bad));
            Assert.Contains("trailer", ex.Message);
        }
    }
}
