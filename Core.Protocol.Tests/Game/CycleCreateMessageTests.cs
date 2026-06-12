using System;
using Armagetron.Protocol;
using Armagetron.Protocol.Game;
using Xunit;

namespace Armagetron.Protocol.Tests
{
    /// <summary>
    /// Tests for the S→C desc=320 gCycle creation decoder.
    ///
    /// PROTOCOL FINDING (2026-06-11): desc=320 is the gCycle nNOInitialisator
    /// descriptor. The server sends it when a new gCycle is created. The owner
    /// field (word[1]) equals the connection slot of the player who owns the
    /// cycle — for our bot that is always 1 (first remote client). Server-AI
    /// cycles have owner=0.
    ///
    /// Wire layout (body format derived from GPL source read 2026-06-11, spec
    /// only — no source was copied into the client):
    ///   [0]  cycleId         u16  (gCycle's netobj_id — also used in desc=321 word[11])
    ///   [1]  connectionSlot  u16  (owner: 0=server/AI, 1=first remote client, …)
    ///   [2]  playerNetObjId  u16  (ePlayerNetID netobj_id that owns this cycle)
    ///   [3+] further data         (color REALs, ReadSync payload — not decoded here)
    ///
    /// To identify OUR cycle: connectionSlot == _session.ConnectionId (typically 1).
    ///
    /// Test bodies are constructed from the known wire format; no PCAP needed.
    /// </summary>
    public class CycleCreateMessageTests
    {
        // desc=320 mid=1 4w body: cycle=100, slot=1 (us), player=50, autodelete=0
        const string OurCycle4w =
            "01400001000400640001003200000001";

        // desc=320 mid=2 4w body: cycle=101, slot=0 (server AI), player=9, autodelete=0
        const string AiCycle4w =
            "01400002000400650000000900000001";

        // desc=24 (wrong descriptor) with same 4w body as OurCycle4w
        const string WrongDescriptor =
            "00180001000400640001003200000001";

        // desc=320 mid=3 2w body: only 2 words — too short to decode player_id
        const string TooShort2w =
            "014000030002006400010001";

        private static NetMessage Msg(string hex) => StreamCodec.Parse(Hex.Decode(hex)).Messages[0];

        [Fact]
        public void DecodesOurCycle_ConnectionSlot1()
        {
            Assert.True(CycleCreateMessage.TryDecode(Msg(OurCycle4w), out var m));
            Assert.Equal(100, m.CycleId);
            Assert.Equal(1, m.ConnectionSlot);
            Assert.Equal(50, m.PlayerNetObjId);
        }

        [Fact]
        public void DecodesAiCycle_ConnectionSlot0()
        {
            Assert.True(CycleCreateMessage.TryDecode(Msg(AiCycle4w), out var m));
            Assert.Equal(101, m.CycleId);
            Assert.Equal(0, m.ConnectionSlot);
            Assert.Equal(9, m.PlayerNetObjId);
        }

        [Fact]
        public void RejectsWrongDescriptor()
        {
            Assert.False(CycleCreateMessage.TryDecode(Msg(WrongDescriptor), out _));
        }

        [Fact]
        public void RejectsShortBody()
        {
            Assert.False(CycleCreateMessage.TryDecode(Msg(TooShort2w), out _));
        }

        [Fact]
        public void RejectsEmptyBody()
        {
            var m = new NetMessage(320, 1, Array.Empty<byte>());
            Assert.False(CycleCreateMessage.TryDecode(m, out _));
        }
    }
}
