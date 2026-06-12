namespace Armagetron.Protocol.Game
{
    /// <summary>
    /// S→C desc=320: gCycle creation message.
    ///
    /// PROTOCOL FINDING (2026-06-11, derived from GPL source + PCAP validation):
    /// desc=320 is the nNOInitialisator descriptor for gCycle. The server sends
    /// it once when a cycle is first created. The body begins with three words:
    ///
    ///   [0]  cycleId         u16  gCycle netobj_id (appears in subsequent desc=24 27w syncs)
    ///   [1]  connectionSlot  u16  owner: 0 = server/AI, 1 = first remote client, etc.
    ///   [2]  playerNetObjId  u16  owning ePlayerNetID netobj_id
    ///   [3+] …further data        (colour REALs, ReadSync payload — not decoded here)
    ///
    /// To identify the bot's own cycle: connectionSlot == session.ConnectionId (typically 1).
    /// </summary>
    public sealed class CycleCreateMessage
    {
        public const int Descriptor = 320;

        /// <summary>gCycle's server-assigned netobj_id (word[0]).</summary>
        public int CycleId { get; }

        /// <summary>Owner connection slot — 0 = AI/server, 1 = first remote client (us).</summary>
        public int ConnectionSlot { get; }

        /// <summary>Owning ePlayerNetID netobj_id (word[2]).</summary>
        public int PlayerNetObjId { get; }

        private CycleCreateMessage(int cycleId, int connectionSlot, int playerNetObjId)
        {
            CycleId = cycleId;
            ConnectionSlot = connectionSlot;
            PlayerNetObjId = playerNetObjId;
        }

        /// <summary>
        /// Decode a desc=320 message. Returns false when the descriptor is not 320
        /// or the body has fewer than 3 words (cannot read cycleId+slot+player).
        /// </summary>
        public static bool TryDecode(NetMessage msg, out CycleCreateMessage result)
        {
            result = null!;
            if (msg.DescriptorId != Descriptor || msg.DataLengthWords < 3)
                return false;

            var r = msg.Reader();
            int cycleId        = r.ReadUInt16();
            int connectionSlot = r.ReadUInt16();
            int playerNetObjId = r.ReadUInt16();

            result = new CycleCreateMessage(cycleId, connectionSlot, playerNetObjId);
            return true;
        }

        public override string ToString() =>
            $"CycleCreate{{cycleId={CycleId} slot={ConnectionSlot} player={PlayerNetObjId}}}";
    }
}
