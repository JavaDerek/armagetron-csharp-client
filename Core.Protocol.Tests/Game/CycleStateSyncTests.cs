using System;
using Armagetron.Protocol;
using Armagetron.Protocol.Game;
using Xunit;

namespace Armagetron.Protocol.Tests
{
    /// <summary>
    /// Golden tests for the S→C cycle position sync (desc=24, 27-word "full"
    /// variant), using bodies captured from a real 0.2.9.3.0 session (cycle
    /// netobj_id=30, movement-only, PII-free).
    ///
    /// Critically, these also pin the *discriminator*: desc=24 is the generic
    /// nNetObject sync, whose body layout depends on the synced object's class.
    /// The 14–22 word variants are player/team objects (they carry names), NOT
    /// cycle positions — decoding them as positions yields garbage. Only the 27w
    /// full cycle sync may seed a position.
    /// </summary>
    public class CycleStateSyncTests
    {
        // desc=24 mid=3 len=27 <body> trailer=0001 — cycle_id=30, heading +x.
        const string FullHeadingX =
            "00180003001b001edaba150b000005000000000067fc151f1f481525561415bc0001691e25f200000010000067fc151f1f4815250064ffff0000ffff0001";
        // same cycle, a later sync heading -y (a turn was made).
        const string FullHeadingNegY =
            "00180003001b001ed9001530000000000000070021381d5e1f481525b79615d20001fd62291b00000011000021381d5e1f481525004fffff0000ffff0001";
        // desc=24 mid=15 len=14 — an ePlayerNetID sync (body contains the name "Erin").
        // Must NOT be accepted as a cycle position sync.
        const string PlayerSync14w =
            "0018000f000e0083000f0007000000057245" + "6e6900000001000000040000000000000001";

        private static NetMessage Msg(string hex) => StreamCodec.Parse(Hex.Decode(hex)).Messages[0];

        private static void Near(float e, float a, float tol) =>
            Assert.True(Math.Abs(e - a) <= tol, $"{a} vs expected {e} (tol {tol})");

        [Fact]
        public void DecodesFullCycleSync_HeadingX()
        {
            Assert.True(CycleStateSync.TryDecodeFull(Msg(FullHeadingX), out var s));
            Assert.Equal(30, s.CycleId);
            Near(1.0f, s.Direction.X, 1e-6f);
            Near(0.0f, s.Direction.Y, 1e-6f);
            Near(17.963f, s.Position.X, 0.01f);
            Near(18.320f, s.Position.Y, 0.01f);
            Near(16.741f, s.GameTime, 0.01f);
        }

        [Fact]
        public void DecodesFullCycleSync_HeadingNegY()
        {
            Assert.True(CycleStateSync.TryDecodeFull(Msg(FullHeadingNegY), out var s));
            Assert.Equal(30, s.CycleId);
            Near(0.0f, s.Direction.X, 1e-6f);
            Near(-1.0f, s.Direction.Y, 1e-6f);
            Near(87.532f, s.Position.X, 0.01f);
            Near(18.320f, s.Position.Y, 0.01f);
            Near(19.053f, s.GameTime, 0.01f);
        }

        [Fact]
        public void RejectsPlayerSync_DoesNotSeedGarbagePosition()
        {
            // The 14-word desc=24 is a player object, not a cycle. The bug being
            // fixed: the bot used to decode any ≥10-word desc=24 as a position.
            NetMessage m = Msg(PlayerSync14w);
            Assert.Equal(24, m.DescriptorId);
            Assert.Equal(14, m.DataLengthWords);
            Assert.False(CycleStateSync.TryDecodeFull(m, out _));
        }

        [Fact]
        public void RejectsWrongDescriptor()
        {
            // A 27-word body under a non-24 descriptor is not a cycle sync.
            var body = Msg(FullHeadingX).Body;
            var notSync = new NetMessage(310, 3, body);
            Assert.False(CycleStateSync.TryDecodeFull(notSync, out _));
        }

        // ── Trailing state: speed (words [11-12]) and the alive flag (word [13]) ──
        // Decoded from the same real captures. Words [11-26] were reverse-engineered
        // live against 0.2.9.3.0 (192.168.68.61:4534, 2026-06-13); see PROTOCOL.md
        // "desc=24 27-word cycle position sync".

        [Fact]
        public void DecodesSpeed_FromTrailingWords()
        {
            Assert.True(CycleStateSync.TryDecodeFull(Msg(FullHeadingX), out var a));
            Near(27.771f, a.Speed, 0.01f);   // cruise speed, words [11-12]
            Assert.True(CycleStateSync.TryDecodeFull(Msg(FullHeadingNegY), out var b));
            Near(29.170f, b.Speed, 0.01f);
        }

        [Fact]
        public void AliveFlag_IsTrue_OnLivingCycleSyncs()
        {
            // word [13] = 1 in every sync from a living cycle (217/217 in the capture).
            Assert.True(CycleStateSync.TryDecodeFull(Msg(FullHeadingX), out var a));
            Assert.True(a.Alive);
            Assert.True(CycleStateSync.TryDecodeFull(Msg(FullHeadingNegY), out var b));
            Assert.True(b.Alive);
        }

        [Fact]
        public void AliveFlag_IsFalse_OnDeathSync()
        {
            // The death mechanic: the server sends one final 27w sync with word [13]=0,
            // then stops syncing the cycle. Speed does NOT drop (cycles crash at full
            // cruise), so the alive flag — not speed — is the death signal. We isolate
            // that single bit by flipping word [13] of a real living-cycle body to 0;
            // every other field stays genuine capture bytes.
            byte[] body = Msg(FullHeadingX).Body;
            const int aliveWord = 13;
            body[aliveWord * 2] = 0x00;     // big-endian high byte
            body[aliveWord * 2 + 1] = 0x00; // low byte → word = 0
            var dead = new NetMessage(CycleStateSync.Descriptor, 3, body);

            Assert.True(CycleStateSync.TryDecodeFull(dead, out var s));
            Assert.False(s.Alive);
            // Position/speed still decode normally on the death sync.
            Near(17.963f, s.Position.X, 0.01f);
            Near(27.771f, s.Speed, 0.01f);
        }
    }
}
