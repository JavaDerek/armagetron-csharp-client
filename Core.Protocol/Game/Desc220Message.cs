namespace Armagetron.Protocol.Game
{
    /// <summary>
    /// S→C desc=220: create or sync a game object (netobj).
    ///
    /// PROTOCOL FINDING (2026-06-11): A desc=220 that carries a player name at
    /// word[5+] is an <em>ePlayerNetID</em> (player object), NOT a gCycle.
    /// Callers must NOT use the netobj_id from such a message as the cycle id —
    /// the gCycle id comes exclusively from desc=310 (CycleAlive).
    ///
    /// Wire layout (15w form observed live against 0.2.9.3.0, carrying name 'AaBot'):
    /// <code>
    ///   [0]     netObjId  u16   (server-assigned netobj_id for this object)
    ///   [1-4]   ?               (unknown: color, team, rubber, etc.)
    ///   [5]     name len  u16   (AA string length field, includes NUL)
    ///   [6+]    name data       (AA byte-swapped, word-padded Latin-1 string)
    ///   [?+]    ?               (further fields not yet decoded)
    /// </code>
    ///
    /// When the body has fewer than 6 words the name is absent and
    /// <see cref="HasName"/> is false. An explicit empty string (len=0) is also
    /// treated as absent.
    /// </summary>
    public sealed class Desc220Message
    {
        /// <summary>Descriptor id for object create/sync messages.</summary>
        public const int Descriptor = 220;

        /// <summary>Server-assigned netobj_id for this object (word[0]).</summary>
        public int NetObjId { get; }

        /// <summary>
        /// Player name if the body carries one; null if the body is too short or
        /// the name string is empty.
        /// </summary>
        public string? Name { get; }

        /// <summary>True when the message carries a non-empty player name.</summary>
        public bool HasName => Name != null;

        private Desc220Message(int netObjId, string? name)
        {
            NetObjId = netObjId;
            Name = name;
        }

        /// <summary>
        /// Decode a desc=220 message. Returns false if the descriptor is not 220
        /// or the body is empty. On success <paramref name="result"/> holds at
        /// least the netobj_id; <see cref="Name"/> is non-null only when the body
        /// is ≥6 words and the name string is non-empty.
        /// </summary>
        public static bool TryDecode(NetMessage msg, out Desc220Message result)
        {
            result = null!;
            if (msg.DescriptorId != Descriptor || msg.DataLengthWords < 1)
                return false;

            var r = msg.Reader();
            int netObjId = r.ReadUInt16();

            string? name = null;
            if (msg.DataLengthWords >= 6)
            {
                r.ReadUInt16(); r.ReadUInt16(); r.ReadUInt16(); r.ReadUInt16(); // skip [1-4]
                try
                {
                    string s = r.ReadString();
                    if (s.Length > 0)
                        name = s;
                }
                catch { }
            }

            result = new Desc220Message(netObjId, name);
            return true;
        }

        public override string ToString() =>
            HasName
                ? $"Desc220{{netobj={NetObjId} name='{Name}'}}"
                : $"Desc220{{netobj={NetObjId}}}";
    }
}
