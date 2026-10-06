using MotoSquid.AI;
using UnityEngine;

namespace MotoSquid.Bike
{
    public class SpeedTrail : MonoBehaviour
    {
        [Header("References (children named \"Trail\" if left empty)")]
        public TrailRenderer[] trails;

        [Header("Boost Gate")]
        public float fadeInTime  = 0.10f;
        public float fadeOutTime = 0.35f;

        [Header("Shape")]
        // Owned here, not read off each renderer. The two bikes had drifted to 0.6 (player) against 0.15
        // (AI) across four renderers each, plus a hand-made leftover at 0.08 and 2.5 s - one bike read as
        // fat stripes and the other as threads
        public float trailWidth = 0.3f;
        public float trailTime  = 1.5f;
        public float startTime  = 0.15f;   // Length at the moment the gate opens

        // The inverse of SlipstreamSystem.trailShape: a light trail is at its widest where it
        // leaves the lamp and tapers to a point, where a wake starts narrow at the bike and opens out
        // behind it. Owned here so all four read the same, rather than four hand-authored curves that
        // had already drifted apart
        public AnimationCurve trailShape = new AnimationCurve(
            new Keyframe(0f,    1f,    0f,    -1.6f),
            new Keyframe(0.45f, 0.30f, -1.1f, -1.1f),
            new Keyframe(1f,    0f,    -0.55f, 0f));

        BoostSystem            m_Boost;
        AIBoostSystem m_AIBoost;
        float m_Level;
        bool  m_Emitting;

        bool Boosting => (m_Boost   != null && m_Boost.isBoosting)
                      || (m_AIBoost != null && m_AIBoost.isBoosting);

        void Start()
        {
            if (trails == null || trails.Length == 0)
                trails = System.Array.FindAll(GetComponentsInChildren<TrailRenderer>(true),
                                              t => t.name.StartsWith("Trail"));

            m_Boost   = GetComponentInParent<BoostSystem>(true);
            m_AIBoost = GetComponentInParent<AIBoostSystem>(true);

            if (m_Boost == null && m_AIBoost == null)
            {
                Debug.LogError("SpeedTrail: no BoostSystem or AIBoostSystem in parents.", this);
                enabled = false;
                return;
            }

            if (trails.Length == 0)
            {
                Debug.LogError("SpeedTrail: no trails assigned, and no child named \"Trail\".", this);
                enabled = false;
                return;
            }

            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null) continue;
                trails[i].widthCurve      = trailShape;
                trails[i].emitting        = false;
                trails[i].widthMultiplier = 0f;
            }
        }

        void Update()
        {
            bool  boosting = Boosting;
            float fade     = boosting ? fadeInTime : fadeOutTime;

            m_Level = fade > 0f
                ? Mathf.MoveTowards(m_Level, boosting ? 1f : 0f, Time.deltaTime / fade)
                : (boosting ? 1f : 0f);

            Apply(m_Level);
        }

        void Apply(float level)
        {
            bool wasEmitting = m_Emitting;
            m_Emitting = level > 0.001f;

            // Restarting leaves the surviving points behind, and the trail bridges the gap with one
            // straight segment across however far the bike travelled while it was off
            bool restarted = m_Emitting && !wasEmitting;

            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null) continue;
                if (restarted) trails[i].Clear();
                trails[i].emitting        = m_Emitting;
                trails[i].widthMultiplier = trailWidth * level;
                if (m_Emitting)
                    trails[i].time = Mathf.Lerp(Mathf.Min(startTime, trailTime), trailTime, level);
            }
        }

#if UNITY_EDITOR
        // Same contract as SlipstreamSystem's: Start() has not run, so cache what it would have and then
        // reuse the real response rather than reimplementing it for the editor. level is 0-1 boost.
        public void EditorPreview(float level)
        {
            if (trails == null || trails.Length == 0)
                trails = System.Array.FindAll(GetComponentsInChildren<TrailRenderer>(true),
                                              t => t.name.StartsWith("Trail"));

            for (int i = 0; i < trails.Length; i++)
                if (trails[i] != null) trails[i].widthCurve = trailShape;

            Apply(Mathf.Clamp01(level));
        }
#endif

        void SetEmitting(bool on)
        {
            if (trails == null) return;
            for (int i = 0; i < trails.Length; i++)
                if (trails[i] != null) trails[i].emitting = on;
        }

        void OnDisable()
        {
            m_Level    = 0f;
            m_Emitting = false;
            SetEmitting(false);
            if (trails == null) return;
            for (int i = 0; i < trails.Length; i++)
                if (trails[i] != null) trails[i].widthMultiplier = 0f;
        }
    }
}
