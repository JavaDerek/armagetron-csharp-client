using System;

namespace Armagetron.Protocol
{
    /// <summary>
    /// One legacy Armagetron nMessage on the wire:
    /// [descriptorId:u16][messageId:u16][dataLength:u16 words][body].
    /// </summary>
    public sealed class NetMessage
    {
        public int DescriptorId { get; }
        public int MessageId { get; }
        private readonly byte[] _body;

        public NetMessage(int descriptorId, int messageId, byte[] body)
        {
            if ((body.Length & 1) != 0)
            {
                throw new ArgumentException("body must be word-aligned (even length), was " + body.Length, nameof(body));
            }
            DescriptorId = descriptorId & 0xFFFF;
            MessageId = messageId & 0xFFFF;
            _body = (byte[])body.Clone();
        }

        public int DataLengthWords => _body.Length / 2;
        public byte[] Body => (byte[])_body.Clone();
        public MessageReader Reader() => new MessageReader(_body);

        public override string ToString() =>
            $"NetMessage{{desc={DescriptorId}, mid={MessageId}, {DataLengthWords}w}}";
    }
}
