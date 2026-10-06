using UnityEngine;

namespace MotoSquid.Bike
{
    // Sideways tyre grip, and the handbrake drift that trades rear grip for a slide
    public sealed class BikeTyreGrip
    {
        readonly BikeController bike;
        float rearLateralGrip;

        public BikeTyreGrip(BikeController bike) => this.bike = bike;

        public bool IsDrifting { get; private set; }
        public float DriftIntensity { get; private set; }   // 0-1, smoothed entry/exit
        public float DriftAngle { get; private set; }       // Current slide angle in degrees, for the lean boost

        public void AddFriction()
        {
            var set = bike.bikeSettings;
            var refs = bike.bikeReferences;
            var rb = refs.BikeRb;
            float dt = Time.fixedDeltaTime;

            if (!bike.bikeIsGrounded)
            {
                DriftIntensity = Mathf.MoveTowards(DriftIntensity, 0f, dt / set.driftExitTime);
                rearLateralGrip = Mathf.MoveTowards(rearLateralGrip, set.rearLateralGripNormal, dt / set.driftExitTime);
                IsDrifting = false;
                return;
            }

            if (bike.Burnout.IsRotating)
                return;

            bool shouldDrift = bike.bikeInput.HandBrake > 0 && Mathf.Abs(bike.localBikeVelocity.z) >= set.minDriftSpeed;
            bool wasDrifting = IsDrifting;

            if (shouldDrift)
            {
                DriftIntensity = Mathf.MoveTowards(DriftIntensity, 1f, dt / set.driftEntryTime);
                rearLateralGrip = Mathf.Lerp(rearLateralGrip, set.rearLateralGripDrift, dt / set.driftEntryTime);
                IsDrifting = DriftIntensity > 0.3f;
            }
            else
            {
                DriftIntensity = Mathf.MoveTowards(DriftIntensity, 0f, dt / set.driftExitTime);
                rearLateralGrip = Mathf.MoveTowards(rearLateralGrip, set.rearLateralGripNormal, dt / set.driftExitTime);
                if (DriftIntensity < 0.05f)
                    IsDrifting = false;
            }

            bool isExitingDrift = wasDrifting && !IsDrifting && DriftIntensity > 0f;

            float frictionForce = (-bike.localBikeVelocity.x * set.frictionCoefficient / dt) * bike.bikeCurves.FrictionCurve.Evaluate(bike.CachedSideRatio);

            // Drift reduces lateral friction (letting the bike slide sideways)
            float driftFrictionFactor = Mathf.Lerp(1f, set.driftFrictionFactor, DriftIntensity);
            rb.AddForceAtPosition(refs.Rotator.right * frictionForce * driftFrictionFactor, bike.transform.position, ForceMode.Acceleration);

            if (bike.rearWheelIsGrounded)
            {
                Vector3 rearLocalVel = refs.Rotator.InverseTransformDirection(rb.GetPointVelocity(refs.RearWheel.position));
                float rearLateralVel = rearLocalVel.x;

                // Steering against the slide is the player asking to catch it, so hand back a share of
                // the rear grip the drift gave up. Without this a slide can only be entered and waited out
                float steer = bike.Steering.SteerSmoother;
                float grip = rearLateralGrip;
                if (DriftIntensity > 0f && steer * rearLateralVel < 0f)
                    grip = Mathf.Lerp(grip, set.rearLateralGripNormal, set.driftCounterSteerAssist * Mathf.Abs(steer));

                float rearLateralGripForce = -rearLateralVel * set.frictionCoefficient * grip * 5f;

                // Clamp to prevent instability but allow the slide
                float maxLateralForce = set.frictionCoefficient * set.maxSpeed * grip * 2f;
                rearLateralGripForce = Mathf.Clamp(rearLateralGripForce, -maxLateralForce, maxLateralForce);

                rb.AddForceAtPosition(refs.Rotator.right * rearLateralGripForce, refs.RearWheel.position, ForceMode.Acceleration);
            }

            // Drift exit recovery, actively damp lateral velocity, stronger as the drift fades
            if (isExitingDrift && DriftIntensity > 0.05f)
            {
                float bodyLateralVel = refs.Rotator.InverseTransformDirection(rb.linearVelocity).x;
                float recoveryStrength = (1f - DriftIntensity) * 8f;
                rb.AddForce(refs.Rotator.right * (-bodyLateralVel * recoveryStrength), ForceMode.Acceleration);
            }

            // Forward momentum preservation during drift
            if (IsDrifting)
                rb.AddForce(-refs.Rotator.forward * set.rollingResistance * 0.5f, ForceMode.Acceleration);

            if (IsDrifting && rb.linearVelocity.magnitude > 1f)
                DriftAngle = Mathf.Abs(Vector3.SignedAngle(refs.Rotator.forward, rb.linearVelocity.normalized, Vector3.up));
            else
                DriftAngle = 0f;
        }
    }
}
