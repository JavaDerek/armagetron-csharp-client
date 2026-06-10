namespace Armagetron.Protocol
{
    /// <summary>
    /// Engine-neutral 2D vector (rule 4). Map to UnityEngine.Vector3 or
    /// MonoGame's Vector2 in the front-end layers — never reference those here.
    /// </summary>
    public readonly struct Vec2
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X:0.##},{Y:0.##})";
    }
}
