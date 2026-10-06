using MotoSquid.Bike;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MotoSquid.UI
{
    public class BoostUI : MonoBehaviour
    {
        [Header("References")]
        public BoostSystem boostSystem;

        [Header("Boost Bar")]
        public Slider boostSlider;
        public Image boostFillImage;
        public Color normalColour   = new Color(0.2f, 0.6f, 1f);
        public Color regenColour    = new Color(0.2f, 1f, 0.45f);
        public Color boostingColour = new Color(1f, 0.5f, 0f);

        public float barSmoothTime = 0.08f;

        [Header("Near Miss Popup")]
        public TextMeshProUGUI nearMissText;
        public float nearMissDisplayTime = 1.2f;

        [Header("Boost Event Popup")]
        public TextMeshProUGUI eventText;
        public float eventDisplayTime = 1.5f;

        [Header("Boost Active Indicator")]
        public GameObject boostActiveIndicator;

        // State
        enum BarState { Normal, Regen, Boosting }

        float    m_DisplayedMeter;
        float    m_MeterVel;
        float    m_NearMissTimer;
        float    m_EventTimer;
        bool     m_WheelieRegen;
        bool     m_DriftRegen;
        bool     AnyRegenActive => m_WheelieRegen || m_DriftRegen;
        BarState m_BarState = BarState.Normal;

        void Start()
        {
            if (boostSystem == null)
            {
                Debug.LogWarning("BoostUI: no BoostSystem assigned.");
                enabled = false;
                return;
            }

            boostSystem.OnNearMiss            += HandleNearMiss;
            boostSystem.OnBoostStart          += HandleBoostStart;
            boostSystem.OnBoostEnd            += HandleBoostEnd;
            boostSystem.OnWheelieRegenStart   += HandleWheelieRegenStart;
            boostSystem.OnWheelieRegenEnd     += HandleWheelieRegenEnd;
            boostSystem.OnDriftRegenStart     += HandleDriftRegenStart;
            boostSystem.OnDriftRegenEnd       += HandleDriftRegenEnd;
            boostSystem.OnWheelieCombo        += HandleWheelieCombo;
            boostSystem.OnCombatKnockoffBoost += HandleCombatKnockoff;

            if (boostSlider   == null) Debug.LogWarning("BoostUI: Boost Slider is not assigned.", this);
            if (boostFillImage == null) Debug.LogWarning("BoostUI: Boost Fill Image is not assigned.", this);

            if (nearMissText != null) nearMissText.gameObject.SetActive(false);
            if (eventText    != null) eventText.gameObject.SetActive(false);
            if (boostActiveIndicator != null) boostActiveIndicator.SetActive(false);

            ApplyBarColour(BarState.Normal);
        }

        void OnDestroy()
        {
            if (boostSystem == null) return;
            boostSystem.OnNearMiss            -= HandleNearMiss;
            boostSystem.OnBoostStart          -= HandleBoostStart;
            boostSystem.OnBoostEnd            -= HandleBoostEnd;
            boostSystem.OnWheelieRegenStart   -= HandleWheelieRegenStart;
            boostSystem.OnWheelieRegenEnd     -= HandleWheelieRegenEnd;
            boostSystem.OnDriftRegenStart     -= HandleDriftRegenStart;
            boostSystem.OnDriftRegenEnd       -= HandleDriftRegenEnd;
            boostSystem.OnWheelieCombo        -= HandleWheelieCombo;
            boostSystem.OnCombatKnockoffBoost -= HandleCombatKnockoff;
        }

        void Update()
        {
            if (boostSystem == null) return;

            // Smooth the bar
            m_DisplayedMeter = Mathf.SmoothDamp(m_DisplayedMeter, boostSystem.boostMeter,
                                   ref m_MeterVel, barSmoothTime);
            if (boostSlider != null && Mathf.Abs(boostSlider.value - m_DisplayedMeter) > 0.001f)
                boostSlider.value = m_DisplayedMeter;

            // Bar colour priority, boosting, regen, normal
            BarState target = boostSystem.isBoosting ? BarState.Boosting :
                              AnyRegenActive         ? BarState.Regen    :
                                                       BarState.Normal;
            if (target != m_BarState)
                ApplyBarColour(target);

            // Near miss timer
            if (m_NearMissTimer > 0f)
            {
                m_NearMissTimer -= Time.deltaTime;
                if (m_NearMissTimer <= 0f && nearMissText != null)
                    nearMissText.gameObject.SetActive(false);
            }

            // Event popup timer
            if (m_EventTimer > 0f)
            {
                m_EventTimer -= Time.deltaTime;
                if (m_EventTimer <= 0f && eventText != null)
                    eventText.gameObject.SetActive(false);
            }
        }

        // Bar colour
        void ApplyBarColour(BarState state)
        {
            m_BarState = state;
            if (boostFillImage == null) return;
            boostFillImage.color = state == BarState.Boosting ? boostingColour :
                                   state == BarState.Regen    ? regenColour    :
                                                                normalColour;
        }

        // Event text
        void ShowEvent(string message)
        {
            if (eventText == null) return;
            eventText.text = message;
            eventText.gameObject.SetActive(true);
            m_EventTimer = eventDisplayTime;
        }

        // Boost system callbacks
        void HandleNearMiss(float amount)
        {
            if (nearMissText == null) return;
            nearMissText.text = "NEAR MISS!";
            nearMissText.gameObject.SetActive(true);
            m_NearMissTimer = nearMissDisplayTime;
        }

        void HandleBoostStart()
        {
            if (boostActiveIndicator != null)
                boostActiveIndicator.SetActive(true);
        }

        void HandleBoostEnd()
        {
            if (boostActiveIndicator != null)
                boostActiveIndicator.SetActive(false);
        }

        void HandleWheelieRegenStart()
        {
            m_WheelieRegen = true;
            ShowEvent("WHEELIE BOOST!");
        }

        void HandleWheelieRegenEnd()
        {
            m_WheelieRegen = false;
        }

        void HandleDriftRegenStart()
        {
            m_DriftRegen = true;
            ShowEvent("DRIFT BOOST!");
        }

        void HandleDriftRegenEnd()
        {
            m_DriftRegen = false;
        }

        void HandleWheelieCombo()  => ShowEvent("WHEELIE COMBO!");
        void HandleCombatKnockoff() => ShowEvent("KNOCKOFF BONUS!");
    }
}
