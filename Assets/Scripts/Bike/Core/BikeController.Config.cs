using MotoSquid.Cameras;
using MotoSquid.Rider;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace MotoSquid.Bike
{
    // Inspector facing configuration. Field names are serialized, renaming one drops its saved value
    public partial class BikeController
    {
        [Serializable]
        public class BikeInput
        {
            public float Accelerate = 0;
            public float Reverse = 0;
            public float HandBrake = 0;
            public float SteeringLeft = 0;
            public float SteeringRight = 0;
            public float Wheelie = 0;
        }

        [HideInInspector]
        public BikeInput bikeInput;


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
            public CameraController cameraController;
            public RagdollActivator ragdollActivator;
        }

        public BikeReferences bikeReferences;


        [Serializable]
        public class BikeGeometry
        {
            public float FrontWheelRadius = 0.5f;
            public float FrontWheelWidth = 0.5f;
            public float RearWheelRadius = 0.5f;
            public float RearWheelWidth = 0.5f;
            public float FrontWheelAngle = 15f;
            public float RearWheelAngle = 0f;
        }
        public BikeGeometry bikeGeometry;

        [Serializable]
        public class BikeSuspension
        {
            public float SpringForce = 100;
            public float DamperForce = 25;
            public float groundStickFactor = 0.2f;
            public float MaxCompression = 0.5f;
            public float MaxDroop = 0.5f;
            public float suspensionCastRadius = 0.25f;
            public float groundNormalSmoothing = 8f;
            public float extraGroundedDistance = 0.5f;
            public float maxBumpUpwardVelocity = 4f;
        }

        public BikeSuspension bikeSuspension;


        [Serializable]
        public class BikeSettings
        {
            public LayerMask drivableLayerMask = ~0;
            public float maxSpeed = 100;
            public float reverseMaxSpeed = 3;
            public float acceleration = 10;
            public int slowLaunchGears = 2;
            public float launchAccelMultiplier = 0.5f;
            public float reverseAcceleration = 5;
            public float deceleration = 20;
            public float handBrakeDeceleration = 0;
            public float maxTurnAngle = 20;

            [Header("Analog Steering")]
            public bool useAnalogSteering = true;
            public float steerAttackRate = 4f;
            public float steerReleaseRate = 6f;
            public bool useLerpTurning = false;
            public float turnLerpSpeed = 5;
            public bool useSteerBuildup = false;
            public float steerBuildupRate = 2f;
            public float steerBuildupDecayRate = 5f;
            public float steeringAnimationSpeed = 5;
            public bool canSteerInAir = false;
            public bool canLeanInAir = true;
            public float turnSpeedInAir = 70;
            public float maxLeanAngle = 35;
            public float leaningAnimationSpeed = 5;
            public float idleLeanAngle = 6f;
            public float idleLeanFadeOutSpeed = 2f;
            // How far the bike stands back up while holding a start line burnout (0 = no change, 1 = fully upright)
            public float burnoutUprightAmount = 0.75f;
            public float burnoutUprightBlendTime = 0.2f;
            public float frictionCoefficient = 0.5f;
            public float driftFrictionFactor = 0.5f;
            public float driftTurnFactor = 2f;
            public float rollingResistance = 1;
            public float gravity = 9.81f;
            public float fallGravityMultiplier = 5f;
            public float burnoutRotationSpeed = 60f;
            public float burnoutSmoothness = 1f;
            public float burnoutMaxRotationSpeed = 5f;
            // Start line burnout launch, holding a burnout on the grid during the countdown banks a launch
            // charge that's released as an instant getaway shove at GO. Burnout is countdown only (see
            // BikeBurnout) so it can't be triggered accidentally mid-race
            public float burnoutLaunchKmh        = 15f;    // Getaway speed off the line for a full charge nailed launch
            public float burnoutLaunchChargeRate = 0.6f;   // How fast the charge builds while burning out
            // The burnout only pays off if you DROP it (release the burnout) close to GO. Drop within
            // burnoutLaunchWindow of GO, shove, drop too early, or hold it through GO, a brief bog
            public float burnoutLaunchWindow     = 0.35f;  // How tight the well-timed release window is (s)
            public float burnoutBogDuration      = 0.6f;   // How long a mistimed launch bogs the bike (s)
            public float burnoutBogDrag          = 1.5f;   // Per second velocity drag while bogging (higher = harsher)
            // Tyre smoke builds while you hold the burnout (scaled by launch charge, 0-1), the longer you
            // burn out the denser/bigger/wider the smoke gets, up to these multipliers over the base prefab
            public float burnoutSmokeMaxRate     = 3f;     // Emission-rate multiplier at a full burnout (density)
            public float burnoutSmokeMaxSize     = 2f;     // Particle start size multiplier at a full burnout (thickness)
            public float burnoutSmokeMaxRadius   = 4f;     // Shape radius multiplier at a full burnout (spread)
            public float maxWheelieAngle = 30;
            public float wheelieAnimationSpeed = 3f;
            public float alignRotatorSpeedGround = 10;
            public float wheelieTurnMultiplier = 0.2f;
            public float alignRotatorSpeedAir = 5f;

            [Header("Drift Settings")]
            public float minDriftSpeed = 15f;
            public float driftEntryTime = 0.1f;
            public float driftExitTime = 0.3f;
            public float rearLateralGripDrift = 0.02f;
            public float rearLateralGripNormal = 0.05f;
            public float driftSteerGain = 1.5f;
            public float driftCounterSteerAssist = 0.5f;
        }

        public BikeSettings bikeSettings;


        [Serializable]
        public class BikeCurves
        {
            public AnimationCurve AccelerationCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.5f, 0.8f), new Keyframe(1, 0.5f));
            public AnimationCurve ReverseAccelerationCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.2f));
            public AnimationCurve SteeringCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.3f));
            public AnimationCurve FrictionCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.1f, 0.5f), new Keyframe(1, 0.1f));
            public AnimationCurve LeanCurve = new AnimationCurve(new Keyframe(0, 0.2f), new Keyframe(1, 1));
        }
        public BikeCurves bikeCurves;

        public int[] gearSpeeds = new int[] { 35, 80, 135, 200, 275 };
        public int currentGear = 1;


        [Serializable]
        public class BikeEvents
        {
            public UnityEvent OnTakeOff;
            public UnityEvent OnGrounded;
            public UnityEvent OnGearChange;
            public UnityEvent OnWheelieExceed;
        }

        public BikeEvents bikeEvents;

        // Throttle and brake are held but no smoke: says which of the four conditions is refusing
        public bool logBurnoutBlockers = true;
    }
}
