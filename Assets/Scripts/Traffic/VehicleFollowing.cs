using UnityEngine;

namespace MotoSquid.Traffic
{
    [RequireComponent(typeof(SplineMover))]
    public class VehicleFollowing : MonoBehaviour
    {
        [Header("Detection")]
        public float lookAheadDistance = 20f;
        public float minFollowDistance = 5f;
        public float castRadius = 0.8f;
        public LayerMask obstacleMask;

        [Header("Speed Response")]
        public float speedLerpRate = 4f;

        [HideInInspector] public float nominalSpeed;
        public float CurrentSpeed => _currentSpeed;
        public bool  BlockedAhead { get; private set; }
        public Collider BlockingCollider { get; private set; }
        private SplineMover _mover;
        private float _currentSpeed;
        private float _cachedTargetSpeed;
        private int   _checkCounter;
        private const int CHECK_INTERVAL = 4;

        private static int _staggerSeed;

        void Start()
        {
            _mover = GetComponent<SplineMover>();
            if (nominalSpeed <= 0f)
                nominalSpeed = _mover != null ? _mover.MaxSpeed : 25f;
            _currentSpeed      = nominalSpeed;
            _cachedTargetSpeed = nominalSpeed;
            _checkCounter      = _staggerSeed++ % CHECK_INTERVAL;
        }

        public void ResetForSpawn()
        {
            _currentSpeed      = nominalSpeed;
            _cachedTargetSpeed = nominalSpeed;
        }

        void Update()
        {
            if (_mover == null || !_mover.IsPlaying) return;

            _checkCounter++;
            if (_checkCounter >= CHECK_INTERVAL)
            {
                _checkCounter      = 0;
                _cachedTargetSpeed = ComputeTargetSpeed();
            }

            _currentSpeed = Mathf.Max(Mathf.Lerp(_currentSpeed, _cachedTargetSpeed, Time.deltaTime * speedLerpRate), 0f);
            _mover.MaxSpeed = _currentSpeed;
        }

        float ComputeTargetSpeed()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Ray ray = new Ray(origin, transform.forward);

            if (Physics.SphereCast(ray, castRadius, out RaycastHit hit, lookAheadDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.gameObject == gameObject)
                {
                    BlockedAhead = false;
                    BlockingCollider = null;
                    return nominalSpeed;
                }

                BlockedAhead = true;
                BlockingCollider = hit.collider;
                float gap = hit.distance;
                float aheadSpeed = 0f;
                var aheadFollowing = hit.transform.GetComponentInParent<VehicleFollowing>();
                if (aheadFollowing != null)
                    aheadSpeed = aheadFollowing.CurrentSpeed;

                if (gap <= minFollowDistance)
                    return aheadSpeed;

                float span = lookAheadDistance - minFollowDistance;
                float t = span > 0.001f ? (gap - minFollowDistance) / span : 1f;
                return Mathf.Lerp(aheadSpeed, nominalSpeed, t);
            }

            BlockedAhead = false;
            BlockingCollider = null;
            return nominalSpeed;
        }
    }
}
