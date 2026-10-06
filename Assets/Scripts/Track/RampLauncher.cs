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

        private readonly Dictionary<BikeController,    RampData> _players = new();
        private readonly Dictionary<BikeAIController,  RampData> _ais     = new();

        private void FixedUpdate()
        {
            foreach (var pair in _players)
            {
                BikeController player = pair.Key;
                Rigidbody rb  = player.bikeReferences.BikeRb;
                Vector3   fwd = player.bikeReferences.Rotator.forward;
                float deficit = pair.Value.entrySpeed - Vector3.Dot(rb.linearVelocity, fwd);
                if (deficit > 0f)
                    rb.AddForce(fwd * deficit * speedMaintenanceGain * rb.mass, ForceMode.Force);
            }

            foreach (var pair in _ais)
            {
                BikeAIController ai = pair.Key;
                Rigidbody rb  = ai.bikeReferences.BikeRb;
                Vector3   fwd = ai.bikeReferences.Rotator.forward;
                float deficit = pair.Value.entrySpeed - Vector3.Dot(rb.linearVelocity, fwd);
                if (deficit > 0f)
                    rb.AddForce(fwd * deficit * speedMaintenanceGain * rb.mass, ForceMode.Force);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<BikeController>();
            if (player != null)
            {
                Rigidbody rb = player.bikeReferences.BikeRb;
                _players[player] = new RampData
                {
                    savedBump  = player.bikeSuspension.maxBumpUpwardVelocity,
                    entrySpeed = Vector3.Dot(rb.linearVelocity, player.bikeReferences.Rotator.forward),
                };
                player.bikeSuspension.maxBumpUpwardVelocity = maxBumpVelocityOverride;

                if (shortcutPlayerRoadIndex >= 0)
                    other.GetComponentInParent<CheckpointTracker>()
                         ?.ForceRoad(shortcutPlayerRoadIndex);
                return;
            }

            var ai = other.GetComponentInParent<BikeAIController>();
            if (ai != null)
            {
                Rigidbody rb = ai.bikeReferences.BikeRb;
                _ais[ai] = new RampData
                {
                    savedBump  = ai.bikeSuspension.maxBumpUpwardVelocity,
                    entrySpeed = Vector3.Dot(rb.linearVelocity, ai.bikeReferences.Rotator.forward),
                };
                ai.bikeSuspension.maxBumpUpwardVelocity = maxBumpVelocityOverride;
                ai.isOnRamp = true;

                // With AI shortcuts off, clipping this trigger committed the AI to the off road jump route anyway,
                // and it pinned itself on the barrier steering at a line 350 m away
                var aiLogic = ai.aiLogic;
                bool shortcutsAllowed = aiLogic != null &&
                                        (aiLogic.routeGraph == null || aiLogic.routeGraph.aiShortcutsEnabled);
                if (shortcutsAllowed)
                {
                    if (shortcutSegmentIndex >= 0) aiLogic.ForceCommitSegment(shortcutSegmentIndex);
                    // Aim the AI toward the landing zone so it launches on the correct trajectory
                    aiLogic.rampLandingTarget = landingTarget;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<BikeController>();
            if (player != null)
            {
                if (_players.TryGetValue(player, out RampData data))
                {
                    ApplyLaunchImpulse(player.bikeReferences.BikeRb);
                    player.bikeSuspension.maxBumpUpwardVelocity = data.savedBump;
                    _players.Remove(player);
                }
                return;
            }

            var ai = other.GetComponentInParent<BikeAIController>();
            if (ai != null)
            {
                if (_ais.TryGetValue(ai, out RampData data))
                {
                    ApplyLaunchImpulse(ai.bikeReferences.BikeRb);
                    ai.bikeSuspension.maxBumpUpwardVelocity = data.savedBump;
                    _ais.Remove(ai);
                    ai.isOnRamp = false;
                    if (ai.aiLogic != null)
                        ai.aiLogic.rampLandingTarget = null;
                }
            }
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
