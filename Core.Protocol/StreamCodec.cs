using System;
using System.Collections.Generic;

namespace Armagetron.Protocol
{
    /// <summary>
    /// Legacy Armagetron nMessage stream framing — the format observed on the
    /// wire between 0.2.9.3.0 peers (verified against a real LAN capture).
    /// Big-endian 16-bit words.
    /// <code>
    ///   UDP payload = message+  followed by a 2-byte packet trailer
    ///   message     = [descriptorId:u16][messageId:u16][dataLength:u16 words][body]
    /// </code>
    /// Field order is descriptor-first (proven: descriptor stays constant while
    /// messageId increments across a run of same-type messages).
    /// </summary>
    public static class StreamCodec
    {
        public static Packet Parse(byte[] data)
        {
            var msgs = new List<NetMessage>();
            int off = 0;
            while (data.Length - off >= 6)
            {
                int desc = U16(data, off);
                int mid = U16(data, off + 2);
                int words = U16(data, off + 4);
                int bodyBytes = words * 2;
                if (off + 6 + bodyBytes > data.Length)
                {
                    break; // remaining bytes are the trailer, not a full message
                }
                var body = new byte[bodyBytes];
                Array.Copy(data, off + 6, body, 0, bodyBytes);
                msgs.Add(new NetMessage(desc, mid, body));
                off += 6 + bodyBytes;
            }
            int leftover = data.Length - off;
            if (leftover != 2)
            {
                throw new ArgumentException(
                    "malformed packet: expected a 2-byte trailer, found " + leftover + " leftover bytes");
            }
            return new Packet(msgs, U16(data, off));
        }

        public static byte[] Serialize(Packet p)
        {
            int size = 2; // trailer
            foreach (var m in p.Messages)
            {
                size += 6 + m.DataLengthWords * 2;
            }
            var outBytes = new byte[size];
            int off = 0;
            foreach (var m in p.Messages)
            {
                PutU16(outBytes, off, m.DescriptorId);
                PutU16(outBytes, off + 2, m.MessageId);
                PutU16(outBytes, off + 4, m.DataLengthWords);
                byte[] body = m.Body;
                Array.Copy(body, 0, outBytes, off + 6, body.Length);
                off += 6 + body.Length;
            }
            PutU16(outBytes, off, p.Trailer);
            return outBytes;
        }

        private static int U16(byte[] b, int off) => ((b[off] & 0xFF) << 8) | (b[off + 1] & 0xFF);

        private static void PutU16(byte[] b, int off, int v)
        {
            b[off] = (byte)((v >> 8) & 0xFF);
            b[off + 1] = (byte)(v & 0xFF);
        }
    }
}
