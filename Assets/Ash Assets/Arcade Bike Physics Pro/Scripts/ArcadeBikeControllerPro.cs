using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;


namespace ArcadeBP_Pro
{
    public class ArcadeBikeControllerPro : MonoBehaviour
    {
        #region Public Inspector Variables

        [Serializable]
        public class BikeInput
        {
            [Tooltip("The float to accelerate the bike.")]
            public float Accelerate = 0;

            [Tooltip("The float to reverse the bike.")]
            public float Reverse = 0;

            [Tooltip("The float to apply the hand brake.")]
            public float HandBrake = 0;

            [Tooltip("The float to steer the bike left.")]
            public float SteeringLeft = 0;

            [Tooltip("The float to steer the bike right.")]
            public float SteeringRight = 0;

            [Tooltip("The float to perform a wheelie.")]
            public float Wheelie = 0;

        }

        [Tooltip("The input data for the bike.")]
        [NonSerialized] public BikeInput bikeInput = new BikeInput();


        [Serializable]
        public class BikeReferences
        {
            [Tooltip("The main transform used for aligning the bike�s rotation with the ground.")]
            public Transform Rotator;

            [Tooltip("The transform used for controlling the wheelie animation.")]
            public Transform WheelieTransform;

            [Tooltip("The transform used for leaning the bike during turns.")]
            public Transform LeanTransform;

            [Tooltip("The parent transform for the front wheel.")]
            public Transform FrontWheelParent;

            [Tooltip("The parent transform for the rear wheel.")]
            public Transform RearWheelParent;

            [Tooltip("The actual front wheel transform.")]
            public Transform FrontWheel;

            [Tooltip("The actual rear wheel transform.")]
            public Transform RearWheel;

            [Tooltip("The transform used for steering the bike.")]
            public Transform BikeSteering;

            [Tooltip("The parent transform for the bike steering.")]
            public Transform BikeSteeringParent;

            [Tooltip("Meshes associated with the bike�s steering.")]
            public Transform SteeringMeshes;

            [Tooltip("The main model of the bike.")]
            public Transform BikeModel;

            [Tooltip("The mesh representing the bike�s body.")]
            public Transform BodyMesh;

            [Tooltip("The Rigidbody component attached to the bike.")]
            public Rigidbody BikeRb;

            [Tooltip("The CapsuleCollider attached to the bike.")]
            public CapsuleCollider collider;

            [Tooltip("Prefab for the skidmarks.")]
            public GameObject skidmarksPrefab;

            [Tooltip("Prefab for the tire smoke.")]
            public GameObject tireSmokePrefab;

            [Tooltip("Custom class for managing biker animations.")]
            public BikerAnimationTargets BikerAnimationTargets;

            [Tooltip("camera Controller of the bike.")]
            public CameraController cameraController;

            [Tooltip("ragdoll activator of the bike")]
            public RagdollActivator ragdollActivator;
        }
        public BikeReferences bikeReferences;


        [Serializable]
        public class BikeGeometry
        {
            [Tooltip("Radius of the front wheel. Affects the ground contact area and suspension calculations.")]
            public float FrontWheelRadius = 0.5f;

            [Tooltip("Width of the front wheel.")]
            public float FrontWheelWidth = 0.5f;

            [Tooltip("Radius of the rear wheel.")]
            public float RearWheelRadius = 0.5f;

            [Tooltip("Width of the rear wheel.")]
            public float RearWheelWidth = 0.5f;

            [Tooltip("Angle of the front wheel relative to the bike�s body. Affects the bike�s suspension and wheel placement calculations.")]
            public float FrontWheelAngle = 15f;

            [Tooltip("Angle of the rear wheel relative to the bike�s body. Similar to the front wheel angle but for the rear wheel.")]
            public float RearWheelAngle = 0f;
        }
        public BikeGeometry bikeGeometry;


        [Serializable]
        public class BikeSuspension
        {
            [Tooltip("The force exerted by the bike�s suspension springs. Higher values result in a stiffer suspension.")]
            public float SpringForce = 100;

            [Tooltip("The force exerted by the dampers to absorb shocks. Higher values result in less oscillation.")]
            public float DamperForce = 10;

            [Tooltip("A factor to adjust how well the bike sticks to the ground.")]
            public float groundStickFactor = 0.2f;

            [Tooltip("Maximum allowable compression for the suspension. Prevents the bike from bottoming out.")]
            public float MaxCompression = 0.5f;
        }
        public BikeSuspension bikeSuspension;


        [Serializable]
        public class BikeSettings
        {
            [Tooltip("LayerMask for drivable surfaces. Remove any layer that you dont want bike to drive on")]
            public LayerMask drivableLayerMask = ~0;

            [Tooltip("Maximum speed the bike can reach in the forward direction.")]
            public float maxSpeed = 80;

            [Tooltip("Maximum speed the bike can reach in reverse.")]
            public float reverseMaxSpeed = 3;

            [Tooltip("Acceleration rate of the bike.")]
            public float acceleration = 10;

            [Tooltip("Acceleration rate when the bike is in reverse.")]
            public float reverseAcceleration = 5;

            [Tooltip("Rate at which the bike decelerates when opposite button(to the bike move direction) is pressed. Basically brake amount. How fast the bike will reach 0 speed.")]
            public float deceleration = 40;

            [Tooltip("Deceleration rate when the hand brake is applied.")]
            public float handBrakeDeceleration = 10;

            [Tooltip("Maximum angle the bike handle can turn.")]
            public float maxTurnAngle = 20;

            [Tooltip("Whether to use smooth turning. Sometimes you don't want instant left to right turning. It delays how fast we can turn the bike by inputs.")]
            public bool useLerpTurning = false;

            [Tooltip("Speed of smooth turning if `useLerpTurning` is true.")]
            public float turnLerpSpeed = 5;

            [Tooltip("Speed of the steering animation.")]
            public float steeringAnimationSpeed = 5;

            [Tooltip("Whether the bike can steer while in the air (not grounded).")]
            public bool canSteerInAir = false;

            [Tooltip("Whether the bike can lean while in the air (not grounded).")]
            public bool canLeanInAir = false;

            [Tooltip("Speed of turning while in the air if `canSteerInAir` is true.")]
            public float turnSpeedInAir = 50;

            [Tooltip("Maximum lean angle for the bike.")]
            public float maxLeanAngle = 40;

            [Tooltip("Speed of the leaning animation.")]
            public float leaningAnimationSpeed = 5;

            [Tooltip("Coefficient for calculating friction force (sideways force).")]
            public float frictionCoefficient = 0.5f;

            [Tooltip("Factor to adjust friction during drifts (when handbrake is applied).")]
            public float driftFrictionFactor = 0.5f;

            [Tooltip("Factor to adjust turning during drifts (when handbrake is applied).")]
            public float driftTurnFactor = 2f;

            [Tooltip("Resistance applied to the bike to simulate rolling friction. It determines how fast bike will come to rest (0 speed) if acceleration/deceleration input is not applied.")]
            public float rollingResistance = 1;

            [Tooltip("Gravity force applied to the bike.")]
            public float gravity = 9.81f;

            [Tooltip("Speed of rotation during a burnout.")]
            public float burnoutRotationSpeed = 10f;

            [Tooltip("Smoothness of the burnout transition.")]
            public float burnoutSmoothness = 1f;

            [Tooltip("Maximum angle for performing a wheelie. upto 45 works best, above 45 rear wheel will go inside the ground a bit.")]
            [Range(0, 60)]
            public float maxWheelieAngle = 30;

            [Tooltip("Speed of the wheelie animation.")]
            public float wheelieAnimationSpeed = 3f;

            [Tooltip("Speed of aligning the rotator when the bike is on the ground. How fast bike align with the ground surface.")]
            public float alignRotatorSpeed_Ground = 10;

            [Tooltip("Speed of aligning the rotator when the bike is in the air. How fast bike align with the world up axis.")]
            public float alignRotatorSpeed_Air = 5f;
        }
        public BikeSettings bikeSettings;


        [Serializable]
        public class BikeCurves
        {
            [Tooltip("Adjusts the acceleration. X-axis represents speed, Y-axis represents acceleration multiplier. Example: Curve from (0, 1) to (1, 0.5) means high acceleration at low speed, decreasing to half acceleration at max speed.")]
            public AnimationCurve AccelerationCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.5f, 0.8f), new Keyframe(1, 0.5f));

            [Tooltip("Adjusts the reverse acceleration. X-axis represents reverse speed, Y-axis represents acceleration multiplier.")]
            public AnimationCurve ReverseAccelerationCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.2f));

            [Tooltip("Adjusts the steering sensitivity. X-axis represents speed, Y-axis represents steering sensitivity. Example: Curve from (0, 1) to (1, 0.3) means sensitive steering at low speed, decreasing sensitivity at high speed. You will have to adjust it with multiple points to fine tune the turning.")]
            public AnimationCurve SteeringCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.3f));

            [Tooltip("Adjusts friction based on speed. X-axis represents sideways speed, Y-axis represents friction multiplier. Example: Curve from (0, 1) to (1, 0.1) means high friction at low sideways speed (meaning when bike is not sliding sideways), decreasing to low friction at high sideways speed (meaning when bike is sliding sideways).")]
            public AnimationCurve FrictionCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.1f, 0.5f), new Keyframe(1, 0.1f));

            [Tooltip("Adjusts the lean angle based on speed. X-axis represents speed, Y-axis represents lean angle multiplier. Example: Curve from (0, 0.2) to (1, 1) means minimal lean at low speed, increasing to maximum lean at high speed.")]
            public AnimationCurve LeanCurve = new AnimationCurve(new Keyframe(0, 0.2f), new Keyframe(1, 1));
        }
        public BikeCurves bikeCurves;


        [Serializable]
        public class BikeAudio
        {
            [Tooltip("The audio source for the engine sound.")]
            public AudioSource engineSound;

            [Tooltip("The audio source for the gear shift sound.")]
            public AudioSource gearShiftSound;

            [Tooltip("Minimum pitch of the engine sound.")]
            [Range(0, 1)]
            public float minPitch = 0.5f;

            [Tooltip("Maximum pitch of the engine sound.")]
            [Range(1, 3)]
            public float maxPitch = 2.0f;

            [Tooltip("The audio source for the skid sound.")]
            public AudioSource SkidSound;
        }
        public BikeAudio bikeAudio;


        [Tooltip("Array of speeds for each gear. The bike will shift gears based on its current speed in kmph.")]
        public int[] gearSpeeds = new int[] { 40, 80, 120, 160, 220 };

        [Tooltip("The current gear of the bike.")]
        public int currentGear = 1;


        [Serializable]
        public class BikeEvents
        {
            [Tooltip("Event triggered when the bike takes off from the ground.")]
            public UnityEvent OnTakeOff;

            [Tooltip("Event triggered when the bike lands on the ground.")]
            public UnityEvent OnGrounded;

            [Tooltip("Event triggered when the bike changes gears.")]
            public UnityEvent OnGearChange;

            //[Tooltip("Event triggered when the Wheelie Angle Exceeds Max Wheelie Angle")]
            //public UnityEvent OnWheelieExceed;
        }
        public BikeEvents bikeEvents;

        #endregion


        #region NonInspector Variables

        //private and public hidden variables
        private float maxFrontRaycastDistance;
        private float maxRearRaycastDistance;
        private float wheelbase;
        private float snapDistance;

        Vector3 groundNormal, groundNormal_front, groundNormal_rear, groundPosition_front, groundPosition_rear;
        Vector3 projectedBikeForward, projectedBikeUp;
        [HideInInspector] public float projectedForwardOffsetAngle;
        RaycastHit frontWheelHit, rearWheelHit;
        [HideInInspector] public bool bikeIsGrounded, frontWheelIsGrounded, rearWheelIsGrounded, isDoingWheelie = false, canAccelerate = true, canTurn = true;

        float compression_f, compression_r, compression_Total;

        [HideInInspector] public Vector3 localBikeVelocity { get; private set; }

        public float CurrentSteerInput { get; private set; }


        public SkidmarkController skidmarkController { get; private set; }
        private ParticleSystem tireSmoke_ps;

        #endregion


        #region Unity Methods

        private void Awake()
        {
            bikeInput = new BikeInput();

            if (bikeReferences.skidmarksPrefab != null)
            {
                var skidmarkControllerInstance = Instantiate(bikeReferences.skidmarksPrefab);
                skidmarkController = skidmarkControllerInstance?.GetComponent<SkidmarkController>();
                if (skidmarkController == null)
                {
                    Debug.LogWarning("No Skidmarks Controller found in the prefab");
                }
            }
            else
            {
                Debug.LogWarning("No Skidmarks Prefab has been assigned");
            }

            if (bikeReferences.tireSmokePrefab != null)
            {
                var tireSmokeInstance = Instantiate(bikeReferences.tireSmokePrefab, bikeReferences.RearWheelParent);
                tireSmokeInstance.transform.localPosition = new Vector3(0, -bikeGeometry.RearWheelRadius, 0);
                tireSmoke_ps = tireSmokeInstance?.GetComponent<ParticleSystem>();
                if (tireSmoke_ps != null)
                {
                    tireSmoke_ps.Stop();
                }
                else
                {
                    Debug.LogWarning("No ParticleSystem found in the Tire Smoke prefab");
                }
            }
            else
            {
                Debug.LogWarning("No Tire Smoke Prefab has been assigned");
            }
        }


        private void Start()
        {
            bikeReferences.BikeRb.useGravity = false;
            wasGrounded = bikeIsGrounded;

            // calculate max raycast distance
            maxFrontRaycastDistance = bikeGeometry.FrontWheelRadius * (1 / Mathf.Cos(bikeGeometry.FrontWheelAngle * Mathf.Deg2Rad)) + bikeGeometry.FrontWheelRadius;
            maxRearRaycastDistance = bikeGeometry.RearWheelRadius * (1 / Mathf.Cos(bikeGeometry.RearWheelAngle * Mathf.Deg2Rad)) + bikeGeometry.RearWheelRadius;

            wheelbase = Vector3.Distance(bikeReferences.FrontWheel.position, bikeReferences.RearWheel.position);

            // Initialize projected bike directions and angles
            projectedBikeForward = bikeReferences.Rotator.forward;
            projectedBikeUp = bikeReferences.Rotator.up;
            projectedForwardOffsetAngle = Vector3.Angle(bikeReferences.Rotator.forward,
                                          (bikeReferences.FrontWheelParent.position - bikeReferences.RearWheelParent.position).normalized);

            // Set the skidmark width
            if (skidmarkController != null)
            {
                skidmarkController.SkidmarkWidth = bikeGeometry.RearWheelWidth;
            }

        }

        private void Update()
        {
            PlaceWheelOnGround(bikeReferences.FrontWheelParent, bikeReferences.FrontWheel, bikeGeometry.FrontWheelRadius, bikeGeometry.FrontWheelAngle,
                               maxFrontRaycastDistance, out frontWheelHit, out frontWheelIsGrounded, out groundNormal_front);
            PlaceWheelOnGround(bikeReferences.RearWheelParent, bikeReferences.RearWheel, bikeGeometry.RearWheelRadius, bikeGeometry.RearWheelAngle,
                               maxRearRaycastDistance, out rearWheelHit, out rearWheelIsGrounded, out groundNormal_rear);

            // call calculate surface params only after placewheelonground is called.
            calculateSurfaceParams();
            AlignRotator(bikeReferences.Rotator, projectedBikeForward, projectedBikeUp);

            //=======================================================================================================================//

            //Update Steer Input
            CurrentSteerInput = bikeInput.SteeringRight - bikeInput.SteeringLeft;

            steeringAnimation(CurrentSteerInput);
            AddTurning(CurrentSteerInput);

            //=======================================================================================================================//

            //Update leaning Animation
            if (CurrentSteerInput != 0)
            {
                if (bikeIsGrounded || bikeSettings.canLeanInAir)
                {
                    leaningAnimation(CurrentSteerInput);
                }
            }
            else
            {
                leaningAnimation(0);
            }

            //=======================================================================================================================//

            ApplyBurnoutRotation();

            wheelieAnimation();

            tireAnimation();

            UpdateEngineSound();

            CalculateWheelSlips();

            UpdateSkidmarks();

            UpdateTireSmoke();

            UpdateSkidSound();
        }

        private void FixedUpdate()
        {
            localBikeVelocity = bikeReferences.Rotator.InverseTransformDirection(bikeReferences.BikeRb.linearVelocity);

            AddGravity();

            calculateSuspensionParams();
            AddSuspension();

            AddFriction();

            HandleAccelerationAndReverse();

            HandleBurnoutAndRotation();

            UpdateGearShift();

        }

        #endregion


        #region Gravity

        private void AddGravity()
        {
            //if (bikeIsGrounded)
            //{
            //    bikeReferences.BikeRb.AddForce(-groundNormal * bikeSettings.gravity * bikeReferences.BikeRb.mass, ForceMode.Acceleration);
            //}
            //else
            //{
            //    bikeReferences.BikeRb.AddForce(Vector3.down * bikeSettings.gravity * bikeReferences.BikeRb.mass, ForceMode.Acceleration);
            //}


            //use above logic if you want bike to stick to ground if its grounded instead of below logic.
            bikeReferences.BikeRb.AddForce(Vector3.down * bikeSettings.gravity, ForceMode.Acceleration);
        }

        #endregion


        #region Wheels Placement

        private void PlaceWheelOnGround(Transform wheelParent, Transform wheel, float radius, float wheelAngle, float maxRaycastDistance,
                                        out RaycastHit hit, out bool isGrounded, out Vector3 groundNormal)
        {
            Vector3 raycastPosition = wheelParent.position + wheelParent.up * radius;
            Vector3 raycastDirection = -wheelParent.up;

            if (Physics.Raycast(raycastPosition, raycastDirection, out hit, maxRaycastDistance, bikeSettings.drivableLayerMask, QueryTriggerInteraction.Ignore))
            {
                float h = radius * (1 / Mathf.Cos(wheelAngle * Mathf.Deg2Rad));
                float localOffset = hit.distance - h - radius;
                wheel.localPosition = new Vector3(0, -localOffset, 0);
                isGrounded = true;
                groundNormal = hit.normal;
            }
            else
            {
                wheel.localPosition = Vector3.Lerp(wheel.localPosition, new Vector3(0, 0, 0), 20 * Time.deltaTime);
                isGrounded = false;
                groundNormal = Vector3.up;
            }

            // debug raycast
            Debug.DrawRay(raycastPosition, raycastDirection * maxRaycastDistance, Color.red);
        }

        #endregion


        #region Alignment

        private bool wasGrounded;

        private void calculateSurfaceParams()
        {
            bikeIsGrounded = frontWheelIsGrounded || rearWheelIsGrounded;

            if (frontWheelIsGrounded && rearWheelIsGrounded)
            {
                groundNormal = (groundNormal_front + groundNormal_rear).normalized;
            }
            else if (frontWheelIsGrounded)
            {
                groundNormal = groundNormal_front;
            }
            else if (rearWheelIsGrounded)
            {
                groundNormal = groundNormal_rear;
            }
            else
            {
                groundNormal = Vector3.up;
            }

            if (frontWheelIsGrounded || rearWheelIsGrounded)
            {
                projectedBikeForward = (bikeReferences.FrontWheel.position - bikeReferences.RearWheel.position).normalized;
                projectedBikeForward = Vector3.ProjectOnPlane(projectedBikeForward, bikeReferences.Rotator.right).normalized;

                Vector3 sidePlane = Vector3.ProjectOnPlane(bikeReferences.Rotator.right, Vector3.up).normalized;
                Vector3 sideProjectedgroundNormal = Vector3.ProjectOnPlane(groundNormal, sidePlane).normalized;

                projectedBikeUp = Vector3.ProjectOnPlane(sideProjectedgroundNormal, projectedBikeForward).normalized;
            }

            if (rearWheelIsGrounded && isDoingWheelie)
            {
                projectedBikeForward = Vector3.ProjectOnPlane(bikeReferences.Rotator.forward, Vector3.up).normalized;
                projectedBikeUp = Vector3.up;
            }
            else if (!frontWheelIsGrounded && !rearWheelIsGrounded)
            {
                projectedBikeForward = Vector3.ProjectOnPlane(bikeReferences.Rotator.forward, groundNormal).normalized;
                projectedBikeUp = groundNormal;
            }

            if (bikeIsGrounded != wasGrounded)
            {
                if (bikeIsGrounded)
                {
                    bikeEvents.OnGrounded?.Invoke();
                }
                else
                {
                    bikeEvents.OnTakeOff?.Invoke();
                }
            }

            wasGrounded = bikeIsGrounded;

        }


        private void AlignRotator(Transform rotator, Vector3 projectedForward, Vector3 projectedUp)
        {
            Vector3 newProjectedForward = RotateVector(projectedForward, -bikeReferences.Rotator.right, projectedForwardOffsetAngle);

            Quaternion targetRotation = Quaternion.LookRotation(newProjectedForward, projectedUp);

            float rotationSpeed = bikeIsGrounded ? bikeSettings.alignRotatorSpeed_Ground : bikeSettings.alignRotatorSpeed_Air;

            //snap to ground
            if (compression_Total > bikeSuspension.MaxCompression)
            {
                rotationSpeed = 50;
            }

            Quaternion slerpRotation = Quaternion.Slerp(rotator.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            rotator.rotation = slerpRotation;

        }

        #endregion


        #region Suspension

        private void calculateSuspensionParams()
        {
            //suspension compression params
            compression_f = frontWheelIsGrounded ? (maxFrontRaycastDistance - frontWheelHit.distance) / maxFrontRaycastDistance : 0;
            compression_f = Mathf.Clamp(compression_f, 0, 1);
            compression_r = rearWheelIsGrounded ? (maxRearRaycastDistance - rearWheelHit.distance) / maxRearRaycastDistance : 0;
            compression_r = Mathf.Clamp(compression_r, 0, 1);
            compression_Total = ((compression_f) + (compression_r)) / 2;
            compression_Total = Mathf.Clamp01(compression_Total);

            // bike snap to ground params
            float f_cos_theta = Mathf.Cos(bikeGeometry.FrontWheelAngle * Mathf.Deg2Rad);
            float r_cos_theta = Mathf.Cos(bikeGeometry.RearWheelAngle * Mathf.Deg2Rad);
            float mard = ((maxFrontRaycastDistance * f_cos_theta) + (maxRearRaycastDistance * r_cos_theta)) / 2; // max average ray distance
            compressionForSnap = ((compression_f * f_cos_theta) + (compression_r * r_cos_theta)) / 2;
            snapDistance = mard * (compressionForSnap - bikeSuspension.MaxCompression);

        }

        float suspensionForceMagnitude = 0;
        bool bikeSnappedToGround = false;
        float compressionForSnap;

        private void AddSuspension()
        {
            Vector3 normal = Vector3.up;

            if (frontWheelIsGrounded && rearWheelIsGrounded)
            {
                normal = groundNormal;
            }
            else if (rearWheelIsGrounded)
            {
                normal = rearWheelHit.normal;
            }

            Vector3 sidePlaneNormal = Vector3.Cross(
                (bikeReferences.FrontWheelParent.position - bikeReferences.RearWheelParent.position).normalized,
                (Vector3.up * Vector3.Dot(bikeReferences.Rotator.up, transform.up)).normalized
            ).normalized;

            Vector3 suspensionNormal = Vector3.ProjectOnPlane(normal, sidePlaneNormal);

            Vector3 springDir = suspensionNormal.normalized;
            Debug.DrawRay(transform.position, springDir, Color.green);

            float springVel = Vector3.Dot(bikeReferences.BikeRb.linearVelocity, springDir);
            float springForce = bikeSuspension.SpringForce * (compression_Total - bikeSuspension.groundStickFactor * compression_Total);
            float damperForce = bikeSuspension.DamperForce * springVel;

            //if ((compression_f > bikeSuspension.MaxCompression || compression_r > bikeSuspension.MaxCompression) && springVel < 0)
            //{
            //    damperForce *= compression_Total * 100;
            //}

            damperForce = Mathf.Clamp(damperForce, -Mathf.Abs(springVel) / Time.fixedDeltaTime, Mathf.Abs(springVel) / Time.fixedDeltaTime);

            if (compression_Total < 0.05f)
            {
                damperForce = 0;
            }

            Vector3 suspensionForce = springDir * (springForce - damperForce);
            suspensionForceMagnitude = suspensionForce.magnitude;

            if (bikeIsGrounded)
            {
                //snap to ground
                if (compressionForSnap > bikeSuspension.MaxCompression)
                {
                    if (!bikeSnappedToGround)
                    {
                        Vector3 SnapMovePosition = bikeReferences.BikeRb.position + (projectedBikeUp * snapDistance);
                        bikeReferences.BikeRb.MovePosition(SnapMovePosition);
                        reduceSurfaceNormalDownVelocity(0.8f);

                        bikeSnappedToGround = true;
                    }
                }
                else
                {
                    bikeSnappedToGround = false;
                }

                float leanAngle = Mathf.Abs(bikeReferences.LeanTransform.localEulerAngles.z);
                float leanFactor = Mathf.Cos(Mathf.Deg2Rad * leanAngle);
                bikeReferences.BikeRb.AddForce(suspensionForce * leanFactor, ForceMode.Acceleration);
            }

        }

        #endregion


        #region Friction

        float frictionForceMagnitude;

        private void AddFriction()
        {
            if (bikeIsGrounded && !isBournoutRotating)
            {
                float driftFrictionFactorCurrent = bikeInput.HandBrake > 0 ? bikeSettings.driftFrictionFactor : 1;

                float sideVelocityRatio = Mathf.Abs(localBikeVelocity.x / bikeSettings.maxSpeed);
                float frictionForce = (-localBikeVelocity.x * bikeSettings.frictionCoefficient / Time.fixedDeltaTime) * bikeCurves.FrictionCurve.Evaluate(sideVelocityRatio);

                Vector3 TotalFrictionForceVector = bikeReferences.Rotator.right * frictionForce * driftFrictionFactorCurrent;

                bikeReferences.BikeRb.AddForce(TotalFrictionForceVector, ForceMode.Acceleration);

                frictionForceMagnitude = frictionForce;
            }
            else
            {
                frictionForceMagnitude = 0;
            }
        }

        #endregion


        #region Acceleration/brake/rolling resistance

        bool isApplyingBrake, isApplyingHandBrake;

        private void HandleAccelerationAndReverse()
        {
            if (bikeIsGrounded && canAccelerate && !isDoingBurnout)
            {
                float currentSpeed = localBikeVelocity.z;
                float targetAcceleration = 0f;

                if (bikeInput.HandBrake > 0)
                {
                    targetAcceleration = currentSpeed >= 0 ? -bikeSettings.handBrakeDeceleration : bikeSettings.handBrakeDeceleration;
                    isApplyingHandBrake = true;
                }
                else
                {
                    isApplyingHandBrake = false;

                    bool isAccelerating = bikeInput.Accelerate > 0;
                    bool isReversing = bikeInput.Reverse > 0;

                    if (isAccelerating)
                    {
                        targetAcceleration = currentSpeed >= 0
                            ? Mathf.Min(bikeSettings.acceleration * bikeInput.Accelerate * bikeCurves.AccelerationCurve.Evaluate(localBikeVelocity.magnitude / bikeSettings.maxSpeed)
                            , (bikeSettings.maxSpeed - currentSpeed) / Time.fixedDeltaTime)
                            : bikeSettings.deceleration;
                        isApplyingBrake = currentSpeed < 0;
                    }
                    else if (isReversing)
                    {
                        targetAcceleration = currentSpeed >= 0
                            ? -bikeSettings.deceleration
                            : Mathf.Max(-bikeSettings.reverseAcceleration * bikeInput.Reverse * bikeCurves.ReverseAccelerationCurve.Evaluate(localBikeVelocity.magnitude / bikeSettings.reverseMaxSpeed)
                            , -(bikeSettings.reverseMaxSpeed + currentSpeed) / Time.fixedDeltaTime);
                        isApplyingBrake = currentSpeed >= 0;
                    }
                    else
                    {
                        AddRollingResistance();
                        isApplyingBrake = false;
                    }
                }


                if (isApplyingHandBrake)
                {
                    float clampedAcceleration = Mathf.Abs(currentSpeed / Time.fixedDeltaTime);
                    targetAcceleration = Mathf.Clamp(targetAcceleration, -clampedAcceleration, clampedAcceleration);
                }
                Vector3 accelerationDirection = Vector3.ProjectOnPlane(projectedBikeForward, groundNormal);
                Vector3 accelerationForce = accelerationDirection * targetAcceleration;

                bikeReferences.BikeRb.AddForce(accelerationForce, ForceMode.Acceleration);
            }
        }

        private void AddRollingResistance()
        {
            Vector3 RollingResistanceDirection = Vector3.ProjectOnPlane(projectedBikeForward, groundNormal);
            Vector3 rollingResistanceForce = -bikeSettings.rollingResistance * Mathf.Sign(localBikeVelocity.z) * RollingResistanceDirection;
            //clamp force to max force
            float maxForce = bikeSettings.rollingResistance * Mathf.Abs(localBikeVelocity.z);
            rollingResistanceForce = Vector3.ClampMagnitude(rollingResistanceForce, maxForce);
            bikeReferences.BikeRb.AddForce(rollingResistanceForce, ForceMode.Acceleration);
        }

        private void ApplyBrake()
        {
            Vector3 brakeDirection = Vector3.ProjectOnPlane(projectedBikeForward, groundNormal);
            Vector3 brakeFroceForce = -bikeSettings.deceleration * Mathf.Sign(localBikeVelocity.z) * brakeDirection;
            //clamp force to max force
            float maxForce = bikeSettings.deceleration * Mathf.Abs(localBikeVelocity.z);
            brakeFroceForce = Vector3.ClampMagnitude(brakeFroceForce, maxForce);
            bikeReferences.BikeRb.AddForce(brakeFroceForce, ForceMode.Acceleration);
        }


        #endregion


        #region Steering/turning

        float currentSteerAngle = 0;
        private void steeringAnimation(float direction)
        {
            float speedRatio = Mathf.Abs(localBikeVelocity.z) / bikeSettings.maxSpeed;
            float steeringAngle = bikeSettings.maxTurnAngle * bikeCurves.SteeringCurve.Evaluate(speedRatio);
            currentSteerAngle = steeringAngle;


            if (bikeInput.HandBrake > 0) { steeringAngle = (-bikeSettings.maxTurnAngle); }
            Quaternion targetRotation = Quaternion.Euler(0, steeringAngle * direction, 0);
            Quaternion slerpRotation = Quaternion.Slerp(bikeReferences.BikeSteering.localRotation, targetRotation, Time.deltaTime * bikeSettings.steeringAnimationSpeed);

            bikeReferences.BikeSteering.localRotation = slerpRotation;

        }

        float steerSmoother = 0;
        private void AddTurning(float direction)
        {
            if (bikeSettings.useLerpTurning)
            {
                steerSmoother = Mathf.MoveTowards(steerSmoother, direction, Time.deltaTime * bikeSettings.turnLerpSpeed);
            }
            else
            {
                steerSmoother = direction;
            }

            if (bikeIsGrounded)
            {
                float driftTurningFactorCurrent = bikeInput.HandBrake > 0 ? bikeSettings.driftTurnFactor : 1;

                float steeringAngleRadians = steerSmoother * Mathf.Deg2Rad * currentSteerAngle; // Convert steering angle to radians.

                // Calculate the turning radius using the bike's geometry and the steering angle.
                // Avoid division by zero by ensuring there's a minimum steering angle.
                if (Mathf.Abs(steeringAngleRadians) < 0.01f)
                    return;

                float turningRadius = wheelbase / Mathf.Tan(steeringAngleRadians);

                // Calculate the bike's angular velocity.
                float angularVelocity = localBikeVelocity.z / turningRadius; // Ensure localBikeVelocity.z is positive for forward movement.

                // Calculate the rotation amount for this frame.
                float rotationAmount = angularVelocity * Time.deltaTime; // This is in radians.

                // Apply rotation around the up axis.
                bikeReferences.Rotator.Rotate(projectedBikeUp, Mathf.Rad2Deg * rotationAmount * driftTurningFactorCurrent, Space.World);
            }
            else if (bikeSettings.canSteerInAir) // need dedicated control for air
            {
                // Apply rotation around the up axis.
                bikeReferences.Rotator.Rotate(Vector3.up, Time.deltaTime * bikeSettings.turnSpeedInAir * steerSmoother, Space.World);
            }
        }

        #endregion


        #region Burnout

        public bool isDoingBurnout { get; private set; } = false;
        private bool isBournoutRotating = false;
        private float burnoutLerp = 0;

        private void HandleBurnoutAndRotation()
        {
            bool isAccelerating = bikeInput.Accelerate > 0;
            bool isReversing = bikeInput.Reverse > 0;
            bool isTurningLeft = bikeInput.SteeringLeft > 0;
            bool isTurningRight = bikeInput.SteeringRight > 0;

            isDoingBurnout = isAccelerating && isReversing && frontWheelIsGrounded && rearWheelIsGrounded;
            isBournoutRotating = (isTurningLeft || isTurningRight) && isAccelerating && isReversing && localBikeVelocity.magnitude < 1f;

            if (isDoingBurnout)
            {
                // Perform burnout
                // You can add particle effects or sound effects here to simulate the burnout

                ApplyBrake();
                isApplyingBrake = true;

                if (isBournoutRotating)
                {
                    // Calculate the force and rotation amount
                    rotationDirection = isTurningLeft ? -1f : 1f;
                    burnoutLerp = Mathf.MoveTowards(burnoutLerp, 1f, Time.fixedDeltaTime * bikeSettings.burnoutSmoothness);

                    rotationAmount = bikeSettings.burnoutRotationSpeed * Time.fixedDeltaTime; // Amount of rotation in this frame


                    // Calculate the distance between the front wheel and the rotator
                    float distance = Vector3.Distance(bikeReferences.FrontWheel.position, bikeReferences.Rotator.position);

                    float speed = rotationAmount * distance;

                    Vector3 speedDirection = -rotationDirection * bikeReferences.Rotator.right;
                    bikeReferences.BikeRb.linearVelocity = speedDirection * speed * burnoutLerp;

                    // Rotate the rotator
                    //bikeReferences.Rotator.Rotate(Vector3.up, rotationAmount * rotationDirection * burnoutLerp, Space.World);
                }
                else
                {
                    burnoutLerp = 0;
                    rotationAmount = 0;
                }
            }
        }

        float rotationAmount = 0;
        float rotationDirection = 0;

        void ApplyBurnoutRotation()
        {
            float timeFactor = Time.deltaTime / Time.fixedDeltaTime;

            if (isBournoutRotating)
            {
                bikeReferences.Rotator.Rotate(Vector3.up, rotationAmount * rotationDirection * burnoutLerp * timeFactor, Space.World);
            }

        }


        #endregion


        #region Leaning Animation

        private float leanSmoother = 0;
        [HideInInspector] public float currentLeanAngle { get; private set; }

        private void leaningAnimation(float direction)
        {
            // Smoothly interpolate the leanSmoother towards the target direction
            leanSmoother = Mathf.MoveTowards(leanSmoother, direction, Time.deltaTime * 10);

            // Calculate the lean angle based on the bike's speed and the smoothed direction
            float leanAngle = -bikeSettings.maxLeanAngle * bikeCurves.LeanCurve.Evaluate(localBikeVelocity.magnitude / bikeSettings.maxSpeed) * Mathf.Sign(leanSmoother);

            Quaternion targetRotation = Quaternion.Euler(0, 0, leanAngle * Mathf.Abs(leanSmoother));
            // Apply the lean angle to the bike's lean transform
            bikeReferences.LeanTransform.localRotation = Quaternion.Slerp(bikeReferences.LeanTransform.localRotation, targetRotation, Time.deltaTime * bikeSettings.leaningAnimationSpeed);

            currentLeanAngle = leanAngle * Mathf.Abs(leanSmoother);
        }

        #endregion


        #region Tire Animation

        private void tireAnimation()
        {
            // front whel
            float radius_f = bikeGeometry.FrontWheelRadius;
            Transform frontWheel = bikeReferences.FrontWheel;
            float Rotation_f = isApplyingBrake ? 0 : (localBikeVelocity.z / radius_f) * Time.deltaTime * Mathf.Rad2Deg;

            frontWheel.RotateAround(frontWheel.position, bikeReferences.FrontWheelParent.right, Rotation_f);
            var rot_f = frontWheel.localRotation;
            rot_f.y = 0;
            rot_f.z = 0;
            frontWheel.localRotation = rot_f;

            //rear wheel
            float radius_r = bikeGeometry.RearWheelRadius;
            Transform rearWheel = bikeReferences.RearWheel;
            float Rotation_r = isApplyingHandBrake || isApplyingBrake ? 0 : (localBikeVelocity.z / radius_r) * Time.deltaTime * Mathf.Rad2Deg;

            if (isDoingBurnout && Mathf.Abs(localBikeVelocity.z) < 1f)
            {
                Rotation_r = (bikeSettings.maxSpeed / radius_r) * Time.deltaTime * Mathf.Rad2Deg;
            }

            rearWheel.RotateAround(rearWheel.position, bikeReferences.RearWheelParent.right, Rotation_r);
            var rot_r = rearWheel.localRotation;
            rot_r.y = 0;
            rot_r.z = 0;
            rearWheel.localRotation = rot_r;


        }

        #endregion


        #region Wheelie Animation

        private void wheelieAnimation()
        {
            if (bikeInput.Wheelie > 0 && rearWheelIsGrounded)
            {
                isDoingWheelie = true;
            }
            if (!(bikeInput.Wheelie > 0) && frontWheelIsGrounded)
            {
                isDoingWheelie = false;
            }

            float surfaceAngle_rear = Vector3.Angle(groundNormal_rear, Vector3.up);
            float surfaceAngle_front = Vector3.Angle(groundNormal_front, Vector3.up);
            float surfaceAngle = Vector3.Angle(groundNormal, Vector3.up);
            float acceptableAngleForWheelie = 20f;

            bool snapWheelie = false;

            if (surfaceAngle > acceptableAngleForWheelie || surfaceAngle_front > acceptableAngleForWheelie || surfaceAngle_rear > acceptableAngleForWheelie)
            {
                if (isDoingWheelie) { snapWheelie = true; }
                isDoingWheelie = false;
            }

            // above logic makes better wheelie but still not perfect

            if (isDoingWheelie)
            {
                if (bikeInput.Wheelie > 0)
                {
                    // smooth lerp rotate wheelieTransform by maxWheelieAngle in local space (local x axis)
                    float targetAngle = -bikeSettings.maxWheelieAngle;
                    float currentAngle = bikeReferences.WheelieTransform.localRotation.eulerAngles.x;
                    float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * bikeSettings.wheelieAnimationSpeed);
                    bikeReferences.WheelieTransform.localRotation = Quaternion.Euler(newAngle, 0, 0);
                }
                else
                {
                    // smooth lerp rotate wheelieTransform to zero rotation in local space (local x axis)
                    float targetAngle = 0f;
                    float currentAngle = bikeReferences.WheelieTransform.localRotation.eulerAngles.x;
                    float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * bikeSettings.wheelieAnimationSpeed);
                    bikeReferences.WheelieTransform.localRotation = Quaternion.Euler(newAngle, 0, 0);
                }

            }
            else
            {
                // smooth lerp rotate wheelieTransform to zero rotation in local space (local x axis)
                float targetAngle = 0f;
                float currentAngle = bikeReferences.WheelieTransform.localRotation.eulerAngles.x;
                float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * bikeSettings.wheelieAnimationSpeed);
                if (snapWheelie)
                {
                    newAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * bikeSettings.alignRotatorSpeed_Ground);
                }

                bikeReferences.WheelieTransform.localRotation = Quaternion.Euler(newAngle, 0, 0);
            }
        }

        #endregion


        #region Gear System

        void UpdateGearShift()
        {
            float bikeSpeed = localBikeVelocity.magnitude;

            for (int i = 0; i < gearSpeeds.Length; i++)
            {
                if (bikeSpeed > gearSpeeds[i])
                {
                    currentGear = i + 1;
                }
                else break;
            }
            if (CurrntGearProperty != currentGear)
            {
                CurrntGearProperty = currentGear;
            }
        }

        private int currentGearTemp;

        public int CurrntGearProperty
        {
            get
            {
                return currentGearTemp;
            }

            set
            {
                currentGearTemp = value;

                if (bikeInput.Accelerate > 0 && localBikeVelocity.z > 0 && !bikeAudio.gearShiftSound.isPlaying && bikeIsGrounded)
                {
                    bikeEvents.OnGearChange.Invoke();
                    bikeAudio.gearShiftSound.Play();
                    StartCoroutine(shiftingGear());
                }

                bikeAudio.engineSound.volume = 0.5f;
            }
        }

        IEnumerator shiftingGear()
        {
            canAccelerate = false;
            yield return new WaitForSeconds(0.3f);
            canAccelerate = true;
        }

        #endregion


        #region Bike Audio

        void UpdateEngineSound()
        {
            float bikeSpeed = localBikeVelocity.magnitude;

            //engineSound.pitch = Mathf.Lerp(minPitch, maxPitch, Mathf.Abs(speed + (verticalInput + angularSpeed/10) * 20) / maxspeed);
            float enginePitch = Mathf.Lerp(bikeAudio.minPitch, bikeAudio.maxPitch, Mathf.Abs(bikeSpeed) / gearSpeeds[Mathf.Clamp(currentGear, 0, gearSpeeds.Length - 1)]);

            if (currentGear == gearSpeeds.Length)
            {
                enginePitch = Mathf.Lerp(bikeAudio.minPitch, bikeAudio.maxPitch, Mathf.Abs(bikeSpeed) / (bikeSettings.maxSpeed));
            }


            if (bikeIsGrounded)
            {
                if (bikeInput.Accelerate > 0 && localBikeVelocity.magnitude < 5f && (isDoingBurnout || isApplyingHandBrake))
                {
                    bikeAudio.engineSound.pitch = Mathf.MoveTowards(bikeAudio.engineSound.pitch, bikeAudio.maxPitch, Time.deltaTime);
                    if (bikeAudio.engineSound.pitch > bikeAudio.maxPitch - 0.05f)
                    {
                        bikeAudio.engineSound.pitch -= 0.2f;
                    }
                }
                else
                {
                    bikeAudio.engineSound.pitch = Mathf.MoveTowards(bikeAudio.engineSound.pitch, enginePitch, 3f * Time.deltaTime);
                }
            }

            if (bikeInput.Accelerate > 0 || bikeInput.Reverse > 0)
            {
                bikeAudio.engineSound.volume = Mathf.MoveTowards(bikeAudio.engineSound.volume, 1, 0.01f);
            }
            else
            {
                bikeAudio.engineSound.volume = Mathf.MoveTowards(bikeAudio.engineSound.volume, 0.5f, 0.01f);
            }


        }

        private void UpdateSkidSound()
        {
            float skidIntensity = Mathf.Max(TotalSlip_frontWheel, TotalSlip_rearWheel);
            bikeAudio.SkidSound.mute = skidIntensity < 0.1f;
            bikeAudio.SkidSound.volume = skidIntensity;
        }

        #endregion


        #region Skidmarks

        float forwardSlip_frontWheel, forwardSlip_rearWheel, sideSlip_frontWheel, sideSlip_rearWheel;
        float TotalSlip_frontWheel, TotalSlip_rearWheel;

        private void CalculateWheelSlips()
        {
            if (isApplyingBrake) { forwardSlip_frontWheel = 1; forwardSlip_rearWheel = 1; }
            else if (isApplyingHandBrake) { forwardSlip_frontWheel = 0; forwardSlip_rearWheel = 1; }
            else if (isDoingBurnout) { forwardSlip_frontWheel = 0; forwardSlip_rearWheel = 1; }
            else { forwardSlip_frontWheel = 0; forwardSlip_rearWheel = 0; }

            // front wheel side slip
            Vector3 sideWaysDirection_frontWheel = Vector3.ProjectOnPlane(bikeReferences.FrontWheelParent.right, projectedBikeUp).normalized;
            float sideWaysSpeed_frontWheel = Vector3.Dot(bikeReferences.BikeRb.GetPointVelocity(bikeReferences.FrontWheelParent.position), sideWaysDirection_frontWheel);
            sideSlip_frontWheel = Mathf.Abs(sideWaysSpeed_frontWheel) < 0.01f ? 0 : sideWaysSpeed_frontWheel / bikeSettings.maxSpeed;

            // rear wheel side slip
            Vector3 sideWaysDirection_rearWheel = Vector3.ProjectOnPlane(bikeReferences.RearWheelParent.right, projectedBikeUp).normalized;
            float sideWaysSpeed_rearWheel = Vector3.Dot(bikeReferences.BikeRb.GetPointVelocity(bikeReferences.RearWheelParent.position), sideWaysDirection_rearWheel);
            sideSlip_rearWheel = Mathf.Abs(sideWaysSpeed_rearWheel) < 0.01f ? 0 : sideWaysSpeed_rearWheel / bikeSettings.maxSpeed;

            TotalSlip_frontWheel = localBikeVelocity.magnitude < 1 ? 0 : (Mathf.Abs(forwardSlip_frontWheel) + Mathf.Abs(sideSlip_frontWheel)) / 2;
            TotalSlip_rearWheel = localBikeVelocity.magnitude < 1 ? 0 : (Mathf.Abs(forwardSlip_rearWheel) + Mathf.Abs(sideSlip_rearWheel)) / 2;

            if (isDoingBurnout) { TotalSlip_rearWheel = (forwardSlip_rearWheel + Mathf.Abs(sideSlip_rearWheel)) / 2; }

            if (!bikeIsGrounded) { TotalSlip_frontWheel = 0; TotalSlip_rearWheel = 0; }
        }


        private int skidmarkIndexFront = -1;
        private int skidmarkIndexRear = -1;

        private void UpdateSkidmarks()
        {
            if (skidmarkController != null)
            {
                if (frontWheelIsGrounded) // Adjust threshold as needed
                {
                    skidmarkIndexFront = skidmarkController.AddSkidMark(frontWheelHit.point, groundNormal_front, TotalSlip_frontWheel, skidmarkIndexFront);
                }
                else
                {
                    skidmarkIndexFront = -1;
                }

                if (rearWheelIsGrounded) // Adjust threshold as needed
                {
                    skidmarkIndexRear = skidmarkController.AddSkidMark(rearWheelHit.point, groundNormal_rear, TotalSlip_rearWheel, skidmarkIndexRear);
                }
                else
                {
                    skidmarkIndexRear = -1;
                }
            }
        }

        private void UpdateTireSmoke()
        {
            if (tireSmoke_ps != null && rearWheelIsGrounded)
            {
                tireSmoke_ps.transform.position = rearWheelHit.point + Vector3.up * 0.05f;

                if (TotalSlip_rearWheel >= 0.5f)
                {
                    if (!tireSmoke_ps.isPlaying)
                        tireSmoke_ps.Play();
                }
                else
                {
                    if (tireSmoke_ps.isPlaying)
                        tireSmoke_ps.Stop();
                }
            }
            else
            {
                 if (tireSmoke_ps != null && tireSmoke_ps.isPlaying)
                    tireSmoke_ps.Stop();
            }
        }


        #endregion


        #region Utility Functions


        public void StartBike()
        {
            canAccelerate = true;
            canTurn = true;
            bikeAudio.engineSound.mute = false;
        }

        public void StopBike()
        {
            canAccelerate = false;
            canTurn = false;
            bikeAudio.engineSound.mute = true;
        }

        public Vector3 RotateVector(Vector3 originalVector, Vector3 axis, float degree)
        {
            // Create a quaternion for rotation around the given axis by the specified degree
            Quaternion rotation = Quaternion.AngleAxis(degree, axis);

            // Apply the rotation to the original vector
            Vector3 rotatedVector = rotation * originalVector;

            return rotatedVector;
        }

        public void reduceSurfaceNormalDownVelocity(float factor)
        {
            Vector3 currentVelocityInSurfaceNormal = Vector3.Project(bikeReferences.BikeRb.linearVelocity, projectedBikeUp);
            bikeReferences.BikeRb.linearVelocity -= currentVelocityInSurfaceNormal * factor;
        }

        public void provideInput(float accelerate, float reverse, float handBrake, float steerLeft, float steerRight, float wheelie)
        {
            bikeInput.Accelerate = accelerate;
            bikeInput.Reverse = reverse;
            bikeInput.HandBrake = handBrake;
            bikeInput.SteeringLeft = steerLeft;
            bikeInput.SteeringRight = steerRight;
            bikeInput.Wheelie = wheelie;
        }


#if UNITY_EDITOR

        [ContextMenu("Update Wheel Angles")]
        void UpdateWheelAngles()
        {
            // Register the undo operation for the bikeGeometry object
            UnityEditor.Undo.RecordObject(this, "Update Wheel Angles");

            // Update the wheel angles
            bikeGeometry.FrontWheelAngle = Vector3.Angle(bikeReferences.FrontWheelParent.up, transform.up);
            bikeGeometry.RearWheelAngle = Vector3.Angle(bikeReferences.RearWheelParent.up, transform.up);

            // Mark the object as dirty to ensure the changes are saved
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Auto Adjust GearSpeeds")]
        void AutoAdjustGearSpeeds()
        {
            // Register the undo operation for the gearSpeeds array
            UnityEditor.Undo.RecordObject(this, "Auto Adjust GearSpeeds");

            // Determine the number of gears based on the length of the gearSpeeds array
            int numberOfGears = gearSpeeds.Length;

            // Calculate the speed increment for each gear
            float speedIncrement = bikeSettings.maxSpeed / numberOfGears;

            // Set the gear speeds
            for (int i = 0; i < numberOfGears; i++)
            {
                // Ensure the last gear reaches slightly below the max speed
                if (i == numberOfGears - 1)
                {
                    gearSpeeds[i] = Mathf.RoundToInt(bikeSettings.maxSpeed * 0.95f); // 95% of maxSpeed for the last gear
                }
                else
                {
                    gearSpeeds[i] = Mathf.RoundToInt(speedIncrement * (i + 1));
                }
            }

            // Mark the object as dirty to ensure the changes are saved
            UnityEditor.EditorUtility.SetDirty(this);
        }

#endif

        #endregion


        #region Gizmos

#if UNITY_EDITOR

        private void drawWheelsGizmos(Transform wheel, float radius, Color color)
        {
            // use handles to draw wheels cylinder with radius and width
            UnityEditor.Handles.color = color;

            UnityEditor.Handles.DrawWireDisc(wheel.position, wheel.right, radius);
        }

        private void OnDrawGizmos()
        {
            //draw wheel geometry
            if (frontWheelIsGrounded)
            {
                drawWheelsGizmos(bikeReferences.FrontWheel, bikeGeometry.FrontWheelRadius, Color.green);
            }
            else
            {
                drawWheelsGizmos(bikeReferences.FrontWheel, bikeGeometry.FrontWheelRadius, Color.red);
            }

            if (rearWheelIsGrounded)
            {
                drawWheelsGizmos(bikeReferences.RearWheel, bikeGeometry.RearWheelRadius, Color.green);
            }
            else
            {
                drawWheelsGizmos(bikeReferences.RearWheel, bikeGeometry.RearWheelRadius, Color.red);
            }

        }

#endif

        #endregion


    }


}