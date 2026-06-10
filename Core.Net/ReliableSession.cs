using System.Collections.Generic;
using Armagetron.Protocol;

namespace Armagetron.Net
{
    /// <summary>
    /// The client side of Armagetron's reliable-UDP session, as reverse-engineered
    /// from a real 0.2.9.3.0 capture:
    /// <list type="bullet">
    ///   <item>Each side runs one monotonic message-id counter for <b>reliable</b>
    ///         messages; <b>Acks (descriptor 1) use message-id 0</b> and are never
    ///         acked themselves.</item>
    ///   <item>On receiving a reliable message (id != 0), the peer sends a desc-1
    ///         Ack listing that id.</item>
    ///   <item>Every packet ends with a 2-byte trailer. Observed: 0 during the
    ///         pre-login handshake, then a small constant once logged in — looks
    ///         like a connection/sender id. <b>[?] exact semantics confirmed live.</b></item>
    /// </list>
    /// This class is transport-agnostic (no socket) so it can be unit-tested.
    /// </summary>
    public sealed class ReliableSession
    {
        public const int AckDescriptor = 1;

        private int _nextReliableId;
        private int _connectionId; // packet trailer; [?] established at login
        private readonly List<int> _pendingAcks = new List<int>();

        public ReliableSession(int firstReliableId = 1, int connectionId = 0)
        {
            _nextReliableId = firstReliableId;
            _connectionId = connectionId & 0xFFFF;
        }

        /// <summary>The 2-byte packet trailer written on every outgoing packet.</summary>
        public int ConnectionId
        {
            get => _connectionId;
            set => _connectionId = value & 0xFFFF;
        }

        public int PendingAckCount => _pendingAcks.Count;

        /// <summary>Next monotonic id for a reliable message.</summary>
        public int NextReliableId() => _nextReliableId++;

        /// <summary>Stamp a payload as a reliable message (fresh monotonic id).</summary>
        public NetMessage Reliable(int descriptor, byte[] body) =>
            new NetMessage(descriptor, NextReliableId(), body);

        /// <summary>Note the reliable ids in a received packet so they can be acked.</summary>
        public void OnReceived(Packet packet)
        {
            foreach (var m in packet.Messages)
            {
                if (m.MessageId != 0)
                {
                    _pendingAcks.Add(m.MessageId);
                }
            }
        }

        /// <summary>Build a desc-1 Ack (message-id 0) for the given reliable ids.</summary>
        public static NetMessage BuildAck(IReadOnlyList<int> ids)
        {
            var w = new MessageWriter();
            foreach (var id in ids)
            {
                w.WriteUInt16(id);
            }
            return new NetMessage(AckDescriptor, 0, w.ToArray());
        }

        /// <summary>Assemble messages into a wire packet with the current trailer.</summary>
        public byte[] Assemble(IReadOnlyList<NetMessage> messages) =>
            StreamCodec.Serialize(new Packet(messages, _connectionId));

        /// <summary>
        /// If anything is awaiting acknowledgement, produce an Ack packet and clear
        /// the queue; otherwise null.
        /// </summary>
        public byte[]? DrainAckPacket()
        {
            if (_pendingAcks.Count == 0)
            {
                return null;
            }
            var ack = BuildAck(_pendingAcks);
            _pendingAcks.Clear();
            return Assemble(new[] { ack });
        }
    }
}
