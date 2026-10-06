using MotoSquid.Bike;
using MotoSquid.Combat;
using MotoSquid.Rider;
using UnityEngine;
using System.Collections.Generic;

namespace MotoSquid.AI
{
    public class AIBoostSystem : MonoBehaviour
    {
        [Header("References")]
        public BikeAIController bikeController;

        [Header("Boost Feel")]
        public float boostDuration        = 4f;
        public float boostSpeedMultiplier = 1.5f;
        // Additive forward shove while boosting, real top speed is the raised maxSpeed + maxBoostSpeedKmh
        // clamp, so this only sets ramp speed toward the cap. Matches the tuned prefab value + player BoostSystem
        public float boostForce           = 5f;
        public float speedSmoothTime      = 0.35f;
        public float maxBoostSpeedKmh     = 460f;

        [Header("Meter")]
        public float startMeter           = 0f;

        [Header("Near Miss Regen")]
        public float nearMissBoostAmount  = 0.25f;
        public float nearMissMaxDistance  = 3.5f;
        public float nearMissMinDistance  = 0.8f;
        public LayerMask trafficLayerMask;
        public float nearMissCooldownPerVehicle = 3f;

        [Header("Regen: Drift")]
        public float driftRegenRate = 0.10f;

        [Header("Regen: Passive")]
        // A trickle, so an AI on a clean empty stretch still banks a boost eventually. Fixing the traffic
        // layer mask made this more necessary, not less: the AI now avoids traffic properly and so passes
        // it wider, earning fewer near misses than when it was ploughing through
        public float passiveRegenRate     = 0.03f;
        public float passiveRegenMinSpeed = 60f;

        [Header("Combat Regen")]
        public float combatKnockoffBoostAmount = 0.4f;

        [Header("Activation Policy")]
        public float activateThreshold    = 0.6f;
        public float maxSteerToBoost      = 0.25f;
        public float maxSteerHardCutoff   = 0.45f;
        public bool  boostToOvertake      = true;
        public bool  requireOvertakeToBoost = false;
        public float dodgeBoostCutoff     = 0.5f;
        public float noBoostAtStartSeconds = 20f;

        // Runtime (readable by UI/debug)
        [HideInInspector] public float boostMeter;
        [HideInInspector] public bool  isBoosting;

        BikeAILogic m_AiLogic;   // Resolved from bikeController.aiLogic in Start
        CombatSystem m_Combat;   // Sibling combat system, for knockoff/forced crash boost
        float m_RaceTime;                 // Seconds the AI has been racing (counts only while it can accelerate)
        float m_BaseMaxSpeed;             // Original (pre rubber band) top speed, restored on disable
        float m_RubberBandMult = 1f;      // set by BikeAILogic's rubber band, scales the natural top speed
        float m_BoostVelocity;            // SmoothDamp ref
        float m_RecoveryTimer;            // > 0 while catching up after a reset
        bool  m_ApplyBoostForce;          // Set in Update, consumed in FixedUpdate (frame rate independent)

        // Near miss tracking, mirrors BoostSystem.TickNearMiss()
        readonly Dictionary<int, float> m_NearMissCooldowns = new();
        readonly List<int> m_ExpiredKeys = new();
        int   _nearMissFrame;
        float _nearMissElapsed;
        const int NEAR_MISS_INTERVAL = 3;
        static readonly RaycastHit[] s_NearMissBuffer = new RaycastHit[16];

        void Start()
        {
            if (bikeController == null)
                bikeController = GetComponentInParent<BikeAIController>(true);

            if (bikeController == null)
            {
                Debug.LogError("AIBoostSystem: BikeAIController not found.", this);
                enabled = false;
                return;
            }

            m_BaseMaxSpeed = bikeController.bikeSettings.maxSpeed;
            boostMeter     = Mathf.Clamp01(startMeter);
            m_AiLogic      = bikeController.aiLogic;

            // Fall back to the AI logic's traffic mask so near-miss detection works without
            // separately assigning the mask on this component in the Inspector
            if (trafficLayerMask == 0 && m_AiLogic != null)
                trafficLayerMask = m_AiLogic.trafficLayerMask;

            // Earn boost when this AI knocks an opponent off or forces them into a crash (same event
            // the player BoostSystem consumes)
            m_Combat = bikeController.GetComponentInChildren<CombatSystem>(true);
            if (m_Combat != null) m_Combat.onDealtKnockoff += HandleDealtKnockoff;
        }

        void HandleDealtKnockoff() => AwardBoost(combatKnockoffBoostAmount);
        public void AwardBoost(float amount) => boostMeter = Mathf.Min(1f, boostMeter + amount);
        public void ConfigureForDifficulty(float speedMultiplier, float force, float nearMissAward,
                                           float nearMissCooldown)
        {
            boostSpeedMultiplier = speedMultiplier;
            boostForce           = force;
            nearMissBoostAmount  = nearMissAward;
            nearMissCooldownPerVehicle = nearMissCooldown;
        }
        public void BeginRecovery(float seconds) => m_RecoveryTimer = Mathf.Max(m_RecoveryTimer, seconds);

        // Set by BikeAILogic's rubber band, scales this AI's natural (un-boosted) top speed so a trailing
        // AI can exceed the player's cap to catch up, and a leading AI tops out slower to stay catchable
        // Lifted only for an AI far enough behind to be off screen, see BikeAILogic.rubberBandFarGap
        float CeilingMs() => maxBoostSpeedKmh / 3.6f * (m_AiLogic != null ? m_AiLogic.CatchUpCeilingMultiplier : 1f);

        public void SetRubberBandMultiplier(float mult) => m_RubberBandMult = Mathf.Clamp(mult, 0.6f, 2f);

        void Regen()
        {
            if (!bikeController.canAccelerate) return;

            if (driftRegenRate > 0f && bikeController.isAIDrifting)
                boostMeter = Mathf.Min(1f, boostMeter + driftRegenRate * Time.deltaTime);

            if (passiveRegenRate > 0f &&
                Mathf.Abs(bikeController.localBikeVelocity.z) * 3.6f >= passiveRegenMinSpeed)
                boostMeter = Mathf.Min(1f, boostMeter + passiveRegenRate * Time.deltaTime);
        }

        void Update()
        {
            if (bikeController == null) return;

            if (m_RecoveryTimer > 0f) m_RecoveryTimer -= Time.deltaTime;

            // Count race time only while actually racing (not during the countdown), so the
            // no boost at start window measures real racing time
            if (bikeController.canAccelerate) m_RaceTime += Time.deltaTime;

            TickNearMiss();
            TickCooldowns();

            // Decide whether to boost
            // Traffic avoidance always wins, never boost into a dodge
            bool dodging = m_AiLogic != null && m_AiLogic.DodgeUrgency > dodgeBoostCutoff;

            bool baseOk = !dodging
                       && bikeController.bikeIsGrounded
                       && bikeController.canAccelerate
                       && !bikeController.isBraking
                       && bikeController.throttle > 0f;

            bool overtaking  = boostToOvertake && m_AiLogic != null && m_AiLogic.IsOvertaking;
            bool recovering  = m_RecoveryTimer > 0f;

            // Overtaking and recovery bypass the straight only steer limit (both inherently steer)
            bool contextOk = overtaking || recovering
                          || (!requireOvertakeToBoost && Mathf.Abs(bikeController.steerInput) <= maxSteerToBoost);

            // No boost at all for the opening seconds of the race.
            bool startWindowOver = m_RaceTime >= noBoostAtStartSeconds;

            // Hard steer cut, even overtaking/recovering must not boost into a sharp turn, or the
            // forward shove pushes the bike wide and it spins out
            bool steerWithinHardLimit = Mathf.Abs(bikeController.steerInput) <= maxSteerHardCutoff;

            bool canBoost = baseOk && contextOk && startWindowOver && steerWithinHardLimit;

            // Hysteresis, need a healthy meter to START, but while recovering, spend any charge at all
            if (isBoosting || recovering)
                isBoosting = canBoost && boostMeter > 0.01f;
            else
                isBoosting = canBoost && boostMeter >= activateThreshold;

            // Drain
            if (isBoosting)
                boostMeter = Mathf.Max(0f, boostMeter - (1f / boostDuration) * Time.deltaTime);
            else
                Regen();

            // Rubber band scales the natural (un boosted) top speed, boost then layers on top of that.
            // maxBoostSpeedKmh caps BOTH, a hard rubber band alone could otherwise put a trailing AI's
            // unboosted top speed above the ceiling the player can reach on full boost
            float natural   = m_BaseMaxSpeed * m_RubberBandMult;
            float targetMax = Mathf.Min(isBoosting ? natural * boostSpeedMultiplier : natural, CeilingMs());
            bikeController.bikeSettings.maxSpeed = Mathf.SmoothDamp(
                bikeController.bikeSettings.maxSpeed, targetMax,
                ref m_BoostVelocity, speedSmoothTime);

            m_ApplyBoostForce = isBoosting && bikeController.bikeIsGrounded;
        }

        void FixedUpdate()
        {
            if (bikeController == null || bikeController.bikeReferences.BikeRb == null) return;

            if (m_ApplyBoostForce)
            {
                Vector3 boostDir = Vector3.ProjectOnPlane(
                    bikeController.bikeReferences.Rotator.forward, Vector3.up).normalized;
                bikeController.bikeReferences.BikeRb.AddForce(
                    boostDir * boostForce, ForceMode.Acceleration);
            }

            // Hard speed ceiling while boosting, the boost force is otherwise uncapped, so clamp the
            // horizontal velocity to maxBoostSpeedKmh
            if (isBoosting)
            {
                Rigidbody rb = bikeController.bikeReferences.BikeRb;
                Vector3 v = rb.linearVelocity;
                Vector3 horizontal = new Vector3(v.x, 0f, v.z);
                float cap = CeilingMs();
                if (horizontal.magnitude > cap)
                {
                    horizontal = horizontal.normalized * cap;
                    rb.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);
                }
            }
        }

        // Near miss
        // Mirrors BoostSystem.TickNearMiss(): sweep the velocity vector against traffic and
        // award boost for squeezing past a vehicle without hitting it
        void TickNearMiss()
        {
            if (trafficLayerMask == 0 || bikeController.bikeReferences.BikeRb == null) return;

            _nearMissElapsed += Time.deltaTime;
            if (++_nearMissFrame < NEAR_MISS_INTERVAL) return;
            _nearMissFrame = 0;
            float intervalElapsed = _nearMissElapsed;
            _nearMissElapsed = 0f;

            Vector3 velocity = bikeController.bikeReferences.BikeRb.linearVelocity;
            float   speed    = velocity.magnitude;
            Vector3 fwd      = bikeController.bikeReferences.Rotator.forward;
            Vector3 castDir  = speed > 0.1f ? velocity.normalized : fwd;
            float   castDist = speed * intervalElapsed + nearMissMaxDistance;

            int hitCount = Physics.SphereCastNonAlloc(
                transform.position, nearMissMaxDistance, castDir, s_NearMissBuffer, castDist, trafficLayerMask);

            for (int i = 0; i < hitCount; i++)
            {
                var hit = s_NearMissBuffer[i];
                int id  = hit.collider.GetInstanceID();
                if (m_NearMissCooldowns.ContainsKey(id)) continue;

                // Lateral distance (perpendicular to travel), not raw distance
                Vector3 toVehicle   = hit.collider.transform.position - transform.position;
                float   lateralDist = Vector3.ProjectOnPlane(toVehicle, fwd).magnitude;
                if (lateralDist < nearMissMinDistance || lateralDist > nearMissMaxDistance) continue;

                float award = nearMissBoostAmount *
                              Mathf.InverseLerp(nearMissMaxDistance, nearMissMinDistance, lateralDist);
                boostMeter = Mathf.Min(1f, boostMeter + award);
                m_NearMissCooldowns[id] = nearMissCooldownPerVehicle;
            }
        }

        void TickCooldowns()
        {
            if (m_NearMissCooldowns.Count == 0) return;

            m_ExpiredKeys.Clear();
            foreach (var kvp in m_NearMissCooldowns) m_ExpiredKeys.Add(kvp.Key);

            for (int i = 0; i < m_ExpiredKeys.Count; i++)
            {
                int key = m_ExpiredKeys[i];
                float remaining = m_NearMissCooldowns[key] - Time.deltaTime;
                if (remaining <= 0f) m_NearMissCooldowns.Remove(key);
                else                 m_NearMissCooldowns[key] = remaining;
            }
        }

        void OnDisable()
        {
            // Don't leave the bike stuck at boosted top speed if it's disabled mid boost
            // (race finished, pooled, etc.). Guard against OnDisable firing before Start
            if (bikeController != null && m_BaseMaxSpeed > 0f)
            {
                bikeController.bikeSettings.maxSpeed = m_BaseMaxSpeed;
                isBoosting = false;
            }
        }

        void OnDestroy()
        {
            if (bikeController != null && m_BaseMaxSpeed > 0f)
                bikeController.bikeSettings.maxSpeed = m_BaseMaxSpeed;
            if (m_Combat != null) m_Combat.onDealtKnockoff -= HandleDealtKnockoff;
        }
    }
}
