using MotoSquid.AI;
using MotoSquid.Combat;
using UnityEngine;

namespace MotoSquid.Bike
{
public class RivalPull : MonoBehaviour
{
    [Header("Draft Lock")]
    public float draftLateralPull = 12f;   // m/s^2 toward the draft centreline at full strength
    public float draftDeadzone    = 0.2f;  // Metres of free play before the lock pulls at all
    public float draftHoldGap     = 3.5f;  // Metres behind the target the lock settles at
    public float draftGapGain     = 3f;    // m/s^2 per metre of gap error
    public float draftTow         = 8f;    // Cap on the closing half of the gap hold
    public float draftEase        = 3f;    // Cap on the backing-off half, deliberately much smaller

    [Header("Combat Hold")]
    public float combatRange    = 5f;
    public float combatStandoff = 1.2f;
    public float combatLinePull = 8f;
    public float combatSidePull = 6f;

    [Header("Authority")]
    public float steerRelease = 0.4f;      // Steering harder than this hands control fully back
    public float dodgeRelease = 0.33f;     // AI only, matches where the other racing biases zero out
    public float minSpeedKmh  = 80f;

    BikeController   m_Player;
    BikeAIController m_Ai;
    BikeAILogic      m_AiLogic;
    CombatAI         m_CombatAI;
    SlipstreamSystem m_Slip;
    CombatSystem     m_Combat;
    Rigidbody m_Rb;
    Transform m_Forward;

    void Start()
    {
        m_Slip   = GetComponentInParent<SlipstreamSystem>(true);
        m_Combat = GetComponentInChildren<CombatSystem>(true);

        m_Player = GetComponentInParent<BikeController>(true);
        if (m_Player != null)
        {
            m_Rb      = m_Player.bikeReferences.BikeRb;
            m_Forward = m_Player.bikeReferences.Rotator;
        }
        else
        {
            m_Ai = GetComponentInParent<BikeAIController>(true);
            if (m_Ai != null)
            {
                m_Rb       = m_Ai.bikeReferences.BikeRb;
                m_Forward  = m_Ai.bikeReferences.Rotator;
                m_AiLogic  = m_Ai.aiLogic;
                m_CombatAI = m_Ai.GetComponentInChildren<CombatAI>(true);
            }
        }

        if (m_Rb == null || m_Forward == null)
        {
            Debug.LogError("RivalPull: no bike controller with a Rigidbody and Rotator " +
                           "in parents.", this);
            enabled = false;
        }
    }

    bool Grounded      => m_Player != null ? m_Player.bikeIsGrounded : m_Ai.bikeIsGrounded;
    bool CanAccelerate => m_Player != null ? m_Player.canAccelerate  : m_Ai.canAccelerate;

    void FixedUpdate()
    {
        if (!Grounded || !CanAccelerate) return;
        if (m_Rb.linearVelocity.magnitude * 3.6f < minSpeedKmh) return;

        float authority = Authority();
        if (authority <= 0f) return;

        Vector3 fwd  = Vector3.ProjectOnPlane(m_Forward.forward, Vector3.up).normalized;
        Vector3 left = Vector3.Cross(Vector3.up, fwd);

        if (m_Slip != null && m_Slip.inSlipstream && m_Slip.draftTarget != null)
            ApplyDraftLock(fwd, left, authority);
        else
            ApplyCombatHold(fwd, left, authority);
    }

    float Authority()
    {
        float steer     = m_Player != null ? m_Player.AnalogSteer : m_Ai.steerInput;
        float authority = 1f - Mathf.Clamp01(Mathf.Abs(steer) / Mathf.Max(steerRelease, 0.01f));

        if (m_AiLogic != null)
            authority *= 1f - Mathf.Clamp01(m_AiLogic.DodgeUrgency / Mathf.Max(dodgeRelease, 0.01f));

        return authority;
    }

    void ApplyDraftLock(Vector3 fwd, Vector3 left, float authority)
    {
        Vector3 toTarget = m_Slip.draftTarget.position - m_Rb.position;
        float   strength = m_Slip.DraftStrength * authority;

        float lateral = Vector3.Dot(toTarget, left);
        float slide   = Mathf.Sign(lateral) * Mathf.Max(0f, Mathf.Abs(lateral) - draftDeadzone);
        m_Rb.AddForce(left * (Mathf.Clamp(slide, -1f, 1f) * draftLateralPull * strength),
                      ForceMode.Acceleration);

        // Holding a gap rather than towing outright is what makes the draft stick, it closes when the
        // target pulls away and eases off before it can shunt them
        float gapError = Vector3.Dot(toTarget, fwd) - draftHoldGap;
        float along    = Mathf.Clamp(gapError * draftGapGain, -draftEase, draftTow);
        m_Rb.AddForce(fwd * (along * strength), ForceMode.Acceleration);
    }

    void ApplyCombatHold(Vector3 fwd, Vector3 left, float authority)
    {
        if (m_Combat == null || m_Combat.IsKnockedOff) return;

        // An AI has already committed to an opponent, honour that rather than grabbing whoever is
        // nearest, or the hold pulls it away from the fight its steering is aiming at
        CombatSystem target = m_CombatAI != null ? m_CombatAI.EngageTarget
                                                          : NearestOpponent();
        if (target == null || target == m_Combat || target.IsKnockedOff) return;

        Vector3 toTarget = Vector3.ProjectOnPlane(target.transform.position - m_Rb.position, Vector3.up);
        if (toTarget.magnitude > combatRange) return;

        // Both terms are zero at the station, so no distance fade is needed to stop it yanking
        float along = Vector3.Dot(toTarget, fwd);
        m_Rb.AddForce(fwd * (Mathf.Clamp(along, -1f, 1f) * combatLinePull * authority),
                      ForceMode.Acceleration);

        float side       = Vector3.Dot(toTarget, left);
        float lateralErr = Mathf.Abs(side) - combatStandoff;
        m_Rb.AddForce(left * (Mathf.Sign(side) * Mathf.Clamp(lateralErr, -1f, 1f)
                              * combatSidePull * authority), ForceMode.Acceleration);
    }

    CombatSystem NearestOpponent()
    {
        CombatSystem nearest = null;
        float nearestDist = combatRange;
        var all = CombatSystem.AllInstances;

        for (int i = 0; i < all.Count; i++)
        {
            CombatSystem c = all[i];
            if (c == null || c == m_Combat || c.IsKnockedOff) continue;

            float dist = Vector3.Distance(m_Rb.position, c.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest     = c;
            }
        }

        return nearest;
    }
}
}
