using MotoSquid.AI;
using MotoSquid.Combat;
using MotoSquid.Rider;
using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Events;

namespace MotoSquid.Bike
{
    public class BikeAIController : MonoBehaviour, IBikeAudioState
    {
        [Serializable]
        public class BikeReferences
        {
            public Transform Rotator;
            public Transform WheelieTransform;
            public Transform LeanTransform;
            public Transform FrontWheelParent;
            public Transform RearWheelParent;
            public Transform FrontWheel;
            public Transform RearWheel;
            public Transform BikeSteering;
            public Transform BikeSteeringParent;
            public Transform SteeringMeshes;
            public Transform BikeModel;
            public Transform BodyMesh;
            public Rigidbody BikeRb;
            public CapsuleCollider collider;
            public GameObject skidmarksPrefab;
            public GameObject tireSmokePrefab;
            public BikeAnimationTargets bikeAnimationTargets;
            public RagdollActivator ragdollActivator;
        }
        public BikeReferences bikeReferences;

        [Serializable]
        public class BikeGeometry
        {
            public float FrontWheelRadius = 0.3269256f;
            public float FrontWheelWidth  = 0.1400601f;
            public float RearWheelRadius  = 0.3269258f;
            public float RearWheelWidth   = 0.1400601f;
            public float FrontWheelAngle  = 54.33804f;
            public float RearWheelAngle   = 0f;
        }
        public BikeGeometry bikeGeometry;

        [Serializable]
        public class BikeSuspension
        {
            public float SpringForce            = 300f;
            public float DamperForce            = 60f;
            public float groundStickFactor      = 0f;
            public float MaxCompression         = 0.4f;
            public float snapVelocityReduction  = 0.8f;
            public float suspensionCastRadius   = 0.25f;
            // Lower value = slower compression tracking = gentler spring response to road seam spikes
            public float groundNormalSmoothing  = 10f;
            // Extra distance beyond the neutral suspension point used only for grounded detection
            // Prevents brief raycast misses on bumps from instantly stripping friction and steering
            public float extraGroundedDistance  = 0.5f;
            // Clamps how fast the bike can be launched upward along the surface normal per frame
            // Prevents road seam impulse spikes from throwing the bike into the air
            public float maxBumpUpwardVelocity  = 4f;
        }
        public BikeSuspension bikeSuspension;

        [Serializable]
        public class BikeSettings
        {
            public LayerMask drivableLayerMask   = ~0;
            public float maxSpeed                = 100f;
            public float reverseMaxSpeed         = 3f;
            public float acceleration            = 10f;
            // Launch easing, keep these matched to the player bike for fair race start balance
            public int   slowLaunchGears         = 2;
            [Range(0f, 1f)] public float launchAccelMultiplier = 0.5f;
            public float reverseAcceleration     = 5f;
            public float deceleration            = 40f;
            public float handBrakeDeceleration   = 10f;
            public float maxTurnAngle            = 20f;
            public bool  useLerpTurning          = false;
            public float turnLerpSpeed           = 5f;
            public float steeringAnimationSpeed  = 5f;
            public bool  canSteerInAir           = false;
            public bool  canLeanInAir            = true;
            public float turnSpeedInAir          = 70f;
            public float maxLeanAngle            = 35f;
            public float leaningAnimationSpeed   = 5f;
            public float idleLeanAngle           = 6f;
            public float idleLeanFadeOutSpeed    = 2f;
            // How far the bike stands back up while holding a start line burnout (0 = no change, 1 = fully upright)
            public float burnoutUprightAmount    = 0.75f;
            public float burnoutUprightBlendTime = 0.2f;
            // A grid burnout was drawing the prefab's base emission, which is tuned for a light skid and
            // reads as a wisp. The player scales these by its launch charge; an AI has no charge to time,
            // so a rolled burnout simply runs them at full
            public float burnoutSmokeMaxRate   = 3f;
            public float burnoutSmokeMaxSize   = 2f;
            public float burnoutSmokeMaxRadius = 4f;
            public float frictionCoefficient     = 0.5f;
            public float rearLateralGripNormal   = 1.0f;
            // AI drift (driven by BikeAILogic on sharp corners)
            public float rearLateralGripDrift    = 0.15f;  // Lower = rear steps out more
            public float driftFrictionFactor     = 0.6f;   // Body lateral friction scale while drifting
            public float driftTurnBoost          = 1.25f;  // Extra turn rate while drifting
            public float driftLeanBoost          = 0.35f;  // Extra lean (× maxLeanAngle) while drifting
            public float driftEntryRate          = 4f;
            public float driftExitRate           = 3f;
            public float minDriftSpeed           = 18f;    // M/s below this the AI won't drift
            public float rollingResistance       = 1f;
            public float uphillAccelerationBoost = 5f;
            public float gravity                 = 9.81f;
            public float fallGravityMultiplier   = 5f;
            public float maxWheelieAngle         = 30f;
            public float wheelieAnimationSpeed   = 3f;
            public float alignRotatorSpeedGround = 10f;
            public float alignRotatorSpeedAir    = 5f;
        }
        public BikeSettings bikeSettings;

        [Serializable]
        public class BikeCurves
        {
            public AnimationCurve AccelerationCurve        = new AnimationCurve(new Keyframe(0,1), new Keyframe(0.5f,0.8f), new Keyframe(1,0.5f));
            public AnimationCurve ReverseAccelerationCurve = new AnimationCurve(new Keyframe(0,1), new Keyframe(1,0.2f));
            public AnimationCurve SteeringCurve            = new AnimationCurve(new Keyframe(0,1), new Keyframe(1,0.3f));
            public AnimationCurve FrictionCurve            = new AnimationCurve(new Keyframe(0,1), new Keyframe(0.1f,0.5f), new Keyframe(1,0.1f));
            public AnimationCurve LeanCurve                = new AnimationCurve(new Keyframe(0,0.2f), new Keyframe(1,1));
        }
        public BikeCurves bikeCurves;

        public float SpeedMs             => canMove ? _cachedSpeed : 0f;
        public float SkidIntensity01     => Mathf.Max(TotalSlip_frontWheel, TotalSlip_rearWheel);
        public bool  IsGrounded          => bikeIsGrounded;
        public bool  IsApplyingHandBrake => false;
        public bool  IsDoingBurnout      => isDoingBurnout;
        public bool  CanMove             => canMove;
        public bool  IsRevving           => isRevving;
        public float ThrottleInput       => throttle;
        public float ReverseInput        => 0f;
        public int   CurrentGear         => currentGear;
        public int[] GearSpeeds          => gearSpeeds;
        public float MaxSpeedKmh         => bikeSettings.maxSpeed * 3.6f;
        public float SteerAmount         => Mathf.Clamp(steerInput, -1f, 1f);
        public bool  IsDoingWheelie      => false;   // AI never wheelies
        public bool  IsBraking           => isBraking;
        public event System.Action GearChanged;
        public event System.Action<bool> EngineMuteRequested;

        public int[] gearSpeeds = new int[] { 35, 80, 135, 200, 275 };
        public int   currentGear = 1;

        [Serializable]
        public class BikeEvents
        {
            public UnityEvent OnTakeOff;
            public UnityEvent OnGrounded;
            public UnityEvent OnGearChange;
            public UnityEvent OnWheelieExceed;
        }
        public BikeEvents bikeEvents;

        // External logic component
        public BikeAILogic aiLogic;

        // Cruise speed proxy
        public float cruiseSpeedKmh
        {
            get => aiLogic != null ? aiLogic.cruiseSpeedKmh : 200f;
            set { if (aiLogic != null) aiLogic.cruiseSpeedKmh = value; }
        }

        // Race state flags
        public  bool canAccelerate = true;
        public  bool isRevving     = false;

        // Start line burnout, RaceManager rolls this per race. A chosen AI burns out on the grid during
        // the countdown for a launch shove at GO. Set false again once the launch is applied
        [HideInInspector] public bool willStartBurnout = false;
        public float startBurnoutLaunchKmh = 12f;   // Getaway speed granted at GO for a grid burnout

        // Physics state
        [HideInInspector] public bool    frontWheelIsGrounded;
        [HideInInspector] public bool    rearWheelIsGrounded;
        private bool canMove = true;
        [HideInInspector] public Vector3 localBikeVelocity    { get; private set; }

        // Driven by BikeAnimationController so a forced riding pose suppresses the standstill tilt
        [System.NonSerialized] public float idleLeanWeight = 1f;
        private float _burnoutUpright01;
        [HideInInspector] public float   currentLeanAngle     { get; private set; }
        [HideInInspector] public bool    isDoingBurnout;   // grid burnout during the countdown (see willStartBurnout)
        [HideInInspector] public int     CurrentSteerInput    => _steerInput > 0.1f ? 1 : (_steerInput < -0.1f ? -1 : 0);
        public  bool bikeIsGrounded => frontWheelIsGrounded || rearWheelIsGrounded;
        [HideInInspector] public bool isOnRamp = false;
        public static bool DebugHoldForCombatTest = false;
        private bool _debugHeld;

        // Input from AI logic
        public  float steerInput  { get; set; }  // −1 … +1

        // Exposed so BikeAILogic can solve the steer angle geometrically instead of guessing at a gain
        public  float Wheelbase        => wheelbase;
        public  float CurrentSteerAngle => currentSteerAngle;
        public  float throttle    { get; set; }  //  0 …  1
        public  bool  isBraking   { get; set; }
        public  bool  driftRequested { get; set; }   // Set by BikeAILogic on sharp corners
        private float _aiDriftIntensity;             // 0..1 smoothed drift amount
        public  bool  isAIDrifting => _aiDriftIntensity > 0.3f;

        private float maxFrontRaycastDistance;
        private float maxRearRaycastDistance;
        private float wheelbase;
        private float snapDistance;
        private float _cosFront;
        private float _cosRear;
        private float compressionForSnap;
        private float suspensionForceMagnitude;
        private bool  bikeSnappedToGround;
        private float compression_f, compression_r, compression_Total;

        private Vector3 groundNormal, groundNormal_front, groundNormal_rear;
        private Vector3 _smoothedNormal_front = Vector3.up;
        private Vector3 _smoothedNormal_rear  = Vector3.up;
        private float   _smoothedCompression_f;
        private float   _smoothedCompression_r;
        private Vector3 projectedBikeForward, projectedBikeUp;
        private float   projectedForwardOffsetAngle;

        private Vector3 _frontWheelParentLocalPos;
        private Vector3 _rearWheelParentLocalPos;

        private RaycastHit frontWheelHit, rearWheelHit;
        private bool wasGrounded;
        private float _airTime;

        private float currentSteerAngle;
        private float steerSmoother;
        private float leanSmoother;
        private bool  isApplyingBrake;
        private int   _steerInput;  // Quantised for animation

        private float _cachedSpeed;
        private float _cachedSpeedRatio;     // _cachedSpeed / maxSpeed for LeanCurve
        private float _cachedFwdSpeedRatio;  // Abs(z) / maxSpeed for SteeringCurve, WheelieAnimation

        private int _skidmarkCounter;
        private int _wheelSlipCounter;
        private int _tireSmokeCounter;
        private const int SKIDMARK_INTERVAL = 4;      // Update every 4 frames
        private const int WHEELSLIP_INTERVAL = 3;     // Update every 3 frames
        private const int TIRE_SMOKE_INTERVAL = 3;    // Update every 3 frames
        private const float SKIDMARK_MIN_SPEED = 3f;  // Don't generate skidmarks below this speed (m/s)

        [HideInInspector] public bool burnoutSmokeSuppressed;

        private ParticleSystem              tireSmoke_ps;
        private ParticleSystem.MainModule     _tireSmokeMain;
        private ParticleSystem.EmissionModule _tireSmokeEmission;
        private ParticleSystem.ShapeModule    _tireSmokeShape;
        private float _tireSmokeBaseSize, _tireSmokeBaseRate, _tireSmokeBaseRadius;
        private SkidmarkController skidmarkController;
        private int   skidmarkIndexFront = -1;
        private int   skidmarkIndexRear  = -1;
        private float forwardSlip_frontWheel, forwardSlip_rearWheel;
        private float sideSlip_frontWheel,   sideSlip_rearWheel;
        private float TotalSlip_frontWheel,  TotalSlip_rearWheel;
        private int   currentGearTemp;

    #region Unity Methods

        private void Awake()
        {
   
            if (!ValidateReferences()) return;

            if (bikeReferences.skidmarksPrefab != null)
            {
                var instance = Instantiate(bikeReferences.skidmarksPrefab);
                skidmarkController = instance?.GetComponent<SkidmarkController>();
            }

            if (bikeReferences.tireSmokePrefab != null)
            {
                var instance = Instantiate(bikeReferences.tireSmokePrefab, bikeReferences.RearWheel);
                instance.transform.localPosition = new Vector3(0, bikeGeometry.RearWheelRadius, 0);
                tireSmoke_ps = instance?.GetComponent<ParticleSystem>();
                if (tireSmoke_ps != null)
                {
                    _tireSmokeMain       = tireSmoke_ps.main;
                    _tireSmokeEmission   = tireSmoke_ps.emission;
                    _tireSmokeShape      = tireSmoke_ps.shape;
                    _tireSmokeBaseSize   = _tireSmokeMain.startSizeMultiplier;
                    _tireSmokeBaseRate   = _tireSmokeEmission.rateOverTimeMultiplier;
                    _tireSmokeBaseRadius = _tireSmokeShape.radius;
                    tireSmoke_ps.Stop();
                }
            }

            if (aiLogic != null)
            {
                aiLogic.bikeTransform = bikeReferences.Rotator;
                aiLogic.controller    = this;
            }
        }

        // Returns false (and disables this component) if any reference the physics loop depends on is
        // unassigned, so a single misconfigured bike can't spam the console every frame
        private bool ValidateReferences()
        {
            string missing = null;
            if      (bikeReferences.BikeRb           == null) missing = "BikeRb";
            else if (bikeReferences.Rotator          == null) missing = "Rotator";
            else if (bikeReferences.FrontWheelParent == null) missing = "FrontWheelParent";
            else if (bikeReferences.RearWheelParent  == null) missing = "RearWheelParent";
            else if (bikeReferences.FrontWheel       == null) missing = "FrontWheel";
            else if (bikeReferences.RearWheel        == null) missing = "RearWheel";

            if (missing != null)
            {
                Debug.LogError($"BikeAIController on '{name}' is missing bikeReferences.{missing}, " +
                               "assign it in the Inspector. Disabling this bike to stop per frame errors.", this);
                enabled = false;
                return false;
            }
            return true;
        }

        private void Start()
        {
            bikeReferences.BikeRb.useGravity = false;

            ValidateCapsule();

            // Rays are cast along the fork axis (-wheelParent.up), so the equilibrium hit distance
            // is radius/cos(angle) + radius. For a vertical fork (angle=0) this simplifies to 2*radius
            _cosFront = Mathf.Cos(bikeGeometry.FrontWheelAngle * Mathf.Deg2Rad);
            _cosRear  = Mathf.Cos(bikeGeometry.RearWheelAngle  * Mathf.Deg2Rad);
            maxFrontRaycastDistance = bikeGeometry.FrontWheelRadius / _cosFront + bikeGeometry.FrontWheelRadius;
            maxRearRaycastDistance  = bikeGeometry.RearWheelRadius  / _cosRear  + bikeGeometry.RearWheelRadius;
            wheelbase = Vector3.Distance(bikeReferences.FrontWheel.position, bikeReferences.RearWheel.position);

            if (bikeReferences.WheelieTransform != null)
                bikeReferences.WheelieTransform.localRotation = Quaternion.identity;

            projectedBikeForward = bikeReferences.Rotator.forward;
            projectedBikeUp      = bikeReferences.Rotator.up;
            projectedForwardOffsetAngle = Vector3.Angle(
                bikeReferences.Rotator.forward,
                (bikeReferences.FrontWheelParent.position - bikeReferences.RearWheelParent.position).normalized);


            _frontWheelParentLocalPos = bikeReferences.Rotator.InverseTransformPoint(bikeReferences.FrontWheelParent.position);
            _rearWheelParentLocalPos  = bikeReferences.Rotator.InverseTransformPoint(bikeReferences.RearWheelParent.position);

            wasGrounded = bikeIsGrounded;

            if (skidmarkController != null)
                skidmarkController.SkidmarkWidth = bikeGeometry.RearWheelWidth;


        }

        private void Update()
        {
            SteeringAnimation(steerInput);
            LeaningAnimation(bikeIsGrounded || bikeSettings.canLeanInAir ? steerInput : 0f);
            WheelieAnimation();
            TireAnimation();
            UpdateTireSmokeThrottled();

#if UNITY_EDITOR
            DebugHoldHotkey();
#endif
        }

#if UNITY_EDITOR
        private static int _debugKeyFrame = -1;
        private static void DebugHoldHotkey()
        {
            if (Time.frameCount == _debugKeyFrame) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.f9Key.wasPressedThisFrame) return;
            _debugKeyFrame = Time.frameCount;
            DebugHoldForCombatTest = !DebugHoldForCombatTest;
            Debug.Log($"[CombatTest] AI hold {(DebugHoldForCombatTest ? "ON, AI bikes stand still but still attack and take hits" : "OFF, AI racing normally")}.");
        }

        private static BikeAIController _debugGuiOwner;
        private void OnGUI()
        {
            if (!DebugHoldForCombatTest) return;
            if (_debugGuiOwner == null) _debugGuiOwner = this;
            if (_debugGuiOwner != this) return;
            GUI.Label(new Rect(10f, 10f, 500f, 24f), "[CombatTest] AI HOLD ACTIVE, F9 to toggle");
        }
#endif

        private void FixedUpdate()
        {
            if (DebugHoldForCombatTest)
            {
                throttle  = 0f;
                isBraking = true;
                canMove   = false;
                _debugHeld = true;
            }
            else if (_debugHeld)
            {
                _debugHeld = false;
                canMove    = true;   // Release back to racing when the toggle is switched off
            }

            localBikeVelocity    = bikeReferences.Rotator.InverseTransformDirection(bikeReferences.BikeRb.linearVelocity);
            _cachedSpeed         = localBikeVelocity.magnitude;
            float invMax         = bikeSettings.maxSpeed > 0f ? 1f / bikeSettings.maxSpeed : 0f;
            _cachedSpeedRatio    = _cachedSpeed * invMax;
            _cachedFwdSpeedRatio = Mathf.Abs(localBikeVelocity.z) * invMax;

            // Ground detection
            PlaceWheelOnGround(bikeReferences.FrontWheelParent, bikeReferences.FrontWheel,
                bikeGeometry.FrontWheelRadius, bikeGeometry.FrontWheelAngle,
                maxFrontRaycastDistance, out frontWheelHit, out frontWheelIsGrounded, out groundNormal_front);
            PlaceWheelOnGround(bikeReferences.RearWheelParent, bikeReferences.RearWheel,
                bikeGeometry.RearWheelRadius, bikeGeometry.RearWheelAngle,
                maxRearRaycastDistance, out rearWheelHit, out rearWheelIsGrounded, out groundNormal_rear);

            isDoingBurnout = isRevving && willStartBurnout && bikeIsGrounded;

            CalculateSurfaceParams();
            AlignRotator(bikeReferences.Rotator, projectedBikeForward, projectedBikeUp);

            // Run AI logic every physics step so inputs are synchronous with the simulation
            if (aiLogic != null)
                aiLogic.OnPhysicsUpdated();

            AddGravity();
            CalculateSuspensionParams();
            AddSuspension();
            ClampBumpVelocity();
            UpdateDriftState();
            AddFriction();
            HandleAcceleration();
            AddTurning(steerInput);
            SuppressCollisionLaunch();
            UpdateGearShift();
            CalculateWheelSlipsThrottled();
            UpdateSkidmarksThrottled();
        }

    #endregion

    #region Wheel Placement

        private void PlaceWheelOnGround(Transform wheelParent, Transform wheel,
            float radius, float wheelAngle, float maxRaycastDistance,
            out RaycastHit hit, out bool isGrounded, out Vector3 groundNrm)
        {
            Vector3 rayPos    = wheelParent.position + wheelParent.up * radius;
            Vector3 rayDir    = -wheelParent.up;
            float castRadius  = bikeSuspension.suspensionCastRadius;

            float groundCheckDistance = maxRaycastDistance + bikeSuspension.extraGroundedDistance;
            if (Physics.SphereCast(rayPos, castRadius, rayDir, out hit, groundCheckDistance,
                bikeSettings.drivableLayerMask, QueryTriggerInteraction.Ignore))
            {
                float h              = radius / Mathf.Cos(wheelAngle * Mathf.Deg2Rad);
                float surfaceDistance = hit.distance + castRadius;
                float localOffset    = Mathf.Max(surfaceDistance - h - radius, -radius * 0.5f);
                wheel.localPosition  = new Vector3(0, -localOffset, 0);
                isGrounded = true;
                groundNrm  = hit.normal;
            }
            else
            {
                wheel.localPosition = Vector3.Lerp(wheel.localPosition, Vector3.zero, 20f * Time.fixedDeltaTime);
                isGrounded = false;
                groundNrm  = Vector3.up;
            }
        }

    #endregion

    #region Alignment

        private void CalculateSurfaceParams()
        {
            // Smooth raw hit normals to filter Road Architect mesh seam spikes (matches player bike)
            float sf = bikeSuspension.groundNormalSmoothing * Time.fixedDeltaTime;
            _smoothedNormal_front = frontWheelIsGrounded
                ? Vector3.Slerp(_smoothedNormal_front, groundNormal_front, sf)
                : Vector3.up;
            _smoothedNormal_rear = rearWheelIsGrounded
                ? Vector3.Slerp(_smoothedNormal_rear, groundNormal_rear, sf)
                : Vector3.up;
            groundNormal_front = _smoothedNormal_front;
            groundNormal_rear  = _smoothedNormal_rear;

            if (frontWheelIsGrounded && rearWheelIsGrounded)
                groundNormal = (groundNormal_front + groundNormal_rear).normalized;
            else if (frontWheelIsGrounded)
                groundNormal = groundNormal_front;
            else if (rearWheelIsGrounded)
                groundNormal = groundNormal_rear;
            else
                groundNormal = Vector3.up;

            if (frontWheelIsGrounded && rearWheelIsGrounded)
            {
                projectedBikeForward = (frontWheelHit.point - rearWheelHit.point).normalized;
                projectedBikeForward = Vector3.ProjectOnPlane(projectedBikeForward, bikeReferences.Rotator.right).normalized;

                projectedBikeUp = Vector3.ProjectOnPlane(groundNormal, projectedBikeForward).normalized;
            }
            else if (frontWheelIsGrounded || rearWheelIsGrounded)
            {
                // Single wheel grounded, target horizontal forward so the bike self levels
                projectedBikeForward = Vector3.ProjectOnPlane(bikeReferences.Rotator.forward, Vector3.up).normalized;

                Vector3 grounded_normal = frontWheelIsGrounded ? groundNormal_front : groundNormal_rear;
                projectedBikeUp = Vector3.ProjectOnPlane(grounded_normal, projectedBikeForward).normalized;
            }

            if (!frontWheelIsGrounded && !rearWheelIsGrounded)
            {
                projectedBikeForward = Vector3.ProjectOnPlane(bikeReferences.Rotator.forward, groundNormal).normalized;
                projectedBikeUp      = groundNormal;
            }

            if (bikeIsGrounded != wasGrounded)
            {
                if (bikeIsGrounded) bikeEvents.OnGrounded?.Invoke();
                else                bikeEvents.OnTakeOff?.Invoke();
            }
            wasGrounded = bikeIsGrounded;
            _airTime = bikeIsGrounded ? 0f : _airTime + Time.fixedDeltaTime;
        }

        private void AlignRotator(Transform rotator, Vector3 projectedForward, Vector3 projectedUp)
        {
            Vector3 newForward = RotateVector(projectedForward, -bikeReferences.Rotator.right, projectedForwardOffsetAngle);
            Quaternion targetRot = Quaternion.LookRotation(newForward, projectedUp);
            float speed = bikeIsGrounded ? bikeSettings.alignRotatorSpeedGround : bikeSettings.alignRotatorSpeedAir;

            if (compression_Total > bikeSuspension.MaxCompression)
                speed = 50f;

            rotator.rotation = Quaternion.Slerp(rotator.rotation, targetRot, Time.fixedDeltaTime * speed);
        }

    #endregion

    #region Suspension

        private void CalculateSuspensionParams()
        {
            float castRadius = bikeSuspension.suspensionCastRadius;
            float raw_f = frontWheelIsGrounded
                ? Mathf.Clamp01((maxFrontRaycastDistance - (frontWheelHit.distance + castRadius)) / maxFrontRaycastDistance)
                : 0f;
            float raw_r = rearWheelIsGrounded
                ? Mathf.Clamp01((maxRearRaycastDistance - (rearWheelHit.distance + castRadius)) / maxRearRaycastDistance)
                : 0f;

            float sf = bikeSuspension.groundNormalSmoothing * Time.fixedDeltaTime;
            _smoothedCompression_f = frontWheelIsGrounded ? Mathf.Lerp(_smoothedCompression_f, raw_f, sf) : 0f;
            _smoothedCompression_r = rearWheelIsGrounded  ? Mathf.Lerp(_smoothedCompression_r, raw_r, sf) : 0f;
            compression_f     = _smoothedCompression_f;
            compression_r     = _smoothedCompression_r;
            compression_Total = Mathf.Clamp01((compression_f + compression_r) / 2f);

            float mard  = ((maxFrontRaycastDistance * _cosFront) + (maxRearRaycastDistance * _cosRear)) / 2f;
            compressionForSnap = ((raw_f * _cosFront) + (raw_r * _cosRear)) / 2f;
            snapDistance = mard * (compressionForSnap - bikeSuspension.MaxCompression);
        }

        private void AddSuspension()
        {
            Vector3 normal = Vector3.up;
            if (frontWheelIsGrounded && rearWheelIsGrounded) normal = groundNormal;
            else if (rearWheelIsGrounded)                    normal = rearWheelHit.normal;

            Vector3 springDir   = normal.normalized;
            float   springVel   = Vector3.Dot(bikeReferences.BikeRb.linearVelocity, springDir);
            float   springForce = bikeSuspension.SpringForce * (compression_Total - bikeSuspension.groundStickFactor * compression_Total);
            float   damperForce = bikeSuspension.DamperForce * springVel;
            damperForce = Mathf.Clamp(damperForce, -Mathf.Abs(springVel) / Time.fixedDeltaTime, Mathf.Abs(springVel) / Time.fixedDeltaTime);

            if (compression_Total < 0.05f) damperForce = 0f;

            Vector3 suspensionForce = springDir * (springForce - damperForce);
            suspensionForceMagnitude = suspensionForce.magnitude;

            if (bikeIsGrounded)
            {
                if (compressionForSnap > bikeSuspension.MaxCompression)
                {
                    if (!bikeSnappedToGround)
                    {
                        bikeReferences.BikeRb.MovePosition(bikeReferences.BikeRb.position + projectedBikeUp * snapDistance);
                        ReduceSurfaceNormalDownVelocity(bikeSuspension.snapVelocityReduction);
                        bikeSnappedToGround = true;
                    }
                }
                else
                {
                    bikeSnappedToGround = false;
                }

                float rawLeanZ   = bikeReferences.LeanTransform.localEulerAngles.z;
                float leanAngle  = Mathf.Abs(rawLeanZ > 180f ? rawLeanZ - 360f : rawLeanZ);
                float leanFactor = Mathf.Cos(Mathf.Deg2Rad * leanAngle);
                bikeReferences.BikeRb.AddForce(suspensionForce * leanFactor, ForceMode.Acceleration);
            }
        }

        private void ClampBumpVelocity()
        {
            if (!bikeIsGrounded) return;
            Vector3 vel = bikeReferences.BikeRb.linearVelocity;
            float upwardVel = Vector3.Dot(vel, groundNormal);
            if (upwardVel > bikeSuspension.maxBumpUpwardVelocity)
                bikeReferences.BikeRb.linearVelocity = vel - groundNormal * (upwardVel - bikeSuspension.maxBumpUpwardVelocity);
        }

    #endregion

    #region Friction

        // Smooths the requested drift into _aiDriftIntensity, gated on speed + grounding
        private void UpdateDriftState()
        {
            bool can = driftRequested && bikeIsGrounded && _cachedSpeed >= bikeSettings.minDriftSpeed;
            float target = can ? 1f : 0f;
            float rate = target > _aiDriftIntensity ? bikeSettings.driftEntryRate : bikeSettings.driftExitRate;
            _aiDriftIntensity = Mathf.MoveTowards(_aiDriftIntensity, target, rate * Time.fixedDeltaTime);
        }

        private void AddFriction()
        {
            if (!bikeIsGrounded) return;

            float sideVelocityRatio = Mathf.Abs(localBikeVelocity.x / bikeSettings.maxSpeed);

            // Drifting eases off body lateral friction so the bike slides instead of gripping.
            float bodyFrictionMul = Mathf.Lerp(1f, bikeSettings.driftFrictionFactor, _aiDriftIntensity);
            float frictionForce   = (-localBikeVelocity.x * bikeSettings.frictionCoefficient / Time.fixedDeltaTime)
                                  * bikeCurves.FrictionCurve.Evaluate(sideVelocityRatio) * bodyFrictionMul;

            bikeReferences.BikeRb.AddForceAtPosition(
                bikeReferences.Rotator.right * frictionForce, transform.position, ForceMode.Acceleration);

            // Rear wheel lateral grip, high by default so the rear holds, dropped toward the drift value
            // while drifting so the back steps out (mirrors the player's handbrake drift)
            if (rearWheelIsGrounded)
            {
                Vector3 rearWorldVel = bikeReferences.BikeRb.GetPointVelocity(bikeReferences.RearWheel.position);
                float   rearLateralVel = bikeReferences.Rotator.InverseTransformDirection(rearWorldVel).x;

                float rearGrip = Mathf.Lerp(bikeSettings.rearLateralGripNormal, bikeSettings.rearLateralGripDrift, _aiDriftIntensity);
                float rearGripForce = -rearLateralVel * bikeSettings.frictionCoefficient * rearGrip * 5f;
                float maxGripForce  = bikeSettings.frictionCoefficient * bikeSettings.maxSpeed * rearGrip * 2f;
                rearGripForce = Mathf.Clamp(rearGripForce, -maxGripForce, maxGripForce);

                bikeReferences.BikeRb.AddForceAtPosition(
                    bikeReferences.Rotator.right * rearGripForce,
                    bikeReferences.RearWheel.position, ForceMode.Acceleration);
            }
        }

    #endregion

    #region Acceleration/Braking

        private void HandleAcceleration()
        {
            float forwardSpeed = localBikeVelocity.z;

            // Countdown lock
            if (!canMove && bikeIsGrounded)
            {
                if (Mathf.Abs(forwardSpeed) > 0.01f)
                {
                    Vector3 brakeDir = Vector3.ProjectOnPlane(projectedBikeForward, groundNormal);
                    bikeReferences.BikeRb.AddForce(brakeDir * (-forwardSpeed / Time.fixedDeltaTime), ForceMode.Acceleration);
                }
                return;
            }

            if (!bikeIsGrounded || !canAccelerate) return;

            float targetAcceleration = 0f;

            if (isBraking && throttle <= 0f)
            {
                targetAcceleration = forwardSpeed >= 0f ? -bikeSettings.deceleration : bikeSettings.deceleration;
                isApplyingBrake = true;
            }
            else if (throttle > 0f && !isRevving)
            {
                // Curve taper matches BikeController. Running flat here gave the AI roughly
                // double the player's acceleration at racing speed, so it walked away down every straight
                targetAcceleration = forwardSpeed >= 0f
                    ? Mathf.Min(
                        bikeSettings.acceleration * throttle * LaunchAccelMultiplier()
                            * bikeCurves.AccelerationCurve.Evaluate(_cachedSpeedRatio),
                        (bikeSettings.maxSpeed - forwardSpeed) / Time.fixedDeltaTime)
                    : bikeSettings.deceleration;
                isApplyingBrake = false;

                float slopeFactor = Mathf.Max(0f, projectedBikeForward.y);
                targetAcceleration += bikeSettings.uphillAccelerationBoost * slopeFactor;
            }
            else
            {
                AddRollingResistance();
                isApplyingBrake = false;
                return;
            }

            Vector3 accelDir = Vector3.ProjectOnPlane(projectedBikeForward, groundNormal);
            bikeReferences.BikeRb.AddForce(accelDir * targetAcceleration, ForceMode.Acceleration);
        }
        private float LaunchAccelMultiplier()
        {
            int slow = bikeSettings.slowLaunchGears;
            if (slow <= 0 || currentGear > slow) return 1f;
            float bandTopKmh = (gearSpeeds != null && gearSpeeds.Length > 0)
                ? gearSpeeds[Mathf.Min(slow, gearSpeeds.Length) - 1]
                : bikeSettings.maxSpeed * 3.6f;
            float speedKmh = Mathf.Abs(_cachedSpeed * 3.6f);
            float t = bandTopKmh > 0f ? Mathf.Clamp01(speedKmh / bandTopKmh) : 1f;
            return Mathf.Lerp(bikeSettings.launchAccelMultiplier, 1f, t);
        }

        private void AddRollingResistance()
        {
            Vector3 dir      = Vector3.ProjectOnPlane(projectedBikeForward, groundNormal);
            Vector3 force    = -bikeSettings.rollingResistance * Mathf.Sign(localBikeVelocity.z) * dir;
            float   maxForce = bikeSettings.rollingResistance * Mathf.Abs(localBikeVelocity.z);
            bikeReferences.BikeRb.AddForce(Vector3.ClampMagnitude(force, maxForce), ForceMode.Acceleration);
        }

    #endregion

    #region Steering/Turning

        private void SteeringAnimation(float direction)
        {
            float steeringAngle = bikeSettings.maxTurnAngle * bikeCurves.SteeringCurve.Evaluate(_cachedFwdSpeedRatio);
            currentSteerAngle   = steeringAngle;

            Quaternion targetRot = Quaternion.Euler(0, steeringAngle * direction, 0);
            bikeReferences.BikeSteering.localRotation = Quaternion.Slerp(
                bikeReferences.BikeSteering.localRotation, targetRot,
                Time.deltaTime * bikeSettings.steeringAnimationSpeed);
        }

        private void AddTurning(float direction)
        {
            if (bikeSettings.useLerpTurning)
                steerSmoother = Mathf.MoveTowards(steerSmoother, direction, Time.fixedDeltaTime * bikeSettings.turnLerpSpeed);
            else
                steerSmoother = direction;

            _steerInput = direction > 0.1f ? 1 : (direction < -0.1f ? -1 : 0);

            if (bikeIsGrounded)
            {
                float steeringAngleRad = steerSmoother * Mathf.Deg2Rad * currentSteerAngle;
                // Only guards the Tan divide. This was 0.01 rad (0.57 deg), and at race speed every lane change the
                // AI asks for is smaller than that, so it was silently discarded and the bike never turned
                if (Mathf.Abs(steeringAngleRad) < 1e-5f) return;

                float turningRadius   = wheelbase / Mathf.Tan(steeringAngleRad);
                float angularVelocity = localBikeVelocity.z / turningRadius;
                float rotationAmount  = angularVelocity * Time.fixedDeltaTime;

                // Drift adds a gentle turn rate boost so the slide actually rotates the bike into the corner
                float driftTurnMul = Mathf.Lerp(1f, bikeSettings.driftTurnBoost, _aiDriftIntensity);
                bikeReferences.Rotator.Rotate(
                    projectedBikeUp, Mathf.Rad2Deg * rotationAmount * driftTurnMul, Space.World);
            }
            else if (bikeSettings.canSteerInAir)
            {
                bikeReferences.Rotator.Rotate(
                    Vector3.up, Time.fixedDeltaTime * bikeSettings.turnSpeedInAir * steerSmoother, Space.World);
            }
        }

    #endregion

    #region Leaning Animation

        private void LeaningAnimation(float direction)
        {
            leanSmoother = Mathf.MoveTowards(leanSmoother, direction, Time.deltaTime * 10f);

            float leanAngle = -bikeSettings.maxLeanAngle
                            * bikeCurves.LeanCurve.Evaluate(_cachedSpeedRatio)
                            * Mathf.Sign(leanSmoother);

            // Lean harder into the slide while drifting (visual cohesion with the player's drift lean)
            if (_aiDriftIntensity > 0.05f)
            {
                leanAngle += Mathf.Sign(leanSmoother) * _aiDriftIntensity * bikeSettings.driftLeanBoost * bikeSettings.maxLeanAngle;
                leanAngle  = Mathf.Clamp(leanAngle, -bikeSettings.maxLeanAngle * 1.5f, bikeSettings.maxLeanAngle * 1.5f);
            }

            currentLeanAngle = leanAngle * Mathf.Abs(leanSmoother);

            // Idle tilt is visual only, so the rider's authored idle pose is not doubled up by it
            _burnoutUpright01 = Mathf.MoveTowards(_burnoutUpright01,
                isDoingBurnout ? bikeSettings.burnoutUprightAmount : 0f,
                Time.deltaTime / Mathf.Max(0.01f, bikeSettings.burnoutUprightBlendTime));
            float idleTilt = idleLeanWeight * BikeIdleLean.Angle(bikeSettings.idleLeanAngle,
                bikeSettings.idleLeanFadeOutSpeed, localBikeVelocity.magnitude, _burnoutUpright01);
            Quaternion targetRot = Quaternion.Euler(0, 0, currentLeanAngle + idleTilt);
            bikeReferences.LeanTransform.localRotation = Quaternion.Slerp(
                bikeReferences.LeanTransform.localRotation, targetRot,
                Time.deltaTime * bikeSettings.leaningAnimationSpeed);
        }

    #endregion

    #region Wheelie Animation

        private void WheelieAnimation()
        {
            if (bikeReferences.WheelieTransform == null) return;

            float targetAngle = 0f;

            float currentAngle = bikeReferences.WheelieTransform.localEulerAngles.x;
            if (currentAngle > 180f) currentAngle -= 360f;
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, Time.deltaTime * bikeSettings.wheelieAnimationSpeed);

            bikeReferences.WheelieTransform.localEulerAngles = new Vector3(currentAngle, 0, 0);
        }

    #endregion

    #region Tire Animation

        private void TireAnimation()
        {
            // Front wheel
            float rot_f = isApplyingBrake
                ? 0f
                : (localBikeVelocity.z / bikeGeometry.FrontWheelRadius) * Time.deltaTime * Mathf.Rad2Deg;
            bikeReferences.FrontWheel.RotateAround(
                bikeReferences.FrontWheel.position, bikeReferences.FrontWheelParent.right, rot_f);
            var rotF = bikeReferences.FrontWheel.localRotation; rotF.y = 0; rotF.z = 0;
            bikeReferences.FrontWheel.localRotation = rotF;

            // Rear wheel
            float rot_r = isApplyingBrake
                ? 0f
                : (localBikeVelocity.z / bikeGeometry.RearWheelRadius) * Time.deltaTime * Mathf.Rad2Deg;
            bikeReferences.RearWheel.RotateAround(
                bikeReferences.RearWheel.position, bikeReferences.RearWheelParent.right, rot_r);
            var rotR = bikeReferences.RearWheel.localRotation; rotR.y = 0; rotR.z = 0;
            bikeReferences.RearWheel.localRotation = rotR;
        }

    #endregion

    #region Gear System

        // Downshift deadband (km/h) mirrors BikeController.GEAR_HYSTERESIS_KMH. Keep in sync
        private const float GEAR_HYSTERESIS_KMH = 6f;

        private void UpdateGearShift()
        {
            // On the start grid the AI revs but is throttle-locked (isRevving, set until GO)
            // Keep it in 1st so revving on the line never climbs gears before the race starts
            if (isRevving)
            {
                currentGear     = 1;
                currentGearTemp = 1;
                return;
            }

            float bikeSpeedKmh = _cachedSpeed * 3.6f;
            // Hysteresis, upshift at the gear's top speed, downshift only after dropping a band below it,
            // so a bike cruising on a gear boundary can't rapidly flip gears
            int g = Mathf.Clamp(currentGear, 1, gearSpeeds.Length + 1);
            while (g <= gearSpeeds.Length && bikeSpeedKmh > gearSpeeds[g - 1]) g++;
            while (g > 1 && bikeSpeedKmh < gearSpeeds[g - 2] - GEAR_HYSTERESIS_KMH) g--;
            currentGear = g;
            if (currentGearTemp != currentGear)
            {
                currentGearTemp = currentGear;
                if (throttle > 0f && localBikeVelocity.z > 0f && bikeIsGrounded)
                {
                    bikeEvents.OnGearChange?.Invoke();
                    GearChanged?.Invoke();
                }
            }
        }

    #endregion

    #region Audio

        private void UpdateTireSmokeThrottled()
        {
            _tireSmokeCounter++;
            if (_tireSmokeCounter >= TIRE_SMOKE_INTERVAL)
            {
                _tireSmokeCounter = 0;
                UpdateTireSmoke();
            }
        }

    #endregion

    #region Skidmarks & Tire Smoke

        private void CalculateWheelSlipsThrottled()
        {
            _wheelSlipCounter++;
            if (_wheelSlipCounter < WHEELSLIP_INTERVAL) return;
            _wheelSlipCounter = 0;
            CalculateWheelSlips();
        }

        private void UpdateSkidmarksThrottled()
        {
            _skidmarkCounter++;
            if (_skidmarkCounter < SKIDMARK_INTERVAL) return;
            _skidmarkCounter = 0;

            // Skip skidmarks at low speed, not visible anyway
            if (_cachedSpeed < SKIDMARK_MIN_SPEED) return;

            UpdateSkidmarks();
        }

        private void CalculateWheelSlips()
        {
            if (isApplyingBrake) { forwardSlip_frontWheel = 1; forwardSlip_rearWheel = 1; }
            else if (isDoingBurnout) { forwardSlip_frontWheel = 0; forwardSlip_rearWheel = 1; }
            else { forwardSlip_frontWheel = 0; forwardSlip_rearWheel = 0; }

            Vector3 sideWaysDirection_frontWheel = Vector3.ProjectOnPlane(bikeReferences.FrontWheelParent.right, projectedBikeUp).normalized;
            float sideWaysSpeed_frontWheel = Vector3.Dot(bikeReferences.BikeRb.GetPointVelocity(bikeReferences.FrontWheelParent.position), sideWaysDirection_frontWheel);
            sideSlip_frontWheel = Mathf.Abs(sideWaysSpeed_frontWheel) < 0.01f ? 0 : sideWaysSpeed_frontWheel / bikeSettings.maxSpeed;

            Vector3 sideWaysDirection_rearWheel = Vector3.ProjectOnPlane(bikeReferences.RearWheelParent.right, projectedBikeUp).normalized;
            float sideWaysSpeed_rearWheel = Vector3.Dot(bikeReferences.BikeRb.GetPointVelocity(bikeReferences.RearWheelParent.position), sideWaysDirection_rearWheel);
            sideSlip_rearWheel = Mathf.Abs(sideWaysSpeed_rearWheel) < 0.01f ? 0 : sideWaysSpeed_rearWheel / bikeSettings.maxSpeed;

            TotalSlip_frontWheel = _cachedSpeed < 1f ? 0f : Mathf.Abs((forwardSlip_frontWheel + sideSlip_frontWheel) / 2f);
            TotalSlip_rearWheel  = _cachedSpeed < 1f ? 0f : Mathf.Abs((forwardSlip_rearWheel  + sideSlip_rearWheel)  / 2f);

            // A grid burnout is stationary, so the speed gate above zeroed its slip and the AI's burnout
            // made no smoke, no skid mark and no noise. BikeController has the same override
            if (isDoingBurnout)
                TotalSlip_rearWheel = Mathf.Abs((forwardSlip_rearWheel + sideSlip_rearWheel) / 2f);

            // Drifting forces rear slip so skidmarks + tyre smoke + skid audio fire during an AI drift
            if (isAIDrifting)
                TotalSlip_rearWheel = Mathf.Max(TotalSlip_rearWheel, _aiDriftIntensity * 0.8f);

            if (!bikeIsGrounded) { TotalSlip_frontWheel = 0f; TotalSlip_rearWheel = 0f; }
        }

        private void UpdateSkidmarks()
        {
            if (skidmarkController == null) return;

            skidmarkIndexFront = frontWheelIsGrounded
                ? skidmarkController.AddSkidMark(frontWheelHit.point, groundNormal_front, TotalSlip_frontWheel, skidmarkIndexFront)
                : -1;

            skidmarkIndexRear = rearWheelIsGrounded
                ? skidmarkController.AddSkidMark(rearWheelHit.point, groundNormal_rear, TotalSlip_rearWheel, skidmarkIndexRear)
                : -1;
        }

        private void UpdateTireSmoke()
        {
            if (tireSmoke_ps == null) return;

            // Only the burnout stands aside for BurnoutVFX: this still owns skids and drifts,
            // and with no BurnoutVFX on the bike it keeps the burnout too
            if (isDoingBurnout && burnoutSmokeSuppressed) { tireSmoke_ps.Stop(); return; }

            if (TotalSlip_rearWheel <= BikeController.SMOKE_SLIP_THRESHOLD)
            {
                tireSmoke_ps.Stop();
                return;
            }

            tireSmoke_ps.Play();

            float burn = isDoingBurnout ? 1f : 0f;
            _tireSmokeMain.startSizeMultiplier        = _tireSmokeBaseSize   * Mathf.Lerp(1f, bikeSettings.burnoutSmokeMaxSize,   burn);
            _tireSmokeEmission.rateOverTimeMultiplier = _tireSmokeBaseRate   * Mathf.Lerp(1f, bikeSettings.burnoutSmokeMaxRate,   burn);
            _tireSmokeShape.radius                    = _tireSmokeBaseRadius * Mathf.Lerp(1f, bikeSettings.burnoutSmokeMaxRadius, burn);
        }

    #endregion

    #region Gravity

        private void AddGravity()
        {
            float multiplier = (!bikeIsGrounded && _airTime >= 0.5f) ? bikeSettings.fallGravityMultiplier : 1f;
            bikeReferences.BikeRb.AddForce(Vector3.down * bikeSettings.gravity * multiplier, ForceMode.Acceleration);
        }

    #endregion

    #region Utility

        private Vector3 RotateVector(Vector3 original, Vector3 axis, float degrees)
        {
            return Quaternion.AngleAxis(degrees, axis) * original;
        }

        private void ReduceSurfaceNormalDownVelocity(float factor)
        {
            Vector3 vel = bikeReferences.BikeRb.linearVelocity;
            float velAlongUp = Vector3.Dot(vel, projectedBikeUp);

            if (velAlongUp < 0f)
                bikeReferences.BikeRb.linearVelocity = vel - projectedBikeUp * (velAlongUp * factor);
        }

        private void SuppressCollisionLaunch()
        {
            if (canMove || !bikeIsGrounded) return;

            Vector3 vel = bikeReferences.BikeRb.linearVelocity;
            if (vel.y > 1.5f)
            {
                vel.y = Mathf.MoveTowards(vel.y, 0f, vel.y * 0.8f);
                bikeReferences.BikeRb.linearVelocity = vel;
            }
        }

        // Warn in console if the capsule collider isn't set up to represent the bike model
        private void ValidateCapsule()
        {
            var cap = bikeReferences.collider;
            if (cap == null)
            {
                Debug.LogWarning($"[BikeAI] {gameObject.name}: bikeReferences.collider is not assigned.", this);
                return;
            }

            // Direction 2 = Z axis (along bike length). 0 = X, 1 = Y (Unity default, wrong for a bike)
            if (cap.direction != 2)
                Debug.LogWarning(
                    $"[BikeAI] {gameObject.name}: CapsuleCollider direction is {cap.direction} " +
                    $"(should be 2 = Z axis so the capsule runs along the bike's length).", this);

            // Height check, the capsule should be long enough to cover the full bike length
            // Capsule total end to end length = height (Unity stores it as the full length including hemispheres)
            if (cap.height < 1.5f)
                Debug.LogWarning(
                    $"[BikeAI] {gameObject.name}: CapsuleCollider height = {cap.height:F2} m. " +
                    $"This may be too short to cover the full bike front/rear will be unprotected.", this);

            // World height check, the capsule's GameObject may be offset from the Rigidbody's root
            // Check the transform world Y relative to the Rigidbody, not the local center offset
            float capsuleWorldY = cap.transform.position.y + cap.center.y;
            float rbWorldY      = bikeReferences.BikeRb.position.y;
            float heightAboveRb = capsuleWorldY - rbWorldY;
            if (heightAboveRb < 0.3f)
                Debug.LogWarning(
                    $"[BikeAI] {gameObject.name}: CapsuleCollider world centre is only {heightAboveRb:F2} m " +
                    $"above the Rigidbody expected ~0.6–1.0 m (mid bike height).", this);
        }

        internal void SetCanMove(bool value) { canMove = value; }

        // Called at GO. If this AI burned out on the grid, reward it with an immediate getaway shove
        // off the line (bypasses the boost system's start lockout). Also clears the burnout state
        public void ApplyStartBurnoutLaunch()
        {
            isDoingBurnout = false;
            if (!willStartBurnout) return;
            willStartBurnout = false;

            Vector3 fwd = bikeReferences.Rotator != null ? bikeReferences.Rotator.forward : transform.forward;
            bikeReferences.BikeRb.linearVelocity += fwd * (startBurnoutLaunchKmh / 3.6f);
        }

    #endregion

#if UNITY_EDITOR
        [ContextMenu("Update Wheel Angles")]
        void UpdateWheelAngles()
        {
            UnityEditor.Undo.RecordObject(this, "Update Wheel Angles");
            bikeGeometry.FrontWheelAngle = Vector3.Angle(bikeReferences.FrontWheelParent.up, transform.up);
            bikeGeometry.RearWheelAngle  = Vector3.Angle(bikeReferences.RearWheelParent.up,  transform.up);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void OnDrawGizmos()
        {
            if (bikeReferences?.FrontWheelParent == null || bikeReferences?.RearWheelParent == null) return;
            Vector3 physicsUp = bikeReferences.Rotator != null ? bikeReferences.Rotator.up : Vector3.up;

            // Front wheel ray green if grounded, red if not
            Vector3 fOrigin = bikeReferences.FrontWheelParent.position + physicsUp * bikeGeometry.FrontWheelRadius;
            Gizmos.color = frontWheelIsGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(fOrigin, fOrigin - physicsUp * maxFrontRaycastDistance);
            Gizmos.DrawWireSphere(fOrigin, 0.05f);

            // Rear wheel ray green if grounded, red if not
            Vector3 rOrigin = bikeReferences.RearWheelParent.position + physicsUp * bikeGeometry.RearWheelRadius;
            Gizmos.color = rearWheelIsGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(rOrigin, rOrigin - physicsUp * maxRearRaycastDistance);
            Gizmos.DrawWireSphere(rOrigin, 0.05f);
        }
#endif
    }
}
