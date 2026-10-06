using MotoSquid.AI;
using MotoSquid.Combat;
using UnityEngine;
namespace MotoSquid.Bike
{
    public class SlipstreamSystem : MonoBehaviour
    {
        [Header("References (auto found in parents if left empty)")]
        public TrailRenderer[] trails;

        [Header("Draft Detection")]
        public float slipstreamRange   = 14f;
        public float minDistance       = 1.5f;
        public float draftWidth        = 1.8f;
        public float draftWidthAtRange = 4.5f;
        public float maxHeadingAngle   = 50f;
        public float minSpeedKmh       = 140f;
        public LayerMask racerLayerMask = ~0;

        [Header("Reward")]
        public float slipstreamRegenRate = 0.15f;
        public float regenFalloffAtRange = 0.25f;
        public float engageDelay        = 0.4f;
        public float releaseTime        = 1.4f;
        public float loseGrace          = 0.7f;

        [Header("Trail")]
        public float trailWidth   = 0.9f;
        public float trailTime    = 0.55f;
        public float trailShowsAt = 0.15f;   // Below this the draft is a graze, and shows nothing at all
        public AnimationCurve trailShape = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(1f, 1f));

        [Header("Detection Cost")]
        public int detectInterval       = 2;

        [HideInInspector] public bool  inSlipstream;
        [HideInInspector] public Transform draftTarget; 
        [HideInInspector] public float draftCloseness;  

        public float DraftStrength { get; private set; }
        public float WakeStrength { get; private set; }
        public bool IsHuman => m_IsHuman;
        public bool VisualsEnabled => m_IsHuman || m_TargetIsHuman;

        public System.Action OnSlipstreamStart;
        public System.Action OnSlipstreamEnd;

        BoostSystem               m_PlayerBoost;
        AIBoostSystem    m_AiBoost;
        Transform m_Forward;    
        Transform m_Root;        
        Rigidbody m_Rb;          

        bool      m_IsHuman;
        Vector3[] m_TrailHome;
        Quaternion[] m_TrailAim;
        int       m_FrameCounter;
        float   m_GraceTimer;

        Transform                 m_CachedTarget;
        SlipstreamSystem m_TargetSlip;
        bool                      m_TargetIsHuman;

        static readonly RaycastHit[] s_Hits = new RaycastHit[16];

        void Start()
        {
            ResolveTrails();

            var player = GetComponentInParent<BikeController>(true);
            if (player != null)
            {
                m_Root        = player.transform;
                m_Forward     = player.bikeReferences.Rotator;
                m_Rb          = player.bikeReferences.BikeRb;
                m_PlayerBoost = player.GetComponentInChildren<BoostSystem>(true);
                m_IsHuman     = true;
            }
            else
            {
                var ai = GetComponentInParent<BikeAIController>(true);
                if (ai != null)
                {
                    m_Root    = ai.transform;
                    m_Forward = ai.bikeReferences.Rotator;
                    m_Rb      = ai.bikeReferences.BikeRb;
                    m_AiBoost = ai.GetComponentInChildren<AIBoostSystem>(true);
                }
            }

            if (m_Forward == null || m_Rb == null)
            {
                Debug.LogError("SlipstreamSystem: no bike controller found in parents.", this);
                enabled = false;
                return;
            }

            if (trails.Length == 0)
                Debug.LogWarning("SlipstreamSystem: no trails assigned, and no child named " +
                                 "\"Slipstream\" - drafting will award boost with no visual.", this);

            if (m_PlayerBoost == null && m_AiBoost == null)
                Debug.LogWarning("SlipstreamSystem: no boost system found, the trail will " +
                                 "still show, but drafting won't award any boost.", this);

            CacheTrails();
            ApplyTrail();
        }

        void ResolveTrails()
        {
            if (trails == null || trails.Length == 0)
                trails = System.Array.FindAll(GetComponentsInChildren<TrailRenderer>(true),
                                              t => t.name.StartsWith("Slipstream"));
        }

        void CacheTrails()
        {
            m_TrailHome = new Vector3[trails.Length];
            m_TrailAim  = new Quaternion[trails.Length];
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null) continue;

                Transform t = trails[i].transform;
                m_TrailHome[i] = t.localPosition;

                m_TrailAim[i] = t.localRotation;

                trails[i].widthCurve = trailShape;
                trails[i].alignment  = LineAlignment.TransformZ;
            }
        }

#if UNITY_EDITOR
        public void EditorPreview(float strength)
        {
            ResolveTrails();
            if (m_TrailHome == null || m_TrailHome.Length != trails.Length) CacheTrails();

            m_IsHuman     = true;
            DraftStrength = Mathf.Clamp01(strength);
            WakeStrength  = DraftStrength;
            ApplyTrail();
        }

        public void EditorPreviewEnd()
        {
            m_IsHuman     = false;
            DraftStrength = 0f;
            WakeStrength  = 0f;
            ApplyTrail();
        }

#endif

        public void ReportDraftedAt(float strength) => WakeStrength = Mathf.Max(WakeStrength, strength);

        void Update()
        {
            bool prevInSlipstream = inSlipstream;

            WakeStrength = Mathf.MoveTowards(WakeStrength, 0f,
                                             releaseTime > 0.01f ? Time.deltaTime / releaseTime : 1f);

            if (++m_FrameCounter >= detectInterval)
            {
                m_FrameCounter = 0;
                Transform found = FindDraftTarget();
                if (found != null)
                {
                    draftTarget  = found;
                    m_GraceTimer = Mathf.Max(loseGrace, Time.deltaTime);
                }
            }

            m_GraceTimer -= Time.deltaTime;
            inSlipstream  = m_GraceTimer > 0f;
            if (!inSlipstream)
            {
                draftTarget    = null;
                draftCloseness = 0f;
            }
            TargetSlipstream();   // refreshes m_TargetIsHuman, which VisualsEnabled reads

            float target = inSlipstream ? Mathf.Lerp(regenFalloffAtRange, 1f, draftCloseness) : 0f;
            float rate   = inSlipstream ? engageDelay : releaseTime;
            DraftStrength = Mathf.MoveTowards(DraftStrength, target,
                                              rate > 0.01f ? Time.deltaTime / rate : 1f);

            if (inSlipstream && !prevInSlipstream)      OnSlipstreamStart?.Invoke();
            else if (!inSlipstream && prevInSlipstream) OnSlipstreamEnd?.Invoke();

            ApplyTrail();

            if (inSlipstream)
            {
                AwardBoost(slipstreamRegenRate * DraftStrength * Time.deltaTime);

                var targetSlip = TargetSlipstream();
                if (targetSlip != null && (m_IsHuman || targetSlip.IsHuman))
                    targetSlip.ReportDraftedAt(DraftStrength);
            }
        }

        Transform FindDraftTarget()
        {
            if ((m_Rb.linearVelocity.magnitude * 3.6f) < minSpeedKmh) return null;

            Vector3 origin = m_Forward.position;
            Vector3 fwd    = m_Forward.forward;

            int count = Physics.SphereCastNonAlloc(
                origin, Mathf.Max(draftWidth, draftWidthAtRange), fwd, s_Hits, slipstreamRange,
                racerLayerMask, QueryTriggerInteraction.Ignore);

            Transform best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var hit = s_Hits[i];
                if (!ResolveRacer(hit.collider, out Transform other, out Vector3 otherFwd)) continue;
                if (other == m_Root) continue;

                Vector3 toOther = other.position - origin;
                float   ahead   = Vector3.Dot(toOther, fwd);
                if (ahead < minDistance || ahead > slipstreamRange) continue;   

                float allowed = Mathf.Lerp(draftWidth, draftWidthAtRange,
                                           Mathf.InverseLerp(minDistance, slipstreamRange, ahead));
                if (Vector3.ProjectOnPlane(toOther, fwd).magnitude > allowed) continue;

                if (Vector3.Angle(fwd, otherFwd) > maxHeadingAngle) continue;

                if (ahead < bestDist)
                {
                    bestDist = ahead;
                    best     = other;
                }
            }

            if (best != null)
                draftCloseness = Mathf.InverseLerp(slipstreamRange, minDistance, bestDist);

            return best;
        }

        static bool ResolveRacer(Collider col, out Transform root, out Vector3 heading)
        {
            root    = null;
            heading = Vector3.forward;
            if (col == null) return false;

            var player = col.GetComponentInParent<BikeController>();
            if (player != null)
            {
                root    = player.transform;
                heading = Heading(player.bikeReferences.Rotator, root);
                return true;
            }

            var ai = col.GetComponentInParent<BikeAIController>();
            if (ai != null)
            {
                root    = ai.transform;
                heading = Heading(ai.bikeReferences.Rotator, root);
                return true;
            }

            return false;
        }

        static Vector3 Heading(Transform rotator, Transform root)
            => (rotator != null ? rotator : root).forward;

        void AwardBoost(float amount)
        {
            if (m_PlayerBoost != null) m_PlayerBoost.AwardBoost(amount);
            else if (m_AiBoost != null) m_AiBoost.AwardBoost(amount);
        }

        SlipstreamSystem TargetSlipstream()
        {
            if (draftTarget != m_CachedTarget)
            {
                m_CachedTarget = draftTarget;
                m_TargetSlip   = draftTarget != null
                    ? draftTarget.GetComponentInChildren<SlipstreamSystem>(true)
                    : null;
                m_TargetIsHuman = m_TargetSlip != null && m_TargetSlip.IsHuman;
            }
            return m_TargetSlip;
        }

        // A TrailRenderer keeps its points in world space, so restarting one without clearing draws a
        // straight streak across however far the bike travelled while it was off
        void ApplyTrail()
        {
            if (trails == null || m_TrailHome == null || m_TrailAim == null) return;

            float show = Mathf.InverseLerp(trailShowsAt, 1f,
                                           Mathf.Max(VisualsEnabled ? DraftStrength : 0f, WakeStrength));
            bool on = show > 0f;
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null) continue;
                if (on && !trails[i].emitting) trails[i].Clear();
                trails[i].emitting        = on;
                trails[i].widthMultiplier = trailWidth * show;
                trails[i].time            = trailTime  * show;

                // Anchored where it was authored, the authored aim is what a full-strength draft reaches
                trails[i].transform.localPosition = m_TrailHome[i];
                trails[i].transform.localRotation = Quaternion.Slerp(Quaternion.identity, m_TrailAim[i], show);
            }
        }

        void OnDisable()
        {
            inSlipstream   = false;
            draftTarget    = null;
            draftCloseness = 0f;
            DraftStrength  = 0f;
            WakeStrength   = 0f;
            m_GraceTimer   = 0f;
            ApplyTrail();
        }

        void OnDrawGizmosSelected()
        {
            Transform f = m_Forward != null ? m_Forward : transform;
            Gizmos.color = inSlipstream ? new Color(1f, 0.4f, 0f, 0.5f) : new Color(0f, 0.7f, 1f, 0.3f);
            Vector3 near = f.position + f.forward * minDistance;
            Vector3 end  = f.position + f.forward * slipstreamRange;
            Gizmos.DrawLine(near, end);
            Gizmos.DrawWireSphere(near, draftWidth);
            Gizmos.DrawWireSphere(end, draftWidthAtRange);
        }
    }
}
