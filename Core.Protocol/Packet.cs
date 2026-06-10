using System.Collections.Generic;

namespace Armagetron.Protocol
{
    /// <summary>
    /// A decoded UDP packet: a sequence of <see cref="NetMessage"/>s followed by
    /// a 2-byte trailer (the reliability-layer packet/ack id). Every packet in
    /// the reference capture ended with exactly this trailer.
    /// </summary>
    public sealed class Packet
    {
        public IReadOnlyList<NetMessage> Messages { get; }
        public int Trailer { get; }

        public Packet(IReadOnlyList<NetMessage> messages, int trailer)
        {
            Messages = messages;
            Trailer = trailer & 0xFFFF;
        }

        public override string ToString() =>
            $"Packet{{messages={Messages.Count}, trailer=0x{Trailer:x}}}";
    }
}
