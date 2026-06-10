using System;
using System.Text;

namespace Armagetron.Protocol
{
    /// <summary>
    /// Sequential reader over a message body — a run of big-endian 16-bit words.
    /// Decodes the Armagetron legacy stream primitives (verified against a real
    /// 0.2.9.3.0 capture).
    /// </summary>
    public sealed class MessageReader
    {
        private readonly byte[] _b;
        private int _pos;

        public MessageReader(byte[] body)
        {
            _b = body;
        }

        public int RemainingBytes => _b.Length - _pos;
        public bool HasMore => _pos < _b.Length;

        public int ReadUInt16()
        {
            if (_pos + 2 > _b.Length)
            {
                throw new InvalidOperationException("ReadUInt16 past end of body");
            }
            int v = ((_b[_pos] & 0xFF) << 8) | (_b[_pos + 1] & 0xFF);
            _pos += 2;
            return v;
        }

        /// <summary>
        /// Read an Armagetron REAL (custom float from nStreamMessage.cpp): a
        /// 32-bit int written low-word-first, where
        /// trans = mantissa(25b) | sign &lt;&lt; 25 | exp &lt;&lt; 26 and the value is
        /// (mantissa / 2^25) * 2^exp (negated if the sign bit is set).
        /// e.g. 1.0 = 0x05000000 = wire bytes 00 00 05 00.
        /// </summary>
        public float ReadReal()
        {
            int low = ReadUInt16();
            int high = ReadUInt16();
            int trans = (low & 0xFFFF) | (high << 16);
            int mant = trans & ((1 << 25) - 1);
            int sign = (trans >> 25) & 1;
            int exp = (trans >> 26) & 0x3F;
            double x = mant / (double)(1 << 25);
            if (sign == 1)
            {
                x = -x;
            }
            return (float)(x * Math.Pow(2.0, exp)); // ScaleB isn't in netstandard2.1
        }

        /// <summary>
        /// Read a length-prefixed string: a u16 length (= chars + 1 for the NUL),
        /// then ceil(length/2) words, each packing two chars as
        /// (char[i] | char[i+1] &lt;&lt; 8) stored big-endian (so bytes look swapped),
        /// word-padded. Latin-1.
        /// </summary>
        public string ReadString()
        {
            int len = ReadUInt16();
            if (len == 0)
            {
                return string.Empty;
            }
            int words = (len + 1) / 2;
            byte[] chars = new byte[words * 2];
            for (int w = 0; w < words; w++)
            {
                int word = ReadUInt16();
                chars[2 * w] = (byte)(word & 0xFF);            // low byte  = first char
                chars[2 * w + 1] = (byte)((word >> 8) & 0xFF); // high byte = second char
            }
            // Content is the first (len-1) chars; chars[len-1] is the NUL.
            // Build Latin-1 directly (Encoding.Latin1 isn't in netstandard2.1).
            var sb = new StringBuilder(len - 1);
            for (int i = 0; i < len - 1; i++)
            {
                sb.Append((char)(chars[i] & 0xFF));
            }
            return sb.ToString();
        }
    }
}
