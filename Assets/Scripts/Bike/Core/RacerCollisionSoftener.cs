using MotoSquid.Audio;
using MotoSquid.Rider;
using System.Collections.Generic;
using UnityEngine;
namespace MotoSquid.Bike
{
    public class RacerCollisionSoftener : MonoBehaviour
    {
        public float forwardRetention = 0.9f;
        public float separationStrength = 3f;
        public float maxRestoreSpeed = 120f;

        // How much of "straight ahead" counts as running into someone rather than brushing past them.
        // Restoring forward speed into a racer you are driving at cancels the collision outright, which
        // is what lets the whole grid pass through itself at the start. 1 = never guard, 0 = always
        [Range(0f, 1f)] public float headOnDot = 0.5f;
        public Rigidbody body;

        [Header("Stuck racers")]
        // Two racers still touching after this long have sunk into each other, and the velocity writes
        // below then fight the solver every step. Their collision is switched off for a moment so they part
        public float stuckSeconds = 0.3f;
        public float stuckIgnoreSeconds = 0.75f;

        private Vector3 _prevVel;
        private readonly Dictionary<Rigidbody, bool> _racerCache = new();
        private readonly Dictionary<Rigidbody, float> _contactSince = new();

        void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (body == null) body = GetComponentInParent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (body != null) _prevVel = body.linearVelocity;
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.rigidbody != null) _contactSince[c.rigidbody] = Time.time;
            Soften(c, impact: true);
        }

        void OnCollisionStay(Collision c)
        {
            Soften(c, impact: false);
            if (c.rigidbody != null && _contactSince.TryGetValue(c.rigidbody, out float since)
                && Time.time - since >= stuckSeconds && IsRacer(c.rigidbody))
            {
                _contactSince.Remove(c.rigidbody);
                StartCoroutine(PartStuckRacers(c.rigidbody, Time.time - since));
            }
        }

        void OnCollisionExit(Collision c)
        {
            if (c.rigidbody != null) _contactSince.Remove(c.rigidbody);
        }

        System.Collections.IEnumerator PartStuckRacers(Rigidbody other, float stuckFor)
        {
            var mine   = body.GetComponentsInChildren<Collider>();
            var theirs = other.GetComponentsInChildren<Collider>();
            SetIgnored(mine, theirs, true);

            Debug.Log($"[RacerCollisionSoftener] '{body.name}' stuck in '{other.name}' for {stuckFor:F2} s, " +
                      $"collision off for {stuckIgnoreSeconds} s.", this);
            AudioBurstLog.Note($"racers stuck ({other.name})", this);

            yield return new WaitForSeconds(stuckIgnoreSeconds);
            SetIgnored(mine, theirs, false);
        }

        static void SetIgnored(Collider[] a, Collider[] b, bool ignore)
        {
            foreach (var x in a)
                foreach (var y in b)
                    if (x != null && y != null) Physics.IgnoreCollision(x, y, ignore);
        }

        void Soften(Collision c, bool impact)
        {
            if (body == null) return;

            Rigidbody other = c.rigidbody;
            if (other == null || other == body) return;
            if (!IsRacer(other)) return;

            // Horizontal pre contact travel direction/speed
            Vector3 preH = _prevVel; preH.y = 0f;
            float preSpeed = preH.magnitude;
            if (preSpeed <= 0.5f) return;   // Nearly stopped already, nothing worth preserving

            Vector3 dir = preH / preSpeed;
            Vector3 v   = body.linearVelocity;

            Vector3 away = body.position - other.position; away.y = 0f;

            // Are we driving into them, or alongside them? Only a glancing contact gets its speed back
            bool headOn = away.sqrMagnitude > 1e-4f &&
                          Vector3.Dot(dir, -away.normalized) > headOnDot;

            if (!headOn && impact)
            {
                float curAlong = Vector3.Dot(v, dir);
                float target   = Mathf.Min(preSpeed * forwardRetention, maxRestoreSpeed);
                if (curAlong < target)
                    v += dir * (target - curAlong);
            }

            Vector3 lateral = Vector3.ProjectOnPlane(away, dir);
            if (lateral.sqrMagnitude > 1e-4f)
            {
                Vector3 latDir = lateral.normalized;
                float curLat = Vector3.Dot(v, latDir);
                if (curLat < separationStrength)
                    v += latDir * (separationStrength - curLat);
            }

            body.linearVelocity = v;
        }

        bool IsRacer(Rigidbody other)
        {
            if (_racerCache.TryGetValue(other, out bool cached)) return cached;

            bool isRacer = other.GetComponentInParent<BikeController>()   != null
                        || other.GetComponentInParent<BikeAIController>() != null
                        || other.GetComponentInParent<RagdollActivator>() != null;

            _racerCache[other] = isRacer;
            return isRacer;
        }
    }
}
