using MotoSquid.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotoSquid.Bike
{
    public class BikeSpeedFX : MonoBehaviour
    {
        [Header("References")]
        public BikeController bikeController;
        public BoostSystem boostSystem;
        public Volume postProcessVolume;

        [Header("Speed Thresholds (km/h)")]
        public float effectStartSpeed = 40f;
        public float effectFullSpeed  = 300f;

        public float blurStartSpeed   = 140f;
        public float blurFullSpeed    = 300f;
        public float linesStartSpeed  = 240f;
        public float linesFullSpeed   = 300f;

        [Header("Smoothing")]
        public float rampUpTime   = 0.12f;
        public float rampDownTime = 0.35f;
        public float brakeSmoothTime = 0.2f;

        [Header("Brake Override")]
        [Range(0f, 1f)]
        public float brakeIntensityScale = 0.3f;


        // Vignette pulse on acceleration
        [Header("Vignette Pulse")]
        [Range(0f, 1f)]
        public float vignettePulseStrength   = 0.45f;
        public float vignettePulseFullAccel  = 40f;
        public float vignettePulseSmoothTime = 0.15f;

        [Header("Split Screen")]
        [SerializeField] private int postProcessVolumeLayer = -1;

        // The FX isolation layer this player's Volume lives on (Player1FX/Player2FX), so the
        // matching CameraController can include only its own player's FX in its volume stack.
        // -1 = no isolation (Volume stays on Default and is shared by all cameras)
        public int FXLayer => postProcessVolumeLayer;

        // Both players spawn from the same prefab, so the prefab cannot know which player it will be,
        // it ships P1's layer and P2 would inherit it, putting both FX volumes on one layer and collapsing
        // the isolation entirely. SplitScreenSetup assigns this per slot at bind time
        public void SetFXLayer(int layer)
        {
            postProcessVolumeLayer = layer;

            if (postProcessVolume == null || layer < 0) return;

            // Toggled so HDRP re-registers the volume against its new layer
            postProcessVolume.enabled = false;
            postProcessVolume.gameObject.layer = layer;
            postProcessVolume.enabled = true;
        }

        [Header("Boost Over-Speed")]
        public float overSpeedSmoothTime = 0.25f;

        [Header("Gear Shift Punch")]
        public float gearBarrelPulse = 0.16f;
        public float gearBarrelDecay = 0.07f;

        BikeController m_Bike;
        SpeedPostProcess         m_FX;

        float m_CurrentIntensity;
        float m_SpeedKmh;
        float m_SpeedVel;
        float m_Brake;
        float m_BrakeVel;
        float m_VignettePulse;
        float m_VignettePulseVel;
        float m_OverSpeed;
        float m_OverSpeedVel;
        float m_LastSpeedKmh;
        int   m_LastGearFX;
        float m_GearBarrelTimer;
        bool  m_FXReset;

        // Expose current smoothed intensity so CameraController can read it
        public float currentIntensity => m_CurrentIntensity;

        void Start()
        {
            // Try the explicit reference first, then search parents as fallback
            if (bikeController == null)
                bikeController = GetComponentInParent<BikeController>(includeInactive: true);

            if (bikeController == null)
            {
                Debug.LogError("BikeSpeedFX: BikeController not found. " +
                               "Assign it manually in the Inspector.");
                enabled = false;
                return;
            }

            m_Bike = bikeController;

            // If the assigned volume is from a different bike (cross-instance scene wiring),
            // find the Volume that actually lives on this bike's hierarchy.
            if (postProcessVolume == null ||
                !postProcessVolume.transform.IsChildOf(m_Bike.transform.root))
            {
                postProcessVolume = m_Bike.GetComponentInChildren<Volume>(true);
            }

            // Move this bike's Volume to its designated isolation layer so cameras with the
            // matching volumeLayerMask see only their own player's effects, both in single-player
            // (where P2's volume would otherwise contaminate P1's stack) and in split-screen.
            // postProcessVolumeLayer is set per-player in the scene (16 for P1, 17 for P2).
            // A value of -1 means no layer override; the volume stays on Default (layer 0).
            // IMPORTANT: changing gameObject.layer alone doesn't update HDRP's VolumeManager,
            // we must disable → change → re-enable so it unregisters/re-registers on the new layer.
            if (postProcessVolume != null && postProcessVolumeLayer >= 0)
            {
                postProcessVolume.enabled = false;
                postProcessVolume.gameObject.layer = postProcessVolumeLayer;
                postProcessVolume.enabled = true;
            }

            // Auto-find boost system if not assigned
            if (boostSystem == null)
                boostSystem = GetComponentInParent<BoostSystem>(includeInactive: true);

            if (postProcessVolume == null)
            {
                Debug.LogWarning("BikeSpeedFX: no Volume assigned.");
                enabled = false;
                return;
            }

            // Clone so each player has their own profile instance, prevents split-screen
            // cross-contamination where both bikes write to the same shared ScriptableObject.
            postProcessVolume.profile = Instantiate(postProcessVolume.profile);

            if (!postProcessVolume.profile.TryGet(out m_FX))
            {
                Debug.LogWarning("BikeSpeedFX: SpeedPostProcess not found in Volume profile.");
                enabled = false;
                return;
            }

            m_FX.active = true;

            m_LastGearFX = m_Bike.currentGear;
        }

        void Update()
        {
            if (m_FX == null) return;

            // Suppress all speed FX in two cases, driving every parameter back to neutral so nothing
            // lingers in the global custom-PP pass (Update() never zeroes values on its own, it just
            // stops refreshing them):
            //   1. The Volume (or its GameObject) was disabled. BikeSpeedFX lives on a *different*
            //      GameObject than the global Volume, so it keeps running and the last values would
            //      otherwise stay stuck on screen.
            //   2. The race hasn't started yet (canMove == false during the countdown). The player can
            //      still rev on the line, but revving must NOT trigger the post-process effects, they
            //      only kick in once the race actually starts.
            bool volumeOff   = postProcessVolume == null || !postProcessVolume.isActiveAndEnabled;
            bool raceNotLive = m_Bike == null || !m_Bike.canMove;
            if (volumeOff || raceNotLive)
            {
                if (!m_FXReset) ResetFX();
                return;
            }
            if (m_FXReset)
            {
                m_FX.active = true;
                m_FXReset   = false;
            }

            // Accessibility: read live each frame (these effects are re-applied every frame, so
            // toggling the setting mid-race takes effect immediately). Default to "show" when no
            // manager is present in the scene.
            var access          = AccessibilityManager.Instance;
            bool motionBlurOn   = access == null || access.MotionBlurEnabled;
            bool epiSafeMode    = access != null && access.EpiSafeMode;

            // Every speed-derived ramp reads one smoothed speed rather than smoothing its own
            // output. The rigidbody velocity collapses in a single physics step on a hard impact,
            // and canAccelerate drops for 60 ms on every upshift smoothing per-ramp let each of
            // those step the ramps independently, which is what made the fades stutter.
            float rawKmh = m_Bike.localBikeVelocity.magnitude * 3.6f;
            m_SpeedKmh   = Mathf.SmoothDamp(m_SpeedKmh, rawKmh, ref m_SpeedVel,
                               rawKmh > m_SpeedKmh ? rampUpTime : rampDownTime);

            // HandBrake is analog, so ease on its value instead of a >0.5 test that stepped the
            // master the instant the brake was touched.
            m_Brake = Mathf.SmoothDamp(m_Brake, Mathf.Clamp01(m_Bike.bikeInput.HandBrake),
                          ref m_BrakeVel, brakeSmoothTime);

            float Ramp(float start, float full) =>
                Mathf.Clamp01((m_SpeedKmh - start) / Mathf.Max(full - start, 1f));

            // ── Master intensity ──────────────────────────────────────────────────
            m_CurrentIntensity = Ramp(effectStartSpeed, effectFullSpeed) *
                                 Mathf.Lerp(1f, brakeIntensityScale, m_Brake);
            m_FX.intensity.Override(m_CurrentIntensity);

            // ── Vignette pulse driven by acceleration ───────────────────────────
            float accelKmhPerSec = (m_SpeedKmh - m_LastSpeedKmh) / Mathf.Max(Time.deltaTime, 0.0001f);
            float accelT         = Mathf.Clamp01(accelKmhPerSec / Mathf.Max(vignettePulseFullAccel, 1f));

            float pulseTarget = 0f;
            if (accelKmhPerSec > 0f)
                pulseTarget = accelT * vignettePulseStrength * m_CurrentIntensity * (1f - m_Brake);

            m_VignettePulse = Mathf.SmoothDamp(m_VignettePulse, pulseTarget,
                                  ref m_VignettePulseVel, vignettePulseSmoothTime);
            m_FX.vignettePulse.Override(m_VignettePulse);

            // ── Ground motion blur ────────────────────────────────────────────────
            // Accessibility: motion-blur off forces zero regardless of speed.
            m_FX.blurIntensity.Override(motionBlurOn ? Ramp(blurStartSpeed, blurFullSpeed) : 0f);

            // ── Speed ramp lines and vignette fade in together above linesStartSpeed ─
            float speedRamp = Ramp(linesStartSpeed, linesFullSpeed);
            // Accessibility: EpiSafe mode suppresses the strobing speed-lines layer, but the
            // vignette still carries the speed read.
            m_FX.speedLinesIntensity.Override(epiSafeMode ? 0f : speedRamp);
            m_FX.vignetteIntensity.Override(speedRamp);

            // ── Boost over-speed past the bike's normal top speed. Deepens the vignette and
            // bleeds the colour grade back in; nothing else responds to it.
            float overTarget = 0f;
            if (boostSystem != null && m_Bike.bikeSettings != null)
                overTarget = Mathf.Clamp01(Mathf.InverseLerp(m_Bike.bikeSettings.maxSpeed * 3.6f,
                                 boostSystem.maxBoostSpeedKmh, m_SpeedKmh));
            m_OverSpeed = Mathf.SmoothDamp(m_OverSpeed, overTarget, ref m_OverSpeedVel, overSpeedSmoothTime);
            m_FX.overSpeedIntensity.Override(m_OverSpeed);

            // ── Centralised barrel pulse boost warp + gear-shift release spike ────
            // This is the sole writer of barrelPulse (BoostSystem no longer touches it), so
            // the two sources can't stomp each other frame-to-frame.
            if (m_Bike.currentGear != m_LastGearFX)
            {
                m_GearBarrelTimer = gearBarrelDecay;
                m_LastGearFX = m_Bike.currentGear;
            }
            float gearBarrel = 0f;
            if (m_GearBarrelTimer > 0f)
            {
                m_GearBarrelTimer -= Time.deltaTime;
                gearBarrel = gearBarrelPulse * Mathf.Clamp01(m_GearBarrelTimer / Mathf.Max(gearBarrelDecay, 0.001f));
            }
            float boostBarrel = (boostSystem != null && boostSystem.isBoosting) ? boostSystem.boostMeter * 0.3f : 0f;
            m_FX.barrelPulse.Override(Mathf.Max(boostBarrel, gearBarrel));

            m_LastSpeedKmh = m_SpeedKmh;
        }

        // Drives every runtime-controlled parameter back to neutral and deactivates the override
        // so the global custom-PP pass reports IsActive() == false and is skipped entirely. Called
        // when the Volume is disabled so disabling it actually turns the effect off on screen.
        // Smoothing state is also reset so re-enabling ramps up from zero instead of snapping.
        void ResetFX()
        {
            m_FX.intensity.Override(0f);
            m_FX.blurIntensity.Override(0f);
            m_FX.speedLinesIntensity.Override(0f);
            m_FX.vignetteIntensity.Override(0f);
            m_FX.overSpeedIntensity.Override(0f);
            m_FX.vignettePulse.Override(0f);
            m_FX.barrelPulse.Override(0f);
            m_FX.nearMissBlurStrength.Override(0f);
            m_FX.active = false;

            m_CurrentIntensity = 0f;
            m_SpeedKmh         = 0f; m_SpeedVel          = 0f;
            m_Brake            = 0f; m_BrakeVel          = 0f;
            m_LastSpeedKmh     = 0f;
            m_OverSpeed        = 0f; m_OverSpeedVel      = 0f;
            m_VignettePulse    = 0f; m_VignettePulseVel   = 0f;
            m_GearBarrelTimer  = 0f;

            m_FXReset = true;
        }
    }
}
