using System;
using Armagetron.Protocol;
using Armagetron.Protocol.Game;
using Xunit;

namespace Armagetron.Protocol.Tests
{
    /// <summary>
    /// Golden tests for the cycle turn command using two consecutive REAL
    /// commands captured from a 0.2.9.3.0 session (movement-only, PII-free),
    /// cross-checked for physical plausibility.
    /// </summary>
    public class CycleDestinationSyncTests
    {
        const string Cmd1 = "0141078f000f9fdc1d6891b019b0000005000000000025c61923000000977a40054700020001";
        const string Cmd2 = "01410797000f8b0e1da591ae19b00000000000000500fc2a199c00000097cf9a05cb00030001";

        private static CycleDestinationSync Cmd(string hex)
        {
            NetMessage m = StreamCodec.Parse(Hex.Decode(hex)).Messages[0];
            Assert.Equal(321, m.DescriptorId);
            return CycleDestinationSync.Decode(m);
        }

        private static void Near(float e, float a, float tol) =>
            Assert.True(Math.Abs(e - a) <= tol, $"{a} vs expected {e} (tol {tol})");

        [Fact]
        public void DecodesRealCommand()
        {
            var c = Cmd(Cmd1);
            Assert.Equal(151, c.CycleId);
            Assert.Equal(0, c.Flags);
            Assert.False(c.Braking);
            Assert.Equal(2, c.Turns);
            Near(1.0f, c.Direction.X, 1e-6f);   // exact unit axis
            Near(0.0f, c.Direction.Y, 1e-6f);
            Near(90.2f, c.Position.X, 0.5f);
            Near(54.1f, c.Position.Y, 0.5f);
            Near(36.4f, c.Distance, 0.5f);
            Near(1.28f, c.GameTime, 0.1f);
        }

        [Fact]
        public void EncodeReproducesCapturedBodyExactly()
        {
            // Decode a real command, re-encode it, and require byte-identical output.
            // (The custom-float encode is idempotent for values it produced, so a
            //  command captured from the real client must round-trip exactly.)
            NetMessage m = StreamCodec.Parse(Hex.Decode(Cmd1)).Messages[0];
            CycleDestinationSync c = CycleDestinationSync.Decode(m);
            Assert.Equal(Hex.Encode(m.Body), Hex.Encode(c.EncodeBody()));

            // And the full outgoing message frames back to the exact captured packet.
            var pkt = new Packet(new[] { c.ToMessage(m.MessageId) }, 0x0001);
            Assert.Equal(Cmd1, Hex.Encode(StreamCodec.Serialize(pkt)));
        }

        [Fact]
        public void ConsecutiveCommandsAreContinuous()
        {
            var a = Cmd(Cmd1); // at (~90,54), heading +x
            var b = Cmd(Cmd2); // the next turn

            Assert.Equal(a.CycleId, b.CycleId);
            Assert.Equal(a.Turns + 1, b.Turns);          // turn counter +1
            Assert.True(b.Distance > a.Distance);
            Assert.True(b.GameTime > a.GameTime);

            Assert.True(b.Position.X > a.Position.X + 5); // moved along +x
            Near(a.Position.Y, b.Position.Y, 1.0f);       // y held while heading +x

            Near(0.0f, b.Direction.X, 1e-6f);             // b announces new heading +y
            Near(1.0f, b.Direction.Y, 1e-6f);
        }
    }
}
