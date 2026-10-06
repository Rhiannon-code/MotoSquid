using MotoSquid.Audio;
using MotoSquid.Combat;
using MotoSquid.Controls;
using MotoSquid.Rider;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MotoSquid.Bike
{
    public class BoostSystem : MonoBehaviour
    {
        [Header("References")]
        public BikeController bikeController;
        public Volume postProcessVolume;

        [Header("Boost: Settings")]
        public float boostDuration      = 4f;
        public float boostSpeedMultiplier = 1.5f;
        public float boostForce         = 5f;
        public float maxBoostSpeedKmh   = 460f;

        [Header("Boost Input")]
        [SerializeField] private InputActionAsset inputActions;
        private InputAction _boost;

        // Had no device filter at all, so in split screen either pad boosted both bikes at once
        [SerializeField] private int  playerDeviceIndex = -1;
        [SerializeField] private bool lockToKeyboard    = false;
        [SerializeField] private bool allowKeyboard     = false;
        bool _inputDisabled;

        public void SetDeviceFilter(bool lockKb, int deviceIndex, bool allowKb = false)
        {
            lockToKeyboard    = lockKb;
            playerDeviceIndex = deviceIndex;
            allowKeyboard     = allowKb;
        }

        public void DisableInput() => _inputDisabled = true;
        public void EnableInput()  => _inputDisabled = false;

        [Header("Regen: Wheelie")]
        public float wheelieRegenRate   = 0.02f;
        public float wheelieRegenDelay  = 5f;

        [Header("Regen: Drift")]
        public float driftRegenRate     = 0.10f;
        public float driftRegenDelay    = 1.5f;

        [Header("Regen: Near Miss")]
        public float nearMissBoostAmount = 0.25f;
        public float nearMissMaxDistance = 3.5f;
        public float nearMissMinDistance = 0.8f;
        public LayerMask trafficLayerMask;
        public float nearMissCooldownPerVehicle = 3f;

        [Header("Regen: Wheelie Combo")]
        public float wheelieNearMissBoostMultiplier = 1.5f;

        [Header("Regen: Slipstream Combo")]
        // Extra near-miss charge while drafting, at full draft (draftCloseness = 1) a near miss is
        // worth (1 + slipstreamNearMissBonus)x. Ties slipstream + near-miss + boost into one loop
        public float slipstreamNearMissBonus = 1f;

        [Header("Regen: Combat")]
        public float combatKnockoffBoostAmount = 0.4f;
        public AudioClip combatKnockoffClip;

        [Header("Regen: Hold Tolerance")]
        // A wheelie or drift drops out for a frame or two over every bump, crest and camber change.
        // Without this window the hold timer snaps back to zero there and a multi second regen delay
        // is never reached at all
        public float regenHoldGrace     = 0.35f;

        [Header("Regen: General")]
        public float passiveRegenRate   = 0.0f;   // Off by default, set > 0 to enable

        public float passiveRegenMinSpeed = 60f;

        [Header("Debug (disable before shipping)")]
        public bool debugForceEnabled   = false;
        public float debugStartBoost    = 0f;

        [Header("Audio")]
        public AudioClip boostStartClip;
        public AudioClip nearMissClip;
        public AudioClip boostChargedClip;
        public AudioSource skidAudioSource;
        public float boostSkidPitch  = 1.6f;
        public float boostSkidVolume = 0.7f;

        // Runtime
        [HideInInspector] public float boostMeter;      // 0-1, readable by UI
        [HideInInspector] public bool  isBoosting;
        [HideInInspector] public bool  boostEnabled = false;

        float m_BaseMaxSpeed;
        float m_WheelieTimer;
        float m_DriftTimer;
        float m_BoostVelocity;          // SmoothDamp ref
        bool  m_ApplyBoostForce;       
        bool  m_WheelieRegenActive;
        bool  m_DriftRegenActive;
        float m_WheelieGrace;
        float m_DriftGrace;

        AudioSource m_OneShotSource;
        bool        m_WasCharged;
        float       m_SkidBasePitch  = 1f;
        float       m_SkidBaseVolume = 1f;

        // Near miss tracking, maps collider instance ID to cooldown timer
        System.Collections.Generic.Dictionary<int, float> m_NearMissCooldowns
            = new System.Collections.Generic.Dictionary<int, float>();
        readonly System.Collections.Generic.List<int> m_ExpiredKeys = new();

        int _nearMissFrame;
        float _nearMissElapsed;         
        const int NEAR_MISS_INTERVAL = 3;

        static readonly RaycastHit[] s_NearMissBuffer = new RaycastHit[16];

        CombatSystem m_Combat;
        SlipstreamSystem m_Slipstream;   // Optional, drafting amplifies near miss charge

        // Public events so UI/audio can react
        public System.Action<float> OnNearMiss;           // Passes boost amount awarded
        public System.Action<float> OnNearMissSide;       // Passes side direction (-1 left, +1 right)
        public System.Action        OnBoostStart;
        public System.Action        OnBoostEnd;
        public System.Action        OnWheelieRegenStart;  // Wheelie held long enough to start regen
        public System.Action        OnWheelieRegenEnd;
        public System.Action        OnDriftRegenStart;    // Drift held long enough to start regen
        public System.Action        OnDriftRegenEnd;
        public System.Action        OnWheelieCombo;       // Near miss multiplier applied during wheelie
        public System.Action        OnCombatKnockoffBoost;

        void OnEnable()  => _boost?.Enable();

        void OnDisable()
        {
            _boost?.Disable();

            // Don't leave the bike stuck at boosted top speed if it's disabled mid-boost (race finished,
            // pooled, etc.). Mirrors AIBoostSystem.OnDisable. Guarded against firing before Start
            if (bikeController != null && m_BaseMaxSpeed > 0f)
            {
                bikeController.bikeSettings.maxSpeed = m_BaseMaxSpeed;
                isBoosting = false;
            }
        }

        void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("BoostSystem: Input Actions asset is not assigned in the Inspector.", this);
                return;
            }
            _boost = inputActions.FindActionMap("Bike", throwIfNotFound: true)
                                 .FindAction("Boost",  throwIfNotFound: true);
        }

        void Start()
        {
            if (bikeController == null)
                bikeController = GetComponentInParent<BikeController>(true);

            if (bikeController == null)
            {
                Debug.LogError("BoostSystem: BikeController not found.");
                enabled = false;
                return;
            }

            m_BaseMaxSpeed = bikeController.bikeSettings.maxSpeed;
#if UNITY_EDITOR
            boostMeter = debugStartBoost;
            if (debugForceEnabled) boostEnabled = true;
#endif

            // Use existing AudioSource if pre added in the prefab, otherwise create one
            m_OneShotSource = gameObject.GetComponent<AudioSource>();
            if (m_OneShotSource == null)
                m_OneShotSource = gameObject.AddComponent<AudioSource>();
            m_OneShotSource.playOnAwake = false;
            m_OneShotSource.spatialBlend = 0f;   // 2D post process style SFX

            // Capture skid source base values so we can restore them after boost
            if (skidAudioSource != null)
            {
                m_SkidBasePitch  = skidAudioSource.pitch;
                m_SkidBaseVolume = skidAudioSource.volume;
            }

            m_Combat = GetComponentInParent<CombatSystem>(true);
            if (m_Combat != null)
                m_Combat.onDealtKnockoff += HandleDealtKnockoff;

            // Sibling on the same bike (may be absent). Used to amplify near miss charge while drafting
            m_Slipstream = bikeController.GetComponentInChildren<SlipstreamSystem>(true);
        }

        void FixedUpdate()
        {
            if (bikeController == null || bikeController.bikeReferences.BikeRb == null) return;

            if (m_ApplyBoostForce)
            {
                Vector3 boostDir = Vector3.ProjectOnPlane(
                    bikeController.bikeReferences.Rotator.forward, Vector3.up).normalized;
                bikeController.bikeReferences.BikeRb.AddForce(
                    boostDir * boostForce,
                    ForceMode.Acceleration);
            }

            // Hard speed ceiling while boosting: the boost force is otherwise uncapped, so clamp the
            // horizontal velocity to maxBoostSpeedKmh (Un boosted, the bike's own maxSpeed governs)
            if (isBoosting)
            {
                Rigidbody rb = bikeController.bikeReferences.BikeRb;
                Vector3 v = rb.linearVelocity;
                Vector3 horizontal = new Vector3(v.x, 0f, v.z);
                float cap = maxBoostSpeedKmh / 3.6f;
                if (horizontal.magnitude > cap)
                {
                    horizontal = horizontal.normalized * cap;
                    rb.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);
                }
            }
        }

        void Update()
        {
            if (bikeController == null) return;

            float speedKmh = bikeController.localBikeVelocity.magnitude * 3.6f;
            bool  wantsBoost = !_inputDisabled && InputDeviceFilter.Pressed(
                                   _boost, lockToKeyboard, playerDeviceIndex, allowKeyboard);

            // Activation
            bool wasBosting = isBoosting;
            isBoosting = boostEnabled && wantsBoost && boostMeter > 0.01f && bikeController.canAccelerate;

            if (isBoosting && !wasBosting)
            {
                OnBoostStart?.Invoke();
                // Only play on the rising edge, held boost does not retrigger
                PlayOneShot(boostStartClip);
            }

            if (!isBoosting && wasBosting)
                OnBoostEnd?.Invoke();

            // Drive skid sound while boosting
            if (skidAudioSource != null)
            {
                if (isBoosting)
                {
                    skidAudioSource.pitch  = boostSkidPitch;
                    skidAudioSource.volume = boostSkidVolume;
                    AudioLoop.EnsurePlaying(skidAudioSource, "boost skid restart", this);
                }
                else
                {
                    skidAudioSource.pitch  = m_SkidBasePitch;
                    skidAudioSource.volume = m_SkidBaseVolume;
                }
            }

            // Speed override
            // Raise maxSpeed while boosting, restore immediately when done
            // Using the settings field directly BikeController reads it each tick
            float targetMax = isBoosting
                ? Mathf.Min(m_BaseMaxSpeed * boostSpeedMultiplier, maxBoostSpeedKmh / 3.6f)
                : m_BaseMaxSpeed;
            bikeController.bikeSettings.maxSpeed = Mathf.SmoothDamp(
                bikeController.bikeSettings.maxSpeed, targetMax,
                ref m_BoostVelocity, 0.15f);


            m_ApplyBoostForce = isBoosting && bikeController.bikeIsGrounded;

            // Drain
            if (isBoosting)
                boostMeter = Mathf.Max(0f, boostMeter - (1f / boostDuration) * Time.deltaTime);

            // Regen wheelie
            bool prevWheelieRegen = m_WheelieRegenActive;
            if (HoldActive(bikeController.isDoingWheelie, ref m_WheelieGrace) && !isBoosting)
            {
                m_WheelieTimer += Time.deltaTime;
                m_WheelieRegenActive = m_WheelieTimer >= wheelieRegenDelay;
                if (m_WheelieRegenActive)
                    boostMeter = Mathf.Min(1f, boostMeter + wheelieRegenRate * Time.deltaTime);
            }
            else
            {
                m_WheelieTimer       = 0f;
                m_WheelieRegenActive = false;
            }
            if (m_WheelieRegenActive && !prevWheelieRegen) OnWheelieRegenStart?.Invoke();
            if (!m_WheelieRegenActive && prevWheelieRegen)  OnWheelieRegenEnd?.Invoke();

            // Regen drift
            bool prevDriftRegen = m_DriftRegenActive;
            if (HoldActive(bikeController.isDrifting, ref m_DriftGrace) && !isBoosting)
            {
                m_DriftTimer += Time.deltaTime;
                m_DriftRegenActive = m_DriftTimer >= driftRegenDelay;
                if (m_DriftRegenActive)
                    boostMeter = Mathf.Min(1f, boostMeter + driftRegenRate * Time.deltaTime);
            }
            else
            {
                m_DriftTimer       = 0f;
                m_DriftRegenActive = false;
            }
            if (m_DriftRegenActive && !prevDriftRegen) OnDriftRegenStart?.Invoke();
            if (!m_DriftRegenActive && prevDriftRegen)  OnDriftRegenEnd?.Invoke();

            // Regen: passive
            if (!isBoosting && passiveRegenRate > 0f && speedKmh >= passiveRegenMinSpeed)
                boostMeter = Mathf.Min(1f, boostMeter + passiveRegenRate * Time.deltaTime);

            // Charged notification
            bool isCharged = boostMeter >= 0.99f;
            if (isCharged && !m_WasCharged && !isBoosting)
                PlayOneShot(boostChargedClip);
            m_WasCharged = isCharged;

            // Near miss detection
            TickNearMiss();

            // Cooldown timers
            TickCooldowns();

        }

        bool HoldActive(bool held, ref float grace)
        {
            if (held)
            {
                grace = regenHoldGrace;
                return true;
            }

            grace -= Time.deltaTime;
            return grace > 0f;
        }

        // Near miss
        void TickNearMiss()
        {
            _nearMissElapsed += Time.deltaTime;
            if (++_nearMissFrame < NEAR_MISS_INTERVAL) return;
            _nearMissFrame = 0;
            float intervalElapsed = _nearMissElapsed;
            _nearMissElapsed = 0f;

            // SphereCastAll along velocity vector, catches vehicles the bike swept
            // past at high speed between frames that OverlapSphere would miss entirely
            Vector3 velocity = bikeController.bikeReferences.BikeRb.linearVelocity;
            float   speed    = velocity.magnitude;
            Vector3 castDir  = speed > 0.1f ? velocity.normalized : bikeController.transform.forward;

            // Swept distance covers everything traversed since the last check
            float   castDist = speed * intervalElapsed + nearMissMaxDistance;

            int hitCount = Physics.SphereCastNonAlloc(
                transform.position, nearMissMaxDistance, castDir, s_NearMissBuffer, castDist, trafficLayerMask);

            float closestDist = float.MaxValue;
            float closestSide = 1f;
            bool  anyNearMiss = false; 

            for (int _i = 0; _i < hitCount; _i++)
            {
                var hit = s_NearMissBuffer[_i];
                int id = hit.collider.GetInstanceID();
                if (m_NearMissCooldowns.ContainsKey(id)) continue;

                // Use lateral distance (perpendicular to travel) not raw distance
                Vector3 toVehicle   = hit.collider.transform.position - transform.position;
                float   lateralDist = Vector3.ProjectOnPlane(toVehicle,
                                          bikeController.transform.forward).magnitude;

                if (lateralDist < nearMissMinDistance || lateralDist > nearMissMaxDistance) continue;

                float award = nearMissBoostAmount * Mathf.InverseLerp(
                    nearMissMaxDistance, nearMissMinDistance, lateralDist);

                if (bikeController.isDoingWheelie)
                {
                    award *= wheelieNearMissBoostMultiplier;
                    OnWheelieCombo?.Invoke();
                }

                m_NearMissCooldowns[id] = nearMissCooldownPerVehicle;
                AddChargeFromNearMiss(award);
                anyNearMiss = true;

                // Track closest for FX side direction
                if (lateralDist < closestDist)
                {
                    closestDist = lateralDist;
                    closestSide = Vector3.Dot(transform.right, toVehicle) >= 0f ? 1f : -1f;
                }
            }

            if (anyNearMiss)
                PlayOneShot(nearMissClip);

            if (closestDist < float.MaxValue)
                OnNearMissSide?.Invoke(closestSide);
        }

        void TickCooldowns()
        {
            m_ExpiredKeys.Clear();
            foreach (var kvp in m_NearMissCooldowns)
                m_ExpiredKeys.Add(kvp.Key);

            for (int i = 0; i < m_ExpiredKeys.Count; i++)
            {
                int key = m_ExpiredKeys[i];
                float remaining = m_NearMissCooldowns[key] - Time.deltaTime;
                if (remaining <= 0f)
                    m_NearMissCooldowns.Remove(key);
                else
                    m_NearMissCooldowns[key] = remaining;
            }
        }

        // Public API
        public void AwardBoost(float amount)
        {
            boostMeter = Mathf.Min(1f, boostMeter + amount);
        }

        public void AddChargeFromNearMiss(float amount)
        {
            if (m_Slipstream != null && m_Slipstream.inSlipstream)
                amount *= 1f + slipstreamNearMissBonus * Mathf.Clamp01(m_Slipstream.draftCloseness);

            boostMeter = Mathf.Min(1f, boostMeter + amount);
            OnNearMiss?.Invoke(amount);
        }

        void PlayOneShot(AudioClip clip)
        {
            if (clip != null && m_OneShotSource != null)
                m_OneShotSource.PlayOneShot(clip);
                AudioBurstLog.Note($"boost ({clip.name})", this);
        }

        void HandleDealtKnockoff()
        {
            AwardBoost(combatKnockoffBoostAmount);
            PlayOneShot(combatKnockoffClip);
            OnCombatKnockoffBoost?.Invoke();
        }

        void OnDestroy()
        {
            if (bikeController != null)
                bikeController.bikeSettings.maxSpeed = m_BaseMaxSpeed;
            if (m_Combat != null)
                m_Combat.onDealtKnockoff -= HandleDealtKnockoff;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, nearMissMaxDistance);
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, nearMissMinDistance);
        }
    }
}
