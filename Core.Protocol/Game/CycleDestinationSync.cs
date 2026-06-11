using Armagetron.Protocol;

namespace Armagetron.Protocol.Game
{
    /// <summary>
    /// A cycle movement command — the client→server "I turn here" message
    /// (descriptor 321 in the reference capture; the Game.CycleDestinationSync
    /// proto). Wire layout verified against a real 0.2.9 session (15 words):
    /// <code>
    ///   [0-1]  position : REAL x
    ///   [2-3]  position : REAL y
    ///   [4-5]  direction: REAL x (unit axis)
    ///   [6-7]  direction: REAL y (unit axis)
    ///   [8-9]  distance : REAL (total from spawn)
    ///   [10]   flags    : u16  (bit0=brake, bit1=chat)
    ///   [11]   cycleId  : u16  (current round's cycle netobj_id — server routes by trailer)
    ///   [12-13] gameTime: REAL (current game clock)
    ///   [14]   turns    : u16
    /// </code>
    /// The server identifies which cycle the message belongs to via the connection-slot
    /// trailer, not from a cycle_id field at word[0]. Confirmed from desc=24 27w
    /// cross-reference: word[11]=151 matches cycle_id=151 from desc=24 word[0].
    /// </summary>
    public sealed class CycleDestinationSync
    {
        public Vec2 Position { get; }
        public Vec2 Direction { get; }
        public float Distance { get; }
        public int Flags { get; }
        public int CycleId { get; }
        public float GameTime { get; }
        public int Turns { get; }

        public CycleDestinationSync(Vec2 position, Vec2 direction, float distance,
            int flags, int cycleId, float gameTime, int turns)
        {
            Position = position;
            Direction = direction;
            Distance = distance;
            Flags = flags;
            CycleId = cycleId;
            GameTime = gameTime;
            Turns = turns;
        }

        public bool Braking => (Flags & 1) != 0;
        public bool Chatting => (Flags & 2) != 0;

        /// <summary>Descriptor id this command is carried under (verified in capture).</summary>
        public const int Descriptor = 321;

        /// <summary>Encode just the message body (inverse of <see cref="Decode(MessageReader)"/>).</summary>
        public byte[] EncodeBody()
        {
            var w = new MessageWriter();
            w.WriteReal(Position.X);     // [0-1]
            w.WriteReal(Position.Y);     // [2-3]
            w.WriteReal(Direction.X);    // [4-5]
            w.WriteReal(Direction.Y);    // [6-7]
            w.WriteReal(Distance);       // [8-9]
            w.WriteUInt16(Flags);        // [10]
            w.WriteUInt16(CycleId);      // [11] cycle_id for this round
            w.WriteReal(GameTime);       // [12-13]
            w.WriteUInt16(Turns);        // [14]
            return w.ToArray();
        }

        /// <summary>Build a full <see cref="NetMessage"/> for sending, with the given reliable message id.</summary>
        public NetMessage ToMessage(int messageId) => new NetMessage(Descriptor, messageId, EncodeBody());

        public static CycleDestinationSync Decode(NetMessage msg) => Decode(msg.Reader());

        public static CycleDestinationSync Decode(MessageReader r)
        {
            float px = r.ReadReal();
            float py = r.ReadReal();
            float dx = r.ReadReal();
            float dy = r.ReadReal();
            float dist = r.ReadReal();
            int flags = r.ReadUInt16();
            int cycleId = r.ReadUInt16();
            float gameTime = r.ReadReal();
            int turns = r.HasMore ? r.ReadUInt16() : 0;
            return new CycleDestinationSync(new Vec2(px, py), new Vec2(dx, dy),
                dist, flags, cycleId, gameTime, turns);
        }

        public override string ToString() =>
            $"CycleDestinationSync{{pos={Position} dir={Direction} " +
            $"dist={Distance:0.#} t={GameTime:0.##} cycle={CycleId} turns={Turns} flags={Flags}}}";
    }
}
