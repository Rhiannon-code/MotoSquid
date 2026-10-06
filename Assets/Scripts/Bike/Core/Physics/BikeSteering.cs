using UnityEngine;

namespace MotoSquid.Bike
{
    // Reads the steering input into a smoothed value, and turns the bike from it
    public sealed class BikeSteering
    {
        readonly BikeController bike;
        int lastSteerDir;

        public BikeSteering(BikeController bike) => this.bike = bike;

        // Digital sign of the steer input, used by animation/AI "is steering" checks
        public int CurrentSteerInput { get; private set; }
        public float AnalogSteer { get; private set; }
        // The steering lock available at the current speed, set each frame
        public float SteerAngle { get; private set; }
        public float SteerSmoother { get; private set; }
        public float SteerBuildup { get; private set; }

        public void UpdateInput()
        {
            var input = bike.bikeInput;
            var set = bike.bikeSettings;

            CurrentSteerInput = 0;
            if (input.SteeringLeft > 0)  CurrentSteerInput -= 1;
            if (input.SteeringRight > 0) CurrentSteerInput += 1;

            // Preserves how far a gamepad stick is pushed and smoothly ramps binary (keyboard) input
            // so turning eases in instead of snapping
            float steerTarget = Mathf.Clamp(input.SteeringRight - input.SteeringLeft, -1f, 1f);
            if (set.useAnalogSteering)
            {
                float rate = Mathf.Abs(steerTarget) > Mathf.Abs(AnalogSteer) ? set.steerAttackRate : set.steerReleaseRate;
                AnalogSteer = Mathf.MoveTowards(AnalogSteer, steerTarget, rate * Time.deltaTime);
            }
            else
            {
                AnalogSteer = CurrentSteerInput;
            }
        }

        public void UpdateSteerAngle()
        {
            SteerAngle = bike.bikeSettings.maxTurnAngle * bike.bikeCurves.SteeringCurve.Evaluate(bike.CachedFwdSpeedRatio);
        }

        public void AddTurning(float direction)
        {
            var set = bike.bikeSettings;
            float dt = Time.fixedDeltaTime;

            if (set.useAnalogSteering)
            {
                // Direction is already the smoothed analog steer value
                SteerSmoother = direction;
            }
            else
            {
                if (set.useSteerBuildup)
                {
                    int dir = direction > 0f ? 1 : direction < 0f ? -1 : 0;
                    if (dir != 0 && dir != lastSteerDir)
                        SteerBuildup = 0f;
                    lastSteerDir = dir;

                    SteerBuildup = dir != 0
                        ? Mathf.MoveTowards(SteerBuildup, 1f, dt * set.steerBuildupRate)
                        : Mathf.MoveTowards(SteerBuildup, 0f, dt * set.steerBuildupDecayRate);

                    direction *= SteerBuildup;
                }

                SteerSmoother = set.useLerpTurning
                    ? Mathf.MoveTowards(SteerSmoother, direction, dt * set.turnLerpSpeed)
                    : direction;
            }

            var rotator = bike.bikeReferences.Rotator;
            if (bike.bikeIsGrounded)
            {
                float driftIntensity = bike.Grip.DriftIntensity;
                // The speed curve squeezes the steering angle to a few degrees at race pace, which is
                // what made a held handbrake feel identical to a normal corner: the slide had no extra
                // lock to rotate into.
                float steerAngle = SteerAngle * Mathf.Lerp(1f, set.driftSteerGain, driftIntensity);
                float steeringAngleRadians = SteerSmoother * Mathf.Deg2Rad * steerAngle;

                // Avoid division by zero by ensuring there's a minimum steering angle
                if (Mathf.Abs(steeringAngleRadians) < 0.01f)
                    return;

                float turningRadius = bike.Wheelbase / Mathf.Tan(steeringAngleRadians);
                float rotationAmount = bike.localBikeVelocity.z / turningRadius * dt;
                float driftTurnBoost = Mathf.Lerp(1f, set.driftTurnFactor, driftIntensity);

                if (bike.isDoingWheelie)
                    rotationAmount *= set.wheelieTurnMultiplier;

                rotator.Rotate(bike.Ground.ProjectedUp, Mathf.Rad2Deg * rotationAmount * driftTurnBoost, Space.World);
            }
            else if (set.canSteerInAir)
            {
                rotator.Rotate(Vector3.up, dt * set.turnSpeedInAir * SteerSmoother, Space.World);
            }
        }
    }
}
