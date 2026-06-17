using Armagetron.Game.UI;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// The tiny text format a head's file store round-trips a remembered <see cref="ConnectChoice"/>
    /// through. Kept pure in Core.Protocol so the (de)serialization is unit-tested and the head's
    /// store is just file I/O around it.
    /// </summary>
    public class ConnectChoiceFormatTests
    {
        [Fact]
        public void RoundTrips_AChoice()
        {
            var c = new ConnectChoice("arena.example.net", 4534, "Vlad");
            Assert.True(ConnectChoiceFormat.TryParse(ConnectChoiceFormat.Serialize(c), out ConnectChoice r));
            Assert.Equal("arena.example.net", r.Host);
            Assert.Equal(4534, r.Port);
            Assert.Equal("Vlad", r.Name);
        }

        [Fact]
        public void Tolerates_CrLf_LineEndings()
        {
            // A file written on Windows (or edited by hand) may carry \r\n; parsing must not keep
            // the carriage return on the host, or the next connect would target "host\r".
            Assert.True(ConnectChoiceFormat.TryParse("h.example\r\n4534\r\nVlad", out ConnectChoice r));
            Assert.Equal("h.example", r.Host);
            Assert.Equal(4534, r.Port);
            Assert.Equal("Vlad", r.Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("only-one-line")]
        [InlineData("host\nnot-a-number\nname")]
        public void TryParse_Rejects_MalformedText(string? text)
        {
            Assert.False(ConnectChoiceFormat.TryParse(text, out _));
        }

        [Fact]
        public void TryParse_AllowsEmptyName()
        {
            // Name is optional in storage; an empty trailing line is a valid (if unusual) record.
            Assert.True(ConnectChoiceFormat.TryParse("h.example\n4534\n", out ConnectChoice r));
            Assert.Equal("", r.Name);
        }
    }
}
