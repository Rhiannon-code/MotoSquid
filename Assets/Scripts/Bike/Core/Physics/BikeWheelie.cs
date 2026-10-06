using UnityEngine;

namespace MotoSquid.Bike
{
    // Whether the bike is up on its back wheel (decided in the physics step), and the pitch animation
    public sealed class BikeWheelie
    {
        const float AcceptableAngleForWheelie = 20f;

        readonly BikeController bike;
        bool snapDown;

        public BikeWheelie(BikeController bike) => this.bike = bike;

        public void Start()
        {
            if (bike.bikeReferences.WheelieTransform != null)
                bike.bikeReferences.WheelieTransform.localRotation = Quaternion.identity;
        }

        // Runs before the ground normals are smoothed, so it sees this step's raw hits
        public void UpdateState()
        {
            bool held = bike.bikeInput.Wheelie > 0;
            bike.isDoingWheelie = held && bike.rearWheelIsGrounded;

            var ground = bike.Ground;
            float surfaceAngleRear  = Vector3.Angle(ground.NormalRear,   Vector3.up);
            float surfaceAngleFront = Vector3.Angle(ground.NormalFront,  Vector3.up);
            float surfaceAngle      = Vector3.Angle(ground.GroundNormal, Vector3.up);

            if (surfaceAngle > AcceptableAngleForWheelie || surfaceAngleFront > AcceptableAngleForWheelie || surfaceAngleRear > AcceptableAngleForWheelie)
            {
                snapDown = bike.isDoingWheelie;
                bike.isDoingWheelie = false;
            }
            else
            {
                snapDown = false;
            }
        }

        public void Animate()
        {
            var set = bike.bikeSettings;
            var wheelie = bike.bikeReferences.WheelieTransform;
            float currentAngle = wheelie.localRotation.eulerAngles.x;

            if (bike.isDoingWheelie)
            {
                float targetAngle = bike.bikeInput.Wheelie > 0 ? -set.maxWheelieAngle : 0f;
                float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * set.wheelieAnimationSpeed);
                wheelie.localRotation = Quaternion.Euler(newAngle, 0, 0);

                // Fire when the animation reaches the maximum wheelie angle
                float normalizedAngle = currentAngle > 180f ? currentAngle - 360f : currentAngle;
                if (normalizedAngle <= -(set.maxWheelieAngle - 2f))
                    bike.bikeEvents.OnWheelieExceed?.Invoke();
            }
            else
            {
                float lerpSpeed = snapDown ? set.alignRotatorSpeedGround : set.wheelieAnimationSpeed;
                float newAngle = Mathf.LerpAngle(currentAngle, 0f, Time.deltaTime * lerpSpeed);
                wheelie.localRotation = Quaternion.Euler(newAngle, 0, 0);
            }
        }
    }
}
