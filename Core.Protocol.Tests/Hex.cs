using System;
using System.Text;

namespace Armagetron.Protocol.Tests
{
    internal static class Hex
    {
        public static byte[] Decode(string s)
        {
            s = s.Replace(" ", "");
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = Convert.ToByte(s.Substring(2 * i, 2), 16);
            }
            return b;
        }

        public static string Encode(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (var x in b)
            {
                sb.Append(x.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
