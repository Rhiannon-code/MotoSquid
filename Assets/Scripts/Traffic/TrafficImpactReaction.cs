using MotoSquid.Bike;
using UnityEngine;

namespace MotoSquid.Traffic
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SplineMover))]
    public class TrafficImpactReaction : MonoBehaviour
    {
        [Header("Trigger")]
        public float minImpactSpeed = 14f;      // Closing speed (m/s) below which the car shrugs the hit off

        [Header("Reaction")]
        [Range(0f, 2f)] public float shoveScale = 0.8f;   // Share of the racer's velocity handed to the car
        public float maxShoveSpeed = 30f;
        public float spinTorque    = 3f;

        Rigidbody            _rb;
        SplineMover _mover;
        bool                 _knocked;

        void Awake()
        {
            _rb    = GetComponent<Rigidbody>();
            _mover = GetComponent<SplineMover>();
        }

        void OnEnable()
        {
            if (!_knocked) return;

            _knocked        = false;
            _rb.isKinematic = true;
            if (_mover != null) _mover.enabled = true;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_knocked) return;
            if (collision.relativeVelocity.magnitude < minImpactSpeed) return;

            Rigidbody hitter = collision.rigidbody;
            if (hitter == null || hitter.isKinematic) return;

            bool isRacer = collision.gameObject.GetComponentInParent<BikeController>() != null
                        || collision.gameObject.GetComponentInParent<BikeAIController>() != null;
            if (!isRacer) return;

            Knock(hitter);
        }

        void Knock(Rigidbody hitter)
        {
            _knocked = true;

            Vector3 inherited = _mover != null ? _mover.Velocity : Vector3.zero;
            if (_mover != null) _mover.enabled = false;

            _rb.isKinematic = false;

            Vector3 shove = Vector3.ClampMagnitude(
                hitter.linearVelocity * shoveScale * Mathf.Clamp01(hitter.mass / Mathf.Max(_rb.mass, 1f)),
                maxShoveSpeed);

            _rb.linearVelocity = inherited + shove;
            _rb.AddTorque(Random.onUnitSphere * spinTorque, ForceMode.VelocityChange);
        }
    }
}
