using UnityEngine;

namespace MotoSquid.Bike
{
    // Finds the ground under each wheel, aligns the bike's rotator to it, and applies gravity
    public sealed class BikeGroundContact
    {
        readonly BikeController bike;

        RaycastHit frontHit, rearHit;
        Vector3 normalFront, normalRear;
        Vector3 smoothedNormalFront = Vector3.up;
        Vector3 smoothedNormalRear  = Vector3.up;
        float frontDroopTarget, rearDroopTarget;
        bool wasGrounded;

        public BikeGroundContact(BikeController bike) => this.bike = bike;

        public RaycastHit FrontHit => frontHit;
        public RaycastHit RearHit => rearHit;
        public Vector3 GroundNormal { get; private set; }
        public Vector3 NormalFront => normalFront;
        public Vector3 NormalRear => normalRear;
        public Vector3 ProjectedForward { get; private set; }
        public Vector3 ProjectedUp { get; private set; }
        public float MaxFrontDistance { get; private set; }
        public float MaxRearDistance { get; private set; }
        public float CosFront { get; private set; }
        public float CosRear { get; private set; }
        public float AirTime { get; private set; }

        // Suspension travel the last physics step asked for. The bike pitches in Update at the frame
        // rate while this is only recomputed in FixedUpdate, so applying it straight from the physics
        // step makes the wheel arrive in 20 ms steps behind a smoothly pitching bike, visible as the
        // front wheel lagging in and out of a wheelie. BikeVisuals eases toward it every frame instead
        public float FrontDroopTarget => frontDroopTarget;
        public float RearDroopTarget => rearDroopTarget;

        public void Start()
        {
            var g = bike.bikeGeometry;
            var r = bike.bikeReferences;
            wasGrounded = bike.bikeIsGrounded;

            // Rays are cast along the fork axis (-wheelParent.up), so the equilibrium hit distance
            // is radius/cos(angle) + radius. For a vertical fork (angle=0) this simplifies to 2*radius
            CosFront = Mathf.Cos(g.FrontWheelAngle * Mathf.Deg2Rad);
            CosRear  = Mathf.Cos(g.RearWheelAngle  * Mathf.Deg2Rad);
            MaxFrontDistance = g.FrontWheelRadius / CosFront + g.FrontWheelRadius;
            MaxRearDistance  = g.RearWheelRadius  / CosRear  + g.RearWheelRadius;

            ProjectedForward = r.Rotator.forward;
            ProjectedUp = r.Rotator.up;
        }

        public void SampleWheels()
        {
            var g = bike.bikeGeometry;
            var r = bike.bikeReferences;
            bike.frontWheelIsGrounded = Sample(r.FrontWheelParent, g.FrontWheelRadius, g.FrontWheelAngle, MaxFrontDistance,
                                               out frontHit, out normalFront, out frontDroopTarget);
            bike.rearWheelIsGrounded  = Sample(r.RearWheelParent, g.RearWheelRadius, g.RearWheelAngle, MaxRearDistance,
                                               out rearHit, out normalRear, out rearDroopTarget);
        }

        bool Sample(Transform wheelParent, float radius, float wheelAngle, float maxRaycastDistance,
                    out RaycastHit hit, out Vector3 groundNormal, out float droopTarget)
        {
            var suspension = bike.bikeSuspension;
            Vector3 raycastPosition  = wheelParent.position + wheelParent.up * radius;
            Vector3 raycastDirection = -wheelParent.up;
            float castRadius = suspension.suspensionCastRadius;
            float groundCheckDistance = maxRaycastDistance + suspension.extraGroundedDistance;
            bool isGrounded;

            // extraGroundedDistance exists to keep the bike counted as grounded over crests and seams,
            // NOT to place the wheel. Placing from the extended hit is what walks the front wheel off
            // its fork in a wheelie: the wheel lifts, the longer cast still finds ground below it, and
            // the suspension dutifully pushes the wheel down to reach it. Grounding uses the long
            // cast; placement only uses a hit within the suspension's actual travel
            if (Physics.SphereCast(raycastPosition, castRadius, raycastDirection, out hit, groundCheckDistance,
                                   bike.bikeSettings.drivableLayerMask, QueryTriggerInteraction.Ignore))
            {
                isGrounded   = true;
                groundNormal = hit.normal;

                float h               = radius / Mathf.Cos(wheelAngle * Mathf.Deg2Rad);
                float surfaceDistance = hit.distance + castRadius;
                float localOffset     = surfaceDistance - h - radius;

                // One continuous expression, not two branches. Branching at MaxDroop meant a frame
                // just inside it snapped the wheel to the clamp and a frame just outside lerped it
                // toward zero, so a wheelie held right at the limit jittered between the two. The
                // clamp alone bounds the travel; the cast missing entirely is still the else below
                droopTarget = Mathf.Clamp(localOffset, -radius * 0.5f, suspension.MaxDroop);
            }
            else
            {
                droopTarget  = 0f;
                isGrounded   = false;
                groundNormal = Vector3.up;
            }

#if UNITY_EDITOR
            Debug.DrawRay(raycastPosition, raycastDirection * groundCheckDistance, Color.red);
#endif
            return isGrounded;
        }

        public void UpdateSurface()
        {
            var r = bike.bikeReferences;
            bool front = bike.frontWheelIsGrounded, rear = bike.rearWheelIsGrounded;

            // Smooth raw hit normals to filter out the single frame spikes caused by road mesh seams
            float sf = bike.bikeSuspension.groundNormalSmoothing * Time.fixedDeltaTime;
            smoothedNormalFront = front ? Vector3.Slerp(smoothedNormalFront, normalFront, sf) : Vector3.up;
            smoothedNormalRear  = rear  ? Vector3.Slerp(smoothedNormalRear,  normalRear,  sf) : Vector3.up;
            normalFront = smoothedNormalFront;
            normalRear  = smoothedNormalRear;

            bike.bikeIsGrounded = front || rear;

            if (front && rear)  GroundNormal = (normalFront + normalRear).normalized;
            else if (front)     GroundNormal = normalFront;
            else if (rear)      GroundNormal = normalRear;
            else                GroundNormal = Vector3.up;

            if (front && rear)
            {
                ProjectedForward = (frontHit.point - rearHit.point).normalized;
                ProjectedForward = Vector3.ProjectOnPlane(ProjectedForward, r.Rotator.right).normalized;
                ProjectedUp = Vector3.ProjectOnPlane(GroundNormal, ProjectedForward).normalized;
            }
            else if (front || rear)
            {
                // Single wheel grounded, target a horizontal forward so the bike self levels
                ProjectedForward = Vector3.ProjectOnPlane(r.Rotator.forward, Vector3.up).normalized;
                Vector3 groundedNormal = front ? normalFront : normalRear;
                ProjectedUp = Vector3.ProjectOnPlane(groundedNormal, ProjectedForward).normalized;
            }

            if (rear && bike.isDoingWheelie)
            {
                ProjectedForward = Vector3.ProjectOnPlane(r.Rotator.forward, Vector3.up).normalized;
                ProjectedUp = Vector3.up;
            }
            else if (!front && !rear)
            {
                ProjectedForward = Vector3.ProjectOnPlane(r.Rotator.forward, GroundNormal).normalized;
                ProjectedUp = GroundNormal;
            }

            if (bike.bikeIsGrounded != wasGrounded)
            {
                if (bike.bikeIsGrounded) bike.bikeEvents.OnGrounded?.Invoke();
                else                     bike.bikeEvents.OnTakeOff?.Invoke();
            }
            wasGrounded = bike.bikeIsGrounded;

            AirTime = bike.bikeIsGrounded ? 0f : AirTime + Time.fixedDeltaTime;
        }

        // compressionTotal is the suspension's value from the previous physics step, as it always was
        public void AlignRotator(float compressionTotal)
        {
            var rotator = bike.bikeReferences.Rotator;
            Vector3 forward = bike.RotateVector(ProjectedForward, -rotator.right, bike.projectedForwardOffsetAngle);
            Quaternion targetRotation = Quaternion.LookRotation(forward, ProjectedUp);

            float rotationSpeed = bike.bikeIsGrounded ? bike.bikeSettings.alignRotatorSpeedGround : bike.bikeSettings.alignRotatorSpeedAir;

            // Snap to ground
            if (compressionTotal > bike.bikeSuspension.MaxCompression)
                rotationSpeed = 50;

            rotator.rotation = Quaternion.Slerp(rotator.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
        }

        public void AddGravity()
        {
            float multiplier = (!bike.bikeIsGrounded && AirTime >= 0.5f) ? bike.bikeSettings.fallGravityMultiplier : 1f;
            bike.bikeReferences.BikeRb.AddForce(Vector3.down * bike.bikeSettings.gravity * multiplier, ForceMode.Acceleration);
        }
    }
}
