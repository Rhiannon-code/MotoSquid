using UnityEngine;

namespace MotoSquid.Bike
{
    public class BoostWheelGlow : MonoBehaviour
    {
        [Header("References")]
        public BoostSystem boostSystem;
        public Renderer wheelRenderer;
        public int materialIndex = 0;

        [Header("Emissive Colour")]
        public Color defaultEmissiveColour = new Color(0.2f, 0.4f, 1f, 1f);  // Dim blue
        public Color chargedEmissiveColour = new Color(1f, 0.5f, 0.05f, 1f); // Orange

        [Header("Intensity")]
        public float defaultIntensity = 1.5f;
        public float chargedIntensity = 8f;

        public float glowSmoothTime = 0.2f;

        [Header("Boost Active Drain")]
        public float boostDrainSmoothTime = 1.2f;

        [Header("Pulse")]
        public bool  pulseWhenCharged = true;
        public float pulseFrequency   = 3f;
        public float pulseDepth       = 0.15f;

        // Runtime
        MaterialPropertyBlock m_Props;
        float    m_CurrentIntensity;
        float    m_IntensityVel;

        static readonly int k_EmissiveColor     = Shader.PropertyToID("_EmissiveColor");
        static readonly int k_EmissiveIntensity = Shader.PropertyToID("_EmissiveIntensity");

        void Start()
        {
            if (boostSystem == null)
                boostSystem = GetComponentInParent<BoostSystem>(includeInactive: true);

            if (wheelRenderer == null)
            {
                Debug.LogError("BoostWheelGlow: no Renderer assigned.");
                enabled = false;
                return;
            }

            m_Props = new MaterialPropertyBlock();

            // Start at default intensity immediately, no ramp from zero
            m_CurrentIntensity = defaultIntensity;
            ApplyToMaterial(defaultEmissiveColour, defaultIntensity);
        }

        void Update()
        {
            if (m_Props == null) return;

            float meter    = boostSystem != null ? boostSystem.boostMeter  : 0f;
            bool  boosting = boostSystem != null && boostSystem.isBoosting;

            float targetIntensity;
            Color targetColour;
            float smoothTime;

            if (boosting)
            {

                targetIntensity = defaultIntensity;
                targetColour    = defaultEmissiveColour;
                smoothTime      = boostDrainSmoothTime;
            }
            else
            {
                // Charging: intensity rises from default to charged as meter fills
                targetIntensity = Mathf.Lerp(defaultIntensity, chargedIntensity, meter);
                targetColour    = Color.Lerp(defaultEmissiveColour, chargedEmissiveColour, meter);
                smoothTime      = glowSmoothTime;

                // Subtle pulse when fully charged and ready
                if (pulseWhenCharged && meter >= 0.98f)
                {
                    float pulse     = (Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
                    targetIntensity *= Mathf.Lerp(1f - pulseDepth, 1f, pulse);
                }
            }

            m_CurrentIntensity = Mathf.SmoothDamp(m_CurrentIntensity, targetIntensity,
                                     ref m_IntensityVel, smoothTime);

            ApplyToMaterial(targetColour, m_CurrentIntensity);
        }

        void ApplyToMaterial(Color colour, float intensity)
        {
            wheelRenderer.GetPropertyBlock(m_Props, materialIndex);
            m_Props.SetColor(k_EmissiveColor,     colour);
            m_Props.SetFloat(k_EmissiveIntensity, intensity);
            wheelRenderer.SetPropertyBlock(m_Props, materialIndex);
        }

        void OnDestroy()
        {
            if (wheelRenderer != null && m_Props != null)
                ApplyToMaterial(defaultEmissiveColour, defaultIntensity);
        }
    }
}
