using UnityEngine;

namespace MotoSquid.Bike
{
    // The start-line burnout: spinning on the spot during the countdown, banking a launch charge,
    // and the launch or bog at GO depending on when the burnout was dropped
    public sealed class BikeBurnout
    {
        readonly BikeController bike;
        float burnoutLerp;
        bool wasCanMove;
        bool wasBurningOut;
        float burnoutReleaseTime = -1f;   // Time.time the burnout was dropped, for launch timing
        float launchBogTimer;             // > 0 while a mistimed launch is bogging the bike
        float nextBlockerLog;

        public BikeBurnout(BikeController bike) => this.bike = bike;

        public bool IsDoing { get; private set; }
        public bool IsRotating { get; private set; }
        public float LaunchCharge { get; private set; }   // 0..1, built while burning out on the grid

        public void Start() => wasCanMove = bike.canMove;

        public void HandleBurnoutAndRotation()
        {
            var input = bike.bikeInput;
            var set = bike.bikeSettings;
            var refs = bike.bikeReferences;
            bool isAccelerating = input.Accelerate > 0;
            bool isReversing = input.Reverse > 0;
            bool isTurningLeft = input.SteeringLeft > 0;
            bool isTurningRight = input.SteeringRight > 0;

            // Burnout is a START LINE move only (countdown, !canMove) so accel+reverse can't trigger it
            // mid-race. Holding it banks a launch charge (see UpdateLaunch) released as boost at GO
            IsDoing = isAccelerating && isReversing && bike.frontWheelIsGrounded && bike.rearWheelIsGrounded && !bike.canMove;

            if (bike.logBurnoutBlockers && isAccelerating && isReversing && !IsDoing && Time.unscaledTime > nextBlockerLog)
            {
                nextBlockerLog = Time.unscaledTime + 1f;
                Debug.LogWarning($"[BikeController] '{bike.name}' burnout blocked: frontGrounded=" +
                                 $"{bike.frontWheelIsGrounded} rearGrounded={bike.rearWheelIsGrounded} " +
                                 $"canMove={bike.canMove} (burnout needs canMove FALSE).", bike);
            }
            IsRotating = (isTurningLeft || isTurningRight) && isAccelerating && isReversing && bike.CachedSpeed < set.burnoutMaxRotationSpeed;

            if (!IsDoing) return;

            bike.Drivetrain.ApplyBrake();
            bike.Drivetrain.IsApplyingBrake = true;

            if (IsRotating)
            {
                float rotationDirection = isTurningLeft ? -1f : 1f;
                burnoutLerp = Mathf.MoveTowards(burnoutLerp, 1f, Time.fixedDeltaTime * set.burnoutSmoothness);

                // Amount of rotation in this fixed step (degrees)
                float rotationAmount = set.burnoutRotationSpeed * Time.fixedDeltaTime;
                refs.Rotator.Rotate(Vector3.up, rotationAmount * rotationDirection * burnoutLerp, Space.World);

                float distance = Vector3.Distance(refs.FrontWheel.position, refs.Rotator.position);
                float speed = rotationAmount * distance;
                refs.BikeRb.linearVelocity = -rotationDirection * refs.Rotator.right * speed * burnoutLerp;
            }
            else
            {
                burnoutLerp = 0;
            }
        }

        public void UpdateLaunch()
        {
            var set = bike.bikeSettings;
            var rb = bike.bikeReferences.BikeRb;

            if (IsDoing)
                LaunchCharge = Mathf.MoveTowards(LaunchCharge, 1f, set.burnoutLaunchChargeRate * Time.fixedDeltaTime);

            // Note the instant the player DROPS the burnout (before GO), the launch is timed
            // from here. Holding it through GO leaves burnoutReleaseTime at -1 (over-held)
            if (wasBurningOut && !IsDoing && !bike.canMove)
                burnoutReleaseTime = Time.time;
            wasBurningOut = IsDoing;

            // GO (canMove false-true), a burnout dropped within the window launches you (boost), one
            // dropped too early, or held all the way through GO, bogs the bike briefly instead
            if (!wasCanMove && bike.canMove)
            {
                if (LaunchCharge > 0.01f)
                {
                    bool droppedInTime = burnoutReleaseTime >= 0f && (Time.time - burnoutReleaseTime) <= set.burnoutLaunchWindow;
                    if (droppedInTime)
                    {
                        // Instant getaway shove off the line, scaled by the banked burnout charge
                        Vector3 fwd = bike.bikeReferences.Rotator != null ? bike.bikeReferences.Rotator.forward : bike.transform.forward;
                        rb.linearVelocity += fwd * (LaunchCharge * set.burnoutLaunchKmh / 3.6f);
                    }
                    else
                    {
                        launchBogTimer = set.burnoutBogDuration;
                    }

                    bike.RaiseBurnoutLaunch(droppedInTime);
                }
                LaunchCharge = 0f;
                burnoutReleaseTime = -1f;
            }
            wasCanMove = bike.canMove;

            // Bog, a mistimed launch drags the bike for a moment so it gets off the line poorly
            if (launchBogTimer > 0f)
            {
                launchBogTimer -= Time.fixedDeltaTime;
                rb.linearVelocity *= Mathf.Exp(-set.burnoutBogDrag * Time.fixedDeltaTime);
            }
        }
    }
}
