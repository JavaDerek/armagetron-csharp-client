using System;
using System.Text.RegularExpressions;
using Armagetron.Protocol;
using Xunit;

namespace Armagetron.Protocol.Tests
{
    public class StringCodecTests
    {
        [Fact]
        public void CameraForbidFree_MatchesWireBytesExactly()
        {
            byte[] enc = new MessageWriter().WriteString("CAMERA_FORBID_FREE").ToArray();
            Assert.Equal("00134143454d4152465f524f49425f44524645450000", Hex.Encode(enc));
            Assert.Equal("CAMERA_FORBID_FREE", new MessageReader(enc).ReadString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("BUG_RIP")]
        [InlineData("ARENA_AXES")]
        [InlineData("ALLOW_CONTROL_DURING_CHAT")]
        [InlineData("CAMERA_FORBID_CUSTOM_GLANCE")]
        public void RoundTrips(string s)
        {
            byte[] enc = new MessageWriter().WriteString(s).ToArray();
            int len = ((enc[0] & 0xFF) << 8) | (enc[1] & 0xFF);
            Assert.Equal(s.Length + 1, len);              // length counts the NUL
            Assert.Equal(0, (enc.Length - 2) % 2);        // body is word-aligned
            Assert.Equal(s, new MessageReader(enc).ReadString());
        }

        [Fact]
        public void DecodesRealConfigCvarNameFromCapture()
        {
            NetMessage cfg = StreamCodec.Parse(Hex.Decode(StreamCodecTests.Config)).Messages[0];
            string cvar = cfg.Reader().ReadString();
            Assert.Matches(new Regex("^[A-Z][A-Z0-9_]+$"), cvar);
            byte[] re = new MessageWriter().WriteString(cvar).ToArray();
            Assert.Equal(cvar, new MessageReader(re).ReadString());
        }
    }

    public class RealCodecTests
    {
        private static float Read(string hex) => new MessageReader(Hex.Decode(hex)).ReadReal();
        private static void Near(float expected, float actual, float tol) =>
            Assert.True(Math.Abs(expected - actual) <= tol, $"{actual} vs expected {expected} (tol {tol})");

        [Fact]
        public void KnownVectors()
        {
            Near(0.0f, Read("00000000"), 0f);
            Near(1.0f, Read("00000500"), 1e-6f);
            Near(-1.0f, Read("00000700"), 1e-6f);
            Near(64.0f, Read("00001d00"), 1e-4f);
        }

        [Theory]
        [InlineData(0f)] [InlineData(1f)] [InlineData(-1f)] [InlineData(0.5f)]
        [InlineData(64f)] [InlineData(90.2f)] [InlineData(-53.9f)] [InlineData(123.4f)]
        [InlineData(0.333f)] [InlineData(1000f)]
        public void RoundTrips(float x)
        {
            byte[] enc = new MessageWriter().WriteReal(x).ToArray();
            Assert.Equal(4, enc.Length); // a REAL is two words
            float back = new MessageReader(enc).ReadReal();
            float tol = Math.Max(1e-3f, Math.Abs(x) * 1e-3f);
            Near(x, back, tol);
        }
    }
}
