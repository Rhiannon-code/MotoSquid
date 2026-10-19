using MotoSquid.AI;
using MotoSquid.Bike;
using MotoSquid.Race;
using MotoSquid.Rider;
using System.Collections.Generic;
using UnityEngine;
namespace MotoSquid.Track
{
    public class RampLauncher : MonoBehaviour
    {
        [SerializeField] float launchImpulseStrength = 3f;
        [SerializeField] float maxBumpVelocityOverride = 40f;
        [SerializeField] float speedMaintenanceGain = 10f;

        [Header("Route Graph Shortcut (optional)")]
        [SerializeField] int shortcutSegmentIndex = -1;

        [Header("Player Checkpoint Shortcut (optional)")]
        [SerializeField] int shortcutPlayerRoadIndex = -1;

        [Header("Landing Target (optional)")]
        [SerializeField] Transform landingTarget;

        private struct RampData
        {
            public float savedBump;
            public float entrySpeed;
        }

        private readonly Dictionary<BikeController, RampData> _bikes = new();

        private void FixedUpdate()
        {
            foreach (var pair in _bikes)
            {
                BikeController bike = pair.Key;
                Rigidbody rb  = bike.bikeReferences.BikeRb;
                Vector3   fwd = bike.bikeReferences.Rotator.forward;
                float deficit = pair.Value.entrySpeed - Vector3.Dot(rb.linearVelocity, fwd);
                if (deficit > 0f)
                    rb.AddForce(fwd * deficit * speedMaintenanceGain * rb.mass, ForceMode.Force);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var bike = other.GetComponentInParent<BikeController>();
            if (bike == null) return;

            Rigidbody rb = bike.bikeReferences.BikeRb;
            _bikes[bike] = new RampData
            {
                savedBump  = bike.bikeSuspension.maxBumpUpwardVelocity,
                entrySpeed = Vector3.Dot(rb.linearVelocity, bike.bikeReferences.Rotator.forward),
            };
            bike.bikeSuspension.maxBumpUpwardVelocity = maxBumpVelocityOverride;
            bike.isOnRamp = true;

            var aiLogic = other.GetComponentInParent<BikeAILogic>();
            if (aiLogic == null)
            {
                if (shortcutPlayerRoadIndex >= 0)
                    other.GetComponentInParent<CheckpointTracker>()?.ForceRoad(shortcutPlayerRoadIndex);
                return;
            }

            // With AI shortcuts off, clipping this trigger committed the AI to the off road jump route anyway,
            // and it pinned itself on the barrier steering at a line 350 m away
            bool shortcutsAllowed = aiLogic.routeGraph == null || aiLogic.routeGraph.aiShortcutsEnabled;
            if (shortcutsAllowed)
            {
                if (shortcutSegmentIndex >= 0) aiLogic.ForceCommitSegment(shortcutSegmentIndex);
                // Aim the AI toward the landing zone so it launches on the correct trajectory
                aiLogic.rampLandingTarget = landingTarget;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var bike = other.GetComponentInParent<BikeController>();
            if (bike == null || !_bikes.TryGetValue(bike, out RampData data)) return;

            ApplyLaunchImpulse(bike.bikeReferences.BikeRb);
            bike.bikeSuspension.maxBumpUpwardVelocity = data.savedBump;
            _bikes.Remove(bike);
            bike.isOnRamp = false;

            var aiLogic = other.GetComponentInParent<BikeAILogic>();
            if (aiLogic != null) aiLogic.rampLandingTarget = null;
        }

        private void ApplyLaunchImpulse(Rigidbody rb)
        {
            if (launchImpulseStrength <= 0f || rb == null) return;
            if (rb.linearVelocity.y > 0f)
                rb.AddForce(Vector3.up * launchImpulseStrength, ForceMode.VelocityChange);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            var col = GetComponent<Collider>();
            if (col is SphereCollider sc)
                Gizmos.DrawSphere(transform.TransformPoint(sc.center), sc.radius * transform.lossyScale.x);
            else if (col is BoxCollider bc)
                Gizmos.DrawCube(transform.TransformPoint(bc.center),
                    Vector3.Scale(bc.size, transform.lossyScale));
        }
#endif
    }
}
