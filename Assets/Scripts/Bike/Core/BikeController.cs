using UnityEngine;

namespace MotoSquid.Bike
{
    // The player bike. Each part of the simulation is its own class, this component owns the shared
    // state and runs the parts in a fixed order, because the physics steps depend on that order
    public partial class BikeController : MonoBehaviour, IBikeAudioState
    {
        [HideInInspector]
        public float projectedForwardOffsetAngle;

        [HideInInspector]
        public bool bikeIsGrounded, frontWheelIsGrounded, rearWheelIsGrounded, isDoingWheelie = false, canAccelerate = true, canMove = true;

        public Vector3 localBikeVelocity { get; private set; }

        // Driven by BikeAnimationController so a forced riding pose suppresses the standstill tilt
        [System.NonSerialized] public float idleLeanWeight = 1f;

        public bool burnoutSmokeSuppressed;

        public BikeGroundContact Ground { get; private set; }
        public BikeSuspensionForces Suspension { get; private set; }
        public BikeTyreGrip Grip { get; private set; }
        public BikeDrivetrain Drivetrain { get; private set; }
        public BikeSteering Steering { get; private set; }
        public BikeBurnout Burnout { get; private set; }
        public BikeWheelie Wheelie { get; private set; }
        BikeVisuals visuals;
        BikeTyreEffects tyreEffects;
        BikeAudioController _audio;

        internal float CachedSpeed { get; private set; }
        internal float CachedSpeedRatio { get; private set; }      // CachedSpeed / maxSpeed, for LeanCurve and AccelerationCurve
        internal float CachedFwdSpeedRatio { get; private set; }   // Abs(z) / maxSpeed, for SteeringCurve
        internal float CachedSideRatio { get; private set; }       // Abs(x) / maxSpeed, for FrictionCurve
        internal float Wheelbase { get; private set; }

        // A stationary burnout produces exactly 0.5 rear slip (forward 1, sideways 0, averaged), so a
        // threshold of 0.5 rejects it by nothing at all. BikeAIController reads this rather than
        // carrying its own copy, which is how the AI's burnout came to be invisible
        public const float SMOKE_SLIP_THRESHOLD = 0.3f;


        void Awake()
        {
            Ground = new BikeGroundContact(this);
            Suspension = new BikeSuspensionForces(this);
            Grip = new BikeTyreGrip(this);
            Drivetrain = new BikeDrivetrain(this);
            Steering = new BikeSteering(this);
            Burnout = new BikeBurnout(this);
            Wheelie = new BikeWheelie(this);
            visuals = new BikeVisuals(this);
            tyreEffects = new BikeTyreEffects(this);
            tyreEffects.CreateEffects();
        }

        void Start()
        {
            bikeReferences.BikeRb.useGravity = false;
            Wheelbase = Vector3.Distance(bikeReferences.FrontWheel.position, bikeReferences.RearWheel.position);
            projectedForwardOffsetAngle = Vector3.Angle(bikeReferences.Rotator.forward,
                (bikeReferences.FrontWheelParent.position - bikeReferences.RearWheelParent.position).normalized);

            Ground.Start();
            Wheelie.Start();
            tyreEffects.Start();
            Burnout.Start();
            _audio = GetComponentInChildren<BikeAudioController>(true);
        }

        void Update()
        {
            Steering.UpdateInput();
            Steering.UpdateSteerAngle();

            visuals.AnimateSteering(Steering.AnalogSteer);
            visuals.AnimateLean((bikeIsGrounded || bikeSettings.canLeanInAir) ? Steering.AnalogSteer : 0f);
            Wheelie.Animate();
            visuals.SpinTyres();

            tyreEffects.Tick();
        }

        // Runs after Update, so the wheels follow this frame's wheelie pitch rather than the last physics step's
        void LateUpdate()
        {
            visuals.EaseWheels(Ground.FrontDroopTarget, Ground.RearDroopTarget);
        }

        void FixedUpdate()
        {
            localBikeVelocity   = bikeReferences.Rotator.InverseTransformDirection(bikeReferences.BikeRb.linearVelocity);
            CachedSpeed         = localBikeVelocity.magnitude;
            float invMax        = bikeSettings.maxSpeed > 0f ? 1f / bikeSettings.maxSpeed : 0f;
            CachedSpeedRatio    = CachedSpeed * invMax;
            CachedFwdSpeedRatio = Mathf.Abs(localBikeVelocity.z) * invMax;
            CachedSideRatio     = Mathf.Abs(localBikeVelocity.x) * invMax;

            Ground.SampleWheels();
            Wheelie.UpdateState();
            Ground.UpdateSurface();
            Ground.AlignRotator(Suspension.CompressionTotal);
            Ground.AddGravity();

            Suspension.UpdateCompression();
            Suspension.AddForces();
            Suspension.ClampBumpVelocity();

            Grip.AddFriction();
            Drivetrain.HandleAccelerationAndReverse();
            Burnout.HandleBurnoutAndRotation();
            Steering.AddTurning(Steering.AnalogSteer);
            Drivetrain.UpdateGearShift();

            tyreEffects.FixedTick();
            Burnout.UpdateLaunch();
        }


        public int CurrentSteerInput => Steering?.CurrentSteerInput ?? 0;

        // Smoothed analog steer in [-1, 1] (gamepad deflection/eased keyboard). Read by the
        // camera to yaw the rider's view toward the corner apex
        public float AnalogSteer => Steering?.AnalogSteer ?? 0f;

        public bool isDrifting => Grip != null && Grip.IsDrifting;
        public float DriftAngle => Grip?.DriftAngle ?? 0f;
        public float DriftIntensity => Grip?.DriftIntensity ?? 0f;
        public SkidmarkController skidmarkController => tyreEffects?.Skidmarks;
        public bool isDoingBurnout => Burnout != null && Burnout.IsDoing;
        public float currentLeanAngle => visuals?.CurrentLeanAngle ?? 0f;

        // How far into the burnout the launch is banked, for effects that should build with it
        public float LaunchCharge => Burnout?.LaunchCharge ?? 0f;

        // Fired at GO with the burnout-launch result, true = nailed (getaway shove), false = bogged.
        // HUD/FX can subscribe to show a "perfect launch/bogged" cue
        public event System.Action<bool> OnBurnoutLaunch;
        internal void RaiseBurnoutLaunch(bool nailed) => OnBurnoutLaunch?.Invoke(nailed);

        // Simulated engine RPM 0..1 (idle - redline), driven by the audio controller. Lets the HUD
        // show revs even at a standstill (start line rev/burnout), where speed derived revs read 0
        public float EngineRpm01 => _audio != null ? _audio.EngineRpm01 : 0f;
        public bool  EngineAtLimiter => _audio != null && _audio.EngineAtLimiter;  // HUD flashes the redline off this

        public int CurrentGearProperty
        {
            get => Drivetrain?.GearProperty ?? 0;
            set { if (Drivetrain != null) Drivetrain.GearProperty = value; }
        }

        public float GearProgress01 => Drivetrain?.GearProgress01 ?? 0f;

        // Report 0 while held on the start line (canMove == false), a spawn/settle velocity
        // transient must not spike the engine RPM or wind before the race has begun
        public float SpeedMs             => canMove ? CachedSpeed : 0f;
        public float SkidIntensity01     => tyreEffects?.SkidIntensity01 ?? 0f;
        public bool  IsGrounded          => bikeIsGrounded;
        public bool  IsApplyingHandBrake => Drivetrain != null && Drivetrain.IsApplyingHandBrake;
        public bool  IsDoingBurnout      => isDoingBurnout;
        public bool  CanMove             => canMove;
        public bool  IsRevving           => false; // Player has no start line rev animation
        public float ThrottleInput       => bikeInput != null ? bikeInput.Accelerate : 0f;
        public float ReverseInput        => bikeInput != null ? bikeInput.Reverse  : 0f;
        public int   CurrentGear         => currentGear;
        public int[] GearSpeeds          => gearSpeeds;
        public float MaxSpeedKmh         => bikeSettings.maxSpeed * 3.6f;
        public float SteerAmount         => AnalogSteer;
        public bool  IsDoingWheelie      => isDoingWheelie;
        public bool  IsBraking           => Drivetrain != null && Drivetrain.IsBraking;
        public event System.Action GearChanged;
        public event System.Action<bool> EngineMuteRequested;
        internal void RaiseGearChanged() => GearChanged?.Invoke();


        public void StartBike()
        {
            canAccelerate = true;
            EngineMuteRequested?.Invoke(false);
        }

        public void StopBike()
        {
            canAccelerate = false;
            EngineMuteRequested?.Invoke(true);
        }

        public Vector3 RotateVector(Vector3 originalVector, Vector3 axis, float degree) =>
            Quaternion.AngleAxis(degree, axis) * originalVector;

        public void ReduceSurfaceNormalDownVelocity(float factor)
        {
            Vector3 vel = bikeReferences.BikeRb.linearVelocity;
            Vector3 up = Ground.ProjectedUp;
            float velAlongUp = Vector3.Dot(vel, up);

            if (velAlongUp < 0f)
                bikeReferences.BikeRb.linearVelocity = vel - up * (velAlongUp * factor);
        }

        public void ProvideInput(float accelerate, float reverse, float handBrake, float steerLeft, float steerRight, float wheelie)
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
            UnityEditor.Undo.RecordObject(this, "Update Wheel Angles");
            bikeGeometry.FrontWheelAngle = Vector3.Angle(bikeReferences.FrontWheelParent.up, transform.up);
            bikeGeometry.RearWheelAngle = Vector3.Angle(bikeReferences.RearWheelParent.up, transform.up);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Auto Adjust GearSpeeds")]
        void AutoAdjustGearSpeeds()
        {
            UnityEditor.Undo.RecordObject(this, "Auto Adjust GearSpeeds");

            int numberOfGears = gearSpeeds.Length;
            // gearSpeeds are compared against speed in KM/H at runtime, but maxSpeed is m/s, convert
            float maxSpeedKmh = bikeSettings.maxSpeed * 3.6f;
            float speedIncrement = maxSpeedKmh / numberOfGears;

            for (int i = 0; i < numberOfGears; i++)
            {
                // The last gear tops out at 95% of max speed
                gearSpeeds[i] = i == numberOfGears - 1
                    ? Mathf.RoundToInt(maxSpeedKmh * 0.95f)
                    : Mathf.RoundToInt(speedIncrement * (i + 1));
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
