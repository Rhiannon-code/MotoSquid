using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace MotoSquid.Traffic
{
    [RequireComponent(typeof(SplineMover))]
    public class TrafficLaneChanger : MonoBehaviour
    {
        public float changeCooldown = 4f;
        public float clearGap       = 15f;
        public float changeDuration = 1f;
        public float retryInterval  = 0.5f;

        [HideInInspector] public LayerMask trafficMask;
        [HideInInspector] public TrafficLaneNetwork generator;

        SplineMover      _mover;
        VehicleFollowing _following;
        float                     _cooldownTimer;

        static readonly Collider[] s_overlap = new Collider[8];

        void Awake()
        {
            _mover     = GetComponent<SplineMover>();
            _following = GetComponent<VehicleFollowing>();
        }

        void OnEnable() => _cooldownTimer = changeCooldown;

        void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (generator == null || _mover == null || _following == null) return;
            if (!_mover.IsPlaying || _mover.IsChangingLane) return;
            if (!_following.BlockedAhead || _cooldownTimer > 0f) return;

            var curLane = _mover.CurrentContainer != null
                ? _mover.CurrentContainer.GetComponent<GeneratedTrafficLane>()
                : null;
            if (curLane == null) return;

            SplineContainer target = FindClearAdjacentSameDirectionLane(curLane);
            if (target != null) _mover.StartLaneChange(target, changeDuration);

            // Without a wait on the failed case a car sitting in stuck traffic re-searches both
            // neighbouring splines every frame, forever. The jitter keeps a blocked queue from
            // searching in lockstep
            _cooldownTimer = target != null
                ? changeCooldown
                : UnityEngine.Random.Range(retryInterval, retryInterval * 1.5f);
        }
        SplineContainer FindClearAdjacentSameDirectionLane(GeneratedTrafficLane cur)
        {
            int first = UnityEngine.Random.value < 0.5f ? -1 : 1;
            for (int s = 0; s < 2; s++)
            {
                int dir  = s == 0 ? first : -first;
                var cand = generator.GetLaneSpline(cur.roadIndex, cur.laneIndex + dir);
                if (cand == null) continue;

                var info = cand.GetComponent<GeneratedTrafficLane>();
                if (info == null || info.isOncoming != cur.isOncoming) continue;
                if (info.trafficLocked) continue;

                if (IsLaneClearNearMe(cand)) return cand;
            }
            return null;
        }

        bool IsLaneClearNearMe(SplineContainer lane)
        {
            Vector3 local = lane.transform.InverseTransformPoint(transform.position);
            SplineUtility.GetNearestPoint(lane.Spline, (float3)local, out float3 nearest, out _);
            Vector3 worldNear = lane.transform.TransformPoint((Vector3)nearest);

            Transform blockerRoot = _following.BlockingCollider != null
                ? _following.BlockingCollider.transform.root : null;

            int n = Physics.OverlapSphereNonAlloc(worldNear, clearGap, s_overlap, trafficMask,
                                                  QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = s_overlap[i];
                if (c == null) continue;
                if (c.transform.IsChildOf(transform)) continue;                  
                if (blockerRoot != null && c.transform.root == blockerRoot) continue; 
                return false;                                                 
            }
            return true;
        }
    }
}
