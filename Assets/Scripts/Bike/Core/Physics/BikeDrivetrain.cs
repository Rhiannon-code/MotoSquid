using System.Collections;
using UnityEngine;

namespace MotoSquid.Bike
{
    // Throttle, brakes, reverse, rolling resistance and the gearbox
    public sealed class BikeDrivetrain
    {
        // Downshift deadband (km/h), how far below a gear's top speed the bike must drop before it
        // shifts back down. Prevents gear hunting when cruising on a boundary
        public const float GEAR_HYSTERESIS_KMH = 6f;

        readonly BikeController bike;
        int gearProperty;
        Coroutine shiftingGear;

        public BikeDrivetrain(BikeController bike) => this.bike = bike;

        public bool IsApplyingBrake { get; set; }
        public bool IsApplyingHandBrake { get; private set; }
        public bool IsBraking => IsApplyingBrake || IsApplyingHandBrake;

        public void HandleAccelerationAndReverse()
        {
            var set = bike.bikeSettings;
            var input = bike.bikeInput;
            var rb = bike.bikeReferences.BikeRb;
            var ground = bike.Ground;
            float dt = Time.fixedDeltaTime;
            float currentSpeed = bike.localBikeVelocity.z;

            if (!bike.canMove && bike.bikeIsGrounded)
            {
                // Hold bike stationary during countdown
                if (Mathf.Abs(currentSpeed) > 0.01f)
                {
                    Vector3 brakeDir = Vector3.ProjectOnPlane(ground.ProjectedForward, ground.GroundNormal);
                    rb.AddForce(brakeDir * (-currentSpeed / dt), ForceMode.Acceleration);
                }
                return;
            }

            if (!bike.bikeIsGrounded || !bike.canAccelerate || bike.Burnout.IsDoing) return;

            float targetAcceleration = 0f;

            if (input.HandBrake > 0)
            {
                targetAcceleration = currentSpeed >= 0 ? -set.handBrakeDeceleration : set.handBrakeDeceleration;
                IsApplyingHandBrake = true;
            }
            else
            {
                IsApplyingHandBrake = false;

                if (input.Accelerate > 0)
                {
                    if (currentSpeed >= 0)
                    {
                        float accel = set.acceleration * input.Accelerate * bike.bikeCurves.AccelerationCurve.Evaluate(bike.CachedSpeedRatio);
                        // Soften the launch, ease the bottom gears so the start isn't a disorienting blur
                        accel *= LaunchAccelMultiplier();
                        // Clamp to 0 so that being above maxSpeed (e.g. during boost) coasts
                        // naturally rather than applying a braking force
                        float speedCap = Mathf.Max(0f, (set.maxSpeed - currentSpeed) / dt);
                        targetAcceleration = Mathf.Min(accel, speedCap);
                    }
                    else
                    {
                        targetAcceleration = set.deceleration;
                    }
                    IsApplyingBrake = currentSpeed < 0;
                }
                else if (input.Reverse > 0)
                {
                    targetAcceleration = currentSpeed >= 0
                        ? -set.deceleration
                        : Mathf.Max(-set.reverseAcceleration * input.Reverse * bike.bikeCurves.AccelerationCurve.Evaluate(bike.CachedSpeed / set.reverseMaxSpeed),
                                    -(set.reverseMaxSpeed + currentSpeed) / dt);
                    IsApplyingBrake = currentSpeed >= 0;
                }
                else
                {
                    AddRollingResistance();
                    IsApplyingBrake = false;
                }
            }

            if (IsApplyingHandBrake)
            {
                float clampedAcceleration = Mathf.Abs(currentSpeed / dt);
                targetAcceleration = Mathf.Clamp(targetAcceleration, -clampedAcceleration, clampedAcceleration);
            }

            Vector3 accelerationDirection = Vector3.ProjectOnPlane(ground.ProjectedForward, ground.GroundNormal);
            rb.AddForce(accelerationDirection * targetAcceleration, ForceMode.Acceleration);
        }

        void AddRollingResistance()
        {
            var ground = bike.Ground;
            float rolling = bike.bikeSettings.rollingResistance;
            float speed = bike.localBikeVelocity.z;
            Vector3 direction = Vector3.ProjectOnPlane(ground.ProjectedForward, ground.GroundNormal);
            Vector3 force = Vector3.ClampMagnitude(-rolling * Mathf.Sign(speed) * direction, rolling * Mathf.Abs(speed));
            bike.bikeReferences.BikeRb.AddForce(force, ForceMode.Acceleration);
        }

        public void ApplyBrake()
        {
            var ground = bike.Ground;
            float decel = bike.bikeSettings.deceleration;
            float speed = bike.localBikeVelocity.z;
            Vector3 direction = Vector3.ProjectOnPlane(ground.ProjectedForward, ground.GroundNormal);
            Vector3 force = Vector3.ClampMagnitude(-decel * Mathf.Sign(speed) * direction, decel * Mathf.Abs(speed));
            bike.bikeReferences.BikeRb.AddForce(force, ForceMode.Acceleration);
        }

        public void UpdateGearShift()
        {
            // Held on the start grid during the countdown (canMove == false): revving is allowed
            // but the gear must never climb, stay in 1st so the launch is consistent and the
            // speed FX gear effects don't fire before GO
            if (!bike.canMove)
            {
                bike.currentGear = 1;
                if (GearProperty != bike.currentGear)
                    GearProperty = bike.currentGear;
                return;
            }

            var gearSpeeds = bike.gearSpeeds;
            // Forward speed only: a shunt from a racer alongside adds sideways speed for a frame or two,
            // which the total used to count, shifting up and straight back down
            float speedKmh = Mathf.Abs(bike.localBikeVelocity.z) * 3.6f;
            // Hysteresis, upshift at the gear's top speed, but only downshift once speed has dropped a
            // band BELOW it. Without this, cruising right on a gear boundary rapidly flips gears, which
            // spammed the clutch cut, the shift SFX, and the camera/BikeSpeedFX gear shake
            int g = Mathf.Clamp(bike.currentGear, 1, gearSpeeds.Length + 1);
            while (g <= gearSpeeds.Length && speedKmh > gearSpeeds[g - 1]) g++;
            while (g > 1 && speedKmh < gearSpeeds[g - 2] - GEAR_HYSTERESIS_KMH) g--;
            bike.currentGear = g;
            if (GearProperty != bike.currentGear)
                GearProperty = bike.currentGear;
        }

        public int GearProperty
        {
            get => gearProperty;
            set
            {
                int previous = gearProperty;
                gearProperty = value;

                if (bike.localBikeVelocity.z > 0 && bike.bikeIsGrounded)
                {
                    bike.bikeEvents.OnGearChange.Invoke();
                    bike.RaiseGearChanged();

                    // Clutch cut on UPSHIFT only, a downshift while braking shouldn't kill the throttle
                    if (value > previous)
                    {
                        if (shiftingGear != null) bike.StopCoroutine(shiftingGear);
                        shiftingGear = bike.StartCoroutine(ClutchCut());
                    }
                }
            }
        }

        // Brief throttle "kick" on an upshift. Short so an arcade racer with 5 gears doesn't feel
        // like it stalls on every shift (0.3s-0.1s-0.06s, the audio gap between gears tracked the
        // speed plateau during this cut, so a shorter cut also tightens the engine note across a shift)
        IEnumerator ClutchCut()
        {
            bike.canAccelerate = false;
            yield return new WaitForSeconds(0.06f);
            bike.canAccelerate = true;
        }

        // 0 at the bottom of the current gear, ramping to 1 as the bike nears the next
        // shift point (same band the engine pitch curve uses). Read by BikeSpeedFX
        // (chromatic build up) and the camera (FOV build up) for redline tension that
        // releases the instant the gear changes and this snaps back toward 0
        public float GearProgress01
        {
            get
            {
                var gearSpeeds = bike.gearSpeeds;
                int currentGear = bike.currentGear;
                if (gearSpeeds == null || gearSpeeds.Length == 0) return 0f;
                // Top gear has no upcoming shift, so there's nothing to build toward,
                // return 0 so the tension FX don't stick on with no release
                if (currentGear > gearSpeeds.Length) return 0f;
                float speedKmh  = Mathf.Abs(bike.CachedSpeed * 3.6f);
                int   gearIndex = Mathf.Clamp(currentGear - 1, 0, gearSpeeds.Length - 1);
                float bottomKmh = gearIndex > 0 ? gearSpeeds[gearIndex - 1] : 0f;
                float topKmh    = gearSpeeds[gearIndex];
                if (topKmh <= bottomKmh) return 0f;
                return Mathf.Clamp01(Mathf.InverseLerp(bottomKmh, topKmh, speedKmh));
            }
        }

        // Launch easing, scales acceleration down in the lowest gears so the bike doesn't rip
        // through them on a race start. Ramps smoothly from launchAccelMultiplier (standstill)
        // up to 1 at the top of the slow-gear band, so there's no acceleration surge when the
        // easing ends, at the shift out of the last slow gear it's already back to full pull
        float LaunchAccelMultiplier()
        {
            var set = bike.bikeSettings;
            var gearSpeeds = bike.gearSpeeds;
            int slow = set.slowLaunchGears;
            if (slow <= 0 || bike.currentGear > slow) return 1f;
            float bandTopKmh = (gearSpeeds != null && gearSpeeds.Length > 0)
                ? gearSpeeds[Mathf.Min(slow, gearSpeeds.Length) - 1]
                : set.maxSpeed * 3.6f;
            float speedKmh = Mathf.Abs(bike.CachedSpeed * 3.6f);
            float t = bandTopKmh > 0f ? Mathf.Clamp01(speedKmh / bandTopKmh) : 1f;
            return Mathf.Lerp(set.launchAccelMultiplier, 1f, t);
        }
    }
}
