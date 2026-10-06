using MotoSquid.Bike;
using MotoSquid.Combat;
using MotoSquid.Race;
using MotoSquid.Track;
using MotoSquid.Traffic;
using UnityEngine;
using System.Collections.Generic;

namespace MotoSquid.AI
{
    public enum AIDifficulty { Easy, Normal, Hard }

    public class BikeAILogic : MonoBehaviour
    {
        [Header("Racing")]
        public float cruiseSpeedKmh      = 420f;
        public float cornerSpeedKmh      = 160f;
        public float minCornerApproachKmh = 25f;
        public float speedVariance       = 20f;
        public float lookAheadDistance   = 18f;
        public float yawGain             = 4f;
        public float steerDamping        = 0.25f;
        public float crossTrackGain      = 0.08f;
        public float steerSmoothing      = 8f;
        public float cornerLookAhead     = 100f;
        public float cornerBrakeAngle    = 12f;
        public float cornerGripMultiplier = 2.5f;
        public float brakeStrength       = 25f;
        public float lanePreferenceVariance = 3f;
        public float maxLateralAimOffset = 12f;
        // Six bias channels SmoothDamp independently into one lateral number, five of them with time
        // constants between 0.25 s and 0.6 s
        public float maxLateralAimSlew = 8f;
        // Only the strongest racer vs racer bias claims the line, instead of all of them being summed
        // Off restores the old summing behaviour
        public bool  singleRacingBias = true;

        [Header("Clinical Racing")]
        public bool clinicalRacing = true;
        // Geometric steering instead of a gain on heading error. Off falls back to yawGain
        public bool usePurePursuitSteering = true;

        [Header("Blocked -> Draft")]
        public float draftYieldClosingSpeed = 12f;              // M/s of closing before a blocked AI hard brakes
        [Range(0f, 1f)] public float draftFollowThrottle = 0.55f;

        // References
        public Transform       bikeTransform;
        public BikeAIController controller;
        public RaceManager      raceManager;
        public TrafficLaneNetwork laneNetwork;
        public WaypointPath waypointPath;
        public int roadIndex = 0;

        [Header("Route Graph (optional overrides waypointPath when assigned)")]
        public RouteGraph routeGraph;

        public float nextSegmentCommitDistance = 80f;

        public float shortcutBias = 0.3f;

        [Header("Barrier / Wall Avoidance")]
        // Layer(s) your barrier/wall colliders live on
        public LayerMask barrierLayerMask;
        // Bike starts steering away when a wall is within this distance (metres)
        public float wallDetectionDistance = 12f;
        // Lateral aim point shift applied per metre inside the detection zone
        public float wallAvoidanceStrength = 5f;

        [Header("Traffic Avoidance")]
        public bool enableTrafficAvoidance = true;
        // A/B toggle, ON uses the context steering danger map (finds gaps, predicts lane changes)
        // OFF uses the legacy pick a side ComputeTrafficDodge. Same output scalar either way
        public bool useContextSteering = true;
        // Number of lateral candidate slots the context map samples across scanWidth (forced odd, min 5)
        public int  contextSlots = 15;
        // Plans the line from every vehicle's known position and velocity a few seconds ahead instead of
        // dodging what the scan catches. Off falls back to the scan dodge above
        public bool  useTrafficPlanner    = true;
        public float plannerHorizon       = 3f;
        public float plannerMargin        = 0.6f;
        public float plannerInterval      = 0.1f;
        // Keeps the AI on the side of the road whose traffic goes its way. Head on, a car closes at
        // 120-140 m/s, faster than a bike at race speed can change lane after seeing it
        public bool  plannerStayOnCarriageway = true;
        // Once a second, logs every steering term for an AI more than 2.5 m off the line the planner chose
        public bool  plannerTrace = true;
        public float plannerLaneHalfWidth     = 1.5f;
        public LayerMask trafficLayerMask;
        public float detectionDistance = 250f;
        public float dodgeStrength = 8f;
        public float sphereCastRadius = 1.0f;
        public float scanWidth = 8f;
        public int scanProbes = 13;
        public float reactTime = 4.5f;
        public float minSecondsOfVisibility = 4f;
        public float scanFanHalfAngle = 8f;
        public float fullUrgencyDistance = 18f;
        public float panicSeconds = 0.4f;
        // Lateral clearance (1 = free, 0 = occupied) a racing bias must find before it will pull the bike
        // across for a rival. DodgeUrgency only sees the bike's CURRENT line, so a bias chasing the player
        // routes straight through a car in the next lane without this. Below the threshold the bias is
        // dropped and the AI holds its line until a gap opens
        [Range(0f, 1f)] public float minRouteClearance = 0.75f;

        [Header("Overtaking")]
        public float overtakeDetectionRange  = 60f;  // Meters ahead to look for a racer to pass
        public float overtakeMaxLateralRange = 4f;   // Ignore racers more than this far sideways
        public float overtakeLateralOffset   = 6f;   // Meters to steer away to execute the pass
        public float overtakeCommitTime      = 3f;   // Seconds to hold the chosen passing side
        public float minFollowingDistance    = 25f;  // Minimum gap to bike ahead before forcing overtake

        [Header("Race Start Launch")]
        public float launchStraightTime      = 4f;   // Seconds after lights out to hold centre line and full throttle

        [Header("Corner Spreading")]
        public float cornerSpreadDistance    = 40f;  // How far ahead to detect corners for spreading
        public float cornerSpreadStrength    = 5f;   // Lateral spread amount on corners
        public float cornerSpreadThreshold   = 8f;   // Minimum corner angle (degrees) to trigger spreading

        [Header("Defensive Racing")]
        // Nudge laterally to cover a rival (especially the player) coming up behind, to defend the line
        public bool  enableDefensiveRacing = true;
        public float defensiveStrength     = 2.5f;  // Max lateral cover offset (m)
        public float defensiveRange        = 18f;   // How close behind a rival must be to trigger

        [Header("Tactical Slipstream")]
        // Tuck in behind a rival to build draft boost, then slingshot out to pass
        public bool  enableTacticalSlipstream = true;
        public float slipstreamBuildTime       = 1.2f;  // Seconds tucked in the wake before the pass

        [Header("Combat Steering")]
        public float combatLateralStrength   = 3f;
        public AICombatPositioning combatPositioning = new AICombatPositioning();

        [Header("AI Drift")]
        public bool  enableAIDrift      = true;
        public float aiDriftCornerAngle = 20f;   // Min upcoming corner angle (deg) before the AI drifts
        public float aiDriftMinSteer    = 0.45f; // Min |steerInput| (committed to the corner) to drift
        public float aiDriftMinSpeedKmh = 70f;   // Don't drift below this speed

        [Header("Rubber Band Catchup")]
        public bool  enableRubberBand      = true;
        public float rubberBandMaxBoost    = 25f;   // Max extra cruise speed in km/h when behind leader
        public float rubberBandGapFull     = 0.15f; // TotalProgress gap (laps + splineT) for full boost

        // Two-directional band, ease off (negative boost) when this AI runs too far AHEAD of the player,
        // so a leading AI stays catchable and races feel close. Banded to the player specifically
        public float rubberBandPlayerEaseMax = 15f;   // Max cruise speed reduction (km/h) when far ahead of player
        public float rubberBandPlayerEaseGap = 0.12f; // Progress gap ahead of the player for full ease off

        // Pace matching: a trailing AI's top speed is pulled up to the leading human's CURRENT speed plus
        // this fraction, so the band answers how fast the player is actually going instead of adding a
        // fixed km/h bonus to the AI's own authored top speed
        public float rubberBandChaseMargin   = 0.15f;
        public float rubberBandMaxMultiplier = 1.45f; // Ceiling on the band (x base top speed)
        public float rubberBandMinMultiplier = 0.85f; // Floor on the band (x base top speed)
        // Past rubberBandGapFull behind the human the band keeps growing, reaching these at rubberBandFarGap,
        // and the AI may then run above the boost ceiling, off screen, to make the gap up
        public float rubberBandFarGap           = 0.3f;
        // Seconds the band takes to reach full strength once the launch ends. Switching it on in one step,
        // in the same instant as boost, read as the AI suddenly out accelerating the player
        public float rubberBandRampIn           = 3f;
        public float rubberBandFarMaxMultiplier = 1.8f;
        public float rubberBandFarCeilingBonus  = 0.2f;
        public float FarBehind01 { get; private set; }
        public float CatchUpCeilingMultiplier => 1f + rubberBandFarCeilingBonus * FarBehind01;

        // Pace matching converges on the human's speed, so an AI a few metres back at the same speed
        // never actually arrives in combat range. This is what lets it close the last stretch
        public bool  enableCombatCloseIn = true;
        public float combatCloseInRange  = 30f;   // Metres from the human before closing starts
        public float combatCloseInKmh    = 12f;   // Top speed granted over the human's while closing

        // The chase half of the band pulls the AI up to the human's speed, but nothing held it there once
        // it got past them, so a leading AI simply drove away and the race was over. This caps a leading
        // AI to the human's own pace, and caps it harder still while it is hunting them for a fight
        public bool  enablePaceCap     = true;
        public float paceCapMargin     = 0.03f;  // Fraction over the human's speed a leading AI may run
        public float paceCapHuntMargin = -0.04f; // Same, while this AI has the human as its combat target
        // Doubles as the band's activity floor: below this the human is launching or crashed, and neither
        // chasing their pace nor capping to it describes a race
        public float paceCapFloorKmh   = 170f;

        [Header("Corner Hints")]
        // How far past a hinted apex the authored speed keeps applying, so the geometry scan can't drag
        // the AI below it again halfway through the corner
        public float cornerHintHoldDistance = 30f;

        [Header("Crash Recovery")]
        public float recoveryDuration        = 5f;
        public float recoveryCruiseBoostKmh  = 40f;

        [Header("Racing Personality")]
        public float personalityStrength     = 1f;

        [Header("Debug")]
        public bool  debugLogThrottle        = false;

        public float DodgeUrgency { get; private set; }

        // True while the launch phase is still holding the bike straight off the line. One source of
        // truth for "not racing yet", the same _launchTimer that suppresses wander, overtake and the
        // combat steering bias already
        public bool IsLaunching => _launchTimer > 0f;
        [HideInInspector] public Transform playerTransform;

        // Set by RampLauncher when the AI enters a ramp trigger, cleared on exit
        // While isOnRamp is true, Tick() aims directly at this point so the AI launches
        // on the correct trajectory toward the landing zone
        [HideInInspector] public Transform rampLandingTarget;
        [HideInInspector] public HashSet<Rigidbody> excludedBodies;
        [HideInInspector] public HashSet<Rigidbody> racerBodies;
        [HideInInspector] public int minLaneIndex = 0;
        [HideInInspector] public int maxLaneIndex = 5;
        [HideInInspector] public float lateralBuffer = 0.7f;
        [HideInInspector] public float bikeHalfWidth = 0.45f;
        [HideInInspector] public float dodgeFraction = 0.55f;
        [HideInInspector] public float biasSpeed = 18f;
        [HideInInspector] public float commitDuration = 0.5f;
        [HideInInspector] public float lanePreference = 0f;
        [HideInInspector] public float racingLineStrength = 0.75f;
        [HideInInspector] public float racingLineSmoothTime = 0.6f;
        [HideInInspector] public float cornerAngle = 15f;

        // Progress around the track (0-1), used by RaceManager for lap detection and standings
        public float SplineT
        {
            get
            {
                if (_wpPositions == null || _waypointCount == 0) return 0f;
                if (routeGraph != null)
                    return routeGraph.GetProgress(_currentSegmentIndex, _currentWp, _segmentProgress);
                return Mathf.Repeat((_currentWp + _segmentProgress) / _waypointCount, 1f);
            }
        }

        // Waypoint/segment data
        // In legacy mode _wpPositions has N pts, _segLengths has N legs (incl. loop-closer), _waypointCount = N
        // In graph mode _wpPositions has N pts, _segLengths has N-1 legs, _waypointCount = N-1
        private Vector3[] _wpPositions;
        private float[]   _segLengths;
        private int       _waypointCount;
        private float     _totalPathLength;

        // Tracking state
        private int   _currentWp;
        private float _segmentProgress;

        // Graph mode tracking
        private int _currentSegmentIndex  = -1;
        private int _committedNextSegment = -1; // The next segment the AI has committed to take

        // Per frame computed
        private Vector3 _nearPos;
        private Vector3 _nearTan;
        private Vector3 _nearLeft;

        // Traffic avoidance
        private float _dodgeBias;
        private readonly AITrafficPlanner _planner = new AITrafficPlanner();
        private System.Func<float, AITrafficPlanner.RouteFrame> _plannerRoute;
        private float _planTimer, _plannedIntent, _planMinLat, _planMaxLat, _traceTimer;
        private float _launchEndedAt;
        private float _dodgeBiasVel;
        private int   _trafficCheckCounter;
        private float _cachedDodge;
        private bool  _trafficBrake;
        private bool  _boxedIn;        // Both dodge sides blocked, must brake, can't swerve
        private int   _dbgCounter;     // Throttles the debug log

        // Reusable buffer for the path-ahead (blind-corner) traffic scan
        private static readonly Collider[] s_pathTrafficBuffer = new Collider[16];
        private float _closingSpeed;

        // Lane commitment AI picks a clear side and holds it to prevent oscillation
        private float _committedDodgeSide = 0f;   // -1 = left, 0 = none, +1 = right
        private float _dodgeCommitTimer   = 0f;
        private const float DODGE_COMMIT_DURATION          = 4.0f;
        private const float DODGE_COMMIT_EMERGENCY_URGENCY = 0.6f;

        // Overtake throttle modifier set by ComputeOvertakeBias(), applied after throttle is assigned
        private float _overtakeThrottleScale = 1f;

        // Reusable buffers for SelectNextSegment, avoids per junction List allocation
        private int[]   _exitBuffer;
        private float[] _exitWeightBuffer;

        // Lateral wander (natural road-width usage)
        private float _lateralWander;
        private float _wanderTarget;
        private float _wanderVel;
        private float _wanderTimer;

        // Overtaking state
        private float _overtakeBias;
        private float _overtakeBiasVel;
        private float _overtakeSide;      // -1 = pass left, +1 = pass right, 0 = uncommitted
        private float _overtakeHoldTimer; // Seconds remaining before releasing committed side
        private bool  _forcedOvertake;    // True when following distance is too small
        private float _closestBikeDist;   // Distance to nearest bike ahead
        public bool IsOvertaking { get; private set; }
    
        // Corner spreading state
        private float _cornerSpreadBias;
        private float _cornerSpreadBiasVel;

        // Combat steering state
        private CombatAI _combatAI;
        private float _combatBias;
        private float _combatBiasVel;

        // Defensive racing state
        private float _defensiveBias;
        private float _defensiveBiasVel;

        // Tactical slipstream state
        private SlipstreamSystem _slipstream;
        private float _draftBuildTimer;

        // Urgency smooth decay, snaps up on new threats, fades gradually when cleared
        private float _urgencyVel;

        // Rubber band catchup, modulates the AI's effective TOP SPEED + ACCELERATION by the race gap
        private float _rubberBandMult = 1f;   // 1 = neutral, >1 = catch up (faster), <1 = ease off
        private int   _rubberBandCounter;
        private const int RUBBER_BAND_INTERVAL = 30;
        private Transform _bandHumanTf;                        // Human the band is currently reading speed from
        private BikeController _bandHumanBike;        // Its controller, resolved once per human
        private float _baseMaxSpeed;          // Controller's original top speed, captured in Start
        private float _baseAccel;             // Controller's original acceleration, captured in Start
        private float _rbMaxSpeedVel;         // SmoothDamp ref for the no boost system maxSpeed path
        private float _lastLateralDisp;       // Slew limiter state for the summed lateral aim offset

        // Crash recovery window (set by BeginRecovery, decremented in Tick)
        private float _recoveryTimer;

        // Per bike racing personality (seeded in Awake)
        private float _cornerCaution;       // 0..1, how much this bike eases corner entry
        private float _spawnSpeedOffset;    // Per bike cruise variance, preserved across difficulty re tuning
        private float _spawnLaneT;          // Per bike lane assignment seed (0..1)
        private bool  _laneFromSpawn;       // True if lanePreference was auto assigned (not externally overridden)

        // Sibling boost system (resolved in Start) recovery auto-spend + difficulty feel
        private AIBoostSystem _boost;
        private float _upcomingCornerAngle;

        private float[]   _scanDists;
        private int       _bestProbe;

        // Cached own Rigidbody for O(1) self exclusion in traffic physics queries
        private Rigidbody _ownRb;

        // Per vehicle VehicleFollowing cache, avoids GetComponentInParent on every sphere cast hit
        private readonly Dictionary<Transform, VehicleFollowing> _vehicleFollowingCache
            = new Dictionary<Transform, VehicleFollowing>();

        // Context steering state. Two maps over lateral slots, interest (prefer minimal swerve) minus
        // danger (predicted traffic occupancy). Pre allocated, sized lazily to (odd) contextSlots
        private float[] _ctxInterest;
        private float[] _ctxDanger;
        private float[] _ctxSlotLat;
        // Frame and age of the built map, so RouteClearance can read the same lateral profile the dodge
        // was picked from rather than re-scanning. Slots are indexed along the bike's right, not _nearLeft
        private Vector3 _ctxRight;
        private float   _ctxBuiltTime = -1f;
        private const float CTX_MAP_MAX_AGE = 0.2f;    // The 3-step scan throttle plus a step of slack
        private static readonly Collider[] s_ctxBuffer = new Collider[32];
        // Per vehicle SplineMover cache for velocity lookup (includes lateral lane-change motion)
        private readonly Dictionary<Transform, SplineMover> _splineMoverCache
            = new Dictionary<Transform, SplineMover>();

        // Pre allocated wall ray buffers avoids heap allocation inside ComputeWallAvoidance every FixedUpdate
        private readonly Vector3[] _wallDirs   = new Vector3[8];
        private static readonly float[] _wallSigns      = { -1f, -1f, -1f, -1f,  1f,  1f,  1f,  1f };
        private static readonly float[] _wallRangeMults = { 1.0f, 1.5f, 2.0f, 2.5f, 1.0f, 1.5f, 2.0f, 2.5f };

        // Wall avoidance throttle, 8 raycasts per AI per step, cached across frames
        private int   _wallAvoidCounter;
        private float _cachedWallCorrection;
        private const int WALL_AVOID_INTERVAL = 2;

        // Corner spread throttle,I racer scan runs every N steps, bias interpolates every frame
        private Vector3   _cornerDirectionAhead;
        private int       _cornerSpreadCounter;
        private float     _cachedCornerSpreadBias;
        private const int CORNER_SPREAD_INTERVAL = 5;

        // Corner brake throttle, 80 m lookahead is the costliest query, safe speed changes slowly
        private int   _cornerBrakeCounter;
        private float _cachedSafeCornerSpeedMs;
        private float _cachedCornerDist = float.MaxValue;   // Distance to the corner that set the safe speed
        private bool  _cornerSpeedIsAuthored;               // True while a route-graph hint owns the corner speed
        private const int CORNER_BRAKE_INTERVAL = 3;

        // Resync
        private int _resyncCounter;
        private const int RESYNC_INTERVAL = 60;

        // Race start launch phase
        private float _launchTimer  = 0f;
        private bool  _wasRevving   = false;

        // Speed distribution
        private static int _spawnSequence = 0;
        private static int _liveInstances = 0;   // live BikeAILogic count, to reset the sequence per race
        private        int _spawnIndex    = 0;
        private        float _defaultDodgeSide = 1f; // Randomised per bike in Awake

        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSpawnSequence() { _spawnSequence = 0; _liveInstances = 0; }

    #region Unity Methods

        private void Awake()
        {

            if (routeGraph == null)        routeGraph        = FindFirstObjectByType<RouteGraph>();
            if (laneNetwork == null)   laneNetwork   = FindFirstObjectByType<TrafficLaneNetwork>();
            if (waypointPath == null) waypointPath = FindFirstObjectByType<WaypointPath>();

            // Every traffic and wall routine early-outs on an empty mask, so a mask left at Nothing turns
            // the whole behaviour off while its enable toggle still reads true in the Inspector.
            if (enableTrafficAvoidance && trafficLayerMask == 0)
                Debug.LogWarning($"[BikeAILogic] '{name}' has Traffic Layer Mask set to Nothing. " +
                                 "Traffic avoidance, dodging and overtaking are all inert.", this);
            if (barrierLayerMask == 0)
                Debug.LogWarning($"[BikeAILogic] '{name}' has Barrier Layer Mask set to Nothing. " +
                                 "Wall avoidance is inert and this AI will not see the track edge.", this);

            _liveInstances++;
            int   idx = _spawnSequence++;
            _spawnIndex = idx;
            float t   = idx <= 0 ? 0f : (float)(idx % 4) / 3f;

            // Per bike cruise variance, stored so ApplyDifficultyPreset can re base cruise speed
            // without losing this bike's individual spread
            _spawnSpeedOffset = clinicalRacing
                ? 0f
                : Mathf.Lerp(-speedVariance, speedVariance, t) + Random.Range(-5f, 5f);
            cruiseSpeedKmh += _spawnSpeedOffset;

            // Corner entry personality: 0 = takes corners at the shared safe limit, 1 = most cautious
            _cornerCaution = clinicalRacing ? 0f : Random.value;

            _defaultDodgeSide = Random.value > 0.5f ? 1f : -1f;

            _wanderTarget  = clinicalRacing ? 0f : Random.Range(-1.2f, 1.2f);
            _lateralWander = _wanderTarget;
            _wanderTimer   = Random.Range(2f, 5f);

            if (lanePreference == 0f && !clinicalRacing)
            {
                _laneFromSpawn = true;
                _spawnLaneT    = (float)(idx % 6) / 5f; // Cycle through 6 positions
                lanePreference = Mathf.Lerp(-lanePreferenceVariance * 0.5f, lanePreferenceVariance * 0.5f, _spawnLaneT)
                               + Random.Range(-0.5f, 0.5f);
            }
        }

        private void OnDestroy()
        {

            if (--_liveInstances <= 0)
            {
                _liveInstances = 0;
                _spawnSequence = 0;
            }
        }

        public void ApplyDifficultyPreset(AIDifficulty difficulty)
        {
            // Per-difficulty tuning bundle.
            float baseCruise, spreadMult, laneVar, rbBoost, rbChase, otOffset, otFollow, panic, personality;
            // boostAccel is m/s², not Newtons: AIBoostSystem applies it with ForceMode.Acceleration,
            // exactly as the player's BoostSystem applies its own 5. Keep these within a small multiple of
            // that 5, the old 150/250/400 read as Newtons and pinned the AI to maxBoostSpeedKmh instantly
            float boostMult, boostAccel, nmAward, nmCooldown;

            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    reactTime = 7.0f; fullUrgencyDistance = 20f; dodgeStrength = 6.0f;
                    detectionDistance = 220f; sphereCastRadius = 0.9f; panic = 0.4f;
                    baseCruise = 400f; spreadMult = 1.3f; laneVar = 4.0f; rbBoost = 20f; rbChase = 0.05f;
                    otOffset = 5f; otFollow = 28f; personality = 0.6f;
                    boostMult = 1.35f; boostAccel =  5f; nmAward = 0.20f; nmCooldown = 3.0f;
                    break;
                case AIDifficulty.Hard:
                    reactTime = 2.5f; fullUrgencyDistance = 14f; dodgeStrength = 9.5f;
                    detectionDistance = 360f; sphereCastRadius = 1.3f; panic = 0.5f;
                    baseCruise = 440f; spreadMult = 1.0f; laneVar = 3.0f; rbBoost = 45f; rbChase = 0.25f;
                    otOffset = 7f; otFollow = 22f; personality = 1.0f;
                    boostMult = 1.60f; boostAccel = 12f; nmAward = 0.30f; nmCooldown = 2.5f;
                    break;
                default: // Normal
                    reactTime = 4.5f; fullUrgencyDistance = 16f; dodgeStrength = 8.0f;
                    detectionDistance = 300f; sphereCastRadius = 1.0f; panic = 0.4f;
                    baseCruise = 420f; spreadMult = 1.1f; laneVar = 3.0f; rbBoost = 40f; rbChase = 0.22f;
                    otOffset = 6f; otFollow = 25f; personality = 1.0f;
                    boostMult = 1.50f; boostAccel =  8f; nmAward = 0.25f; nmCooldown = 3.0f;
                    break;
            }

            panicSeconds          = panic;
            rubberBandMaxBoost     = rbBoost;
            rubberBandChaseMargin  = rbChase;
            // Progress spans the whole ~13 km route, so the prefab's 0.15 only reached full chase ~1.9 km back:
            // at a 200 m gap the AI ran ~1.5% faster than the player and never won back a mistake
            rubberBandGapFull      = 0.025f;
            rubberBandFarGap       = 0.1f;
            overtakeLateralOffset  = otOffset;
            minFollowingDistance   = otFollow;
            personalityStrength    = clinicalRacing ? 1f : personality;
            lanePreferenceVariance = laneVar;

            // Re base cruise speed on the difficulty while preserving this bike's individual spread
            cruiseSpeedKmh = baseCruise + _spawnSpeedOffset * spreadMult;

            // Re derive auto assigned lane with the difficulty specific width (skip if externally overridden)
            if (_laneFromSpawn)
                lanePreference = clinicalRacing ? 0f : Mathf.Lerp(-laneVar * 0.5f, laneVar * 0.5f, _spawnLaneT);

            // Push boost feel to the sibling boost system
            ResolveBoost();
            if (_boost != null)
                _boost.ConfigureForDifficulty(boostMult, boostAccel, nmAward, nmCooldown);
        }

        // Resolve the sibling boost system robustly, regardless of Start() ordering (controller may
        // not be wired yet when difficulty is applied at spawn)
        private void ResolveBoost()
        {
            if (_boost != null) return;
            if (controller != null)
                _boost = controller.GetComponentInChildren<AIBoostSystem>(true);
            if (_boost == null)
                _boost = GetComponentInParent<AIBoostSystem>()
                      ?? GetComponentInChildren<AIBoostSystem>(true);
        }
        public void BeginRecovery()
        {
            _recoveryTimer = recoveryDuration;
            ResolveBoost();
            _boost?.BeginRecovery(recoveryDuration);
        }

        // Speed scaled panic (max-urgency) distance. At higher/boosted speeds a fixed distance gives
        // too little reaction time, so widen it to cover panicSeconds of travel, keeps dodging reliable
        private float PanicDistance()
        {
            float speedMs = controller != null ? controller.localBikeVelocity.magnitude : 0f;
            return Mathf.Max(fullUrgencyDistance, speedMs * panicSeconds);
        }

        private void Start()
        {
            TryInitializeWaypoints();

            // Pre allocate junction exit buffers to avoid first-use heap allocation at runtime
            _exitBuffer       = new int[8];
            _exitWeightBuffer = new float[8];
            _scanDists        = new float[Mathf.Max(scanProbes, 3)];

            if (controller != null)
            {
                _ownRb = controller.bikeReferences.BikeRb;
                _boost = controller.GetComponentInChildren<AIBoostSystem>(true);
                // Capture the un-rubber-banded baselines so the band can scale relative to them.
                _baseMaxSpeed = controller.bikeSettings.maxSpeed;
                _baseAccel    = controller.bikeSettings.acceleration;
            }

            _cachedSafeCornerSpeedMs = cruiseSpeedKmh / 3.6f;
            _combatAI = GetComponentInChildren<CombatAI>();
            _slipstream = GetComponentInChildren<SlipstreamSystem>(true);
        }

        private bool TryInitializeWaypoints()
        {
            if (_wpPositions != null) return true;

            // Graph mode
            if (routeGraph != null)
                return TryInitializeFromGraph();

            // Legacy mode
            if (waypointPath == null) return false;

            var waypoints = waypointPath.GetWaypoints(roadIndex);
            if (waypoints == null || waypoints.Length < 2) return false;

            _waypointCount = waypoints.Length;
            _wpPositions   = new Vector3[_waypointCount];

            for (int i = 0; i < _waypointCount; i++)
                _wpPositions[i] = waypoints[i].position;

            // Auto reverse if waypoints run opposite to the bike's facing direction
            if (bikeTransform != null)
            {
                int mid     = _waypointCount / 2;
                int midNext = (mid + 1) % _waypointCount;
                Vector3 midDir = (_wpPositions[midNext] - _wpPositions[mid]).normalized;

                float bestDist = float.MaxValue;
                int closestIdx = 0;
                for (int i = 0; i < _waypointCount; i++)
                {
                    float d = (bikeTransform.position - _wpPositions[i]).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; closestIdx = i; }
                }
                int closestNext = (closestIdx + 1) % _waypointCount;
                Vector3 localDir = (_wpPositions[closestNext] - _wpPositions[closestIdx]).normalized;

                if (Vector3.Dot(localDir, bikeTransform.forward) < 0f)
                {
                    System.Array.Reverse(_wpPositions);
                    Debug.Log("[BikeAILogic] Waypoints were reversed to match race direction.");
                }
            }

            // Segment lengths (includes loop closer so _segLengths.Length == _waypointCount)
            _segLengths = new float[_waypointCount];
            _totalPathLength = 0f;
            for (int i = 0; i < _waypointCount; i++)
            {
                int next = (i + 1) % _waypointCount;
                _segLengths[i] = Mathf.Max(Vector3.Distance(_wpPositions[i], _wpPositions[next]), 0.01f);
                _totalPathLength += _segLengths[i];
            }

            if (bikeTransform != null)
                FindNearestWaypoint(bikeTransform.position);

            return true;
        }

        // Graph mode initialisation

        private bool TryInitializeFromGraph()
        {
            if (routeGraph == null) return false;
            if (!routeGraph.IsBaked) routeGraph.Bake();
            _currentSegmentIndex  = routeGraph.startSegmentIndex;
            _committedNextSegment = -1;
            if (!LoadSegmentFromGraph(_currentSegmentIndex)) return false;
            if (bikeTransform != null)
                FindNearestWaypoint(bikeTransform.position);
            return true;
        }

        private bool LoadSegmentFromGraph(int segIdx)
        {
            if (routeGraph == null || !routeGraph.IsBaked) return false;
            if (segIdx < 0 || segIdx >= routeGraph.BakedSegments.Length) return false;

            var b = routeGraph.BakedSegments[segIdx];
            if (b.positions == null || b.positions.Length < 2) return false;

            // In graph mode _waypointCount = number of legs = positions.Length - 1
            // _wpPositions has positions.Length elements so _wpPositions[_waypointCount] is always the endpoint
            _wpPositions     = b.positions;
            _segLengths      = b.legLengths;
            _waypointCount   = b.positions.Length - 1;
            _totalPathLength = b.totalLength;
            return true;
        }

        private void FindNearestWaypoint(Vector3 worldPos)
        {
            float bestScore = float.MinValue;
            _currentWp = 0;
            _segmentProgress = 0f;

            for (int i = 0; i < _waypointCount; i++)
            {
                int next = NextWp(i);
                Vector3 segDir = (_wpPositions[next] - _wpPositions[i]).normalized;
                float dist  = Vector3.Distance(worldPos, _wpPositions[i]);
                float align = bikeTransform != null ? Vector3.Dot(segDir, bikeTransform.forward) : 0f;
                float score = align * 10f - dist;
                if (score > bestScore) { bestScore = score; _currentWp = i; }
            }

            int nextWp = NextWp(_currentWp);
            Vector3 seg = _wpPositions[nextWp] - _wpPositions[_currentWp];
            float segLenSq = seg.sqrMagnitude;
            _segmentProgress = segLenSq > 0.001f
                ? Mathf.Clamp01(Vector3.Dot(worldPos - _wpPositions[_currentWp], seg) / segLenSq)
                : 0f;
        }

        public void OnPhysicsUpdated()
        {
            if (controller == null) return;
            if (!TryInitializeWaypoints()) return;
            Tick(controller.localBikeVelocity.z);
        }

        public void ResetTracking()
        {
            _lastLateralDisp = 0f;

            bool wasOnTriggerSeg = routeGraph != null
                && _currentSegmentIndex >= 0
                && _currentSegmentIndex < routeGraph.segments.Length
                && routeGraph.segments[_currentSegmentIndex].requiresTrigger;

            _wpPositions = null;

            if (routeGraph != null)
            {
                if (!routeGraph.IsBaked) routeGraph.Bake();

                if (bikeTransform != null)
                    routeGraph.FindNearestProgress(bikeTransform.position,
                        out _currentSegmentIndex, out _, out _,
                        avoidTriggerOnly: !wasOnTriggerSeg);
                else
                    _currentSegmentIndex = routeGraph.startSegmentIndex;

                _committedNextSegment = -1;
                if (!LoadSegmentFromGraph(_currentSegmentIndex)) return;
            }
            else
            {
                if (waypointPath == null || bikeTransform == null) return;
                if (!TryInitializeWaypoints()) return;
            }

            if (bikeTransform != null)
                FindNearestWaypoint(bikeTransform.position);
        }

        // Returns a respawn position and forward direction advanced by advanceDistance
        // along the AI's current route. Returns false if waypoints are not initialised
        public bool GetRespawnPoint(float advanceDistance, out Vector3 position, out Vector3 forward)
        {
            position = bikeTransform != null ? bikeTransform.position : Vector3.zero;
            forward  = bikeTransform != null ? bikeTransform.forward  : Vector3.forward;

            if (_wpPositions == null || _waypointCount < 2) return false;

            var (_, _, pos) = GetPointAtDistance(advanceDistance);
            position = pos;

            int nxt = NextWp(_currentWp);
            Vector3 dir = _wpPositions[nxt] - _wpPositions[_currentWp];
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) forward = dir.normalized;
            return true;
        }

        public float HorizontalDistanceFromPath()
        {
            if (bikeTransform == null || _wpPositions == null || _waypointCount < 2) return 0f;
            Vector3 d = bikeTransform.position - _nearPos;
            d.y = 0f;
            return d.magnitude;
        }

    #endregion

    #region Waypoint Helpers

        private int NextWp(int wp)
        {
            if (routeGraph != null)
                return wp + 1; // Valid because _wpPositions.Length == _waypointCount + 1 in graph mode
            return (wp + 1) % _waypointCount;
        }

        private Vector3 GetSegmentDir(int wp)
        {
            // Clamp so look ahead values that crossed a segment boundary don't go out of range
            int clamped = Mathf.Clamp(wp, 0, _waypointCount - 1);
            Vector3 dir = _wpPositions[NextWp(clamped)] - _wpPositions[clamped];
            dir.y = 0f;
            return dir.normalized;
        }

        private (int wp, float frac, Vector3 pos) GetPointAtDistance(float distance)
        {
            if (routeGraph != null)
                return GetPointAtDistanceGraph(distance);

            // Legacy flat loop implementation fallback
            int wp = _currentWp;
            float remaining = distance;

            float currentSegRemaining = _segLengths[wp] * (1f - _segmentProgress);
            if (remaining <= currentSegRemaining)
            {
                float frac = _segmentProgress + remaining / Mathf.Max(_segLengths[wp], 0.001f);
                Vector3 pos = Vector3.Lerp(_wpPositions[wp], _wpPositions[NextWp(wp)], frac);
                return (wp, frac, pos);
            }

            remaining -= currentSegRemaining;
            wp = NextWp(wp);

            for (int safety = 0; safety < _waypointCount; safety++)
            {
                float segLen = _segLengths[wp];
                if (remaining <= segLen)
                {
                    float frac = remaining / Mathf.Max(segLen, 0.001f);
                    Vector3 pos = Vector3.Lerp(_wpPositions[wp], _wpPositions[NextWp(wp)], frac);
                    return (wp, frac, pos);
                }
                remaining -= segLen;
                wp = NextWp(wp);
            }

            return (wp, 0f, _wpPositions[wp]);
        }

        // Graph mode look ahead, crosses segment boundaries using the committed next segment
        private (int wp, float frac, Vector3 pos) GetPointAtDistanceGraph(float distance)
        {
            Vector3[] pts   = _wpPositions;
            float[]   lens  = _segLengths;
            int       count = _waypointCount; // Number of legs in this pts array
            int       activeSeg = _currentSegmentIndex;
            int       wp    = _currentWp;
            float     remaining = distance;

            // Consume remainder of the current leg
            float legLen = lens[wp];
            float legRem = legLen * (1f - _segmentProgress);
            if (remaining <= legRem)
            {
                float f = _segmentProgress + remaining / Mathf.Max(legLen, 0.001f);
                return (wp, f, Vector3.Lerp(pts[wp], pts[wp + 1], f));
            }
            remaining -= legRem;
            wp++;

            const int MAX_ITERS = 300;
            for (int iter = 0; iter < MAX_ITERS && remaining > 0.001f; iter++)
            {
                if (wp >= count)
                {
                    // Crossed end of this segment step into the next one
                    int nextSeg = (activeSeg == _currentSegmentIndex && _committedNextSegment >= 0)
                        ? _committedNextSegment
                        : PickFirstExit(activeSeg);

                    if (nextSeg < 0 || nextSeg == activeSeg) break; // Dead end or self loop

                    var nb = routeGraph.BakedSegments[nextSeg];
                    if (nb.positions == null || nb.positions.Length < 2) break;

                    activeSeg = nextSeg;
                    pts   = nb.positions;
                    lens  = nb.legLengths;
                    count = pts.Length - 1;
                    wp    = 0;
                }

                legLen = lens[wp];
                if (remaining <= legLen)
                {
                    float f = remaining / Mathf.Max(legLen, 0.001f);
                    return (wp, f, Vector3.Lerp(pts[wp], pts[wp + 1], f));
                }
                remaining -= legLen;
                wp++;
            }

            // Fallback, endpoint of wherever we ended up
            return (Mathf.Max(0, count - 1), 1f, pts[pts.Length - 1]);
        }

        private int PickFirstExit(int segIdx)
        {
            int[] exits = routeGraph.GetExits(segIdx);
            int fallback = -1;
            foreach (int e in exits)
            {
                if (e < 0 || e >= routeGraph.BakedSegments.Length) continue;
                var b = routeGraph.BakedSegments[e];
                if (b.positions == null || b.positions.Length < 2) continue;
                if (fallback < 0) fallback = e;

                // Prefer the first non trigger only exit for look-ahead so the AI doesn't
                // aim at jump shortcut waypoints before it has physically hit the trigger
                if (e < routeGraph.segments.Length && routeGraph.segments[e].requiresTrigger) continue;

                // Also skip shortcuts in look ahead when the debug toggle is off
                if (e < routeGraph.segments.Length && routeGraph.segments[e].isShortcut
                    && !routeGraph.aiShortcutsEnabled) continue;
                return e;
            }
            return fallback;
        }

        private void AdvanceWaypoint(float fwdSpeed)
        {
            float distToTravel = Mathf.Max(0f, fwdSpeed) * Time.fixedDeltaTime;

            for (int safety = 0; safety < _waypointCount && distToTravel > 0.001f; safety++)
            {
                float segLen  = _segLengths[_currentWp];
                float remaining = segLen * (1f - _segmentProgress);

                if (distToTravel < remaining)
                {
                    _segmentProgress += distToTravel / Mathf.Max(segLen, 0.001f);
                    break;
                }

                distToTravel -= remaining;

                int next = NextWp(_currentWp);

                // Graph mode, detect segment end before advancing
                if (routeGraph != null && next >= _waypointCount)
                {
                    TransitionToNextSegment();
                    break;
                }

                _currentWp = next;
                _segmentProgress = 0f;
            }
        }

        // Graph mode segment transitions

        private void TransitionToNextSegment()
        {
            // Hold at the end until the bike is really there; ResyncForward walks it back if it ran ahead
            if (bikeTransform != null)
            {
                Vector3 end = _wpPositions[_waypointCount];
                Vector3 toEnd = end - bikeTransform.position;
                toEnd.y = 0f;
                if (toEnd.sqrMagnitude > 40f * 40f)
                {
                    _currentWp       = _waypointCount - 1;
                    _segmentProgress = 1f;
                    return;
                }
            }

            int next = _committedNextSegment >= 0
                ? _committedNextSegment
                : SelectNextSegment(_currentSegmentIndex);

            if (next < 0)
            {
                // No exits clamp at segment end
                _currentWp       = _waypointCount - 1;
                _segmentProgress = 1f;
                return;
            }

            _currentSegmentIndex  = next;
            _committedNextSegment = -1;
            LoadSegmentFromGraph(next);

            // The next segment need not start where this one ended, so enter it where the bike actually is
            if (bikeTransform != null)
            {
                FindNearestWaypoint(bikeTransform.position);
                Vector3 onRoute = Vector3.Lerp(_wpPositions[_currentWp], _wpPositions[NextWp(_currentWp)], _segmentProgress);
                float gap = Vector3.Distance(onRoute, bikeTransform.position);
                if (gap > 40f)
                    Debug.LogWarning($"[BikeAILogic] {name} entered route segment {next} {gap:F0} m from it at " +
                                     $"{bikeTransform.position:F0}. The route graph has a gap here.", this);
            }
            else
            {
                _currentWp       = 0;
                _segmentProgress = 0f;
            }
        }

        public void ForceCommitSegment(int segmentIndex)
        {
            _committedNextSegment = segmentIndex;
        }

        private void MaybeCommitNextSegment()
        {
            if (_committedNextSegment >= 0) return;
            if (routeGraph == null) return;

            // Sum remaining distance in current segment
            float distRemaining = _segLengths[_currentWp] * (1f - _segmentProgress);
            for (int i = _currentWp + 1; i < _waypointCount; i++)
                distRemaining += _segLengths[i];

            bool onShortcutSeg = _currentSegmentIndex < routeGraph.segments.Length
                && (routeGraph.segments[_currentSegmentIndex].isShortcut
                    || routeGraph.segments[_currentSegmentIndex].requiresTrigger);

            if (onShortcutSeg || distRemaining <= nextSegmentCommitDistance)
                _committedNextSegment = SelectNextSegment(_currentSegmentIndex);
        }

        private int SelectNextSegment(int fromSeg)
        {
            if (routeGraph == null) return -1;

            int[] exits = routeGraph.GetExits(fromSeg);
            if (exits == null || exits.Length == 0) return -1;
            if (exits.Length == 1) return exits[0];

            // Grow reusable buffers only when needed avoids a list allocation on every junction crossing
            if (_exitBuffer == null || _exitBuffer.Length < exits.Length)
            {
                _exitBuffer       = new int[exits.Length];
                _exitWeightBuffer = new float[exits.Length];
            }

            // Weighted random between exits, shortcuts weighted by shortcutBias
            float totalW    = 0f;
            int optionCount = 0;
            foreach (int e in exits)
            {
                if (e < 0 || e >= routeGraph.segments.Length) continue;
                var def = routeGraph.segments[e];

                // Trigger-only segments are never chosen here, ForceCommitSegment selects them
                if (def.requiresTrigger) continue;

                // Respect the debug toggle, skip shortcuts when disabled
                if (def.isShortcut && !routeGraph.aiShortcutsEnabled) continue;
                float w = def.isShortcut ? shortcutBias : (1f - shortcutBias);
                w = Mathf.Max(w, 0.01f);
                _exitBuffer[optionCount]       = e;
                _exitWeightBuffer[optionCount] = w;
                optionCount++;
                totalW += w;
            }

            if (optionCount == 0) return exits[0];

            float r   = Random.Range(0f, totalW);
            float cum = 0f;
            for (int i = 0; i < optionCount; i++)
            {
                cum += _exitWeightBuffer[i];
                if (r <= cum) return _exitBuffer[i];
            }
            return _exitBuffer[optionCount - 1];
        }

        // Only searches forward from the current position to avoid snapping backward
        private void ResyncForward()
        {
            if (bikeTransform == null) return;

            Vector3 pos = bikeTransform.position;
            float bestDistSq = float.MaxValue;
            int bestWp = _currentWp;
            float bestFrac = _segmentProgress;

            // Dead reckoning runs ahead of a fast bike on a line smoother than the one it rides, and a
            // forward-only search could never pull it back, so it reached the segment end early and
            // jumped onto the next segment hundreds of metres short of the junction
            for (int offset = -2; offset < 5; offset++)
            {
                int wp = routeGraph != null
                    ? Mathf.Clamp(_currentWp + offset, 0, _waypointCount - 1)
                    : (_currentWp + offset + _waypointCount) % _waypointCount;

                int next = NextWp(wp);
                // In graph mode NextWp(wp) = wp+1, _wpPositions[_waypointCount] is the endpoint valid
                Vector3 seg    = _wpPositions[next] - _wpPositions[wp];
                float segLenSq = seg.sqrMagnitude;
                float frac = segLenSq > 0.001f
                    ? Mathf.Clamp01(Vector3.Dot(pos - _wpPositions[wp], seg) / segLenSq)
                    : 0f;
                Vector3 closest = Vector3.Lerp(_wpPositions[wp], _wpPositions[next], frac);
                float distSq    = (pos - closest).sqrMagnitude;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestWp     = wp;
                    bestFrac   = frac;
                }
            }

            _currentWp       = bestWp;
            _segmentProgress = bestFrac;
        }

    #endregion

    #region Main Tick

        private void Tick(float fwdSpeed)
        {
            bool revving = controller == null || controller.isRevving;

            // Detect lights out, revving just ended, start the straight line launch phase
            if (_wasRevving && !revving && controller != null)
                _launchTimer = launchStraightTime;
            _wasRevving = revving;

            if (revving)
            {
                if (controller != null)
                {
                    controller.steerInput = 0f;
                    controller.throttle   = 0f;
                    controller.isBraking  = false;
                }
                return;
            }

            if (_wpPositions == null || _waypointCount < 2) return;

            // Countdown and compute suppression factor for the launch phase (1 = start, 0 = normal)
            if (_launchTimer > 0f) _launchTimer -= Time.fixedDeltaTime;
            float launchSuppression = Mathf.Clamp01(_launchTimer / launchStraightTime);
            float launchScale = 1f - launchSuppression;

            AdvanceWaypoint(fwdSpeed);

            // In graph mode, decide which branch to take before reaching the junction
            if (routeGraph != null)
                MaybeCommitNextSegment();

            _resyncCounter++;
            if (_resyncCounter >= RESYNC_INTERVAL)
            {
                _resyncCounter = 0;
                ResyncForward();
            }

            // Path reference from current segment (flatten tangent to horizontal for steering)
            int nextWp = NextWp(_currentWp);
            Vector3 segDelta = _wpPositions[nextWp] - _wpPositions[_currentWp];
            segDelta.y = 0f;
            _nearTan = segDelta.normalized;
            _nearLeft = Vector3.Cross(_nearTan, Vector3.up).normalized;
            _nearPos = Vector3.Lerp(_wpPositions[_currentWp], _wpPositions[nextWp], _segmentProgress);

            // Corner spreading, run first so _upcomingCornerAngle is current when traffic check reads it
            // Overtaking is also computed before traffic so both are available for suppression logic below

            // Aim at a point ahead on the path (crosses segment boundaries in graph mode)
            float speedFraction = Mathf.Clamp01(Mathf.Abs(fwdSpeed) / (cruiseSpeedKmh / 3.6f));
            float dynLookAhead  = Mathf.Lerp(lookAheadDistance, lookAheadDistance * 1.8f, speedFraction);

            float effectiveLookAhead = dynLookAhead;
            if (routeGraph != null && _committedNextSegment < 0)
            {
                float distToSegEnd = _segLengths[_currentWp] * (1f - _segmentProgress);
                for (int i = _currentWp + 1; i < _waypointCount; i++)
                    distToSegEnd += _segLengths[i];
                effectiveLookAhead = Mathf.Min(dynLookAhead, distToSegEnd);
            }

            var (_, _, aimPoint) = GetPointAtDistance(effectiveLookAhead);
            Vector3 pathAimPoint = aimPoint; // Save raw path point before lateral offsets

            // Overtaking, steer laterally around a slower racer directly ahead
            float overtakeBias = ComputeOvertakeBias();

            // Defensive, cover the line against a rival closing from behind
            float defensiveBias = clinicalRacing ? 0f : ComputeDefensiveBias();


            {
                _upcomingCornerAngle  = 0f;
                _cornerDirectionAhead = GetSegmentDir(_currentWp);
                const float CORNER_ANGLE_STEP = 10f;
                for (float sd = CORNER_ANGLE_STEP; sd <= cornerSpreadDistance; sd += CORNER_ANGLE_STEP)
                {
                    var (sWp, _, _) = GetPointAtDistance(sd);
                    Vector3 dir = GetSegmentDir(sWp);
                    float   a   = Mathf.Abs(Vector3.SignedAngle(_nearTan, dir, Vector3.up));
                    if (a > _upcomingCornerAngle) { _upcomingCornerAngle = a; _cornerDirectionAhead = dir; }
                }
            }

            // Corner spreading, rescan racers every N frames (slow changing), interpolate bias each frame
            _cornerSpreadCounter++;
            if (_cornerSpreadCounter >= CORNER_SPREAD_INTERVAL)
            {
                _cornerSpreadCounter    = 0;
                _cachedCornerSpreadBias = ComputeCornerSpread(fwdSpeed);
            }
            // Corner fanning and blocking are field-shaping behaviours, not line-finding ones
            float cornerSpreadBias = clinicalRacing ? 0f : _cachedCornerSpreadBias;

            // Traffic avoidance sampled every 3 frames normally, every frame only when
            // urgency is high. The corner angle trigger was removed, it fired every frame
            // for every AI bike in any corner, causing a burst of sphere casts per frame
            float dodgeBias = 0f;
            bool planning = enableTrafficAvoidance && useTrafficPlanner;
            if (enableTrafficAvoidance && trafficLayerMask != 0)
            {
                // Decrement here every fixed frame so the commit duration is wall clock accurate
                // regardless of whether ComputeTrafficDodge runs (which is throttled)
                _dodgeCommitTimer = Mathf.Max(0f, _dodgeCommitTimer - Time.fixedDeltaTime);

                // Always-on single centre probe catches fast-closing threats between full scans
                Vector3 centreOrigin = bikeTransform.position + Vector3.up * 0.8f;
                float   panicDist    = PanicDistance();
                var (centreDist, _) = TrafficSphereCast(centreOrigin, bikeTransform.forward, sphereCastRadius, panicDist);
                if (centreDist < panicDist)
                    DodgeUrgency = 1f;

                _trafficCheckCounter++;
                if (!planning && (_trafficCheckCounter >= 3 || DodgeUrgency > 0.6f))
                {
                    _trafficCheckCounter = 0;
                    _cachedDodge = useContextSteering
                        ? ComputeTrafficDodgeContext(fwdSpeed)
                        : ComputeTrafficDodge(fwdSpeed);
                }
                if (!planning)
                {
                    float smoothTime = Mathf.Lerp(0.03f, 0.4f, 1f - DodgeUrgency);
                    _dodgeBias = Mathf.SmoothDamp(_dodgeBias, _cachedDodge, ref _dodgeBiasVel, smoothTime);
                    dodgeBias = _dodgeBias;
                }
            }

            // Update lateral wander (natural road width usage)
            if (clinicalRacing)
            {
                _lateralWander = 0f;
            }
            else
            {
                _wanderTimer -= Time.fixedDeltaTime;
                if (_wanderTimer <= 0f)
                {
                    _wanderTarget = Random.Range(-1.2f, 1.2f);
                    _wanderTimer  = Random.Range(5f, 10f);
                }
                _lateralWander = Mathf.SmoothDamp(_lateralWander, _wanderTarget, ref _wanderVel, 4.0f);
            }

            // Suppress wander when actively dodging or overtaking
            float overtakeScale = Mathf.Clamp01(1f - Mathf.Abs(_overtakeBias) / Mathf.Max(overtakeLateralOffset, 0.1f));
            float wanderScale = (1f - DodgeUrgency) * overtakeScale;

            // lanePreference is structural lane separation, never suppressed by urgency so bikes
            // dodge from their own lane rather than all converging to centre under heavy traffic.
            // Only the random wander (cosmetic within lane noise) is scaled by wanderScale
            aimPoint += _nearLeft * (lanePreference + _lateralWander * launchScale * wanderScale);

            // Combat steering, nudge toward the engage target when proximity engagement is active,
            // gated on room to move rather than on urgency. Urgency measures only the bike's own line, so
            // it both let the AI close on a target past a car in the next lane (zero urgency, blocked route)
            // and blocked it from closing through a wide gap (high urgency, clear route). RouteGate answers
            // the first; _boxedIn / _trafficBrake answer the second, being the cases that really mean the
            // bike has nowhere to put itself
            float combatBias = 0f;
            bool combatHold = combatPositioning.Tick(
                _combatAI != null && _combatAI.EngageTarget != null && !_boxedIn && !_trafficBrake, Time.deltaTime);
            if (_combatAI != null && _combatAI.EngageTarget != null && !_boxedIn && !_trafficBrake)
            {
                Vector3 toTarget       = _combatAI.EngageTarget.transform.position - bikeTransform.position;
                float   lateralToTarget = Vector3.Dot(toTarget, _nearLeft);
                float   targetCombatBias = Mathf.Clamp(lateralToTarget, -combatLateralStrength, combatLateralStrength);
                _combatBias = Mathf.SmoothDamp(_combatBias, RouteGate(targetCombatBias), ref _combatBiasVel, 0.4f);
            }
            else
            {
                _combatBias = Mathf.SmoothDamp(_combatBias, 0f, ref _combatBiasVel, 0.6f);
            }
            combatBias = _combatBias;

            // Overtake and corner spread are suppressed as traffic urgency rises so dodge can take full effect.
            // Multiplier 3 (was 2) zeroes overtake at urgency 0.33 instead of 0.5, closing the window where
            // an opposing overtake bias partially cancels the dodge at medium urgency
            float racingBiasScale = 1f - Mathf.Clamp01(DodgeUrgency * 3f);

            // If the racing biases NET against the active dodge, kill them all immediately regardless of
            // scale. This tested overtakeBias alone, so a combat or defensive pull toward the player could
            // still cancel a dodge at low urgency, which is exactly where the dodge has not yet ramped up
            float netRacingBias = overtakeBias + cornerSpreadBias + combatBias + defensiveBias;
            if (Mathf.Abs(dodgeBias) > 0.5f && netRacingBias != 0f &&
                Mathf.Sign(netRacingBias) != Mathf.Sign(dodgeBias))
                racingBiasScale = 0f;
            float racingBias = overtakeBias;
            if (singleRacingBias)
            {
                if (Mathf.Abs(cornerSpreadBias) > Mathf.Abs(racingBias)) racingBias = cornerSpreadBias;
            }
            else racingBias += cornerSpreadBias;

            aimPoint += _nearLeft * racingBias * launchScale * racingBiasScale;
            aimPoint += _nearLeft * dodgeBias * launchScale;
            // Combat yields to the dodge only where it CONFLICTS with it. Pulling the same way as the dodge
            // is free - the gap being steered into is where the target is - so scaling it by urgency like the
            // other racing biases stopped the AI fighting in clear air for no safety gain. Opposed, it takes
            // the full racing scale and so is zeroed by the veto above
            float combatScale = combatBias != 0f && dodgeBias != 0f &&
                                Mathf.Sign(combatBias) != Mathf.Sign(dodgeBias)
                ? racingBiasScale
                : 1f;
            aimPoint += _nearLeft * combatBias * launchScale * combatScale;
            // Defensive cover, suppressed under traffic urgency like the other racing biases. Dropped
            // entirely when another racing bias already has a stronger claim, for the same reason.
            if (!singleRacingBias || Mathf.Abs(defensiveBias) > Mathf.Abs(racingBias))
                aimPoint += _nearLeft * defensiveBias * launchScale * racingBiasScale;

            float dodgeScale = planning ? 1f : Mathf.Clamp01(1f - DodgeUrgency * 1.5f);

            // Include the active dodge in the preferred position so path correction assists the dodge
            // rather than pulling the bike back to lane centre while it is trying to avoid traffic
            float dodgeContrib = _dodgeBias * launchScale;

            // lanePreference is already applied to aimPoint directly, exclude it here to avoid double counting
            Vector3 preferredPos = _nearPos + _nearLeft * (_lateralWander * launchScale * wanderScale + dodgeContrib);

            // The planner owns the whole lateral line: what the racing biases want is its preference, and
            // it returns the nearest line to that which stays clear of every vehicle over its horizon
            if (planning)
            {
                float intent = combatHold
                    ? combatPositioning.LateralIntent(bikeTransform.position, _combatAI.EngageTarget.transform.position,
                                                      _nearPos, _nearLeft)
                    : lanePreference + launchScale *
                      (_lateralWander + overtakeBias + cornerSpreadBias + combatBias + defensiveBias);
                RunPlanner(fwdSpeed, intent, effectiveLookAhead);

                aimPoint     = pathAimPoint + _nearLeft * _planner.TargetLateral;
                preferredPos = _nearPos     + _nearLeft * _planner.TargetLateral;
            }
            Vector3 toPath    = preferredPos - bikeTransform.position;
            toPath.y = 0f;
            float lateralError  = toPath.magnitude;
            float dynamicGain   = (crossTrackGain + Mathf.Clamp01(lateralError / 10f) * 0.5f) * dodgeScale;
            aimPoint += toPath * dynamicGain;

            // Clamp total lateral displacement from the raw path point so combined biases
            // (lane + wander + dodge + overtake) cannot push the bike into the barriers
            Vector3 aimDelta  = aimPoint - pathAimPoint;
            float lateralDisp = Vector3.Dot(aimDelta, _nearLeft);
            float clampedDisp = Mathf.Clamp(lateralDisp, -maxLateralAimOffset, maxLateralAimOffset);

            if (maxLateralAimSlew > 0f)
            {
                float slew = maxLateralAimSlew * Mathf.Lerp(1f, 4f, DodgeUrgency);
                clampedDisp = Mathf.MoveTowards(_lastLateralDisp, clampedDisp, slew * Time.fixedDeltaTime);
            }
            _lastLateralDisp = clampedDisp;

            aimPoint         += _nearLeft * (clampedDisp - lateralDisp);

            // While in a ramp trigger with a designated landing target, override the aim point
            // entirely so the AI is steered directly toward the landing zone. All lane/wander/dodge
            // biases are bypassed, the bike needs to go straight to the target, not drift sideways
            if (controller.isOnRamp && rampLandingTarget != null)
                aimPoint = rampLandingTarget.position;

            // Steering
            Vector3 desiredDir = aimPoint - bikeTransform.position;
            desiredDir.y = 0f;
            if (desiredDir.sqrMagnitude < 0.001f) desiredDir = Vector3.forward;
            desiredDir.Normalize();

            Vector3 flatForward = bikeTransform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = desiredDir;
            flatForward.Normalize();

            float headingError = Vector3.SignedAngle(flatForward, desiredDir, Vector3.up);
            _wallAvoidCounter++;
            if (_wallAvoidCounter >= WALL_AVOID_INTERVAL)
            {
                _wallAvoidCounter = 0;
                _cachedWallCorrection = ComputeWallAvoidance(flatForward);
            }
            // If the wall is pushing against an active high urgency traffic dodge, yield to the dodge.
            // The wall still contributes 30% at max urgency, enough to prevent actual contact but
            // not enough to cancel a dodge that needs to clear a vehicle
            float effectiveWallCorrection = _cachedWallCorrection;
            if (DodgeUrgency > 0.3f && Mathf.Abs(_dodgeBias) > 0.5f &&
                Mathf.Abs(_cachedWallCorrection) > 0.1f && Mathf.Abs(headingError) > 0.1f &&
                Mathf.Sign(_cachedWallCorrection) != Mathf.Sign(headingError))
            {
                effectiveWallCorrection *= Mathf.Lerp(1f, 0.3f, (DodgeUrgency - 0.3f) / 0.7f);
            }
            headingError = Mathf.Clamp(headingError + effectiveWallCorrection, -90f, 90f);

            // Steer on where the nose will be pointing, not where it is: subtracting the current yaw rate
            // takes the lock off as the bike comes round instead of after it has overshot.
            float predictedError = headingError;
            if (steerDamping > 0f && controller.bikeReferences.BikeRb != null)
            {
                float yawRateDeg = controller.bikeReferences.BikeRb.angularVelocity.y * Mathf.Rad2Deg;
                predictedError  -= yawRateDeg * steerDamping;
            }

            float rawSteer;
            if (usePurePursuitSteering && controller.Wheelbase > 0.01f && controller.CurrentSteerAngle > 0.01f)
            {
                Vector3 toAim = aimPoint - bikeTransform.position;
                toAim.y = 0f;
                float lookDist = Mathf.Max(toAim.magnitude, 1f);

                float alpha    = predictedError * Mathf.Deg2Rad;
                float deltaDeg = Mathf.Atan2(2f * controller.Wheelbase * Mathf.Sin(alpha), lookDist) * Mathf.Rad2Deg;

                rawSteer = Mathf.Clamp(deltaDeg / controller.CurrentSteerAngle, -1f, 1f);
            }
            else
            {
                rawSteer = Mathf.Clamp(predictedError * yawGain / 20f, -1f, 1f);
            }
            float effectiveSmoothing = Mathf.Lerp(steerSmoothing, steerSmoothing * 4f, DodgeUrgency);
            controller.steerInput = Mathf.MoveTowards(controller.steerInput, rawSteer, effectiveSmoothing * Time.fixedDeltaTime);

            if (planning && plannerTrace)
            {
                _traceTimer -= Time.fixedDeltaTime;
                Vector3 off = bikeTransform.position - _nearPos;
                off.y = 0f;
                float lateralNow = Vector3.Dot(off, _nearLeft);
                if (_traceTimer <= 0f && Mathf.Abs(lateralNow - _planner.TargetLateral) > 2.5f)
                {
                    _traceTimer = 1f;
                    Debug.Log($"[PlannerTrace] {name} at {bikeTransform.position:F0} {Mathf.Abs(fwdSpeed) * 3.6f:F0} km/h | " +
                              $"lateral {lateralNow:F1} target {_planner.TargetLateral:F1} carriageway {_planMinLat:F1}..{_planMaxLat:F1} | " +
                              $"aim wanted {lateralDisp:F1} got {clampedDisp:F1} (slew from {_lastLateralDisp:F1}) | " +
                              $"heading err {headingError - effectiveWallCorrection:F1} wall {effectiveWallCorrection:F1} | " +
                              $"rawSteer {rawSteer:F2} steerInput {controller.steerInput:F2} maxSteer {controller.CurrentSteerAngle:F1} | " +
                              $"blocked {_planner.Blocked} ramp {controller.isOnRamp}", this);
                }
            }


            _cornerBrakeCounter++;
            if (_cornerBrakeCounter >= CORNER_BRAKE_INTERVAL)
            {
                _cornerBrakeCounter = 0;
                float maxAngle     = 0f;

                // Look far enough ahead to actually brake in time at the current speed (braking distance
                // d = v²/2a, plus a margin), so the AI starts slowing early enough even at high/boosted
                // speeds. Never shorter than the authored cornerLookAhead
                float curSpeedMs = Mathf.Abs(controller.localBikeVelocity.z);
                float brakeDist  = (curSpeedMs * curSpeedMs) /
                                   (2f * Mathf.Max(controller.bikeSettings.deceleration, 1f));
                float dynCornerLookAhead = Mathf.Max(cornerLookAhead, brakeDist + 30f);
                const float BRAKE_SCAN_STEP = 15f;
                const float CURVE_ARC       = 45f;   // Arc the curvature is measured over

                // Radius from the angle the track turns through a FIXED arc, so it does not depend on how
                // far away the corner is. Deriving it from the distance to the sharpest sample (as this
                // did) made one corner read as a gentle sweep at 300 m and a hairpin at 50 m, so the safe
                // speed collapsed as the AI arrived and every corner was braked for late and hard.
                float tightestRadius = float.MaxValue;
                float tightestDist   = float.MaxValue;

                for (float scanDist = 0f; scanDist <= dynCornerLookAhead; scanDist += BRAKE_SCAN_STEP)
                {
                    var (wpA, _, _) = GetPointAtDistance(scanDist);
                    var (wpB, _, _) = GetPointAtDistance(scanDist + CURVE_ARC);

                    float turned = Mathf.Abs(Vector3.SignedAngle(GetSegmentDir(wpA), GetSegmentDir(wpB), Vector3.up));

                    float fromHere = Mathf.Abs(Vector3.SignedAngle(_nearTan, GetSegmentDir(wpA), Vector3.up));
                    if (fromHere > maxAngle) maxAngle = fromHere;

                    if (turned < 0.5f) continue;
                    float radius = CURVE_ARC / (turned * Mathf.Deg2Rad);
                    if (radius < tightestRadius) { tightestRadius = radius; tightestDist = scanDist; }
                }

                _cachedSafeCornerSpeedMs = cruiseSpeedKmh / 3.6f;
                _cachedCornerDist        = float.MaxValue;   // no corner → effectively infinite room
                if (maxAngle > cornerBrakeAngle && tightestRadius < float.MaxValue)
                {
                    float turnRadius  = tightestRadius;
                    float maxLatAccel = 0.5f * 15f * cornerGripMultiplier;

                    // Scale the minimum speed floor down for tight corners. The flat
                    // cornerSpeedKmh floor (default 120 km/h) is appropriate for gentle
                    // bends but overrides the physics-based safe speed for sharp 90° turns,
                    // leaving the AI going 120 km/h into a corner it needs to take at ~40 km/h
                    float angleBlend       = Mathf.InverseLerp(cornerBrakeAngle, 90f, maxAngle);
                    float minCornerSpeedMs = Mathf.Lerp(cornerSpeedKmh, 30f, angleBlend) / 3.6f;
                    _cachedSafeCornerSpeedMs = Mathf.Max(Mathf.Sqrt(maxLatAccel * turnRadius), minCornerSpeedMs);
                    _cachedCornerDist        = tightestDist;
                }

                // Corner hints: designer-placed speed targets in the route graph are authoritative for
                // any annotated corner in range, overriding the angle scan in BOTH directions. The scan's
                // radius estimate shrinks with the distance to the apex, so on a 90° intersection it keeps
                // dropping as the AI arrives and buries the authored speed
                _cornerSpeedIsAuthored = false;
                if (routeGraph != null && routeGraph.BakedCornerHints != null
                    && routeGraph.BakedCornerHints.Length > 0)
                {
                    // Distance the AI has already travelled into the current segment
                    float distIntoSeg = 0f;
                    for (int i = 0; i < _currentWp && i < _segLengths.Length; i++)
                        distIntoSeg += _segLengths[i];
                    if (_segLengths != null && _currentWp < _segLengths.Length)
                        distIntoSeg += _segmentProgress * _segLengths[_currentWp];

                    float segRemaining = _totalPathLength - distIntoSeg;

                    float nearestHintDist = float.MaxValue;
                    foreach (var hint in routeGraph.BakedCornerHints)
                    {
                        float distToHint;
                        if (hint.segmentIndex == _currentSegmentIndex)
                            distToHint = hint.distanceFromSegStart - distIntoSeg;
                        else if (hint.segmentIndex == _committedNextSegment)
                            distToHint = segRemaining + hint.distanceFromSegStart;
                        else
                            continue;

                        if (distToHint > dynCornerLookAhead || distToHint < -cornerHintHoldDistance) continue;
                        if (Mathf.Abs(distToHint) >= Mathf.Abs(nearestHintDist)) continue;

                        nearestHintDist          = distToHint;
                        _cachedSafeCornerSpeedMs = hint.approachSpeedMs;
                        _cachedCornerDist        = Mathf.Max(0f, distToHint);
                        _cornerSpeedIsAuthored   = true;
                    }
                }
            }
            // Racing "personality", cautious bikes shave a little off the shared safe corner speed so the
            // pack fans out and trades places through corners instead of single-filing. Only ever slower
            // than the physics safe limit, never faster, so cornering/dodging stays reliable
            float cornerCautionFactor = _cornerSpeedIsAuthored
                ? 1f
                : 1f - 0.15f * _cornerCaution * personalityStrength;

            // Floor the corner speed so the AI never brakes to a crawl/stop (guards against a mis set
            // corner hint with approachSpeedMs ~0, which would otherwise make it stop dead at that corner)
            float safeCornerSpeedMs = Mathf.Max(_cachedSafeCornerSpeedMs * cornerCautionFactor,
                                                minCornerApproachKmh / 3.6f);

            // Keep the cached corner distance fresh between the throttled corner scans so the
            // braking onset stays accurate frame to frame as we close on the corner
            if (_cachedCornerDist < float.MaxValue * 0.5f)
                _cachedCornerDist = Mathf.Max(0f, _cachedCornerDist - Mathf.Abs(fwdSpeed) * Time.fixedDeltaTime);

            // Crash recovery window decrement (its boost is folded into the rubber-band factor below)
            if (_recoveryTimer > 0f) _recoveryTimer -= Time.fixedDeltaTime;

            _rubberBandCounter++;
            if (_rubberBandCounter >= RUBBER_BAND_INTERVAL)
            {
                _rubberBandCounter = 0;
                _rubberBandMult    = ComputeRubberBandFactor();
            }
            ApplyRubberBand();   // Scale top speed + acceleration toward the gap target

            // Cruise stays above the maxSpeed cap on purpose, so the AI always pushes to its (now
            // rubber-banded) top speed, the rubber band moves that cap rather than this cruise target
            // The band raises maxSpeed past cruise when this AI is behind, and tapering against cruise alone
            // lifted the throttle from 85% of it (~357 km/h), so the extra top speed was never used
            float cruiseMs      = Mathf.Max(cruiseSpeedKmh / 3.6f, controller.bikeSettings.maxSpeed * 1.2f);
            float speedRatio    = fwdSpeed / Mathf.Max(cruiseMs, 1f);
            float cruiseThrottle = speedRatio < 0.85f ? 1f : Mathf.Lerp(1f, 0.2f, (speedRatio - 0.85f) / 0.15f);

            // While boosting, hold full throttle and let the boost-raised maxSpeed be the only limiter,
            // mirroring the player's boost (which exceeds the normal top speed). Without this the cruise
            // throttle tapers at cruiseSpeedKmh and the AI never uses the extra speed the boost unlocks
            bool boosting = _boost != null && _boost.isBoosting;
            if (boosting) cruiseThrottle = 1f;

            float brakeBudget    = controller.bikeSettings.deceleration * 0.85f;
            float allowedApproach = _cachedCornerDist >= float.MaxValue * 0.5f
                ? float.MaxValue
                : Mathf.Sqrt(safeCornerSpeedMs * safeCornerSpeedMs
                             + 2f * brakeBudget * Mathf.Max(0f, _cachedCornerDist));
            bool cornerBraking = fwdSpeed > allowedApproach + 1f;

            float trafficThrottle   = cruiseThrottle;
            bool trafficBrakeNeeded = false;
            if (planning)
            {
                // Lift in proportion to the threat on the current line while moving across to the new one,
                // as F1 2011's context steering did, rather than only once every line is blocked
                if (!cornerBraking && DodgeUrgency > 0.3f)
                    trafficThrottle = Mathf.Min(trafficThrottle, 1f - Mathf.Lerp(0f, 0.6f, (DodgeUrgency - 0.3f) / 0.7f));

                if (!cornerBraking && _planner.Blocked)
                {
                    if (fwdSpeed > _planner.SpeedCapMs + 1f) trafficBrakeNeeded = true;
                    else trafficThrottle = Mathf.Min(trafficThrottle, draftFollowThrottle);
                }
            }
            else if (!cornerBraking && DodgeUrgency > 0.3f)
            {
                float lift = Mathf.Lerp(0f, 0.6f, (DodgeUrgency - 0.3f) / 0.7f);
                trafficThrottle = Mathf.Min(trafficThrottle, 1f - lift);

                if (_boxedIn)
                {
                    if (_closingSpeed > draftYieldClosingSpeed)
                        trafficBrakeNeeded = true;
                    else
                        trafficThrottle = Mathf.Min(trafficThrottle, draftFollowThrottle);
                }
            }

            // Level with the target, hold its speed rather than riding past it
            if (combatHold && !cornerBraking)
            {
                float holdKmh = combatPositioning.HoldSpeedKmh(_combatAI.EngageTarget, bikeTransform.position, _nearTan);
                if (holdKmh > 0f)
                    trafficThrottle = Mathf.Min(trafficThrottle, Mathf.Clamp01((holdKmh - fwdSpeed * 3.6f) / 8f));
            }

            float effectiveOvertakeScale = 1f;

            bool fullBrake = cornerBraking || (trafficBrakeNeeded && Mathf.Abs(fwdSpeed) > 4f);
            controller.throttle  = fullBrake
                ? 0f
                : Mathf.Max(trafficThrottle, 0.05f) * effectiveOvertakeScale;
            controller.isBraking = fullBrake;

            if (fwdSpeed < 5f)
                controller.steerInput *= Mathf.Clamp01(fwdSpeed / 5f);

            // Launch phase, pin throttle and prevent corner braking; steering follows path naturally
            if (launchSuppression > 0f)
            {
                controller.throttle  = Mathf.Max(controller.throttle, launchSuppression);
                controller.isBraking = false;
            }

            // Ramp trigger, hold full throttle and suppress braking while physically on a ramp collider.
            // Also extends to the entire requiresTrigger shortcut segment so that look ahead seeing a
            // curve on the exit road doesn't cause the AI to brake before the jump-down ramp trigger
            bool onShortcutSegNow = routeGraph != null
                && _currentSegmentIndex >= 0
                && _currentSegmentIndex < routeGraph.segments.Length
                && routeGraph.segments[_currentSegmentIndex].requiresTrigger;

            if (controller.isOnRamp || onShortcutSegNow)
            {
                controller.throttle  = 1f;
                controller.isBraking = false;
            }

            // Drift decision, slide through a genuine corner when committed to it at speed, but never while
            // dodging traffic (don't slide into a car) or on a ramp. The controller smooths/enacts it
            controller.driftRequested = enableAIDrift
                && _upcomingCornerAngle >= aiDriftCornerAngle
                && Mathf.Abs(controller.steerInput) >= aiDriftMinSteer
                && Mathf.Abs(fwdSpeed) * 3.6f >= aiDriftMinSpeedKmh
                && DodgeUrgency < 0.5f
                && !controller.isOnRamp;

            // Diagnostic
            // Logs the full throttle decision for one AI so we can see exactly what is slowing it
            if (debugLogThrottle && _spawnIndex == 0 && (++_dbgCounter % 25 == 0))
            {
                Debug.Log(
                    $"[AIdbg] spd={fwdSpeed:F1} cruiseKmh={cruiseSpeedKmh:F0} cruiseMs={cruiseMs:F1} " +
                    $"THR={controller.throttle:F2} brake={controller.isBraking} || " +
                    $"cornerBrake={cornerBraking} allowApproach={allowedApproach:F1} cornerDist={_cachedCornerDist:F1} " +
                    $"safeCorner={safeCornerSpeedMs:F1} || trafBrake={trafficBrakeNeeded} boxed={_boxedIn} " +
                    $"urgency={DodgeUrgency:F2} || launch={launchSuppression:F2} grounded={controller.bikeIsGrounded} " +
                    $"canAcc={controller.canAccelerate} revving={controller.isRevving} onRamp={controller.isOnRamp}",
                    this);
            }
        }

    #endregion

    #region Traffic Avoidance
        private void RunPlanner(float fwdSpeed, float intent, float lookAhead)
        {
            _planTimer -= Time.fixedDeltaTime;
            if (_planTimer <= 0f)
            {
                _planTimer = plannerInterval;
                _plannedIntent = intent;

                // Never shorter than braking to traffic speed takes, or "blocked" arrives after braking can help
                float brakeSeconds = Mathf.Max(0f, Mathf.Abs(fwdSpeed) - 25f) /
                                     Mathf.Max(controller.bikeSettings.deceleration, 1f);
                _planner.horizon       = Mathf.Max(plannerHorizon, brakeSeconds + 1f);
                _planner.lateralTimeConstant = Mathf.Clamp(lookAhead / Mathf.Max(Mathf.Abs(fwdSpeed), 1f), 0.3f, 2f);
                _planner.margin        = plannerMargin;
                _planner.bikeHalfWidth = bikeHalfWidth;

                Vector3 offset = bikeTransform.position - _nearPos;
                offset.y = 0f;
                _plannerRoute ??= PlannerRouteFrame;

                float minLat = -maxLateralAimOffset, maxLat = maxLateralAimOffset;
                var spawner = TrafficSpawner.Instance;
                // Lanes are sampled every 40 m, so a 45 m radius sometimes caught a single lane and pinned the
                // bike to a sliver at the road edge. A span narrower than a lane and a half is not trusted
                if (plannerStayOnCarriageway && spawner != null &&
                    spawner.TryGetCarriageway(_nearPos, _nearTan, _nearLeft, 60f, out float laneMin, out float laneMax) &&
                    laneMax - laneMin >= 5f)
                {
                    minLat = Mathf.Max(minLat, laneMin - plannerLaneHalfWidth);
                    maxLat = Mathf.Min(maxLat, laneMax + plannerLaneHalfWidth);
                    if (minLat > maxLat) { minLat = -maxLateralAimOffset; maxLat = maxLateralAimOffset; }
                }

                _planMinLat = minLat;
                _planMaxLat = maxLat;
                _planner.Plan(bikeTransform.position, Mathf.Abs(fwdSpeed), Vector3.Dot(offset, _nearLeft),
                              Mathf.Clamp(intent, minLat, maxLat), minLat, maxLat,
                              -maxLateralAimOffset, maxLateralAimOffset, _plannerRoute);
            }

            // Snap up, ease down, as the scan dodge did, so the panic probe's 1 still decays smoothly
            float urgency = _planner.Urgency;
            DodgeUrgency = urgency > DodgeUrgency
                ? urgency
                : Mathf.SmoothDamp(DodgeUrgency, urgency, ref _urgencyVel, 1.2f);

            _boxedIn      = _planner.Blocked;
            _trafficBrake = _planner.Blocked;
            _dodgeBias    = _planner.TargetLateral - _plannedIntent;
        }

        public string PlannerDebug(Component hit)
        {
            if (!useTrafficPlanner) return "planner off";
            Vector3 offset = bikeTransform.position - _nearPos;
            offset.y = 0f;
            return $"{_planner.Describe(hit)}; carriageway {_planMinLat:F1}..{_planMaxLat:F1}; " +
                   $"now at lateral {Vector3.Dot(offset, _nearLeft):F1}, " +
                   $"{Mathf.Abs(controller.localBikeVelocity.z) * 3.6f:F0} km/h";
        }

        // The same route the AI steers by. The heading comes from two samples because the wp index
        // GetPointAtDistance returns belongs to whichever segment the distance reached, not the current one
        private AITrafficPlanner.RouteFrame PlannerRouteFrame(float distance)
        {
            Vector3 a = GetPointAtDistance(distance).pos;
            Vector3 tangent = GetPointAtDistance(distance + 2f).pos - a;
            tangent.y = 0f;
            tangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : _nearTan;

            return new AITrafficPlanner.RouteFrame
            {
                position = a,
                tangent  = tangent,
                left     = Vector3.Cross(tangent, Vector3.up),
            };
        }

        private float ComputePathTrafficBias(float fwdSpeed)
        {
            // Only worth scanning when there's actually a bend ahead, on a straight the forward cone
            // already covers the route
            if (trafficLayerMask == 0 || _upcomingCornerAngle <= cornerBrakeAngle) return 0f;

            float speedMs   = Mathf.Abs(fwdSpeed);
            float lookahead = Mathf.Clamp(Mathf.Max(40f, speedMs * 2.5f), 40f, 120f);
            float laneR     = scanWidth * 0.5f + 1f;   // Proximity radius around each path point
            const float STEP = 12f;

            for (float d = 15f; d <= lookahead; d += STEP)
            {
                var (wp, _, pos) = GetPointAtDistance(d);
                int n = Physics.OverlapSphereNonAlloc(pos, laneR, s_pathTrafficBuffer,
                                                      trafficLayerMask, QueryTriggerInteraction.Ignore);
                WarnIfSaturated(n, s_pathTrafficBuffer.Length, "blind corner scan");
                for (int i = 0; i < n; i++)
                {
                    Collider c = s_pathTrafficBuffer[i];
                    if (c == null) continue;
                    Rigidbody rb = c.attachedRigidbody;
                    if (rb != null && (rb == _ownRb ||
                        (excludedBodies != null && excludedBodies.Contains(rb)))) continue;   // Ignore self/racers
                    if (playerTransform != null && c.transform.IsChildOf(playerTransform)) continue;

                    // Which side of the route is the traffic on? Dodge toward the OPEN side.
                    Vector3 pathDir   = GetSegmentDir(wp);
                    Vector3 pathRight = Vector3.Cross(Vector3.up, pathDir).normalized;
                    float   side      = Vector3.Dot(c.transform.position - pos, pathRight); // + = on the right
                    // Convention (see no-threat branch): traffic on the right → dodge LEFT (+), else RIGHT (-).
                    _committedDodgeSide = side >= 0f ? -1f : 1f;
                    _dodgeCommitTimer   = DODGE_COMMIT_DURATION;
                    return side >= 0f ? dodgeStrength * 0.4f : -dodgeStrength * 0.4f;
                }
            }
            return 0f;
        }

        private float ComputeTrafficDodge(float fwdSpeed)
        {
            if (bikeTransform == null) return 0f;

            Vector3 origin = bikeTransform.position + Vector3.up * 0.8f;

            Vector3 fwd = bikeTransform.forward;
            float cornerBlend = Mathf.Clamp01((_upcomingCornerAngle - cornerBrakeAngle) / 20f);
            if (cornerBlend > 0f && _nearTan.sqrMagnitude > 0.001f)
                fwd = Vector3.Slerp(fwd, _nearTan, cornerBlend * 0.6f).normalized;

            Vector3 bikeRight = Vector3.Cross(Vector3.up, fwd).normalized;
            if (bikeRight.sqrMagnitude < 0.001f) bikeRight = _nearLeft;

            float speedMs = Mathf.Abs(fwdSpeed);
            float effectiveDist = minSecondsOfVisibility > 0f
                ? Mathf.Max(detectionDistance, speedMs * minSecondsOfVisibility)
                : detectionDistance;

            int probes = Mathf.Max(scanProbes, 3);
            if (probes % 2 == 0) probes++;  // Keep odd so a centre probe always exists
            float halfScan = scanWidth * 0.5f;

            // Widen the cast radius at race speed so near-miss lateral threats are caught
            float radius = sphereCastRadius + Mathf.Clamp01(speedMs / (cruiseSpeedKmh / 3.6f)) * 0.5f;

            if (_scanDists == null)
                _scanDists = new float[Mathf.Max(scanProbes + 1, 3)]; // +1 guards against probes++ when scanProbes is even

            float minDist  = effectiveDist;
            float bestDist = 0f;
            int bestProbe  = probes / 2;
            float nearestClosingSpeed = 0f;

            float fanHalfAngle = scanFanHalfAngle;
            for (int i = 0; i < probes; i++)
            {
                float t           = (float)i / (probes - 1);
                float lateral     = Mathf.Lerp(-halfScan, halfScan, t);
                Vector3 probeOrigin = origin + bikeRight * lateral;
                float fanAngle    = Mathf.Lerp(-fanHalfAngle, fanHalfAngle, t);
                Vector3 probeDir  = Quaternion.AngleAxis(fanAngle, Vector3.up) * fwd;

                var (dist, vel)  = TrafficSphereCast(probeOrigin, probeDir, radius, effectiveDist);

                _scanDists[i] = dist;

                if (dist < minDist)
                {
                    minDist = dist;
                    float closing = vel.sqrMagnitude > 0.01f
                        ? fwdSpeed - Vector3.Dot(vel, fwd)
                        : fwdSpeed;
                    if (closing > nearestClosingSpeed)
                        nearestClosingSpeed = closing;
                }

                if (dist > bestDist)
                {
                    bestDist  = dist;
                    bestProbe = i;
                }
            }

            float sideDetection = scanWidth + 2f;  // Extra 2 m catches wide vehicles just outside the scan width
            float sideLeftDist  = TrafficRaycast(origin, -bikeRight, sideDetection);
            float sideRightDist = TrafficRaycast(origin, bikeRight, sideDetection);

            _closingSpeed = nearestClosingSpeed;
            _bestProbe    = bestProbe;

            // No threat ahead
            if (minDist >= effectiveDist)
            {
                _trafficBrake = false;
                _boxedIn      = false;

                bool blockedLeft  = sideLeftDist  < sideDetection;
                bool blockedRight = sideRightDist < sideDetection;

                if (blockedLeft || blockedRight)
                {
                    DodgeUrgency = Mathf.Max(DodgeUrgency, 0.4f);
                    if (blockedLeft && !blockedRight)
                    {
                        // Main section convention, _committedDodgeSide = 1f → dodge = -1f*strength, RIGHT
                        _committedDodgeSide = 1f;
                        _dodgeCommitTimer   = DODGE_COMMIT_DURATION;
                        return -dodgeStrength * 0.4f;
                    }
                    if (blockedRight && !blockedLeft)
                    {
                        // Main section convention, _committedDodgeSide = -1f, dodge = +1f*strength, LEFT
                        _committedDodgeSide = -1f;
                        _dodgeCommitTimer   = DODGE_COMMIT_DURATION;
                        return dodgeStrength * 0.4f;
                    }
                }

                // Nothing in the forward cone, but check the upcoming route for traffic around a blind
                // corner the cone can't see yet, and pre position toward the open side if so
                float anticipated = ComputePathTrafficBias(fwdSpeed);
                if (anticipated != 0f)
                {
                    DodgeUrgency = Mathf.Max(DodgeUrgency, 0.4f);  // alert + lined up, not a panic/brake
                    return anticipated;
                }

                // Smoothly decay urgency rather than snapping to zero, prevents abrupt throttle/steer jumps
                DodgeUrgency = Mathf.SmoothDamp(DodgeUrgency, 0f, ref _urgencyVel, 1.2f);
                return 0f;
            }

            // Compute urgency
            float panicDist = PanicDistance();
            float urgency;
            if (minDist <= panicDist)
            {
                urgency = 1f;
            }
            else
            {
                float effectiveClosing = Mathf.Max(nearestClosingSpeed, 1f);
                float timeToImpact     = minDist / effectiveClosing;
                urgency = Mathf.Clamp01(1f - timeToImpact / reactTime);
            }
            // Snap up immediately on a new/worsening threat, decay smoothly as it recedes
            if (urgency > DodgeUrgency)
                DodgeUrgency = urgency;
            else
                DodgeUrgency = Mathf.SmoothDamp(DodgeUrgency, urgency, ref _urgencyVel, 1.2f);

            _trafficBrake = (minDist < panicDist || bestDist < effectiveDist * 0.3f || _closingSpeed > 15f) && urgency > 0.5f;

            // Determine which side is clearer
            // Probes near the centre of the fan (just left/right of the bike's path) get higher weight
            // than edge probes, so a vehicle directly ahead-left biases the score more than one far left
            int   centerIdx       = probes / 2;
            float leftWeighted    = 0f, leftTotalW   = 0f;
            float rightWeighted   = 0f, rightTotalW  = 0f;
            for (int i = 0; i < centerIdx; i++)
            {
                float w = i + 1; // Weight rises toward centre: 1 at far-left, centerIdx at near centre
                leftWeighted  += _scanDists[i] * w;
                leftTotalW    += w;
            }
            for (int i = centerIdx + 1; i < probes; i++)
            {
                float w = probes - i; // Weight rises toward centre, 1 at far-right, centerIdx at near-centre
                rightWeighted += _scanDists[i] * w;
                rightTotalW   += w;
            }
            float leftAvgDist  = leftTotalW  > 0f ? leftWeighted  / leftTotalW  : effectiveDist;
            float rightAvgDist = rightTotalW > 0f ? rightWeighted / rightTotalW : effectiveDist;

            bool sideBlockedLeft  = sideLeftDist  < sideDetection;
            bool sideBlockedRight = sideRightDist < sideDetection;

            // Boxed in, both sides blocked at real urgency, there's nowhere to dodge, so the AI must
            // brake rather than swerve into traffic. Read by Tick() to force a brake
            _boxedIn = sideBlockedLeft && sideBlockedRight && urgency > 0.5f;

            // Pick the better side, side blockage wins, then probe average with a 2 m hysteresis gap,
            // then fall back to the single clearest probe as a tiebreaker before using the committed side
            float newSide;
            if      (sideBlockedLeft  && !sideBlockedRight)  newSide =  1f;
            else if (sideBlockedRight && !sideBlockedLeft)   newSide = -1f;
            else if (rightAvgDist > leftAvgDist + 2f)        newSide =  1f;
            else if (leftAvgDist  > rightAvgDist + 2f)       newSide = -1f;
            else if (_bestProbe > centerIdx)                 newSide =  1f;  // Clearest single probe is right
            else if (_bestProbe < centerIdx)                 newSide = -1f;  // Clearest single probe is left
            else newSide = _committedDodgeSide != 0f ? _committedDodgeSide : _defaultDodgeSide;

            // Emergency override, the committed side is now blocked at very high urgency
            bool emergency = urgency >= DODGE_COMMIT_EMERGENCY_URGENCY &&
                ((_committedDodgeSide > 0f && sideBlockedRight) ||
                 (_committedDodgeSide < 0f && sideBlockedLeft));

            if (_committedDodgeSide == 0f || _dodgeCommitTimer <= 0f || emergency)
            {
                _committedDodgeSide = newSide;
                _dodgeCommitTimer   = DODGE_COMMIT_DURATION;
            }

            // _committedDodgeSide, -1 aim left (+nearLeft), +1 aim right (-nearLeft)
            // Concave curve: AI commits laterally at medium urgency rather than waiting until near collision
            // e.g. urgency 0.3 to 0.44x offset instead of 0.3x, urgency 0.6 to 0.74x instead of 0.6x
            float dodgeCurve = Mathf.Pow(urgency, 0.6f);
            float dodge = -_committedDodgeSide * dodgeStrength * dodgeCurve;
            return Mathf.Clamp(dodge, -dodgeStrength, dodgeStrength);
        }

        // Context steering traffic avoidance. Builds an interest map (prefer staying on the current
        // line) and a danger map (each nearby vehicle's *predicted* lateral occupancy, including its
        // lane change drift), then picks the lateral slot that maximises interest - danger. Returns the
        // same lateral dodge offset (in metres along _nearLeft) that ComputeTrafficDodge returns, and
        // sets the same DodgeUrgency / _trafficBrake / _boxedIn / _closingSpeed outputs Tick relies on
        private float ComputeTrafficDodgeContext(float fwdSpeed)
        {
            if (bikeTransform == null) return 0f;

            Vector3 origin = bikeTransform.position + Vector3.up * 0.8f;

            Vector3 fwd = bikeTransform.forward;
            float cornerBlend = Mathf.Clamp01((_upcomingCornerAngle - cornerBrakeAngle) / 20f);
            if (cornerBlend > 0f && _nearTan.sqrMagnitude > 0.001f)
                fwd = Vector3.Slerp(fwd, _nearTan, cornerBlend * 0.6f).normalized;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = bikeTransform.forward;
            fwd.Normalize();

            Vector3 bikeRight = Vector3.Cross(Vector3.up, fwd).normalized;   // +lateral = right
            if (bikeRight.sqrMagnitude < 0.001f) bikeRight = -_nearLeft;

            float speedMs = Mathf.Abs(fwdSpeed);
            float effectiveDist = minSecondsOfVisibility > 0f
                ? Mathf.Max(detectionDistance, speedMs * minSecondsOfVisibility)
                : detectionDistance;

            int   slots    = EnsureContextBuffers();
            int   centerIdx = slots / 2;
            float halfScan = scanWidth * 0.5f;
            float band     = sphereCastRadius + bikeHalfWidth + 0.6f;  // Half width of a car's danger band

            // Build slot lateral positions and a mild centring interest (prefer the least swerve)
            for (int i = 0; i < slots; i++)
            {
                float t = slots == 1 ? 0.5f : (float)i / (slots - 1);
                float lat = Mathf.Lerp(-halfScan, halfScan, t);
                _ctxSlotLat[i]  = lat;
                _ctxInterest[i] = 1f - 0.25f * Mathf.Abs(lat) / Mathf.Max(halfScan, 0.001f);
                _ctxDanger[i]   = 0f;
            }

            // Gather traffic in the corridor actually being scanned. This was a sphere of the corridor's
            // LENGTH as its radius - about 76x the volume - which in six lane traffic returned far more
            // colliders than s_ctxBuffer holds. OverlapSphere does not order its results, so the
            // overflow silently dropped arbitrary cars, sometimes the one straight ahead.
            // The 90 m ceiling this used to carry silently overrode both detectionDistance and
            // minSecondsOfVisibility: at 83 m/s closing on 25 m/s traffic it left ~1.5 s to react, which
            // is not enough to cross a lane. The volume worry behind it was a SPHERE of the corridor's
            // length; this box is about 14 m wide and 6 m tall, so length is cheap. WarnIfSaturated
            // still reports if 32 colliders is ever not enough
            float gatherDist   = Mathf.Clamp(effectiveDist, 40f, 250f);
            Vector3 gatherCenter = origin + fwd * gatherDist * 0.5f;
            Vector3 gatherHalf   = new Vector3(halfScan + band + 1.5f, 3f, gatherDist * 0.5f);
            int n = Physics.OverlapBoxNonAlloc(gatherCenter, gatherHalf, s_ctxBuffer,
                                               Quaternion.LookRotation(fwd, Vector3.up),
                                               trafficLayerMask, QueryTriggerInteraction.Ignore);
            WarnIfSaturated(n, s_ctxBuffer.Length, "context scan");

            float nearestClosing    = 0f;
            float nearestThreatDist = effectiveDist;

            for (int k = 0; k < n; k++)
            {
                Collider c = s_ctxBuffer[k];
                if (c == null) continue;
                Rigidbody rb = c.attachedRigidbody;
                if (rb != null && (rb == _ownRb ||
                    (excludedBodies != null && excludedBodies.Contains(rb)))) continue;   // self / racers
                if (playerTransform != null && c.transform.IsChildOf(playerTransform)) continue;

                Vector3 rel = c.bounds.center - origin;
                float f = Vector3.Dot(rel, fwd);
                float l = Vector3.Dot(rel, bikeRight);
                if (f > effectiveDist) continue;
                if (f < -3f)           continue;   // Keep just beside/behind cars so side blocking registers

                Vector3 vvel = GetTrafficVelocity(c, rb);
                float vFwd   = Vector3.Dot(vvel, fwd);
                float vLat   = Vector3.Dot(vvel, bikeRight);
                float closing = fwdSpeed - vFwd;

                // Time for the bike to draw level with this vehicle's current forward distance
                float tImpact = f <= 0f ? 0f : f / Mathf.Max(closing, 0.5f);
                // Predicted lateral offset when we get there, includes the car's lane change drift
                float predLat = l + vLat * tImpact;

                float timeDanger = Mathf.Clamp01(1f - tImpact / reactTime);
                float proxDanger = Mathf.Clamp01(1f - f / Mathf.Max(fullUrgencyDistance, 1f));
                float mag = Mathf.Max(timeDanger, proxDanger);
                // A car slower than us and still far away is a low threat (we're closing slowly/it clears)
                if (closing <= 0.5f && f > fullUrgencyDistance) mag *= 0.3f;
                if (mag <= 0.01f) continue;

                for (int i = 0; i < slots; i++)
                {
                    float d = Mathf.Abs(_ctxSlotLat[i] - predLat);
                    if (d > band + 1.5f) continue;
                    float falloff = d <= band ? 1f : Mathf.Clamp01(1f - (d - band) / 1.5f);
                    _ctxDanger[i] = Mathf.Min(1f, _ctxDanger[i] + mag * falloff);
                }

                if (mag > 0.2f && f >= 0f && f < nearestThreatDist) nearestThreatDist = f;
                if (closing > nearestClosing) nearestClosing = closing;
            }

            _closingSpeed = nearestClosing;

            // Threat on the bike's current trajectory drives urgency (centre slot + weighted neighbours)
            float centerDanger = _ctxDanger[centerIdx];
            if (centerIdx - 1 >= 0)    centerDanger = Mathf.Max(centerDanger, _ctxDanger[centerIdx - 1] * 0.8f);
            if (centerIdx + 1 < slots) centerDanger = Mathf.Max(centerDanger, _ctxDanger[centerIdx + 1] * 0.8f);

            float urgency = centerDanger;
            if (urgency > DodgeUrgency) DodgeUrgency = urgency;                                   // snap up
            else DodgeUrgency = Mathf.SmoothDamp(DodgeUrgency, urgency, ref _urgencyVel, 1.2f);   // ease down

            // Pick the slot with the best interest-minus-danger score (danger weighted to dominate)
            int   best      = centerIdx;
            float bestScore = float.NegativeInfinity;
            float minDanger = 1f;
            for (int i = 0; i < slots; i++)
            {
                if (_ctxDanger[i] < minDanger) minDanger = _ctxDanger[i];
                float score = _ctxInterest[i] - _ctxDanger[i] * 2f;
                if (score > bestScore) { bestScore = score; best = i; }
            }

            // Boxed in, the bike's path is threatened and even the clearest slot is dangerous, there is
            // nowhere safe to swerve, so signal Tick to brake rather than dive into a car
            _boxedIn = centerDanger > 0.5f && minDanger > 0.55f;
            _trafficBrake = (centerDanger > 0.6f || nearestThreatDist < PanicDistance() || _closingSpeed > 15f)
                            && urgency > 0.5f;

            // Publish the map's frame and age so RouteClearance can read this same lateral profile instead
            // of re-scanning: the racing biases need to know which slots are occupied, not just which one
            // this dodge picked
            _ctxRight     = bikeRight;
            _ctxBuiltTime = Time.time;

            // Chosen slot lateral is along bikeRight (+ = right); the dodge is consumed along _nearLeft
            // (+ = left), so negate. When boxed in, relax the swerve (the brake handles it)
            float dodge = -_ctxSlotLat[best];
            if (_boxedIn) dodge *= 0.3f;
            return Mathf.Clamp(dodge, -dodgeStrength, dodgeStrength);
        }

        private static bool _warnedSaturated;

        // A NonAlloc query that fills its buffer has silently discarded everything past it, and gives no
        // ordering, so the discarded ones can be the closest. Worth knowing about rather than guessing.
        private static void WarnIfSaturated(int count, int capacity, string what)
        {
            if (count < capacity || _warnedSaturated) return;
            _warnedSaturated = true;
            Debug.LogWarning($"[BikeAILogic] traffic {what} filled its {capacity} collider buffer, so " +
                             "some vehicles were dropped from the scan and dodging may miss them.");
        }

        // Ensures the context map buffers exist and are sized to an odd slot count (so a centre slot,
        // the bike's current trajectory, always exists). Returns the slot count
        private int EnsureContextBuffers()
        {
            int slots = Mathf.Max(contextSlots, 5);
            if ((slots & 1) == 0) slots++;
            if (_ctxDanger == null || _ctxDanger.Length != slots)
            {
                _ctxDanger   = new float[slots];
                _ctxInterest = new float[slots];
                _ctxSlotLat  = new float[slots];
            }
            return slots;
        }

        // World velocity of a traffic vehicle: SplineMover.Velocity (which includes lateral lane change
        // motion), falling back to VehicleFollowing's forward-only speed.
        // Traffic rides on KINEMATIC rigidbodies, which PhysX never integrates - linearVelocity on one
        // is not a reading. Preferring it, as this used to, told the AI every car was parked: closing
        // speed collapsed to our own speed, so oncoming traffic was seen far too late and same
        // direction traffic panicked it, and lane change drift was ignored entirely.
        private Vector3 GetTrafficVelocity(Collider c, Rigidbody rb)
        {
            if (rb != null && !rb.isKinematic) return rb.linearVelocity;

            Transform root = c.transform.root;
            if (!_splineMoverCache.TryGetValue(root, out SplineMover mover))
            {
                mover = root.GetComponentInChildren<SplineMover>();
                _splineMoverCache[root] = mover;
            }
            if (mover != null) return mover.Velocity;

            if (!_vehicleFollowingCache.TryGetValue(root, out VehicleFollowing following))
            {
                following = c.transform.GetComponentInParent<VehicleFollowing>();
                _vehicleFollowingCache[root] = following;
            }
            if (following != null) return following.transform.forward * following.CurrentSpeed;
            return Vector3.zero;
        }

        private float TrafficRaycast(Vector3 origin, Vector3 direction, float maxDist)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDist,
                    trafficLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody != null && hit.rigidbody == _ownRb) return maxDist;
                if (playerTransform != null && hit.transform.IsChildOf(playerTransform)) return maxDist;
                if (excludedBodies != null && hit.rigidbody != null && excludedBodies.Contains(hit.rigidbody))
                    return maxDist;
                return hit.distance;
            }
            return maxDist;
        }

        private (float distance, Vector3 velocity) TrafficSphereCast(
            Vector3 origin, Vector3 direction, float radius, float maxDist = -1f)
        {
            if (maxDist < 0f) maxDist = detectionDistance;

            if (Physics.SphereCast(origin, radius, direction, out RaycastHit hit, maxDist,
                    trafficLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody != null && hit.rigidbody == _ownRb) return (maxDist, Vector3.zero);
                if (playerTransform != null && hit.transform.IsChildOf(playerTransform))
                    return (maxDist, Vector3.zero);
                if (excludedBodies != null && hit.rigidbody != null && excludedBodies.Contains(hit.rigidbody))
                    return (maxDist, Vector3.zero);

                return (hit.distance, GetTrafficVelocity(hit.collider, hit.rigidbody));
            }
            return (maxDist, Vector3.zero);
        }

        // How clear the road is at a lateral offset from the current line, in the _nearLeft convention
        // every bias uses (+ = left). 1 = free, 0 = occupied. Every bias that pulls the bike toward a
        // rival asks this first, so "get to the player" can never route through a vehicle. Reads the
        // context danger map the last traffic scan already built, and falls back to a side probe when
        // there is no usable map (legacy dodge mode, or before the first scan)
        private float RouteClearance(float biasAlongNearLeft)
        {
            if (trafficLayerMask == 0 || bikeTransform == null) return 1f;
            if (Mathf.Abs(biasAlongNearLeft) < 0.25f) return 1f;   // Not actually changing lane

            if (_ctxDanger != null && Time.time - _ctxBuiltTime <= CTX_MAP_MAX_AGE)
            {
                // Every slot BETWEEN here and the destination has to be driven through to reach it, so the
                // whole swept band counts, not just the slot the bias lands on
                float destLat = Vector3.Dot(_nearLeft * biasAlongNearLeft, _ctxRight);
                float margin  = bikeHalfWidth + sphereCastRadius;
                float lo = Mathf.Min(0f, destLat) - margin;
                float hi = Mathf.Max(0f, destLat) + margin;

                float worst = 0f;
                for (int i = 0; i < _ctxDanger.Length; i++)
                {
                    if (_ctxSlotLat[i] < lo || _ctxSlotLat[i] > hi) continue;
                    if (_ctxDanger[i] > worst) worst = _ctxDanger[i];
                }
                return 1f - Mathf.Clamp01(worst);
            }

            Vector3 origin = bikeTransform.position + Vector3.up * 0.8f;
            Vector3 dir    = biasAlongNearLeft > 0f ? _nearLeft : -_nearLeft;
            float   probe  = Mathf.Abs(biasAlongNearLeft) + bikeHalfWidth + 1f;
            return TrafficRaycast(origin, dir, probe) < probe ? 0f : 1f;
        }

        // Scales a bias that chases a rival by how clear its route is: full strength through free road,
        // nothing at all below minRouteClearance, so the AI holds its own line and waits for the gap
        private float RouteGate(float bias) =>
            bias * Mathf.InverseLerp(minRouteClearance, 1f, RouteClearance(bias));

        // Is there room to fight right now? Read by CombatAI before it swings. A swing pulls the rider off
        // the bars and the lunge shoves the bike sideways, so it needs somewhere to BE - not merely an
        // absence of traffic. DodgeUrgency answered the wrong question: it is high whenever anything is
        // ahead, including when the gap the AI is already tracking is wide open, so it blocked swings in
        // clear air. Nowhere to swerve (_boxedIn) or already braking for traffic are the real vetoes
        public bool SafeToEngage(Vector3 targetPosition)
        {
            if (_boxedIn || _trafficBrake) return false;
            if (bikeTransform == null) return true;

            float lateral = Vector3.Dot(targetPosition - bikeTransform.position, _nearLeft);
            return RouteClearance(Mathf.Clamp(lateral, -combatLateralStrength, combatLateralStrength))
                   >= minRouteClearance;
        }

        private float ComputeOvertakeBias()
        {
            if (racerBodies == null || racerBodies.Count == 0)
            {
                _overtakeBias = Mathf.SmoothDamp(_overtakeBias, 0f, ref _overtakeBiasVel, 0.6f);
                _forcedOvertake = false;
                IsOvertaking = false;
                return _overtakeBias;
            }
            if (bikeTransform == null || controller == null)
            {
                _overtakeBias = Mathf.SmoothDamp(_overtakeBias, 0f, ref _overtakeBiasVel, 0.6f);
                _forcedOvertake = false;
                IsOvertaking = false;
                return _overtakeBias;
            }

            Vector3 pos   = bikeTransform.position;
            Vector3 fwd   = bikeTransform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            Rigidbody ownRb = controller.bikeReferences.BikeRb;

            float closestFwdDist = float.MaxValue;
            bool  racerAhead     = false;
            Vector3 closestRacerOffset = Vector3.zero;
            Rigidbody closestRb = null;

            foreach (var rb in racerBodies)
            {
                if (rb == null || rb == ownRb) continue;

                Vector3 toRacer = rb.position - pos;
                toRacer.y = 0f;

                float fwdDist = Vector3.Dot(toRacer, fwd);
                float latDist = Vector3.Dot(toRacer, right);
                float absLatDist = Mathf.Abs(latDist);

                // Detect bikes from close range to far ahead
                if (fwdDist < 0.5f || fwdDist > overtakeDetectionRange) continue;
                if (absLatDist > overtakeMaxLateralRange * 1.5f) continue;

                // Track the closest bike ahead
                if (fwdDist < closestFwdDist)
                {
                    closestFwdDist = fwdDist;
                    closestRacerOffset = toRacer;
                    closestRb = rb;
                    racerAhead = true;
                }
            }

            _closestBikeDist = closestFwdDist;

            // No bike ahead, decay overtake bias
            if (!racerAhead)
            {
                _overtakeHoldTimer = Mathf.Max(0f, _overtakeHoldTimer - Time.fixedDeltaTime);
                if (_overtakeHoldTimer <= 0f) _overtakeSide = 0f;
                _overtakeBias = Mathf.SmoothDamp(_overtakeBias, 0f, ref _overtakeBiasVel, 0.5f);
                _forcedOvertake = false;
                _overtakeThrottleScale = 1f;
                _draftBuildTimer = 0f;
                IsOvertaking = false;
                return _overtakeBias;
            }

            // Determine which side the other bike is on
            float otherBikeSide = Vector3.Dot(closestRacerOffset, right);

            _overtakeSide = ValidateOvertakeSide(_overtakeSide);

            // FORCED OVERTAKE, If gap is too small, aggressively try to pass
            _forcedOvertake = closestFwdDist < minFollowingDistance;

            if (_forcedOvertake)
            {
                // Force commitment if not already committed
                if (_overtakeSide == 0f)
                    _overtakeSide = ChooseOvertakeSide(otherBikeSide, closestRb, pos, right);

                // Longer hold time when forced to prevent oscillation
                _overtakeHoldTimer = Mathf.Max(_overtakeHoldTimer, overtakeCommitTime * 1.5f);

                // Strong lateral offset when forced scale by how tight the gap is
                float urgency = Mathf.Clamp01(1f - closestFwdDist / minFollowingDistance);
                float forcedOffset = overtakeLateralOffset * (0.8f + 0.4f * urgency);
                float targetBias = RouteGate(_overtakeSide * forcedOffset);

                _overtakeBias = Mathf.SmoothDamp(_overtakeBias, targetBias, ref _overtakeBiasVel, 0.25f);

                // The AI no longer backs off to make a gap (effectiveOvertakeScale is forced to 1 in Tick),
                // so a close, committed pass is exactly when boost should fire to power through and complete
                // the overtake on the racer ahead (player or AI)
                _overtakeThrottleScale = 1f;
                IsOvertaking = true;
                return _overtakeBias;
            }

            // NORMAL OVERTAKE, Standard passing behavior
            // Pick which side to pass on commit once and hold to avoid oscillation
            if (_overtakeSide == 0f)
                _overtakeSide = ChooseOvertakeSide(otherBikeSide, closestRb, pos, right);

            _overtakeHoldTimer = overtakeCommitTime;

            // Scale overtaking offset by distance, stronger avoidance when closer
            float distanceScale = Mathf.Clamp01(1f - closestFwdDist / overtakeDetectionRange);
            float dynamicOffset = overtakeLateralOffset * (0.5f + 0.5f * distanceScale);

            // Tactical slipstream, if we're sitting in this rival's wake, tuck in behind for a beat to
            // build draft boost before swinging out. IsOvertaking stays false during the build so the
            // boost system holds fire until the pull out, then fires to slingshot us past
            bool drafting = enableTacticalSlipstream && _slipstream != null && _slipstream.inSlipstream;
            if (drafting && _draftBuildTimer < slipstreamBuildTime)
            {
                _draftBuildTimer += Time.fixedDeltaTime;
                float tuckBias = RouteGate(_overtakeSide * dynamicOffset * 0.2f);   // Stay nearly in line, in the wake
                _overtakeBias = Mathf.SmoothDamp(_overtakeBias, tuckBias, ref _overtakeBiasVel, 0.4f);
                _overtakeThrottleScale = 1f;
                IsOvertaking = false;
                return _overtakeBias;
            }
            if (!drafting) _draftBuildTimer = 0f;

            float normalTargetBias = RouteGate(_overtakeSide * dynamicOffset);

            _overtakeBias = Mathf.SmoothDamp(_overtakeBias, normalTargetBias, ref _overtakeBiasVel, 0.4f);

            // Light throttle reduction when close to preserve momentum, applied in Tick() after throttle is assigned
            _overtakeThrottleScale = closestFwdDist < 5f
                ? 1f - Mathf.Lerp(0.2f, 0f, closestFwdDist / 5f)
                : 1f;

            // Committed to a side and holding (near) full throttle to pull around, boost is useful here
            IsOvertaking = true;
            return _overtakeBias;
        }

        // Chooses which side to pass on. Default is opposite the rival (or deterministic when dead ahead
        // so two AIs split). Traffic aware, if the chosen side is blocked by traffic but the other side
        // is clear, flip to the clear side so the AI doesn't commit a pass straight into a vehicle
        private float ChooseOvertakeSide(float otherBikeSide, Rigidbody closestRb, Vector3 pos, Vector3 right)
        {
            float side = Mathf.Abs(otherBikeSide) < 1.5f
                ? DeterministicOvertakeSide(closestRb)
                : (otherBikeSide <= 0f ? 1f : -1f);

            if (trafficLayerMask != 0)
            {
                Vector3 origin = pos + Vector3.up * 0.8f;
                float probe = Mathf.Max(overtakeLateralOffset, 4f);
                bool leftBlocked  = TrafficRaycast(origin, -right, probe) < probe;
                bool rightBlocked = TrafficRaycast(origin,  right, probe) < probe;
                // Side > 0 commits toward +right space here (matches the otherBikeSide mapping above)
                if      (side > 0f && rightBlocked && !leftBlocked) side = -1f;
                else if (side < 0f && leftBlocked  && !rightBlocked) side =  1f;
            }
            // Chosen in +right space above; the overtake bias is consumed along _nearLeft (+ = left), so
            // negate, exactly as the dodge does. Unnegated, the bias steered INTO the rival being passed,
            // and the traffic-aware flip just above sent the bike to whichever side it had found BLOCKED
            return -side;
        }

        // A side chosen up to overtakeCommitTime ago can have a car in it by now. Re-checked every frame so
        // a pass flips to the clear side rather than being held into traffic. When neither side is clear the
        // commitment stays put (flipping every frame would only oscillate) and RouteGate collapses the bias
        private float ValidateOvertakeSide(float side)
        {
            if (side == 0f || trafficLayerMask == 0) return side;

            float wanted = side * Mathf.Max(overtakeLateralOffset, 4f);
            if (RouteClearance(wanted) >= minRouteClearance) return side;
            return RouteClearance(-wanted) >= minRouteClearance ? -side : side;
        }

        // Defensive racing, when a rival (especially the player) is closing from behind and offset to one
        // side to set up a pass, drift laterally to cover that side. Returns a lateral bias in the same
        // _nearLeft convention as the overtake bias. Mild and capped so it defends without pinballing
        private float ComputeDefensiveBias()
        {
            if (!enableDefensiveRacing || racerBodies == null || racerBodies.Count == 0 ||
                bikeTransform == null || controller == null)
            {
                _defensiveBias = Mathf.SmoothDamp(_defensiveBias, 0f, ref _defensiveBiasVel, 0.5f);
                return _defensiveBias;
            }

            Vector3 fwd = bikeTransform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f)
            {
                _defensiveBias = Mathf.SmoothDamp(_defensiveBias, 0f, ref _defensiveBiasVel, 0.5f);
                return _defensiveBias;
            }
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 pos   = bikeTransform.position;
            Rigidbody ownRb = controller.bikeReferences.BikeRb;

            float nearestBehind = float.MaxValue;
            float attackerLat   = 0f;
            bool  threat        = false;
            bool  playerAttacker = false;

            foreach (var rb in racerBodies)
            {
                if (rb == null || rb == ownRb) continue;
                Vector3 to = rb.position - pos;
                to.y = 0f;
                float f = Vector3.Dot(to, fwd);
                if (f >= -0.5f || f < -defensiveRange) continue;   // Must be behind us, within range

                float behind = -f;
                if (behind < nearestBehind)
                {
                    nearestBehind  = behind;
                    attackerLat    = Vector3.Dot(to, right);
                    threat         = true;
                    playerAttacker = playerTransform != null && rb.transform.IsChildOf(playerTransform);
                }
            }

            float target = 0f;
            if (threat)
            {
                // Cover the side the attacker is lined up on, if dead behind, hold the current line
                float side       = Mathf.Abs(attackerLat) < 0.8f ? 0f : Mathf.Sign(attackerLat);
                float closeScale  = Mathf.Clamp01(1f - nearestBehind / Mathf.Max(defensiveRange, 0.1f));
                // Defend harder against the player (race-feel) than against AI (avoid pack pinball)
                float aggression = playerAttacker ? 1f : 0.5f;
                // side is in +right space, the bias is consumed along _nearLeft (+ = left), so negate.
                // Unnegated this uncovered the exact side the rival was lining up on
                target = -side * defensiveStrength * closeScale * aggression * personalityStrength;
                // Don't defend while committed to passing someone ahead
                if (IsOvertaking) target *= 0.3f;
                // Covering a line is never worth moving into an occupied lane to do
                target = RouteGate(target);
            }

            _defensiveBias = Mathf.SmoothDamp(_defensiveBias, target, ref _defensiveBiasVel, 0.45f);
            return _defensiveBias;
        }

        // Detects upcoming corners and spreads bikes laterally to prevent single filing
        private float ComputeCornerSpread(float fwdSpeed)
        {
            // _upcomingCornerAngle and _cornerDirectionAhead are updated every frame in Tick()

            // Only spread on significant corners
            if (_upcomingCornerAngle < cornerSpreadThreshold)
            {
                _cornerSpreadBias = Mathf.SmoothDamp(_cornerSpreadBias, 0f, ref _cornerSpreadBiasVel, 0.5f);
                return _cornerSpreadBias;
            }

            // Determine which way the corner turns
            float cornerSign = Mathf.Sign(Vector3.SignedAngle(_nearTan, _cornerDirectionAhead, Vector3.up));
        
            // Check if there's a bike ahead in our path
            bool bikeAheadInCorner = false;
            float closestAheadDist = float.MaxValue;
        
            if (racerBodies != null && racerBodies.Count > 0)
            {
                Rigidbody ownRb = controller?.bikeReferences?.BikeRb;
                Vector3 pos = bikeTransform.position;
                Vector3 fwd = bikeTransform.forward;
                fwd.y = 0f;
                fwd.Normalize();
            
                foreach (var rb in racerBodies)
                {
                    if (rb == null || rb == ownRb) continue;
                
                    Vector3 toRacer = rb.position - pos;
                    toRacer.y = 0f;
                    float fwdDist = Vector3.Dot(toRacer, fwd);
                
                    // Bike ahead within corner approach zone
                    if (fwdDist > 1f && fwdDist < cornerSpreadDistance * 1.2f)
                    {
                        float latDist = Mathf.Abs(Vector3.Dot(toRacer, _nearLeft));
                        if (latDist < overtakeMaxLateralRange * 1.2f)
                        {
                            bikeAheadInCorner = true;
                            closestAheadDist = Mathf.Min(closestAheadDist, fwdDist);
                        }
                    }
                }
            }

            // If no bike ahead, stay close to racing line (small offset based on lane preference)
            if (!bikeAheadInCorner)
            {
                float racingLineOffset = lanePreference * 0.3f;
                _cornerSpreadBias = Mathf.SmoothDamp(_cornerSpreadBias, racingLineOffset, ref _cornerSpreadBiasVel, 0.4f);
                return _cornerSpreadBias;
            }

            // Bike ahead on corner, spread to opposite side
            // Determine if we should take inside or outside line
            float targetSpread;
        
            // If corner turns right, inside is right (-), outside is left (+)
            // Prefer outside line for overtaking on corners (faster but wider)
            if (cornerSign > 0f) // Left turn
            {
                // Outside line is left (+)
                targetSpread = cornerSpreadStrength;
            }
            else // Right turn
            {
                // Outside line is right (-)
                targetSpread = -cornerSpreadStrength;
            }

            // Flip corner line per bike using the randomised dodge side so not all bikes queue identically
            if (_defaultDodgeSide < 0f) targetSpread *= -1f;

            // Scale by how close the bike ahead is, more aggressive when closer
            float proximityScale = 1f;
            if (closestAheadDist < cornerSpreadDistance * 0.5f)
            {
                proximityScale = Mathf.Lerp(0.5f, 1f, 1f - closestAheadDist / (cornerSpreadDistance * 0.5f));
            }

            targetSpread *= proximityScale;

            _cornerSpreadBias = Mathf.SmoothDamp(_cornerSpreadBias, targetSpread, ref _cornerSpreadBiasVel, 0.3f);
            return _cornerSpreadBias;
        }

        // Paired AI bikes get opposite sides deterministically so they always diverge rather than
        // both trying to pass the same way. The bike with the smaller instance ID takes the right
        // side (+1), the other takes the left (-1). Falls back to _defaultDodgeSide for non AI targets
        private float DeterministicOvertakeSide(Rigidbody otherRb)
        {
            if (otherRb == null) return _defaultDodgeSide;
            var otherLogic = otherRb.GetComponentInParent<BikeAILogic>();
            if (otherLogic == null) return _defaultDodgeSide;
            return GetInstanceID() < otherLogic.GetInstanceID() ? 1f : -1f;
        }

    #endregion

    #region Barrier Avoidance

        private float ComputeWallAvoidance(Vector3 flatForward)
        {
            if (bikeTransform == null || barrierLayerMask == 0) return 0f;

            Vector3 origin = bikeTransform.position + Vector3.up * 0.8f;
            Vector3 fwd    = flatForward.sqrMagnitude > 0.001f ? flatForward.normalized : _nearTan;
            Vector3 right  = Vector3.Cross(Vector3.up, fwd).normalized;

            // Populate pre allocated direction buffer (directions depend on per-frame right/fwd)
            // Sign -1 = left side ray (push right = +heading), +1 = right-side ray (push left = -heading)
            // Forward facing rays use a longer range so inside-corner barriers are seen in time
            _wallDirs[0] = -right;                                          // 90° left
            _wallDirs[1] = (-right + fwd).normalized;                       // 45° front-left
            _wallDirs[2] = (-right * 0.5f + fwd).normalized;               // ~27° front-left
            _wallDirs[3] = (-right * 0.2f + fwd).normalized;               // ~11° front-left
            _wallDirs[4] =  right;                                          // 90° right
            _wallDirs[5] = ( right + fwd).normalized;                       // 45° front-right
            _wallDirs[6] = ( right * 0.5f + fwd).normalized;               // ~27° front-right
            _wallDirs[7] = ( right * 0.2f + fwd).normalized;               // ~11° front-right

            float correction = 0f;
            for (int i = 0; i < 8; i++)
            {
                float range = wallDetectionDistance * _wallRangeMults[i];
                float dist  = WallRaycast(origin, _wallDirs[i], range);
                if (dist < range)
                {
                    float proximity = 1f - dist / range;
                    // Scale to degrees, wallAvoidanceStrength * 9 gives ~45° at strength=5, proximity=1
                    correction += -_wallSigns[i] * wallAvoidanceStrength * 9f * proximity;
                }
            }

            return Mathf.Clamp(correction, -90f, 90f);
        }

        private float WallRaycast(Vector3 origin, Vector3 direction, float range)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, range,
                    barrierLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform)) return range;
                return hit.distance;
            }
            return range;
        }

    #endregion

    #region Rubber Band

        // Applies the current rubber band multiplier to this AI's top speed and acceleration so trailing
        // AI genuinely catch up and a leading AI genuinely eases off. Top speed goes through the boost
        // system (which owns bikeSettings.maxSpeed) when present, else is written directly, acceleration
        // is modulated directly (nothing else writes it)
        private void ApplyRubberBand()
        {
            if (controller == null) return;

            if (_boost != null)
            {
                _boost.SetRubberBandMultiplier(_rubberBandMult);
            }
            else if (_baseMaxSpeed > 0f)
            {
                float target = _baseMaxSpeed * _rubberBandMult;
                controller.bikeSettings.maxSpeed = Mathf.SmoothDamp(
                    controller.bikeSettings.maxSpeed, target, ref _rbMaxSpeedVel, 0.35f);
            }

            // Top speed only. An AI that out-accelerates the player reads as cheating the moment they
            // launch together, where a slightly higher top speed does not, so the band may ease grunt
            // off but never add it
            if (_baseAccel > 0f)
                controller.bikeSettings.acceleration = _baseAccel * Mathf.Min(_rubberBandMult, 1f);
        }

        // Returns a multiplier (1 = neutral) on the AI's base top speed + acceleration, from the race gap,
        // behind the leader >1 (catch up), well ahead of the human <1 (stay catchable)
        private float ComputeRubberBandFactor()
        {
            FarBehind01 = 0f;
            if (!enableRubberBand || raceManager == null) return 1f;

            // The band keeps a race close, it is not there to shape the getaway. Left on, the ease-off
            // half bled the AI off the line and the chase half then handed them a fraction of whatever
            // speed the player was building, which is exactly "slow away, then blows past"
            if (IsLaunching) { _launchEndedAt = Time.time; return 1f; }

            var standings = raceManager.Standings;
            float ownProgress   = float.MaxValue;
            float leadProgress  = 0f;
            float humanProgress = float.MinValue;
            Transform humanTf   = null;

            for (int i = 0; i < standings.Count; i++)
            {
                var entry = standings[i];
                if (entry.finished) continue;
                float prog = entry.TotalProgress;
                if (prog > leadProgress) leadProgress = prog;
                if (entry.ai == controller) ownProgress = prog;
                // Human entries carry no AI controller. Band against whichever human leads, so split
                // screen bands against the player actually setting the pace
                if (entry.ai == null && entry.transform != null && prog > humanProgress)
                {
                    humanProgress = prog;
                    humanTf       = entry.transform;
                }
            }

            if (ownProgress >= float.MaxValue) return 1f;

            if (humanTf != null && humanProgress > ownProgress)
                FarBehind01 = Mathf.InverseLerp(rubberBandGapFull, rubberBandFarGap, humanProgress - ownProgress);

            // Catch up headroom (km/h), full by gapFull behind the leader, then half rate up to 1.5x for
            // very large gaps so a far behind AI keeps closing instead of capping out
            float catchUpKmh = 0f;
            if (leadProgress > ownProgress)
            {
                float gapT = (leadProgress - ownProgress) / Mathf.Max(rubberBandGapFull, 0.001f);
                catchUpKmh = rubberBandMaxBoost * (Mathf.Clamp01(gapT) + 0.5f * Mathf.Clamp01(gapT - 1f));
            }

            // Crash recovery adds to catch up so a freshly-reset AI rejoins the pack
            if (_recoveryTimer > 0f) catchUpKmh += recoveryCruiseBoostKmh;

            float baseKmh = Mathf.Max(_baseMaxSpeed * 3.6f, 1f);

            // Pace matching, the strong half of the band. With a human ahead, pull this AI's top speed to
            // the human's CURRENT speed plus a margin that grows with the gap, so a player running on
            // boost, draft or a shortcut is chased at the speed they are actually doing rather than at a
            // fixed bonus over the AI's authored top speed. Only ever raises the catch up figure
            if (humanTf != null && humanProgress > ownProgress)
            {
                float humanKmh = HumanSpeedKmh(humanTf);
                if (humanKmh > paceCapFloorKmh)
                {
                    float gapT    = Mathf.Clamp01((humanProgress - ownProgress) / Mathf.Max(rubberBandGapFull, 0.001f));
                    float paceKmh = humanKmh * Mathf.Lerp(1f + rubberBandChaseMargin * gapT,
                                                          rubberBandFarMaxMultiplier, FarBehind01);

                    // The only part of the band that buys speed purely to arrive at the human, so it is the
                    // only part that has to yield to traffic: closing the last few metres never outranks
                    // having somewhere to put the bike
                    if (enableCombatCloseIn && bikeTransform != null && DodgeUrgency < 0.3f &&
                        Vector3.Distance(bikeTransform.position, humanTf.position) <= combatCloseInRange)
                        paceKmh = Mathf.Max(paceKmh, humanKmh + combatCloseInKmh);

                    catchUpKmh    = Mathf.Max(catchUpKmh, paceKmh - baseKmh);
                }
            }

            // Ease-off (km/h) bleed top speed when running well ahead of the human, so a leading AI
            // stays catchable. Banded to the human specifically, not the overall leader
            float easeKmh = 0f;
            if (humanTf != null && ownProgress > humanProgress)
            {
                float aheadT = (ownProgress - humanProgress) / Mathf.Max(rubberBandPlayerEaseGap, 0.001f);
                easeKmh = rubberBandPlayerEaseMax * Mathf.Clamp01(aheadT);
            }

            // Convert the net km/h adjustment into a multiplier on the bike's base top speed so it moves
            // the REAL cap (the old code added it to cruise, which sat above the cap and did nothing)
            float ceiling = Mathf.Lerp(rubberBandMaxMultiplier, rubberBandFarMaxMultiplier, FarBehind01);
            float mult = Mathf.Clamp(1f + (catchUpKmh - easeKmh) / baseKmh, rubberBandMinMultiplier, ceiling);

            // Applied after the clamp on purpose: the floor exists to stop the band stalling an AI in
            // traffic, it must not let a leading AI outrun the human it is supposed to stay racing
            // Only while the human is genuinely racing: at the line, or after they crash, capping the
            // field to their speed would hold the whole race at the floor instead of letting it go
            if (enablePaceCap && humanTf != null && ownProgress > humanProgress)
            {
                float humanKmh = HumanSpeedKmh(humanTf);
                if (humanKmh > paceCapFloorKmh)
                {
                    bool hunting = _combatAI != null && _combatAI.HuntingPlayer;
                    float capKmh = Mathf.Max(humanKmh * (1f + (hunting ? paceCapHuntMargin : paceCapMargin)),
                                             paceCapFloorKmh);
                    mult = Mathf.Min(mult, capKmh / baseKmh);
                }
            }

            return Mathf.Lerp(1f, mult, Mathf.Clamp01((Time.time - _launchEndedAt) / Mathf.Max(rubberBandRampIn, 0.01f)));
        }

        // Forward speed (km/h) of the human the band is tracking, resolved once and cached per transform
        private float HumanSpeedKmh(Transform humanTf)
        {
            if (humanTf != _bandHumanTf)
            {
                _bandHumanTf   = humanTf;
                _bandHumanBike = humanTf.GetComponentInChildren<BikeController>()
                              ?? humanTf.GetComponentInParent<BikeController>();
            }

            return _bandHumanBike != null ? Mathf.Abs(_bandHumanBike.localBikeVelocity.z) * 3.6f : 0f;
        }

    #endregion

    }
}
