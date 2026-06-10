using System.IO;

namespace Armagetron.Protocol
{
    /// <summary>
    /// Produces a message body — the inverse of <see cref="MessageReader"/>.
    /// Big-endian 16-bit words.
    /// </summary>
    public sealed class MessageWriter
    {
        private readonly MemoryStream _out = new MemoryStream();

        public MessageWriter WriteUInt16(int v)
        {
            _out.WriteByte((byte)((v >> 8) & 0xFF));
            _out.WriteByte((byte)(v & 0xFF));
            return this;
        }

        /// <summary>Write an Armagetron REAL; see <see cref="MessageReader.ReadReal"/>.</summary>
        public MessageWriter WriteReal(float xf)
        {
            double y = xf;
            int sign = 0;
            if (y < 0)
            {
                y = -y;
                sign = 1;
            }
            int exp = 0;
            while (y >= 64 && exp < (1 << 6) - 6) { exp += 6; y /= 64; }
            while (y >= 1 && exp < (1 << 6) - 1) { exp++; y /= 2; }
            int mant = (int)(y * (1 << 25));
            if (mant > (1 << 25) - 1)
            {
                mant = (1 << 25) - 1;
            }
            int trans = (mant & ((1 << 25) - 1)) | (sign << 25) | (exp << 26);
            WriteUInt16(trans & 0xFFFF);          // low word first
            WriteUInt16((trans >> 16) & 0xFFFF);  // then high word
            return this;
        }

        /// <summary>Write a length-prefixed string; see <see cref="MessageReader.ReadString"/>.</summary>
        public MessageWriter WriteString(string s)
        {
            int len = s.Length + 1; // include trailing NUL
            WriteUInt16(len);
            int words = (len + 1) / 2;
            for (int w = 0; w < words; w++)
            {
                int lo = (2 * w < s.Length) ? (s[2 * w] & 0xFF) : 0;         // char, NUL, or pad
                int hi = (2 * w + 1 < s.Length) ? (s[2 * w + 1] & 0xFF) : 0; // char, NUL, or pad
                WriteUInt16((hi << 8) | lo);
            }
            return this;
        }

        public byte[] ToArray() => _out.ToArray();
    }
}
