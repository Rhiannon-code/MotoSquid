using MotoSquid.Cameras;
using MotoSquid.DevTools;
using UnityEngine;
using TMPro;

namespace MotoSquid.Bike
{
    public class BikeDashScreen : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private BikeController   bike;
        [SerializeField] private BoostSystem               boost;
        [SerializeField] private CameraController cameraController;

        [Header("Overlay quads")]
        [SerializeField] private Renderer revBar;
        [SerializeField] private Renderer gear;
        [SerializeField] private TMP_Text gearText;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private bool logFrames;

        [Header("Feel (match the screen HUD)")]
        [SerializeField] private float smoothingRate      = 3f;
        [SerializeField] private float redlineFlashPeriod = 0.16f;

        const int RevCells      = 12;
        const int RevFirstCell  = 1;
        const int GearCells     = 7;
        const int GearFirstCell = 1;
        const int GearFrames    = 6;

        static readonly int EmissiveMap = Shader.PropertyToID("_EmissiveColorMap");
        static readonly int UnlitMap    = Shader.PropertyToID("_UnlitColorMap");

        Material _revMaterial, _gearMaterial;
        float _smoothedFrame;
        int   _lastGear = -1, _lastSpeedKmh = -1, _lastFrame = -1;
        bool  _shown = true;
        float _nextCameraSearch;

        void Awake()
        {
            if (revBar != null) _revMaterial  = revBar.material;
            if (gear   != null) _gearMaterial = gear.material;
            Show(false);
        }

        void OnDestroy()
        {
            if (_revMaterial  != null) Destroy(_revMaterial);
            if (_gearMaterial != null) Destroy(_gearMaterial);
        }
        CameraController ResolveCamera()
        {
            if (cameraController != null) return cameraController;
            if (Time.unscaledTime < _nextCameraSearch) return null;
            _nextCameraSearch = Time.unscaledTime + 1f;

            foreach (var candidate in FindObjectsByType<CameraController>(FindObjectsSortMode.None))
                if (candidate.Bike == bike) { cameraController = candidate; break; }

            return cameraController;
        }

        void Update()
        {
            if (bike == null) return;

            var camera = ResolveCamera();
            Show(camera != null && camera.IsFirstPersonView);
            if (!_shown) return;

            _smoothedFrame = Mathf.Lerp(_smoothedFrame,
                                        BikeGauge.TargetFrame(bike, boost),
                                        Time.deltaTime * smoothingRate);

            int frame = BikeGauge.DisplayFrame(_smoothedFrame, bike, redlineFlashPeriod);
            SetCell(_revMaterial, RevCells, RevFirstCell + frame);

            if (logFrames && frame != _lastFrame)
            {
                _lastFrame = frame;
                Debug.Log($"Dash: frame {frame} -> SPEEDO_{RevFirstCell + frame}, " +
                          $"gear {bike.currentGear}, {BikeGauge.SpeedKmh(bike):F0} km/h, " +
                          $"material {(_revMaterial != null ? _revMaterial.name : "NULL")}");
            }

            if (bike.currentGear != _lastGear)
            {
                _lastGear = bike.currentGear;
                SetCell(_gearMaterial, GearCells,
                        GearFirstCell + Mathf.Clamp(_lastGear - 1, 0, GearFrames - 1));
                if (gearText != null) gearText.text = _lastGear.ToString();
            }

            int speedKmh = (int)BikeGauge.SpeedKmh(bike);
            if (speedText != null && speedKmh != _lastSpeedKmh)
            {
                _lastSpeedKmh  = speedKmh;
                speedText.text = speedKmh.ToString();
            }
        }

        void Show(bool show)
        {
            if (show == _shown) return;
            _shown = show;

            if (revBar != null) revBar.enabled = show;
            if (gear   != null) gear.enabled   = show;
            if (gearText  != null) gearText.enabled  = show;
            if (speedText != null) speedText.enabled = show;

            _lastGear = _lastSpeedKmh = -1;
        }

        static void SetCell(Material material, int cells, int cell)
        {
            if (material == null) return;

            var scale  = new Vector2(1f / cells, 1f);
            var offset = new Vector2((float)cell / cells, 0f);

            material.SetTextureScale(EmissiveMap, scale);
            material.SetTextureOffset(EmissiveMap, offset);
            material.SetTextureScale(UnlitMap, scale);
            material.SetTextureOffset(UnlitMap, offset);
        }
    }
}
