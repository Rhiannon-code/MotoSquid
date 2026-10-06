using MotoSquid.Rider;
using UnityEngine;

namespace MotoSquid.Bike
{
    // Visual only motion, handlebars, lean, wheel spin and wheel travel. Nothing here touches physics
    public sealed class BikeVisuals
    {
        readonly BikeController bike;
        float leanSmoother;
        float burnoutUpright01;

        public BikeVisuals(BikeController bike) => this.bike = bike;

        public float CurrentLeanAngle { get; private set; }

        public void AnimateSteering(float direction)
        {
            var set = bike.bikeSettings;
            var steering = bike.Steering;
            float steeringAngle = bike.bikeInput.HandBrake > 0 ? set.maxTurnAngle : steering.SteerAngle;
            float visualDirection = (!set.useAnalogSteering && set.useSteerBuildup) ? direction * steering.SteerBuildup : direction;

            Transform bars = bike.bikeReferences.BikeSteering;
            Quaternion target = Quaternion.Euler(0, steeringAngle * visualDirection, 0);
            bars.localRotation = Quaternion.Slerp(bars.localRotation, target, Time.deltaTime * set.steeringAnimationSpeed);
        }

        public void AnimateLean(float direction)
        {
            var set = bike.bikeSettings;
            var grip = bike.Grip;
            leanSmoother = Mathf.MoveTowards(leanSmoother, direction, Time.deltaTime * 10);

            float leanAngle = -set.maxLeanAngle * bike.bikeCurves.LeanCurve.Evaluate(bike.CachedSpeedRatio) * Mathf.Sign(leanSmoother);

            // When drifting, lean harder into the slide so it looks like the bike is fighting for grip
            if (grip.IsDrifting && grip.DriftAngle > 2f)
            {
                float driftLeanBoost = grip.DriftIntensity * Mathf.Min(grip.DriftAngle / 30f, 1f) * 0.5f;
                leanAngle += Mathf.Sign(leanSmoother) * driftLeanBoost * set.maxLeanAngle;
                leanAngle = Mathf.Clamp(leanAngle, -set.maxLeanAngle * 1.5f, set.maxLeanAngle * 1.5f);
            }

            CurrentLeanAngle = leanAngle * Mathf.Abs(leanSmoother);

            // Idle tilt is visual only, so the rider's authored idle pose is not doubled up by it
            burnoutUpright01 = Mathf.MoveTowards(burnoutUpright01,
                bike.isDoingBurnout ? set.burnoutUprightAmount : 0f,
                Time.deltaTime / Mathf.Max(0.01f, set.burnoutUprightBlendTime));
            float idleTilt = bike.idleLeanWeight * BikeIdleLean.Angle(set.idleLeanAngle,
                set.idleLeanFadeOutSpeed, bike.localBikeVelocity.magnitude, burnoutUpright01);

            Transform lean = bike.bikeReferences.LeanTransform;
            Quaternion target = Quaternion.Euler(0, 0, CurrentLeanAngle + idleTilt);
            lean.localRotation = Quaternion.Slerp(lean.localRotation, target, Time.deltaTime * set.leaningAnimationSpeed);
        }

        public void SpinTyres()
        {
            var refs = bike.bikeReferences;
            var geometry = bike.bikeGeometry;
            var drivetrain = bike.Drivetrain;
            float speed = bike.localBikeVelocity.z;

            float frontRotation = drivetrain.IsApplyingBrake ? 0 : (speed / geometry.FrontWheelRadius) * Time.deltaTime * Mathf.Rad2Deg;
            Spin(refs.FrontWheel, refs.FrontWheelParent, frontRotation);

            float rearRotation = drivetrain.IsBraking ? 0 : (speed / geometry.RearWheelRadius) * Time.deltaTime * Mathf.Rad2Deg;
            if (bike.isDoingBurnout && Mathf.Abs(speed) < 1f)
                rearRotation = (bike.bikeSettings.maxSpeed / geometry.RearWheelRadius) * Time.deltaTime * Mathf.Rad2Deg;
            Spin(refs.RearWheel, refs.RearWheelParent, rearRotation);
        }

        static void Spin(Transform wheel, Transform parent, float degrees)
        {
            wheel.RotateAround(wheel.position, parent.right, degrees);
            var rot = wheel.localRotation;
            rot.y = 0;
            rot.z = 0;
            wheel.localRotation = rot;
        }

        public void EaseWheels(float frontDroopTarget, float rearDroopTarget)
        {
            var refs = bike.bikeReferences;
            if (refs == null) return;
            EaseWheel(refs.FrontWheel, frontDroopTarget);
            EaseWheel(refs.RearWheel, rearDroopTarget);
        }

        static void EaseWheel(Transform wheel, float droopTarget)
        {
            if (wheel == null) return;
            var p = wheel.localPosition;
            float y = Mathf.Lerp(p.y, -droopTarget, Mathf.Min(25f * Time.deltaTime, 1f));
            wheel.localPosition = new Vector3(p.x, y, p.z);
        }
    }
}
