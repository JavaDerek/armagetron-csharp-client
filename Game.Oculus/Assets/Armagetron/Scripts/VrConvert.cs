using UnityEngine;
using Armagetron.Game;       // RenderColor
using Armagetron.Protocol;   // Vec2, Vec3

namespace Armagetron.Oculus
{
    /// <summary>Which viewpoint the VR rig takes. The headset always owns head rotation; these
    /// only decide where the rig is anchored relative to the local cycle.</summary>
    public enum VrCameraMode
    {
        /// <summary>Rig rides on the cycle (in-cockpit). Intense — comfort-tune before shipping.</summary>
        FirstPerson,
        /// <summary>Rig floats behind/above the cycle (god-view chase), computed by <c>Camera3D</c>.</summary>
        ThirdPerson,
    }

    /// <summary>
    /// Maps the engine-neutral core value types onto UnityEngine equivalents. This is the whole of
    /// the "Unity-specific" type bridge — rule 4 keeps the core free of <c>UnityEngine.Vector3</c>,
    /// so the conversion lives here in the head. World space matches the core convention exactly:
    /// arena (x, y) lifts to (x, height, y), floor at Y=0, up is +Y — which is already Unity's
    /// left-handed Y-up convention, so it is a straight component copy.
    /// </summary>
    public static class VrConvert
    {
        public static Vector3 ToUnity(Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>An arena (2D) point lifted onto the floor plane (Y=0).</summary>
        public static Vector3 Floor(Vec2 p) => new Vector3(p.X, 0f, p.Y);

        public static Color ToUnity(RenderColor c) =>
            new Color(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
    }
}
