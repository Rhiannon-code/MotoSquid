using MotoSquid.Cameras;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotoSquid.Bike
{
    public class NearMissFX : MonoBehaviour
    {
        [Header("References")]
        public BoostSystem               boostSystem;
        public CameraController cameraController;
        public Volume                    postProcessVolume;

        [Header("Directional Blur")]
        public float blurStrength  = 0.8f;
        public float blurDuration  = 0.35f;
        public float blurFadeSpeed = 5f;

        [Header("Camera Shake")]
        public float shakeAmplitude = 3f;
        public float shakeDuration  = 0.2f;

        // Read by CameraController
        [HideInInspector] public float nearMissShakeAmplitude;
        [HideInInspector] public float nearMissShakeTimer;

        // Runtime
        SpeedPostProcess m_FX;
        float            m_BlurTimer;
        float            m_CurrentBlur;
        float            m_BlurVel;
        float            m_BlurSide;

        void Start()
        {
            if (boostSystem == null)
                boostSystem = GetComponentInParent<BoostSystem>(true);

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CameraController>();
                if (cameraController == null)
                    Debug.LogWarning("NearMissFX: CameraController not found in scene, near miss camera effects will be skipped.", this);
            }

            // m_FX is resolved lazily in UpdateBlur() so it always reads the cloned
            // profile that BikeSpeedFX installs in its own Start() (execution order non deterministic)

            if (boostSystem != null)
                boostSystem.OnNearMissSide += OnNearMiss;
        }

        void OnDestroy()
        {
            if (boostSystem != null)
                boostSystem.OnNearMissSide -= OnNearMiss;

            if (m_FX != null)
            {
                m_FX.nearMissBlurStrength.Override(0f);
                m_FX.nearMissBlurSide.Override(1f);
            }
        }

        void Update()
        {
            UpdateBlur();
            UpdateShakeTimer();
        }

        void OnNearMiss(float side)
        {
            TriggerFX(side);
        }

        void TriggerFX(float side)
        {
            m_BlurSide             = side;
            m_BlurTimer            = blurDuration;
            nearMissShakeAmplitude = shakeAmplitude;
            nearMissShakeTimer     = shakeDuration;
        }

        void UpdateBlur()
        {
            if (m_FX == null && postProcessVolume != null)
                postProcessVolume.profile.TryGet(out m_FX);
            if (m_FX == null) return;

            m_BlurTimer      = Mathf.Max(0f, m_BlurTimer - Time.deltaTime);
            float targetBlur = m_BlurTimer > 0f ? blurStrength : 0f;
            m_CurrentBlur    = Mathf.SmoothDamp(m_CurrentBlur, targetBlur,
                                   ref m_BlurVel, 1f / Mathf.Max(blurFadeSpeed, 0.01f));

            m_FX.nearMissBlurStrength.Override(m_CurrentBlur);
            m_FX.nearMissBlurSide.Override(m_BlurSide);
        }

        void UpdateShakeTimer()
        {
            if (nearMissShakeTimer > 0f)
                nearMissShakeTimer -= Time.deltaTime;
        }
    }
}
