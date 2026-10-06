using UnityEngine;

namespace MotoSquid.Bike
{
    // Suspension compression, the spring and damper force, snapping to the ground on a hard landing,
    // and the cap on how fast a bump can throw the bike upward
    public sealed class BikeSuspensionForces
    {
        readonly BikeController bike;
        float smoothedCompressionFront, smoothedCompressionRear;
        float snapDistance, compressionForSnap;
        bool snappedToGround;

        public BikeSuspensionForces(BikeController bike) => this.bike = bike;

        public float CompressionFront { get; private set; }
        public float CompressionRear { get; private set; }
        public float CompressionTotal { get; private set; }

        public void UpdateCompression()
        {
            var ground = bike.Ground;
            var s = bike.bikeSuspension;
            bool front = bike.frontWheelIsGrounded, rear = bike.rearWheelIsGrounded;

            // SphereCast hit.distance is sphere centre travel, add castRadius to recover the surface distance
            // used in the original raycast formula so compression is zero at the wheel's rest height
            float castRadius = s.suspensionCastRadius;
            float rawFront = front ? (ground.MaxFrontDistance - (ground.FrontHit.distance + castRadius)) / ground.MaxFrontDistance : 0f;
            float rawRear  = rear  ? (ground.MaxRearDistance  - (ground.RearHit.distance  + castRadius)) / ground.MaxRearDistance  : 0f;
            rawFront = Mathf.Clamp01(rawFront);
            rawRear  = Mathf.Clamp01(rawRear);

            float sf = s.groundNormalSmoothing * Time.fixedDeltaTime;
            smoothedCompressionFront = front ? Mathf.Lerp(smoothedCompressionFront, rawFront, sf) : 0f;
            smoothedCompressionRear  = rear  ? Mathf.Lerp(smoothedCompressionRear,  rawRear,  sf) : 0f;
            CompressionFront = smoothedCompressionFront;
            CompressionRear  = smoothedCompressionRear;
            CompressionTotal = Mathf.Clamp01((CompressionFront + CompressionRear) / 2f);

            // Snap uses raw compression so the position correction stays responsive on real bumps
            float meanDistance = ((ground.MaxFrontDistance * ground.CosFront) + (ground.MaxRearDistance * ground.CosRear)) / 2f;
            compressionForSnap = ((rawFront * ground.CosFront) + (rawRear * ground.CosRear)) / 2f;
            snapDistance       = meanDistance * (compressionForSnap - s.MaxCompression);
        }

        public void AddForces()
        {
            var ground = bike.Ground;
            var s = bike.bikeSuspension;
            var rb = bike.bikeReferences.BikeRb;

            Vector3 normal = Vector3.up;
            if (bike.frontWheelIsGrounded && bike.rearWheelIsGrounded)
                normal = ground.GroundNormal;
            else if (bike.rearWheelIsGrounded)
                normal = ground.RearHit.normal;

            // Spring pushes along the road surface normal. Using the full normal (not side projected)
            // means the force correctly counters gravity on banked roads instead of only pushing vertically
            Vector3 springDir = normal.normalized;
#if UNITY_EDITOR
            Debug.DrawRay(bike.transform.position, springDir, Color.green);
#endif

            float springVel   = Vector3.Dot(rb.linearVelocity, springDir);
            float springForce = s.SpringForce * (CompressionTotal - s.groundStickFactor * CompressionTotal);
            float damperForce = s.DamperForce * springVel;
            damperForce = Mathf.Clamp(damperForce, -Mathf.Abs(springVel) / Time.fixedDeltaTime, Mathf.Abs(springVel) / Time.fixedDeltaTime);
            if (CompressionTotal < 0.05f)
                damperForce = 0;

            Vector3 suspensionForce = springDir * (springForce - damperForce);

            if (!bike.bikeIsGrounded) return;

            // Snap to ground
            if (compressionForSnap > s.MaxCompression)
            {
                if (!snappedToGround)
                {
                    rb.MovePosition(rb.position + (ground.ProjectedUp * snapDistance));
                    bike.ReduceSurfaceNormalDownVelocity(0.8f);
                    snappedToGround = true;
                }
            }
            else
            {
                snappedToGround = false;
            }

            float rawLeanZ = bike.bikeReferences.LeanTransform.localEulerAngles.z;
            float leanAngle = Mathf.Abs(rawLeanZ > 180f ? rawLeanZ - 360f : rawLeanZ);
            float leanFactor = Mathf.Cos(Mathf.Deg2Rad * leanAngle);
            rb.AddForce(suspensionForce * leanFactor, ForceMode.Acceleration);
        }

        public void ClampBumpVelocity()
        {
            if (!bike.bikeIsGrounded) return;
            var rb = bike.bikeReferences.BikeRb;
            Vector3 normal = bike.Ground.GroundNormal;
            Vector3 vel = rb.linearVelocity;
            float upwardVel = Vector3.Dot(vel, normal);
            float max = bike.bikeSuspension.maxBumpUpwardVelocity;
            if (upwardVel > max)
                rb.linearVelocity = vel - normal * (upwardVel - max);
        }
    }
}
