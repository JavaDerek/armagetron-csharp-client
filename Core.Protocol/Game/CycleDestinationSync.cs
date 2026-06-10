using Armagetron.Protocol;

namespace Armagetron.Protocol.Game
{
    /// <summary>
    /// A cycle movement command — the client→server "I turn here" message
    /// (descriptor 321 in the reference capture; the Game.CycleDestinationSync
    /// proto). Field order verified field-by-field against real play: decoded
    /// positions trace a 90°-turn grid path, directions are exact unit-axis
    /// vectors, and distance/gameTime/turns increase monotonically.
    /// <code>
    ///   position : Vec2 (REAL x, REAL y)     direction: Vec2 (REAL x, REAL y, unit axis)
    ///   distance : REAL    flags: u16 (bit1=brake, bit2=chat)
    ///   cycleId  : u16     gameTime: REAL     turns: u16
    /// </code>
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
            w.WriteReal(Position.X);
            w.WriteReal(Position.Y);
            w.WriteReal(Direction.X);
            w.WriteReal(Direction.Y);
            w.WriteReal(Distance);
            w.WriteUInt16(Flags);
            w.WriteUInt16(CycleId);
            w.WriteReal(GameTime);
            w.WriteUInt16(Turns);
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
            int turns = r.HasMore ? r.ReadUInt16() : 0; // older clients may omit
            return new CycleDestinationSync(new Vec2(px, py), new Vec2(dx, dy),
                dist, flags, cycleId, gameTime, turns);
        }

        public override string ToString() =>
            $"CycleDestinationSync{{cycle={CycleId} pos={Position} dir={Direction} " +
            $"dist={Distance:0.#} t={GameTime:0.##} turns={Turns} flags={Flags}}}";
    }
}
