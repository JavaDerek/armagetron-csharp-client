using System.Collections.Generic;
using UnityEngine;
using Armagetron.Game;       // Scene3DBuilder, WorldScene, CycleMarker, CyclePalette, CameraController, CameraSettings, CameraPose, Camera3D
using Armagetron.Game.UI;    // ConnectionStatus
using Armagetron.Protocol;   // Vec2, Vec3, CycleSnapshot
using Armagetron.Lib;        // UiArmaClient

namespace Armagetron.Oculus
{
    /// <summary>
    /// The whole Quest/VR head, as one MonoBehaviour. It connects through the SAME
    /// <see cref="UiArmaClient"/> the desktop/Android/iOS heads use, then each frame asks the
    /// engine-neutral <c>Scene3DBuilder</c> (Core.Protocol) for the arena geometry and renders it
    /// with Unity meshes: one combined wall mesh + a pooled cube per cycle. The VR camera is just
    /// rig placement — the headset owns head rotation — using <c>Camera3D</c> for the third-person
    /// chase pose. Drop this on a GameObject in a scene that also has an XR Origin (OpenXR), wire
    /// the references in the Inspector, and press Play.
    ///
    /// STARTED 2026-06-14 — scaffold. Cannot compile outside the Unity Editor (references
    /// UnityEngine); the core types it uses ARE built and verified (netstandard2.1). See README.md.
    /// </summary>
    public sealed class ArmagetronRunner : MonoBehaviour
    {
        [Header("Server")]
        public string Host = "192.168.68.61";
        public int Port = 4534;
        public string PlayerName = "Vlad"; // novel names trip the server cheat gate; see notes

        [Header("Arena (no Z in the wire protocol yet — tune to taste)")]
        public float ArenaSize = 176.78f;
        public float WallHeight = 8f;

        [Header("VR rig")]
        public VrCameraMode CameraMode = VrCameraMode.ThirdPerson;
        [Tooltip("The XR Origin / camera rig root to anchor to the cycle. The HMD still owns look.")]
        public Transform XrOrigin;

        [Header("Materials (tintable; walls want Cull Off + emissive for the neon look)")]
        public Material WallMaterial;
        public Material FloorMaterial;
        public Material CycleMaterial;

        private UiArmaClient _client;
        private readonly CyclePalette _palette = new CyclePalette();
        private readonly CameraController _camera = new CameraController(CameraSettings.Default);

        private Mesh _wallMesh;
        private readonly List<GameObject> _cyclePool = new List<GameObject>();

        private void Start()
        {
            _client = new UiArmaClient();
            _client.BeginConnect(Host, Port, PlayerName);

            _camera.SetMode(CameraMode == VrCameraMode.FirstPerson
                ? Armagetron.Game.CameraMode.FirstPerson
                : Armagetron.Game.CameraMode.ThirdPerson);

            BuildFloor();

            _wallMesh = new Mesh { name = "ArmagetronWalls" };
            var walls = new GameObject("Walls");
            walls.transform.SetParent(transform, false);
            walls.AddComponent<MeshFilter>().mesh = _wallMesh;
            walls.AddComponent<MeshRenderer>().sharedMaterial = WallMaterial;
        }

        private void Update()
        {
            if (_client == null || _client.Status != ConnectionStatus.Connected) return;

            HandleSteering();

            CycleSnapshot[] snap = _client.Snapshot();
            WorldScene world = Scene3DBuilder.Build(snap, _client.MyCycleId, _palette, ArenaSize, WallHeight);

            WallMeshBuilder.Build(_wallMesh, world);
            SyncCycles(world);
            PositionRig(snap);
        }

        // Steering: map the VR controllers' left/right (or keyboard in the Editor) to turns. Wire
        // the actual XR Input actions in the Inspector for a real build; this keeps the Editor
        // play-testable.
        private void HandleSteering()
        {
            if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.LeftArrow)) _client.TurnLeft();
            if (Input.GetButtonDown("Fire2") || Input.GetKeyDown(KeyCode.RightArrow)) _client.TurnRight();
        }

        // A flat textured quad for the arena floor (the designer's tile can drive FloorMaterial).
        private void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "Floor";
            floor.transform.SetParent(transform, false);
            floor.transform.position = new Vector3(ArenaSize / 2f, 0f, ArenaSize / 2f);
            floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // face up
            floor.transform.localScale = new Vector3(ArenaSize, ArenaSize, 1f);
            if (FloorMaterial != null) floor.GetComponent<MeshRenderer>().sharedMaterial = FloorMaterial;
        }

        // Reuse a pool of cubes, one per live cycle, tinted to the player colour. Cheap placeholder
        // until the Phase-2 lightcycle model (DESIGN_BRIEF_3D.md) is imported as a prefab.
        private void SyncCycles(WorldScene world)
        {
            int i = 0;
            foreach (CycleMarker m in world.Cycles)
            {
                GameObject go = i < _cyclePool.Count ? _cyclePool[i] : NewCycle();
                go.SetActive(true);
                go.transform.position = new Vector3(m.Position.X, WallHeight * 0.4f, m.Position.Y);
                if (m.Direction.X != 0f || m.Direction.Y != 0f)
                    go.transform.rotation = Quaternion.LookRotation(
                        new Vector3(m.Direction.X, 0f, m.Direction.Y), Vector3.up);
                var r = go.GetComponent<MeshRenderer>();
                if (r != null) r.material.color = VrConvert.ToUnity(m.Color);
                i++;
            }
            for (; i < _cyclePool.Count; i++) _cyclePool[i].SetActive(false);
        }

        private GameObject NewCycle()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Cycle";
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(2f, WallHeight * 0.5f, 5f);
            if (CycleMaterial != null) go.GetComponent<MeshRenderer>().sharedMaterial = CycleMaterial;
            _cyclePool.Add(go);
            return go;
        }

        // Anchor the rig to the local cycle. The HMD provides head rotation, so we set position
        // (and, optionally, a yaw to face travel) but never force pitch/roll.
        private void PositionRig(CycleSnapshot[] snap)
        {
            if (XrOrigin == null) return;

            Vec2 pos = new Vec2(ArenaSize / 2f, ArenaSize / 2f), dir = new Vec2(1, 0);
            foreach (CycleSnapshot c in snap)
                if (c.CycleId == _client.MyCycleId) { pos = c.Position; dir = c.Direction; }

            if (CameraMode == VrCameraMode.FirstPerson)
            {
                XrOrigin.position = new Vector3(pos.X, _camera.Settings.EyeHeight, pos.Y);
            }
            else
            {
                CameraPose pose = _camera.Pose(pos, dir); // third-person chase eye/target
                XrOrigin.position = VrConvert.ToUnity(pose.Eye);
                Vector3 flatForward = new Vector3(pose.Forward.X, 0f, pose.Forward.Z);
                if (flatForward.sqrMagnitude > 1e-4f)
                    XrOrigin.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            }
        }

        private void OnDestroy() => _client?.Dispose();
    }
}
