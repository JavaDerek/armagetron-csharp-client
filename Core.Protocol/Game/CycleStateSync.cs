namespace Armagetron.Protocol.Game
{
    /// <summary>
    /// The server→client cycle position sync (descriptor 24, the 27-word "full"
    /// variant). desc=24 is the generic nNetObject sync — its body layout depends
    /// on the synced object's <em>class</em>, not on the message length:
    /// <list type="bullet">
    ///   <item>2w  — compact rubber/score update (handled elsewhere)</item>
    ///   <item>5w  — game-timer object (word[0]=6)</item>
    ///   <item>14–22w — ePlayerNetID / team objects (carry a player name)</item>
    ///   <item>27w — the full gCycle position sync decoded here</item>
    /// </list>
    /// Decoding a non-cycle variant as a position yields garbage, so callers must
    /// route only genuine cycle syncs here. <see cref="TryDecodeFull"/> accepts
    /// <b>only</b> the 27-word descriptor-24 form and rejects everything else,
    /// which is the discriminator that stops the bot seeding its spawn position
    /// off a player-object sync.
    ///
    /// Wire layout (27 words, verified against a real 0.2.9.3.0 capture, cycle 30):
    /// <code>
    ///   [0]     cycleId  : u16  (gCycle netobj_id)
    ///   [1-2]   gameTime : REAL
    ///   [3-4]   direction: REAL x (unit axis)
    ///   [5-6]   direction: REAL y (unit axis)
    ///   [7-8]   position : REAL x
    ///   [9-10]  position : REAL y
    ///   [11-12] speed    : REAL  (units/sec; ≈20 at spawn, cruises 28-33)
    ///   [13]    alive    : u16   (1 = alive, 0 = dead — the death signal)
    ///   [14-26] further state (distance/turn-counter/last-pos/rubber) — partially decoded
    /// </code>
    ///
    /// <b>Death mechanic</b> (live-verified 2026-06-13): a dying cycle gets one final
    /// 27w sync with <c>alive=0</c>, then no more syncs. Speed does <i>not</i> drop to 0
    /// (cycles crash into walls at full cruise), so <see cref="Alive"/> — not speed — is
    /// the death signal a renderer uses to freeze the cycle. See PROTOCOL.md.
    /// </summary>
    public sealed class CycleStateSync
    {
        /// <summary>Descriptor id carrying nNetObject syncs (verified in capture).</summary>
        public const int Descriptor = 24;

        /// <summary>Word count of the full gCycle position sync.</summary>
        public const int FullSyncWords = 27;

        public int CycleId { get; }
        public Vec2 Position { get; }
        public Vec2 Direction { get; }
        public float GameTime { get; }

        /// <summary>Current cycle speed in units/sec (words [11-12]).</summary>
        public float Speed { get; }

        /// <summary>
        /// The gCycle alive flag (word [13]): true while the cycle is in play, false on
        /// the single final sync the server sends when it dies. The authoritative death
        /// signal — speed stays at cruise on a wall-crash death, so it can't be used.
        /// </summary>
        public bool Alive { get; }

        public CycleStateSync(int cycleId, Vec2 position, Vec2 direction, float gameTime,
                              float speed, bool alive)
        {
            CycleId = cycleId;
            Position = position;
            Direction = direction;
            GameTime = gameTime;
            Speed = speed;
            Alive = alive;
        }

        /// <summary>
        /// Decode a desc=24 message as a full cycle position sync. Returns false
        /// (and a null <paramref name="sync"/>) unless the message is descriptor 24
        /// with exactly <see cref="FullSyncWords"/> words — i.e. a real gCycle sync,
        /// not a player/team/timer object that also rides desc=24.
        /// </summary>
        public static bool TryDecodeFull(NetMessage msg, out CycleStateSync sync)
        {
            sync = null!;
            if (msg.DescriptorId != Descriptor || msg.DataLengthWords != FullSyncWords)
            {
                return false;
            }

            var r = msg.Reader();
            int cycleId = r.ReadUInt16();   // [0]
            float gt = r.ReadReal();        // [1-2]
            float dx = r.ReadReal();        // [3-4]
            float dy = r.ReadReal();        // [5-6]
            float px = r.ReadReal();        // [7-8]
            float py = r.ReadReal();        // [9-10]
            float speed = r.ReadReal();     // [11-12]
            bool alive = r.ReadUInt16() != 0; // [13]
            sync = new CycleStateSync(cycleId, new Vec2(px, py), new Vec2(dx, dy), gt, speed, alive);
            return true;
        }

        public override string ToString() =>
            $"CycleStateSync{{cycle={CycleId} pos={Position} dir={Direction} " +
            $"t={GameTime:0.##} speed={Speed:0.##} alive={Alive}}}";
    }
}
