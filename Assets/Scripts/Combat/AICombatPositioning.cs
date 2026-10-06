using System;
using UnityEngine;

namespace MotoSquid.Combat
{
    // Where an engaged AI puts itself to fight: level with the target and an arm's length to one side. Steering
    // toward the target added its offset to the AI's own lane, so the AI settled halfway across and never arrived,
    // and the rubber band only ever set a ceiling, so it rode straight past a slower target
    [Serializable]
    public class AICombatPositioning
    {
        public float alongsideGap = 1.6f;
        public float holdWindow = 35f;
        public float closeKmhPerMetre = 1.5f;
        public float maxCloseKmh = 40f;
        public float maxHoldSeconds = 6f;
        public float restSeconds = 8f;

        int _side = 1;
        float _heldFor, _restFor;
        CombatSystem _target;
        Rigidbody _targetRb;

        // Once per AI tick. False while resting after a spell alongside
        public bool Tick(bool engaged, float dt)
        {
            if (_restFor > 0f) { _restFor -= dt; _heldFor = 0f; return false; }
            if (!engaged) { _heldFor = 0f; return false; }

            _heldFor += dt;
            if (_heldFor < maxHoldSeconds) return true;
            _restFor = restSeconds;
            return false;
        }

        // Offset from the path point along pathLeft, the same frame as the traffic planner's intent
        public float LateralIntent(Vector3 selfPos, Vector3 targetPos, Vector3 pathPos, Vector3 pathLeft)
        {
            float targetLat = Vector3.Dot(targetPos - pathPos, pathLeft);
            float selfLat   = Vector3.Dot(selfPos   - pathPos, pathLeft);
            // Re-picked only once clearly to one side, so an AI directly behind doesn't weave across
            if (Mathf.Abs(selfLat - targetLat) > alongsideGap * 0.5f) _side = selfLat > targetLat ? 1 : -1;
            return targetLat + _side * alongsideGap;
        }

        // Speed to hold near the target, or -1 when it is too far along the road for holding to apply
        public float HoldSpeedKmh(CombatSystem target, Vector3 selfPos, Vector3 pathForward)
        {
            if (target != _target)
            {
                _target   = target;
                _targetRb = target != null ? target.GetComponentInParent<Rigidbody>() : null;
            }
            if (_targetRb == null) return -1f;

            float ahead = Vector3.Dot(_targetRb.position - selfPos, pathForward);
            if (Mathf.Abs(ahead) > holdWindow) return -1f;

            float targetKmh = Vector3.Dot(_targetRb.linearVelocity, pathForward) * 3.6f;
            return targetKmh + Mathf.Clamp(ahead * closeKmhPerMetre, -maxCloseKmh, maxCloseKmh);
        }
    }
}
