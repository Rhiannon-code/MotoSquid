using MotoSquid.Bike;
using MotoSquid.Combat;
using MotoSquid.Controls;
using MotoSquid.Core;
using MotoSquid.DevTools;
using MotoSquid.Race;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace MotoSquid.Cameras
{
    [DefaultExecutionOrder(100)]
    public class SplitScreenSetup : MonoBehaviour
    {
        [Header("Camera references")]
        [SerializeField] private Camera player1Camera;
        [SerializeField] private Camera player2Camera;

        public Camera Player1Camera => player1Camera;
        public Camera Player2Camera => player2Camera;

        [Header("Cinemachine isolation")]
        [SerializeField] private LayerMask p1VirtualCamLayer;
        [SerializeField] private LayerMask p2VirtualCamLayer;

        [Header("Volume isolation")]
        [SerializeField] private LayerMask p1VolumeLayer;
        [SerializeField] private LayerMask p2VolumeLayer;

        [Header("Split screen display")]
        [SerializeField] private RawImage p1Display;
        [SerializeField] private RawImage p2Display;

        [Header("Filled at spawn: only wire these for scenes with pre-placed bikes")]
        [SerializeField] private CameraController p1CameraController;
        [SerializeField] private CameraController p2CameraController;
        [SerializeField] private BikeInput p1BikeInput;
        [SerializeField] private BikeInput p2BikeInput;

        [Header("Debug")]
        [SerializeField] private bool forceEnabled = false;

        private RenderTexture _p1RT;
        private RenderTexture _p2RT;
        private bool _splitScreen;

        void Awake()
        {
            // forceEnabled must write the session flag, not just a local bool: RaceManager and HUDManager
            // read GameSession.IsSplitScreen in their own Start, so a local only flag gave split cameras
            // with no P2 HUD, no P2 grid slot and the AI still racing
            if (forceEnabled && GameSession.Instance == null)
                new GameObject("GameSession [ForceCreated]").AddComponent<GameSession>();

            if (forceEnabled && GameSession.Instance != null)
                GameSession.Instance.IsSplitScreen = true;

            _splitScreen = GameSession.Instance != null && GameSession.Instance.IsSplitScreen;

            if (!_splitScreen)
            {
                if (player2Camera != null) player2Camera.gameObject.SetActive(false);
                if (p2Display     != null) p2Display.gameObject.SetActive(false);
            }
        }

        // CinemachineBrain rewrites the camera's clip planes from the live vcam every frame, so the value
        // on the Camera itself never survives. Installed here rather than left as a component to remember,
        // it has to be on both cameras or the far clip silently reverts on whichever one was missed
        void EnsureFarClipLock(Camera cam)
        {
            if (cam == null) return;
            if (cam.GetComponent<CameraFarClipLock>() == null)
                cam.gameObject.AddComponent<CameraFarClipLock>();
        }

        void Start()
        {
            EnsureFarClipLock(player1Camera);
            EnsureFarClipLock(player2Camera);

            WireInputDevices();
            if (!_splitScreen) return;
            // Here, not in BindPlayer: PlayerRacerSetup binds from its Awake, before our Awake has read the split flag
            StartCoroutine(FlattenBikeAudioNextFrame(_p1Bike));
            StartCoroutine(FlattenBikeAudioNextFrame(_p2Bike));
            _raceManager = FindFirstObjectByType<RaceManager>();
            if (_raceManager != null) _raceManager.OnRaceComplete += FreezeLoser;
            SetupRenderTextures();
            IsolateCinemachineBrains();
            if (player2Camera != null) player2Camera.gameObject.SetActive(true);
        }

        void SetupRenderTextures()
        {
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
            bool topBottom = aspect <= 2.0f;

            int w = topBottom ? Mathf.Max(Screen.width,      1) : Mathf.Max(Screen.width  / 2, 1);
            int h = topBottom ? Mathf.Max(Screen.height / 2, 1) : Mathf.Max(Screen.height,     1);

            _p1RT = new RenderTexture(w, h, 0, RenderTextureFormat.DefaultHDR) { name = "P1_RT" };
            _p2RT = new RenderTexture(w, h, 0, RenderTextureFormat.DefaultHDR) { name = "P2_RT" };
            _p1RT.Create();
            _p2RT.Create();

            if (player1Camera != null)
            {
                player1Camera.targetTexture = _p1RT;
                player1Camera.rect          = new Rect(0f, 0f, 1f, 1f);
            }
            if (player2Camera != null)
            {
                player2Camera.targetTexture = _p2RT;
                player2Camera.rect          = new Rect(0f, 0f, 1f, 1f);
            }

            if (p1Display != null) { p1Display.texture = _p1RT; p1Display.color = Color.white; }
            if (p2Display != null) { p2Display.texture = _p2RT; p2Display.color = Color.white; }

            ArrangeDisplays(topBottom);
        }

        void ArrangeDisplays(bool topBottom)
        {
            if (topBottom)
            {
                SetAnchors(p1Display, new Vector2(0f, 0.5f), new Vector2(1f, 1f));
                SetAnchors(p2Display, new Vector2(0f, 0f),   new Vector2(1f, 0.5f));
            }
            else
            {
                SetAnchors(p1Display, new Vector2(0f,  0f), new Vector2(0.5f, 1f));
                SetAnchors(p2Display, new Vector2(0.5f, 0f), new Vector2(1f,  1f));
            }

            // Both camera views belong behind every HUD element. P1Display was authored as the
            // second-to-last child of the canvas, so once split screen gave it a render texture it drew
            // opaque over the HUD panels that come before it in the hierarchy.
            if (p2Display != null) p2Display.rectTransform.SetAsFirstSibling();
            if (p1Display != null) p1Display.rectTransform.SetAsFirstSibling();
        }

        static void SetAnchors(RawImage display, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (display == null) return;
            var rt       = display.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // A bike switch changes which object owns the P1 slot, and every coupling below is by
        // reference, so they have to be re-taken rather than re-read
        // Resolved at bind time, not serialized: both bikes are spawned at runtime
        CombatInput _p1Combat, _p2Combat;
        BoostSystem          _p1Boost,  _p2Boost;
        ResetBike   _p1Reset,  _p2Reset;
        BikeController _p1Bike, _p2Bike;
        RaceManager _raceManager;

        public void BindPlayer1(BikeController bike) => BindPlayer(1, bike);

        public void BindPlayer2(BikeController bike) => BindPlayer(2, bike);

        // One path for both slots: every coupling below is by reference, and a spawned bike is in
        // none of them until it is bound
        public void BindPlayer(int slot, BikeController bike)
        {
            if (bike == null) return;

            var input = bike.GetComponentInChildren<BikeInput>(true);
            var rig   = CameraRigOf(bike);

            var combat = bike.GetComponentInChildren<CombatInput>(true);
            var boost  = bike.GetComponentInChildren<BoostSystem>(true);
            var reset  = bike.GetComponentInChildren<ResetBike>(true);

            if (slot == 2) { p2BikeInput = input; p2CameraController = rig; _p2Combat = combat; _p2Boost = boost; _p2Reset = reset; }
            else           { p1BikeInput = input; p1CameraController = rig; _p1Combat = combat; _p1Boost = boost; _p1Reset = reset; }

            // The FX volume layer is what keeps one player's speed lines off the other's screen, and it
            // cannot come from the prefab: both bikes are the same prefab and it ships P1's layer.
            LayerMask volMask = slot == 2 ? p2VolumeLayer : p1VolumeLayer;
            if (volMask.value != 0)
            {
                var fx = bike.GetComponentInChildren<BikeSpeedFX>(true)
                      ?? bike.GetComponentInParent<BikeSpeedFX>(true);
                if (fx != null) fx.SetFXLayer(LayerMaskToLayerIndex(volMask));
                else Debug.LogWarning($"[SplitScreenSetup] P{slot} bike has no BikeSpeedFX, its post " +
                                      "processing cannot be isolated from the other player.", this);
            }

            var slotCamera = slot == 2 ? player2Camera : player1Camera;
            if (rig != null) rig.AssignPhysicalCamera(slotCamera);
            if (slotCamera == null)
                Debug.LogError($"[SplitScreenSetup] No player{slot} camera assigned, the rig has no " +
                               "physical camera to render through.", this);

            Debug.Log($"[SplitScreenSetup] P{slot} bound to '{bike.name}': input " +
                      $"{(input != null ? "ok" : "MISSING")}, rig {(rig != null ? rig.name : "MISSING")}.", this);

            IsolateCinemachineBrains();
            WireInputDevices();
            StartCoroutine(ReportCameraStateNextFrame(bike));
            if (slot == 2) _p2Bike = bike; else _p1Bike = bike;
        }

        // The only AudioListener is on P1's camera, so anything 3D on P2's bike fades with the distance
        // between the two players. Both own bikes go fully 2D so each is heard at the same level
        System.Collections.IEnumerator FlattenBikeAudioNextFrame(BikeController bike)
        {
            yield return null;
            if (bike == null) yield break;

            var sources = bike.transform.root.GetComponentsInChildren<AudioSource>(true);
            foreach (var s in sources) s.spatialBlend = 0f;
            Debug.Log($"[SplitScreenSetup] '{bike.name}': {sources.Length} audio sources set to 2D.", this);
        }

        // CameraController.Start picks the live camera, so sampling during Awake reports the
        // authored state rather than what the brain ends up rendering
        System.Collections.IEnumerator ReportCameraStateNextFrame(BikeController bike)
        {
            yield return null;
            yield return null;
            ReportCameraState(bike);

            // Watch the whole intro: the handover is what is failing, and a single sample cannot see it
            var brain = player1Camera != null ? player1Camera.GetComponent<CinemachineBrain>() : null;
            string last = null;
            for (float t = 0f; t < 14f; t += 0.25f)
            {
                if (brain == null || bike == null) yield break;
                var live = brain.ActiveVirtualCamera;
                string now = live != null ? live.Name : "NONE";
                string state = now + (brain.IsBlending ? "  [BLENDING]" : "");
                if (state != last)
                {
                    last = state;
                    Debug.Log($"[CamWatch] t={t:0.00}  live={state}  camPos={player1Camera.transform.position:F1}" +
                              $"  bikePos={bike.transform.position:F1}", this);
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        void ReportCameraState(BikeController bike)
        {
            var sb = new System.Text.StringBuilder("[SplitScreenSetup] camera state for ").Append(bike.name).Append('\n');

            var brain = player1Camera != null ? player1Camera.GetComponent<CinemachineBrain>() : null;
            sb.Append("   P1 camera : ").Append(player1Camera != null ? player1Camera.name : "NULL")
              .Append("   brain: ").Append(brain != null ? "yes, channel " + brain.ChannelMask : "MISSING")
              .Append("   cullingMask: ").Append(player1Camera != null ? player1Camera.cullingMask.ToString() : "-")
              .Append('\n');

            if (brain != null)
            {
                var live = brain.ActiveVirtualCamera;
                sb.Append("   LIVE vcam : ").Append(live != null ? live.Name : "NONE")
                  .Append("   camera pos ").Append(player1Camera != null ? player1Camera.transform.position.ToString("F2") : "-")
                  .Append("   bike pos ").Append(bike.transform.position.ToString("F2"))
                  .Append('\n');
            }

            if (p1CameraController == null) { sb.Append("   rig: NULL\n"); Debug.LogError(sb.ToString(), this); return; }

            sb.Append("   rig: ").Append(p1CameraController.name)
              .Append("  parent: ").Append(p1CameraController.transform.parent != null
                                           ? p1CameraController.transform.parent.name : "(root)").Append('\n');

            Report(sb, "third person", p1CameraController.cameras);
            Report(sb, "look behind", p1CameraController.lookBehindCameras);
            Debug.Log(sb.ToString(), this);
        }

        static void Report(System.Text.StringBuilder sb, string label, CinemachineCamera[] cams)
        {
            if (cams == null) { sb.Append("   ").Append(label).Append(": NULL array\n"); return; }
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null) { sb.Append("   ").Append(label).Append('[').Append(i).Append("]: NULL\n"); continue; }
                sb.Append("   ").Append(label).Append('[').Append(i).Append("] ").Append(c.name)
                  .Append("  enabled=").Append(c.isActiveAndEnabled)
                  .Append("  prio=").Append(c.Priority.Value)
                  .Append("  layer=").Append(c.gameObject.layer)
                  .Append("  chan=").Append(c.OutputChannel)
                  .Append("  Follow=").Append(c.Follow != null ? c.Follow.name : "NULL")
                  .Append("  LookAt=").Append(c.LookAt != null ? c.LookAt.name : "NULL")
                  .Append('\n');
            }
        }

        // CameraController.Awake reparents itself to the scene root, and Instantiate runs
        // that Awake before the caller gets the reference back, so a child search finds nothing
        public static CameraController CameraRigOf(BikeController bike)
        {
            var onBike = bike.GetComponentInChildren<CameraController>(true);
            if (onBike != null) return onBike;

            foreach (var rig in FindObjectsByType<CameraController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (rig.Bike == bike) return rig;

            Debug.LogError($"[SplitScreenSetup] No CameraController found for '{bike.name}'. Its vcams " +
                           "keep whatever channel they were authored with, so the wrong camera renders.", bike);
            return null;
        }

        void IsolateCinemachineBrains()
        {
            int p1Layer = LayerMaskToLayerIndex(p1VirtualCamLayer);
            int p2Layer = LayerMaskToLayerIndex(p2VirtualCamLayer);

            if (p1VirtualCamLayer.value == 0 || p2VirtualCamLayer.value == 0 || p1Layer == p2Layer)
            {
                Debug.LogError("[SplitScreenSetup] P1/P2 Virtual Cam Layers must be assigned to two " +
                    "DISTINCT layers, otherwise both Cinemachine brains share vcams and the split screen " +
                    "views bleed into each other. Assign them in the Inspector.", this);
            }

            ApplyLayerToController(p1CameraController, p1Layer);
            ApplyLayerToController(p2CameraController, p2Layer);
            AssignChannel(p1CameraController, player1Camera, OutputChannels.Default);
            AssignChannel(p2CameraController, player2Camera, OutputChannels.Channel01);

            if (player1Camera != null)
                player1Camera.cullingMask = (player1Camera.cullingMask | p1VirtualCamLayer.value) & ~p2VirtualCamLayer.value;
            if (player2Camera != null)
                player2Camera.cullingMask = (player2Camera.cullingMask | p2VirtualCamLayer.value) & ~p1VirtualCamLayer.value;

            if (p1VolumeLayer.value != 0) IsolateVolumeStack(player1Camera, p1VolumeLayer.value);
            if (p2VolumeLayer.value != 0) IsolateVolumeStack(player2Camera, p2VolumeLayer.value);
        }

        static void AssignChannel(CameraController controller, Camera playerCamera, OutputChannels channel)
        {
            if (controller?.cameras != null)
                foreach (var vcam in controller.cameras)
                    if (vcam != null) vcam.OutputChannel = channel;

            if (controller?.lookBehindCameras != null)
                foreach (var vcam in controller.lookBehindCameras)
                    if (vcam != null) vcam.OutputChannel = channel;

            var brain = playerCamera != null ? playerCamera.GetComponent<CinemachineBrain>() : null;
            if (brain != null) brain.ChannelMask = channel;
            else if (playerCamera != null)
                Debug.LogWarning($"[SplitScreenSetup] No CinemachineBrain on {playerCamera.name}, " +
                                 "its vcams cannot be isolated by channel.", playerCamera);
        }

        static void IsolateVolumeStack(Camera cam, int playerVolumeBits)
        {
            if (cam == null) return;
            var hdData = cam.GetComponent<HDAdditionalCameraData>();
            if (hdData == null) return;
            hdData.volumeLayerMask = playerVolumeBits | (1 << 0);
        }

        void ApplyLayerToController(CameraController controller, int layer)
        {
            if (controller == null) return;
            foreach (var vcam in controller.cameras)
                if (vcam != null) vcam.gameObject.layer = layer;
            if (controller.lookBehindCameras != null)
                foreach (var vcam in controller.lookBehindCameras)
                    if (vcam != null) vcam.gameObject.layer = layer;
        }

        static int LayerMaskToLayerIndex(LayerMask mask)
        {
            int m = mask.value;
            for (int i = 0; i < 32; i++)
                if ((m & (1 << i)) != 0) return i;
            return 0;
        }

        void WireInputDevices()
        {
            int gamepadCount = Gamepad.all.Count;

            bool p1Kb; int p1Idx;
            bool p2Kb; int p2Idx;

            if (_splitScreen)
            {
                if (gamepadCount >= 2)
                {
                    p1Kb = false; p1Idx = 0;
                    p2Kb = false; p2Idx = 1;
                }
                else if (gamepadCount == 1)
                {
                    p1Kb = false; p1Idx = 0;
                    p2Kb = true;  p2Idx = -1;
                }
                else
                {
                    p1Kb = true;  p1Idx = -1;
                    p2Kb = true;  p2Idx = -1;
                }
            }
            else
            {
                // One player, so a pad and the keyboard both belong to them. Pinning P1 to the pad alone
                // silently killed the keyboard the moment a controller was plugged in.
                p1Kb  = gamepadCount == 0;
                p1Idx = gamepadCount > 0 ? 0 : -1;
                p2Kb  = true; p2Idx = -1;
            }

            // Split screen has to keep the two apart; single player does not
            bool p1AllowKb = !_splitScreen;

            p1BikeInput?.SetDeviceFilter(p1Kb, p1Idx, p1AllowKb);
            p1CameraController?.SetDeviceFilter(p1Kb, p1Idx, p1AllowKb);
            _p1Combat?.SetDeviceFilter(p1Kb, p1Idx, p1AllowKb);
            _p1Boost?.SetDeviceFilter(p1Kb, p1Idx, p1AllowKb);
            _p1Reset?.SetDeviceFilter(p1Kb, p1Idx, p1AllowKb);
            _p1Boost?.EnableInput();

            if (_splitScreen)
            {
                p2BikeInput?.SetDeviceFilter(p2Kb, p2Idx);
                p2CameraController?.SetDeviceFilter(p2Kb, p2Idx);
                _p2Combat?.SetDeviceFilter(p2Kb, p2Idx);
                _p2Combat?.EnableInput();
                _p2Boost?.SetDeviceFilter(p2Kb, p2Idx);
                _p2Boost?.EnableInput();
                _p2Reset?.SetDeviceFilter(p2Kb, p2Idx);
                p2BikeInput?.EnableInput();
            }
            else
            {
                p2BikeInput?.DisableInput();
                _p2Combat?.DisableInput();
                _p2Boost?.DisableInput();
            }
        }

        // The first player across ends the race; the other loses their controls and coasts to a stop. Input only:
        // canMove false is the start grid hold, which pins a grounded bike dead where it is
        void FreezeLoser(System.Collections.Generic.List<RaceManager.RacerInfo> finishOrder)
        {
            var winner = finishOrder != null && finishOrder.Count > 0 ? finishOrder[0].transform : null;
            bool p1Won = winner != null && _p1Bike != null && winner.root == _p1Bike.transform.root;
            bool p2Won = winner != null && _p2Bike != null && winner.root == _p2Bike.transform.root;

            if (!p1Won)
            {
                p1BikeInput?.DisableInput();
                _p1Combat?.DisableInput();
                _p1Boost?.DisableInput();
            }
            if (!p2Won)
            {
                p2BikeInput?.DisableInput();
                _p2Combat?.DisableInput();
                _p2Boost?.DisableInput();
            }
        }

        // Called by the end sequence under its fade to black, so the victory orbit fills the screen on P1's camera
        public void CollapseToPlayer1()
        {
            if (!_splitScreen) return;

            if (player1Camera != null)
            {
                player1Camera.targetTexture = null;
                player1Camera.rect          = new Rect(0f, 0f, 1f, 1f);
            }
            if (player2Camera != null) player2Camera.gameObject.SetActive(false);
            if (p1Display     != null) p1Display.gameObject.SetActive(false);
            if (p2Display     != null) p2Display.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_raceManager != null) _raceManager.OnRaceComplete -= FreezeLoser;
            if (_p1RT != null) { _p1RT.Release(); Destroy(_p1RT); }
            if (_p2RT != null) { _p2RT.Release(); Destroy(_p2RT); }
        }
    }
}
