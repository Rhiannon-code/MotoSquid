using MotoSquid.Cameras;
using MotoSquid.UI;
using UnityEngine;
namespace MotoSquid.Settings
{
    public class AccessibilityConsumer : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private CameraController[] cameraRigs;
        [SerializeField] private HUDManager hud;
        [SerializeField] private GameObject motionBlurVolumeObject;

        // Authored base FOV the CameraFOV preference is expressed relative to (manager defaults to 60)
        [SerializeField] private float baseFov = 60f;

        AccessibilityManager _acc;

        void Start()
        {
            if (cameraRigs == null || cameraRigs.Length == 0)
                cameraRigs = FindObjectsOfType<CameraController>(true);
            if (hud == null) hud = FindObjectOfType<HUDManager>(true);

            _acc = AccessibilityManager.Instance;
            if (_acc == null) return;

            _acc.OnCameraFOVChanged     += ApplyFov;
            _acc.OnMotionBlurChanged    += ApplyMotionBlur;
            _acc.OnSimplifiedHUDChanged += ApplySimplifiedHUD;
            _acc.OnReduceClutterChanged += ApplyReduceClutter;

            // Apply the current values immediately so the scene reflects saved settings on load
            ApplyFov(_acc.CameraFOV);
            ApplyMotionBlur(_acc.MotionBlurEnabled);
            ApplySimplifiedHUD(_acc.SimplifiedHUD);
            ApplyReduceClutter(_acc.ReduceClutter);
        }

        void OnDestroy()
        {
            if (_acc == null) return;
            _acc.OnCameraFOVChanged     -= ApplyFov;
            _acc.OnMotionBlurChanged    -= ApplyMotionBlur;
            _acc.OnSimplifiedHUDChanged -= ApplySimplifiedHUD;
            _acc.OnReduceClutterChanged -= ApplyReduceClutter;
        }

        void ApplyFov(float fov)
        {
            if (cameraRigs == null) return;
            float bias = fov - baseFov;
            foreach (var cam in cameraRigs)
                if (cam != null) cam.accessibilityFovBias = bias;
        }

        void ApplyMotionBlur(bool enabled)
        {
            if (motionBlurVolumeObject != null && motionBlurVolumeObject.activeSelf != enabled)
                motionBlurVolumeObject.SetActive(enabled);
        }

        // Simplified HUD and Reduce Clutter both collapse the non essential HUD, honour either being on
        void ApplySimplifiedHUD(bool _) => RefreshHud();
        void ApplyReduceClutter(bool _) => RefreshHud();

        void RefreshHud()
        {
            if (hud == null || _acc == null) return;
            hud.SetSimplified(_acc.SimplifiedHUD || _acc.ReduceClutter);
        }
    }
}
