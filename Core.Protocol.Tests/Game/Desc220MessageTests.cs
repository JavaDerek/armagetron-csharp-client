using System;
using Armagetron.Protocol;
using Armagetron.Protocol.Game;
using Xunit;

namespace Armagetron.Protocol.Tests
{
    /// <summary>
    /// Tests for the S→C desc=220 object-create parser.
    ///
    /// PROTOCOL FINDING (2026-06-11): desc=220 carrying a player name is an
    /// ePlayerNetID object create/sync — NOT a gCycle. Our gCycle id comes
    /// from desc=310 (CycleAlive). This distinction is the root-cause fix for
    /// _myCycleId being set to the player object id instead of the gCycle id.
    ///
    /// Wire layout (15w form observed live, carrying player name 'AaBot'):
    ///   [0]     netObjId  u16
    ///   [1-4]   unknown        (color, team, or other fields)
    ///   [5]     name len  u16
    ///   [6+]    name data      (AA byte-swapped, word-padded)
    ///   [?+]    unknown        (additional fields past the name)
    ///
    /// Test bodies are constructed from the wire format; no PCAP file needed.
    /// </summary>
    public class Desc220MessageTests
    {
        // desc=220 mid=1 9w body:
        //   word[0] = 248 (netobj_id = 0x00F8)
        //   words[1-4] = 0,0,0,0
        //   word[5] = 6  (string len = 5 chars + NUL)
        //   word[6] = 0x6141  ('A'=0x41 lo, 'a'=0x61 hi)
        //   word[7] = 0x6F42  ('B'=0x42 lo, 'o'=0x6F hi)
        //   word[8] = 0x0074  ('t'=0x74 lo, NUL=0x00 hi)
        // trailer = 1
        const string PlayerCreate9w =
            "00dc0001000900f80000000000000000000661416f4200740001";

        // desc=220 mid=2 3w body: netobj_id=248 only (too short for name)
        const string ShortBody3w =
            "00dc0002000300f800000000" + "0001";

        // desc=220 mid=3 6w body: netobj_id=248, 4 zeros, string len=0 (empty name)
        const string SixWordEmptyName =
            "00dc0003000600f8000000000000000000000001";

        // desc=310 (wrong descriptor) with same 9w body as PlayerCreate9w
        const string WrongDescriptor310 =
            "01360001000900f80000000000000000000661416f4200740001";

        private static NetMessage Msg(string hex) => StreamCodec.Parse(Hex.Decode(hex)).Messages[0];

        [Fact]
        public void DecodesPlayerCreate_ExtractsNetObjIdAndName()
        {
            Assert.True(Desc220Message.TryDecode(Msg(PlayerCreate9w), out var m));
            Assert.Equal(248, m.NetObjId);
            Assert.Equal("AaBot", m.Name);
            Assert.True(m.HasName);
        }

        [Fact]
        public void DecodesShortBody_NetObjIdOnly_NoName()
        {
            Assert.True(Desc220Message.TryDecode(Msg(ShortBody3w), out var m));
            Assert.Equal(248, m.NetObjId);
            Assert.Null(m.Name);
            Assert.False(m.HasName);
        }

        [Fact]
        public void DecodesEmptyNameString_TreatsAsNoName()
        {
            Assert.True(Desc220Message.TryDecode(Msg(SixWordEmptyName), out var m));
            Assert.Equal(248, m.NetObjId);
            Assert.False(m.HasName);
        }

        [Fact]
        public void RejectsWrongDescriptor()
        {
            Assert.False(Desc220Message.TryDecode(Msg(WrongDescriptor310), out _));
        }

        [Fact]
        public void RejectsEmptyBody()
        {
            var m = new NetMessage(220, 1, Array.Empty<byte>());
            Assert.False(Desc220Message.TryDecode(m, out _));
        }
    }
}
