using MotoSquid.AI;
using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Core;
using MotoSquid.Race;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MotoSquid.Traffic
{
    public enum TrafficDensity { Low, Medium, High }
    public enum GameDifficulty  { Easy, Normal, Hard }

    public class TrafficSpawner : MonoBehaviour
    {
        [Header("References")]
        public TrafficLaneNetwork laneNetwork;
        public Transform playerTransform;
        [InspectorName("AI Racer Transforms")]
        public Transform[] allRacerTransforms;
        public Camera playerCamera;
        public RaceManager raceManager;

        [Header("Showcase Lock")]
        // Every race runs Hard AI in Normal traffic whatever the menus hold, and whatever useCustomSpawnSettings
        // says. Untick to give the Difficulty and Traffic Density menus back control
        public bool lockShowcaseSettings = true;

        [Header("Inner Lane Delay")]
        public float innerLaneUnlockDelay = 5f;

        [Header("Prefabs")]
        public GameObject[] vehiclePrefabs;

        public AudioClip[] vehicleEngineClips;

        [Header("Spawn Settings")]
        public bool useCustomSpawnSettings = false;

        // Traffic is held at a density around every racer instead of spawned on a clock. A clock
        // rate divided by rising speed is a falling density, which emptied the road late in a race
        public int targetVehiclesAhead = 8;

        // AI racers only need enough traffic for their avoidance to read correctly, not the density the
        // human is weaving through. Holding all six racers at the player's target costs ~560 vehicles.
        public int aiTargetVehiclesAhead = 6;

        public float densityTickInterval = 0.1f;

        public int maxSpawnsPerTick = 1;

        public int maxVehicles = 20;

        public float spawnAheadDistance = 150f;

        public float minSpawnAheadDistance = 60f;

        public float despawnBehindDistance = 60f;

        public float laneProximityRadius = 60f;

        // A vehicle this close to a racer is never recycled, whatever the lane bookkeeping says. Kept
        // separate from spawnAheadDistance on purpose: that can be many hundreds of metres, and reusing
        // it here would mark the whole field permanently relevant and starve RecycleStalest.
        public float keepAliveRadius = 150f;

        public float laneSampleSpacing = 40f;

        public float laneLinkRadius = 40f;

        [Header("Pool Prewarm Fallback")]
        // Used only when nothing ahead of Main carried a TrafficPoolPrewarmer. 0 disables it
        public int fallbackPrewarmPerPrefab = 8;

        [Header("Lane Changing")]
        // Off for the 3 October showcase build: the change reads badly and the search behind it was
        // the largest script cost in the profiler
        public bool enableLaneChanging = false;

        [Header("Lane Distribution")]
        public int[] innerLanes = { 2, 3 };
        // Where the other lanes already have traffic, the centre pair and the racers' middle lane fill only to
        // this share of a lane's quota, so a racer usually has room to pick a line without any lane being closed
        public int[] quietLanes = { 2, 3, 4 };
        [Range(0f, 1f)] public float quietLaneShare = 0.5f;
        public float quietLaneRadius = 200f;

        [Header("Layer")]
        public string vehicleLayerName = "Traffic";

        // Speculative is the only continuous mode a kinematic body supports, and without it a racer
        // closing at 60 m/s steps straight through a car between two physics frames. It widens each
        // vehicle's broadphase bounds, so it is a switch: turn it off if traffic density costs more
        // than the tunnelling does.
        [Header("Physics")]
        public bool continuousTrafficCollision = true;
        public bool physicsDrivenTraffic = true;
        // One switch for all of this component's routine chatter. Renamed from logDespawnReasons so the
        // value serialized into Main 2 (which was on, and logged a line per despawned vehicle) is dropped.
        public bool verboseLogging = false;

        [Header("Occlusion")]
        public LayerMask occlusionMask = Physics.DefaultRaycastLayers;

        [Range(0f, 0.5f)]
        public float frustumMargin = 0.05f;

        public float vehicleProbeHeight = 2f;

        // Past this depth a vehicle is small enough that appearing is not noticeable, so it may spawn
        // in plain view. An ABSOLUTE distance on purpose: the old test used half of spawnAheadDistance,
        // so pushing the band further out also pushed the threshold, and the far half of the band was
        // never checked at all. 0 disables and requires every spawn to be hidden.
        public float farSpawnDistance = 400f;

        // How hard new vehicles are pushed to the far edge of the band. Spreading them uniformly over
        // the whole band drops them at random, and random points clump. Feeding them in at the frontier
        // and letting the racer close on them turns the same spawn rate into an even stream.
        // 1 = uniform, higher = tighter to the frontier.
        [Range(1f, 8f)]
        public float frontierBias = 3f;

        // Seconds a vehicle may go without any racer having it inside their band before it is recycled
        public float relevanceTimeout = 3f;

        [Header("Speed")]
        public float minSpeed = 20f;
        public float maxSpeed = 40f;
        public float minSpawnSeparation = 40f;

        [Header("Pre populate (Editor)")]
        public int prePopulatePerLane = 3;

        private readonly List<GameObject>      _active          = new List<GameObject>();
        [Header("Runaway Player")]
        // Extra vehicles put in front of a human who has left the AI behind, so a runaway race still has
        // something to drive through. Scales with the progress gap; 0 disables.
        public int   runawayExtraVehicles = 6;
        public float runawayLeadForFull   = 0.25f;   // TotalProgress (laps + splineT) gap for the full bonus

        private readonly List<SplineContainer> _cachedLanes     = new List<SplineContainer>();
        private bool _warnedNoPrefabs, _warnedNoLanes;

        void WarnOnce(ref bool flag, string message)
        {
            if (flag) return;
            flag = true;
            Debug.LogWarning("[TrafficSpawner] " + message, this);
        }

        private readonly List<bool>            _laneIsOncoming  = new List<bool>();
        private bool                           _innerLanesUnlocked;

        private readonly Dictionary<SplineContainer, int> _laneIndexOf = new Dictionary<SplineContainer, int>();
        private readonly List<Vector3[]>       _laneSamples = new List<Vector3[]>();
        private readonly List<bool>            _laneIsQuiet = new List<bool>();
        private readonly List<Transform>       _anchors     = new List<Transform>();
        private readonly List<Camera>          _cameras     = new List<Camera>();
        private readonly List<int>             _candidates  = new List<int>();
        private int[]                          _bandLaneCount  = System.Array.Empty<int>();
        private float[]                        _laneReferenceT = System.Array.Empty<float>();
        private float[]                        _laneLength     = System.Array.Empty<float>();
        private bool                           _reportedCapBind;
        private readonly List<bool>            _anchorIsHuman = new List<bool>();
        private readonly List<BikeAILogic> _anchorAI = new List<BikeAILogic>();
        private readonly Dictionary<GameObject, float> _lastRelevant = new Dictionary<GameObject, float>();
        private readonly List<float>           _candidateT  = new List<float>();
        private readonly List<Queue<GameObject>> _pools = new List<Queue<GameObject>>();
        private readonly Dictionary<GameObject, int> _vehiclePrefabIdx = new Dictionary<GameObject, int>();
        private readonly Dictionary<GameObject, SplineMover> _vehicleMover = new Dictionary<GameObject, SplineMover>();
        private readonly System.Predicate<GameObject> _isInactiveVehicle = v => v == null || !v.activeSelf;
        private WaitForSeconds _densityWait;
        private WaitForSeconds _despawnWait;

        [Header("Editor Test Override")]
        public bool           overrideDifficultyForTesting = false;
        public GameDifficulty testDifficulty     = GameDifficulty.Normal;
        public TrafficDensity testTrafficDensity = TrafficDensity.Medium;

        void Start()
        {
            if (laneNetwork == null)
                laneNetwork = GetComponent<TrafficLaneNetwork>();

            if (vehiclePrefabs != null)
            {
                for (int i = 0; i < vehiclePrefabs.Length; i++)
                    _pools.Add(new Queue<GameObject>());
            }

            _innerLanesUnlocked = (raceManager == null);

            BuildAnchors();
            BuildLaneLinks();
            ApplyInnerLaneLocks();
            RebuildLaneCache();

            foreach (var marker in GetComponentsInChildren<PrePopulatedTraffic>())
            {
                _active.Add(marker.gameObject);
            }

            _densityWait = new WaitForSeconds(densityTickInterval);
            _despawnWait = new WaitForSeconds(0.5f);

            if (playerCamera == null)
                playerCamera = Camera.main;

            BuildCameras();

            var prewarmer = TrafficPoolPrewarmer.Instance;
            if (prewarmer != null)
            {
                for (int i = 0; i < prewarmer.Pools.Count && i < _pools.Count; i++)
                {
                    while (prewarmer.Pools[i].Count > 0)
                    {
                        GameObject go = prewarmer.Pools[i].Dequeue();
                        go.transform.SetParent(null, worldPositionStays: true);
                        // Unparenting leaves it in the DontDestroyOnLoad scene, where it outlived a Retry and sat
                        // parked on a spline that no longer existed
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                        _vehiclePrefabIdx[go] = i;
                        _pools[i].Enqueue(go);
                    }
                }
                prewarmer.Consume();
            }

            // The prewarmer's array can be shorter than, or ordered differently to, this one, and a
            // pool it never filled would otherwise stay empty for the whole race
            PrewarmFromOwnPrefabs();

            StartCoroutine(SpawnLoop());
            StartCoroutine(DespawnLoop());
            if (raceManager != null)
                StartCoroutine(InnerLaneUnlockWatcher());
        
            if (lockShowcaseSettings)
            {
                ApplyAIDifficultyPreset(AIDifficulty.Hard);
                ApplyTrafficDensityPreset(TrafficDensity.Medium);
                Debug.Log("[TrafficSpawner] Showcase lock: Hard AI, Normal traffic.", this);
            }
            else if (overrideDifficultyForTesting)
            {
                ApplyAIDifficultyPreset((AIDifficulty)(int)testDifficulty);
                if (!useCustomSpawnSettings)
                    ApplyTrafficDensityPreset(testTrafficDensity);
            }
            else
            {
                var session = GameSession.Instance;
                int diffPreset    = session != null ? Mathf.Clamp(session.DifficultyPreset, 0, 2) : 1;
                int trafficPreset = session != null ? Mathf.Clamp(session.TrafficDensity,   0, 2) : 1;
                ApplyAIDifficultyPreset((AIDifficulty)diffPreset);
                if (!useCustomSpawnSettings)
                    ApplyTrafficDensityPreset((TrafficDensity)trafficPreset);
            }
        }

        private void RebuildLaneCache()
        {
            _cachedLanes.Clear();
            _laneIsOncoming.Clear();
            _laneIndexOf.Clear();
            _laneSamples.Clear();
            _laneIsQuiet.Clear();

            if (laneNetwork == null)
            {
                Debug.LogError("[TrafficSpawner] No Traffic Spline Generator assigned, so there are no " +
                               "lanes and no traffic will spawn.", this);
                return;
            }

            if (laneNetwork.laneSplines == null) return;

            int locked = 0;

            foreach (var road in laneNetwork.laneSplines)
            {
                foreach (var lane in road)
                {
                    if (lane == null) continue;

                    // Sampled before the lock test so the locked inner lanes are paid for at Start, under
                    // the fade, not on the unlock frame mid-race
                    var geometry = LaneGeometry(lane);

                    var info = lane.GetComponent<GeneratedTrafficLane>();
                    if (info != null && info.trafficLocked) { locked++; continue; }

                    _laneIndexOf[lane] = _cachedLanes.Count;
                    _cachedLanes.Add(lane);
                    _laneIsOncoming.Add(info != null && info.isOncoming);
                    _laneSamples.Add(geometry.samples);
                    _laneIsQuiet.Add(info != null && quietLanes != null &&
                                     System.Array.IndexOf(quietLanes, info.laneIndex) >= 0);
                }
            }

            _bandLaneCount  = new int[_cachedLanes.Count];
            _laneReferenceT = new float[_cachedLanes.Count];
            _laneLength     = new float[_cachedLanes.Count];
            for (int i = 0; i < _cachedLanes.Count; i++)
                _laneLength[i] = LaneGeometry(_cachedLanes[i]).length;

            if (_cachedLanes.Count == 0)
                Debug.LogError($"[TrafficSpawner] Lane cache is empty ({locked} lane(s) traffic-locked), " +
                               "so no traffic will spawn. Check the Traffic Spline Generator built any lanes.", this);
        }

        void ApplyInnerLaneLocks()
        {
            if (laneNetwork?.laneSplines == null) return;

            var innerSet = new HashSet<int>(innerLanes ?? System.Array.Empty<int>());

            foreach (var road in laneNetwork.laneSplines)
            {
                foreach (var lane in road)
                {
                    if (lane == null) continue;
                    var info = lane.GetComponent<GeneratedTrafficLane>();
                    if (info == null) continue;

                    info.isInnerLane   = innerSet.Contains(info.laneIndex);
                    info.trafficLocked = info.isInnerLane && !_innerLanesUnlocked;
                }
            }
        }

        // SplitScreenSetup owns the player cameras, so read them from it rather than keeping a second
        // list here that would drift out of step with it
        void BuildCameras()
        {
            _cameras.Clear();

            var split = FindFirstObjectByType<SplitScreenSetup>();
            if (split != null)
            {
                if (split.Player1Camera != null) _cameras.Add(split.Player1Camera);
                if (split.Player2Camera != null) _cameras.Add(split.Player2Camera);
            }

            if (playerCamera != null && !_cameras.Contains(playerCamera))
                _cameras.Add(playerCamera);

            if (_cameras.Count == 0)
                Debug.LogWarning("[TrafficSpawner] No player camera found, vehicles will be allowed to " +
                                 "spawn in view.", this);
        }

        // Racers are instantiated after this component's Start, so the anchor list has to be
        // retakeable or traffic keeps spawning around a playerTransform that no longer exists
        public void RefreshAnchors()
        {
            BuildAnchors();
            // RaceManager spawns the AI and hands them over here after this component's Start already applied
            // the difficulty to an empty list, so every spawned AI raced on raw prefab values
            if (_aiDifficulty.HasValue) ApplyAIDifficultyPreset(_aiDifficulty.Value);
            if (verboseLogging)
                Debug.Log($"[TrafficSpawner] Anchors rebuilt: {_anchors.Count}.", this);
        }

        void BuildAnchors()
        {
            _anchors.Clear();
            _anchorIsHuman.Clear();
            _anchorAI.Clear();

            if (playerTransform != null) AddAnchor(playerTransform);
            if (allRacerTransforms != null)
            {
                foreach (var r in allRacerTransforms)
                    if (r != null && !_anchors.Contains(r)) AddAnchor(r);
            }
        }

        // Derived rather than hand assigned so a second human player is never mistaken for AI.
        // includeInactive is required: a racer switched off in the scene would otherwise find no
        // controller and be typed HUMAN, which is the densest anchor there is
        void AddAnchor(Transform racer)
        {
            _anchors.Add(racer);
            _anchorIsHuman.Add(racer.GetComponentInChildren<BikeAIController>(true) == null);
            _anchorAI.Add(racer.GetComponentInChildren<BikeAILogic>(true));
        }

        // A racer that is switched off is not racing, so it must not hold traffic around wherever it is
        // parked - a disabled bike left in allRacerTransforms was pinning a human sized band of vehicles
        // to the start grid for the whole race. Tested per query, not at BuildAnchors, so a racer enabled
        // or disabled mid race is picked up without rebuilding the list
        static bool AnchorIsLive(Transform a) => a != null && a.gameObject.activeInHierarchy;

        // Chains every lane onto the lane its flow continues into, so a vehicle runs the whole route
        // instead of being deleted at each road section seam
        void BuildLaneLinks()
        {
            if (laneNetwork?.laneSplines == null) return;

            var all = new List<SplineContainer>();
            foreach (var road in laneNetwork.laneSplines)
                foreach (var lane in road)
                    if (lane != null) all.Add(lane);

            float radiusSq = laneLinkRadius * laneLinkRadius;
            int links = 0;

            foreach (var lane in all)
            {
                var info = lane.GetComponent<GeneratedTrafficLane>();
                if (info == null) continue;
                info.next = null;
                info.prev = null;
            }

            foreach (var lane in all)
            {
                var info = lane.GetComponent<GeneratedTrafficLane>();
                if (info == null) continue;

                LaneEndpoint(lane, 1f, out Vector3 endPos, out Vector3 endDir);

                SplineContainer best = null;
                float bestSq = radiusSq;

                foreach (var other in all)
                {
                    if (other == lane) continue;
                    var otherInfo = other.GetComponent<GeneratedTrafficLane>();
                    if (otherInfo == null || otherInfo.roadIndex == info.roadIndex) continue;
                    if (otherInfo.isOncoming != info.isOncoming) continue;

                    LaneEndpoint(other, 0f, out Vector3 startPos, out Vector3 startDir);
                    if (Vector3.Dot(endDir, startDir) < 0.7f) continue;

                    float d = (startPos - endPos).sqrMagnitude;
                    if (d < bestSq) { bestSq = d; best = other; }
                }

                if (best == null) continue;

                info.next = best;
                var bestInfo = best.GetComponent<GeneratedTrafficLane>();
                if (bestInfo != null) bestInfo.prev = lane;
                links++;
            }

            if (links == 0)
                Debug.LogWarning($"[TrafficSpawner] No lane of {all.Count} chains to another road " +
                                 $"section within {laneLinkRadius} m. Traffic will still despawn at " +
                                 "each section end, raise laneLinkRadius or check the roads meet.", this);
            else
            if (verboseLogging)
                Debug.Log($"[TrafficSpawner] Chained {links} of {all.Count} lanes across sections.", this);
        }

        void LaneEndpoint(SplineContainer lane, float t, out Vector3 pos, out Vector3 dir)
        {
            lane.Spline.Evaluate(t, out var p, out var tan, out _);
            pos = lane.transform.TransformPoint((Vector3)(Unity.Mathematics.float3)p);
            dir = lane.transform.TransformDirection((Vector3)(Unity.Mathematics.float3)tan).normalized;
        }

        // Lanes never move after generation, and resampling all of them at once on the inner lane unlock
        // was a 236 ms frame ten seconds into the race
        readonly Dictionary<SplineContainer, (Vector3[] samples, float length)> _laneGeometry =
            new Dictionary<SplineContainer, (Vector3[] samples, float length)>();

        public static TrafficSpawner Instance { get; private set; }
        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        // Lateral span, along left, of the lanes near pos whose traffic runs the way tangent points.
        // Vehicles only ever advance along increasing spline t, so sample order is the traffic direction
        public bool TryGetCarriageway(Vector3 pos, Vector3 tangent, Vector3 left, float radius,
                                      out float minLateral, out float maxLateral)
        {
            minLateral = float.MaxValue;
            maxLateral = float.MinValue;
            float radiusSq = radius * radius;

            foreach (var geometry in _laneGeometry.Values)
            {
                Vector3[] pts = geometry.samples;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector3 rel = pts[i] - pos;
                    rel.y = 0f;
                    if (rel.sqrMagnitude > radiusSq) continue;

                    Vector3 dir = pts[i + 1] - pts[i];
                    dir.y = 0f;
                    if (Vector3.Dot(dir.normalized, tangent) < 0.7f) continue;

                    float lateral = Vector3.Dot(rel, left);
                    minLateral = Mathf.Min(minLateral, lateral);
                    maxLateral = Mathf.Max(maxLateral, lateral);
                }
            }
            return minLateral <= maxLateral;
        }

        readonly Dictionary<SplineContainer, SplineContainer> _laneNext = new Dictionary<SplineContainer, SplineContainer>();

        // Where a vehicle will be after the given seconds, following its lane (and the lane it chains
        // onto) through the cached samples. A straight line off its velocity puts a car ~10 m off its
        // lane within 2 s in a 170 m bend, which is where the AI kept hitting traffic
        public bool TryPredict(SplineMover mover, float seconds, out Vector3 position)
        {
            position = default;
            SplineContainer lane = mover.CurrentContainer;
            if (lane == null || mover.IsChangingLane || !_laneGeometry.TryGetValue(lane, out var geometry))
                return false;

            float distance = mover.NormalizedT * geometry.length + mover.Velocity.magnitude * seconds;
            for (int guard = 0; guard < 4 && distance > geometry.length; guard++)
            {
                if (!_laneNext.TryGetValue(lane, out SplineContainer next))
                {
                    var info = lane.GetComponent<GeneratedTrafficLane>();
                    next = info != null ? info.next : null;
                    _laneNext[lane] = next;
                }
                if (next == null || !_laneGeometry.TryGetValue(next, out var nextGeometry)) break;

                distance -= geometry.length;
                lane = next;
                geometry = nextGeometry;
            }

            Vector3[] pts = geometry.samples;
            float index = Mathf.Clamp01(distance / Mathf.Max(geometry.length, 1f)) * (pts.Length - 1);
            int i = Mathf.Min((int)index, pts.Length - 2);
            position = Vector3.Lerp(pts[i], pts[i + 1], index - i);
            return true;
        }

        (Vector3[] samples, float length) LaneGeometry(SplineContainer lane)
        {
            if (!_laneGeometry.TryGetValue(lane, out var geometry))
            {
                geometry = (SampleLane(lane), lane.Spline.GetLength());
                _laneGeometry[lane] = geometry;
            }
            return geometry;
        }

        // A coarse arc of world points per lane. Finding the nearest of these is far cheaper than
        // SplineUtility.GetNearestPoint on a 700 knot lane, and the band is 200 m wide so it is ample
        Vector3[] SampleLane(SplineContainer lane)
        {
            float length = lane.Spline.GetLength();
            int count = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(1f, laneSampleSpacing)) + 1);
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                lane.Spline.Evaluate((float)i / (count - 1), out var p, out _, out _);
                points[i] = lane.transform.TransformPoint((Vector3)(Unity.Mathematics.float3)p);
            }
            return points;
        }

        float NearestSampleT(int laneIdx, Vector3 pos, out float distSq)
        {
            Vector3[] points = _laneSamples[laneIdx];
            int best = 0;
            distSq = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                float d = (points[i] - pos).sqrMagnitude;
                if (d < distSq) { distSq = d; best = i; }
            }
            return (float)best / (points.Length - 1);
        }

        IEnumerator InnerLaneUnlockWatcher()
        {
            while (raceManager.State != RaceManager.RaceState.Racing)
            {
                if (raceManager == null)
                    yield break;
                if (raceManager.State == RaceManager.RaceState.Finished)
                    yield break;
                yield return null;
            }

            yield return new WaitForSeconds(innerLaneUnlockDelay);

            _innerLanesUnlocked = true;
            ApplyInnerLaneLocks();
            RebuildLaneCache();
        }

        IEnumerator SpawnLoop()
        {
            yield return null;

            while (true)
            {
                yield return _densityWait;

                _active.RemoveAll(_isInactiveVehicle);

                // Both of these used to be bare continues, so a misconfigured spawner produced no traffic
                // and no message for the whole race
                if (vehiclePrefabs == null || vehiclePrefabs.Length == 0)
                {
                    WarnOnce(ref _warnedNoPrefabs, "Vehicle Prefabs is empty, so no traffic will spawn.");
                    continue;
                }
                if (_cachedLanes.Count == 0)
                {
                    WarnOnce(ref _warnedNoLanes, "No traffic lanes are cached, so no traffic will spawn.");
                    continue;
                }

                // Every anchor must be visited even when the field is at maxVehicles: TopUpAroundAnchor
                // is what refreshes relevance, and skipping it at the cap left every timestamp stale so
                // DespawnLoop recycled the whole field one relevanceTimeout later. The cap is handled
                // inside, after the refresh
                // Humans first. At the cap whoever is visited last gets nothing, and a player on empty
                // road notices it in a way a trailing AI never will. Every anchor is still visited, so
                // the relevance refresh that keeps the field alive is unaffected.
                for (int pass = 0; pass < 2; pass++)
                {
                    bool humanPass = pass == 0;
                    for (int i = 0; i < _anchors.Count; i++)
                    {
                        if (_anchorIsHuman[i] != humanPass) continue;
                        TopUpAroundAnchor(_anchors[i], _anchorIsHuman[i], _anchorAI[i]);
                        yield return null;
                    }
                }
            }
        }

        void TopUpAroundAnchor(Transform anchor, bool isHuman, BikeAILogic ai)
        {
            if (!AnchorIsLive(anchor)) return;

            Vector3 pos = anchor.position;

            BuildCandidates(pos);

            // Keeping vehicles alive is not a side effect of having somewhere to spawn. This used to sit
            // below the early return, so an anchor further than laneProximityRadius from every cached
            // lane - a junction, a jump, a section that is not cached - refreshed nothing at all and the
            // whole field despawned together one relevanceTimeout later.
            CountBandPerLane(pos);

            // An AI this far back is off every screen and has to make up the gap, so it gets an empty road
            if (!isHuman && ai != null && ai.FarBehind01 > 0f)
            {
                ClearAheadOfFarAI();
                return;
            }

            if (_candidates.Count == 0) return;

            int target = isHuman ? targetVehiclesAhead + RunawayBonus() : aiTargetVehiclesAhead;
            float perLaneTarget = (float)target / _candidates.Count;

            for (int n = 0; n < maxSpawnsPerTick; n++)
            {
                if (maxVehicles > 0 && _active.Count >= maxVehicles && !(isHuman ? ReclaimForHuman() : RecycleStalest()))
                {
                    if (!_reportedCapBind)
                    {
                        _reportedCapBind = true;
                        Debug.LogWarning($"[TrafficSpawner] maxVehicles ({maxVehicles}) is below what " +
                                         "the racers ask for, and every vehicle is still in someone's " +
                                         "band, so top ups stop here and gaps will open. Lower " +
                                         "targetVehiclesAhead rather than maxVehicles: the cap is a " +
                                         "ceiling, not a density control.", this);
                    }
                    return;
                }
                if (!TrySpawnAhead(perLaneTarget)) return;
            }
        }

        int RunawayBonus()
        {
            if (runawayExtraVehicles <= 0 || raceManager == null) return 0;

            float human = float.MinValue, bestAI = float.MinValue;
            var standings = raceManager.Standings;
            for (int i = 0; i < standings.Count; i++)
            {
                var r = standings[i];
                if (r == null || r.transform == null) continue;
                if (r.ai == null) { if (r.TotalProgress > human)  human  = r.TotalProgress; }
                else              { if (r.TotalProgress > bestAI) bestAI = r.TotalProgress; }
            }

            if (human == float.MinValue || bestAI == float.MinValue || human <= bestAI) return 0;

            float lead = Mathf.Clamp01((human - bestAI) / Mathf.Max(runawayLeadForFull, 0.001f));
            return Mathf.RoundToInt(lead * runawayExtraVehicles);
        }

        // A car the player has passed stays inside every trailing AI's band, so its timestamp never goes
        // stale and RecycleStalest refused the player all race, emptying the road ahead as the field
        // spread out. The player takes the car furthest from any human that is outside this player's
        // band and out of view, so it is always an AI's car that goes
        bool ReclaimForHuman()
        {
            GameObject donor = null;
            float farthestSq = -1f;

            for (int i = 0; i < _active.Count; i++)
            {
                GameObject v = _active[i];
                if (v == null || !v.activeSelf) continue;

                Vector3 p = v.transform.position;
                if (NearAnyHumanAnchor(p) || TryBandPosition(v, out _, out _) || IsVisibleToCamera(p)) continue;

                float dSq = NearestHumanDistanceSq(p);
                if (dSq > farthestSq) { farthestSq = dSq; donor = v; }
            }

            if (donor == null) return false;

            _active.Remove(donor);
            _lastRelevant.Remove(donor);
            ReturnToPool(donor);
            return true;
        }

        // Uses the band BuildCandidates just took for this AI, and never removes a car a player could see
        void ClearAheadOfFarAI()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                GameObject v = _active[i];
                if (v == null || !v.activeSelf) continue;
                if (!TryBandPosition(v, out _, out float along) || along < 0f) continue;

                Vector3 p = v.transform.position;
                if (NearAnyHumanAnchor(p) || IsVisibleToCamera(p)) continue;

                _active.RemoveAt(i);
                _lastRelevant.Remove(v);
                ReturnToPool(v);
            }
        }

        float NearestHumanDistanceSq(Vector3 pos)
        {
            float best = float.MaxValue;
            for (int i = 0; i < _anchors.Count; i++)
                if (_anchorIsHuman[i] && AnchorIsLive(_anchors[i]))
                    best = Mathf.Min(best, (_anchors[i].position - pos).sqrMagnitude);
            return best;
        }

        bool RecycleStalest()
        {
            GameObject stalest = null;
            float oldest = float.MaxValue;

            for (int i = 0; i < _active.Count; i++)
            {
                GameObject v = _active[i];
                if (v == null) continue;
                if (NearAnyAnchor(v.transform.position)) continue;
                float seen = _lastRelevant.TryGetValue(v, out float t) ? t : 0f;
                if (seen < oldest) { oldest = seen; stalest = v; }
            }

            if (stalest == null || Time.time - oldest < densityTickInterval * 4f) return false;

            _active.Remove(stalest);
            _lastRelevant.Remove(stalest);
            ReturnToPool(stalest);
            return true;
        }

        // The band refresh in CountBandPerLane only runs for anchors already reached this tick, and it
        // needs lane bookkeeping to agree. Recycling is the one path that can delete a car in plain view,
        // so it checks the racers directly rather than trusting the timestamps
        bool NearAnyAnchor(Vector3 pos)
        {
            float keepSq = keepAliveRadius * keepAliveRadius;
            for (int i = 0; i < _anchors.Count; i++)
            {
                Transform a = _anchors[i];
                if (AnchorIsLive(a) && (a.position - pos).sqrMagnitude <= keepSq) return true;
            }
            return false;
        }

        bool NearAnyHumanAnchor(Vector3 pos)
        {
            float keepSq = keepAliveRadius * keepAliveRadius;
            for (int i = 0; i < _anchors.Count; i++)
            {
                if (!_anchorIsHuman[i]) continue;
                Transform a = _anchors[i];
                if (AnchorIsLive(a) && (a.position - pos).sqrMagnitude <= keepSq) return true;
            }
            return false;
        }

        void BuildCandidates(Vector3 pos)
        {
            _candidates.Clear();
            _candidateT.Clear();
            for (int i = 0; i < _laneReferenceT.Length; i++) _laneReferenceT[i] = -1f;

            float lateralSq = laneProximityRadius * laneProximityRadius;

            for (int i = 0; i < _cachedLanes.Count; i++)
            {
                float t = NearestSampleT(i, pos, out float distSq);
                if (distSq > lateralSq) continue;

                _laneReferenceT[i] = t;
                _candidates.Add(i);
                _candidateT.Add(t);
            }
        }

        void CountBandPerLane(Vector3 anchorPos)
        {
            System.Array.Clear(_bandLaneCount, 0, _bandLaneCount.Length);

            float near = NearSpawnDistance();
            float keepSq = keepAliveRadius * keepAliveRadius;

            foreach (var v in _active)
            {
                if (v == null || !v.activeSelf) continue;

                // A vehicle this close to a racer is relevant whether or not the lane bookkeeping
                // agrees with that, so one bad frame of lane matching cannot recycle it in plain view
                if ((v.transform.position - anchorPos).sqrMagnitude <= keepSq)
                    _lastRelevant[v] = Time.time;

                if (!TryBandPosition(v, out int lane, out float along)) continue;

                _lastRelevant[v] = Time.time;
                if (along >= near) _bandLaneCount[lane]++;
            }
        }

        // Where a vehicle sits in the band of the anchor last passed to BuildCandidates, forward positive
        bool TryBandPosition(GameObject v, out int lane, out float along)
        {
            lane = -1;
            along = 0f;
            if (!_vehicleMover.TryGetValue(v, out SplineMover mover)) return false;
            if (mover == null || mover.CurrentContainer == null) return false;
            if (!_laneIndexOf.TryGetValue(mover.CurrentContainer, out lane)) return false;

            float referenceT = _laneReferenceT[lane];
            if (referenceT < 0f) return false;

            float delta = _laneIsOncoming[lane] ? referenceT - mover.NormalizedT
                                                : mover.NormalizedT - referenceT;
            along = delta * _laneLength[lane];
            return along <= spawnAheadDistance && along >= -despawnBehindDistance;
        }

        bool TrySpawnAhead(float perLaneTarget)
        {

            while (_candidates.Count > 0)
            {
                int pick = PickCandidate(perLaneTarget);
                int laneIdx = _candidates[pick];
                float referenceT = _candidateT[pick];
                _candidates.RemoveAt(pick);
                _candidateT.RemoveAt(pick);

                if (_bandLaneCount[laneIdx] >= perLaneTarget) continue;

                if (!ResolveSpawnPoint(laneIdx, referenceT, out SplineContainer lane, out float spawnT))
                    continue;

                Vector3 spawnPos = EvaluateLaneWorldPos(lane, spawnT);
                if (IsVisibleToCamera(spawnPos)) continue;
                if (IsTooCloseToExistingVehicle(spawnPos, lane)) continue;
                // A quota, not a per-attempt coin flip: a refused spawn is retried every tick, so a coin flip
                // only delayed the car by a fraction of a second
                if (_laneIsQuiet[laneIdx] && _bandLaneCount[laneIdx] >= perLaneTarget * quietLaneShare &&
                    OtherLaneTrafficNear(lane, spawnPos)) continue;

                SpawnVehicle(lane, spawnT);
                _bandLaneCount[laneIdx]++;
                return true;
            }

            return false;
        }

        bool OtherLaneTrafficNear(SplineContainer lane, Vector3 pos)
        {
            float radiusSq = quietLaneRadius * quietLaneRadius;
            foreach (var v in _active)
            {
                if (v == null || !v.activeSelf) continue;
                if (_vehicleMover.TryGetValue(v, out SplineMover mover) && mover != null &&
                    mover.CurrentContainer == lane) continue;
                if ((v.transform.position - pos).sqrMagnitude <= radiusSq) return true;
            }
            return false;
        }

        int PickCandidate(float perLaneTarget)
        {
            float total = 0f;
            for (int i = 0; i < _candidates.Count; i++) total += CandidateWeight(i, perLaneTarget);

            float roll = Random.Range(0f, total);
            float run  = 0f;
            for (int i = 0; i < _candidates.Count; i++)
            {
                run += CandidateWeight(i, perLaneTarget);
                if (roll <= run) return i;
            }
            return _candidates.Count - 1;
        }

        // The emptiest lane relative to its own share wins. The small floor keeps a lane that is already
        // at target reachable, so a full lane never blocks a spawn outright.
        float CandidateWeight(int candidate, float perLaneTarget)
        {
            int laneIdx = _candidates[candidate];
            return Mathf.Max(0f, perLaneTarget - _bandLaneCount[laneIdx]) + 0.05f;
        }

        // Lanes are open per section curves, so the band can run past the end of one. Carrying it onto
        // the linked lane is what keeps traffic present through a section seam, where it used to stop
        bool ResolveSpawnPoint(int laneIdx, float referenceT, out SplineContainer lane, out float spawnT)
        {
            lane   = _cachedLanes[laneIdx];
            bool isOncoming = _laneIsOncoming[laneIdx];

            spawnT = GetSpawnOffset(referenceT, isOncoming, _laneLength[laneIdx]);
            if (spawnT >= 0f) return true;

            var info = lane.GetComponent<GeneratedTrafficLane>();
            SplineContainer beyond = info == null ? null : (isOncoming ? info.prev : info.next);
            if (beyond == null || !_laneIndexOf.ContainsKey(beyond)) return false;

            float length = beyond.Spline.GetLength();
            if (length < 1f) return false;

            float near = NearSpawnDistance() / length;
            float far  = Mathf.Min(1f, spawnAheadDistance / length);
            if (far - near < 0.01f) return false;

            float into = Random.Range(near, far);
            lane   = beyond;
            spawnT = isOncoming ? 1f - into : into;
            return true;
        }

        IEnumerator DespawnLoop()
        {
            while (true)
            {
                yield return _despawnWait;

                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    GameObject vehicle = _active[i];
                    if (vehicle == null)
                    {
                        _active.RemoveAt(i);
                        continue;
                    }

                    bool tracked = _lastRelevant.TryGetValue(vehicle, out float seen);
                    bool shouldDespawn = !tracked || Time.time - seen > relevanceTimeout;
                    string reason = !tracked ? "never entered the band"
                                  : shouldDespawn ? $"out of band for {Time.time - seen:F1}s" : null;

                    _vehicleMover.TryGetValue(vehicle, out SplineMover mover);
                    if (!shouldDespawn && mover != null && !mover.IsPlaying)
                    {
                        shouldDespawn = true;
                        reason = "ran off the end of its lane";
                    }

                    if (shouldDespawn)
                    {
                        if (verboseLogging)
                            Debug.Log($"[TrafficSpawner] despawn '{vehicle.name}': {reason} " +
                                      $"(t={(mover != null ? mover.NormalizedT : -1f):F3} " +
                                      $"speed={(mover != null ? mover.MaxSpeed : -1f):F1} " +
                                      $"lane={(mover != null && mover.CurrentContainer != null ? mover.CurrentContainer.name : "none")})",
                                      vehicle);
                        _active.RemoveAt(i);
                        _lastRelevant.Remove(vehicle);
                        ReturnToPool(vehicle);
                    }
                }
            }
        }

        // Measured in metres so the band can shrink to whatever road is left before a junction rather
        // than being rejected whole. Traffic thinning out towards a dead end is right; the band going
        // from full to nothing the moment it overruns the section end is not.
        float GetSpawnOffset(float referenceT, bool isOncoming, float splineLength)
        {
            if (splineLength < 1f) return -1f;

            float near = NearSpawnDistance();
            float remaining = (isOncoming ? referenceT : 1f - referenceT) * splineLength;
            if (remaining < near) return -1f;

            float far = Mathf.Min(spawnAheadDistance, remaining);
            float distance = Mathf.Lerp(far, near, Mathf.Pow(Random.value, frontierBias));
            float offset = distance / splineLength;

            return Mathf.Clamp01(isOncoming ? referenceT - offset : referenceT + offset);
        }

        Vector3 EvaluateLaneWorldPos(SplineContainer lane, float t)
        {
            lane.Spline.Evaluate(t, out var pos, out _, out _);
            return lane.transform.TransformPoint((Vector3)(Unity.Mathematics.float3)pos);
        }

        const float SEPARATION_FLOOR = 12f;

        bool IsTooCloseToExistingVehicle(Vector3 spawnPos, SplineContainer lane)
        {
            float separation = Mathf.Max(minSpawnSeparation, SEPARATION_FLOOR);
            if (separation <= 0f) return false;
            float minDistSq = separation * separation;
            foreach (var v in _active)
            {
                if (v == null || !v.activeSelf) continue;
                if (lane != null && _vehicleMover.TryGetValue(v, out SplineMover m)
                    && m != null && m.CurrentContainer != lane)
                    continue;
                if ((v.transform.position - spawnPos).sqrMagnitude < minDistSq)
                    return true;
            }
            return false;
        }

        // A point counts as visible when any live player camera has it inside the frustum with nothing
        // between. The old test skipped everything past half of spawnAheadDistance, so on a 200-500 m
        // band almost every spawn went unchecked and vehicles appeared in plain sight.
        bool IsVisibleToCamera(Vector3 worldPos)
        {
            int trafficLayer = LayerMask.NameToLayer(vehicleLayerName);
            int staticMask = trafficLayer >= 0 ? occlusionMask & ~(1 << trafficLayer) : occlusionMask.value;

            foreach (Camera cam in _cameras)
            {
                if (cam == null || !cam.isActiveAndEnabled) continue;

                // A vehicle is not a point, so probe its roofline too or it appears over a low wall
                if (IsPointVisible(cam, worldPos, staticMask)) return true;
                if (IsPointVisible(cam, worldPos + Vector3.up * vehicleProbeHeight, staticMask)) return true;
            }

            return false;
        }

        bool IsPointVisible(Camera cam, Vector3 worldPos, int staticMask)
        {
            Vector3 view = cam.WorldToViewportPoint(worldPos);
            if (view.z <= cam.nearClipPlane || view.z > cam.farClipPlane) return false;
            if (farSpawnDistance > 0f && view.z > farSpawnDistance) return false;
            if (view.x < -frustumMargin || view.x > 1f + frustumMargin) return false;
            if (view.y < -frustumMargin || view.y > 1f + frustumMargin) return false;

            return !Physics.Linecast(cam.transform.position, worldPos, staticMask);
        }

        float NearSpawnDistance() => Mathf.Clamp(minSpawnAheadDistance, 0f, spawnAheadDistance * 0.95f);

        void SpawnVehicle(SplineContainer lane, float startOffset)
        {
            int prefabIndex = Random.Range(0, vehiclePrefabs.Length);
            GameObject prefab = vehiclePrefabs[prefabIndex];
            if (prefab == null) return;

            lane.Spline.Evaluate(startOffset, out var pos, out var tan, out var up);
            Vector3 worldPos = lane.transform.TransformPoint((Vector3)(Unity.Mathematics.float3)pos);
            Vector3 worldUp  = lane.transform.TransformDirection((Vector3)(Unity.Mathematics.float3)up);
            if (worldUp.sqrMagnitude < 0.001f) worldUp = Vector3.up;
            Quaternion worldRot = Quaternion.LookRotation(
                lane.transform.TransformDirection((Vector3)(Unity.Mathematics.float3)tan),
                worldUp);

            GameObject vehicle = GetFromPool(prefabIndex, prefab, worldPos, worldRot);

            int layer = LayerMask.NameToLayer(vehicleLayerName);
            if (layer >= 0) SetLayerRecursive(vehicle, layer);


            EnsureTrafficPhysics(vehicle);

            SplineMover mover = vehicle.GetComponent<SplineMover>();
            if (mover == null) mover = vehicle.AddComponent<SplineMover>();
            mover.driveThroughPhysics = physicsDrivenTraffic;

            float speed = Random.Range(minSpeed, maxSpeed);
            mover.Setup(lane, startOffset, speed, false);
            vehicle.SetActive(true);

            var following = vehicle.GetComponent<VehicleFollowing>();
            if (following == null) following = vehicle.AddComponent<VehicleFollowing>();
            if (following.obstacleMask == 0 && layer >= 0)
                following.obstacleMask = 1 << layer;  
            following.nominalSpeed = speed;
            following.ResetForSpawn();

            var changer = vehicle.GetComponent<TrafficLaneChanger>();
            if (enableLaneChanging && laneNetwork != null && layer >= 0)
            {
                if (changer == null) changer = vehicle.AddComponent<TrafficLaneChanger>();
                changer.generator   = laneNetwork;
                changer.trafficMask = 1 << layer;
                changer.enabled     = true;
            }
            else if (changer != null) changer.enabled = false;

            if (vehicleEngineClips != null && prefabIndex < vehicleEngineClips.Length
                && vehicleEngineClips[prefabIndex] != null)
            {
                var engineSound = vehicle.GetComponent<TrafficEngineSound>();
                if (engineSound == null) engineSound = vehicle.AddComponent<TrafficEngineSound>();
                engineSound.Init(mover, vehicleEngineClips[prefabIndex], maxSpeed);
            }

            _active.Add(vehicle);
            _vehicleMover[vehicle] = mover;
            _lastRelevant[vehicle] = Time.time;
        }

        void EnsureTrafficPhysics(GameObject vehicle)
        {
            if (vehicle.GetComponentInChildren<Collider>() == null)
            {
                var renderers = vehicle.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Transform root = vehicle.transform;
                    bool has = false;
                    Bounds local = default;
                    foreach (var r in renderers)
                    {
                        Bounds wb = r.bounds;
                        for (int c = 0; c < 8; c++)
                        {
                            Vector3 corner = wb.center + Vector3.Scale(wb.extents,
                                new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                            Vector3 lp = root.InverseTransformPoint(corner);
                            if (!has) { local = new Bounds(lp, Vector3.zero); has = true; }
                            else local.Encapsulate(lp);
                        }
                    }
                    if (has)
                    {
                        var box = vehicle.AddComponent<BoxCollider>();
                        box.center    = local.center;
                        box.size      = local.size;
                        box.isTrigger = false;
                    }
                }
            }

            var rb = vehicle.GetComponent<Rigidbody>();
            if (rb == null) rb = vehicle.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = continuousTrafficCollision
                ? CollisionDetectionMode.ContinuousSpeculative
                : CollisionDetectionMode.Discrete;
        }

        // Sourced from vehiclePrefabs rather than a second list, so this pool cannot drift out of
        // index with what actually spawns the way the prewarmer's own list did
        void PrewarmFromOwnPrefabs()
        {
            if (vehiclePrefabs == null || fallbackPrewarmPerPrefab <= 0) return;

            for (int i = 0; i < vehiclePrefabs.Length && i < _pools.Count; i++)
            {
                if (vehiclePrefabs[i] == null) continue;
                if (_pools[i].Count > 0) continue;

                if (verboseLogging)
                    Debug.Log($"[TrafficSpawner] Pool {i} ('{vehiclePrefabs[i].name}') was empty after " +
                              $"the prewarm handover, filling {fallbackPrewarmPerPrefab} locally.", this);

                for (int j = 0; j < fallbackPrewarmPerPrefab; j++)
                {
                    GameObject go = Instantiate(vehiclePrefabs[i]);
                    go.SetActive(false);
                    _vehiclePrefabIdx[go] = i;
                    _pools[i].Enqueue(go);
                }
            }
        }

        GameObject GetFromPool(int prefabIndex, GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefabIndex < _pools.Count && _pools[prefabIndex].Count > 0)
            {
                GameObject pooled = _pools[prefabIndex].Dequeue();
                pooled.transform.SetPositionAndRotation(position, rotation);

                _vehiclePrefabIdx[pooled] = prefabIndex;
                return pooled;
            }

            GameObject vehicle = Instantiate(prefab, position, rotation);
            vehicle.name = $"{prefab.name}_Traffic";
            _vehiclePrefabIdx[vehicle] = prefabIndex;
            return vehicle;
        }

        void ReturnToPool(GameObject vehicle)
        {
            vehicle.SetActive(false);
            _vehicleMover.Remove(vehicle);

            if (_vehiclePrefabIdx.TryGetValue(vehicle, out int idx) && idx < _pools.Count)
            {
                _vehiclePrefabIdx.Remove(vehicle);
                _pools[idx].Enqueue(vehicle);
                return;
            }

            _vehiclePrefabIdx.Remove(vehicle);
            Destroy(vehicle);
        }

        internal static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        // High is the density the showcase was tuned in (Main 2's custom settings, 2026-09-29); Medium and
        // Low scale the car counts to about two thirds and a half with spacing opened to match. Traffic speed
        // is left alone on purpose: slower traffic raises closing speed and makes a race harder, not easier
        public void ApplyTrafficDensityPreset(TrafficDensity density)
        {
            minSpeed = 20f;
            maxSpeed = 40f;
            switch (density)
            {
                case TrafficDensity.Low:
                    maxVehicles           = 8;
                    targetVehiclesAhead   = 3;
                    aiTargetVehiclesAhead = 1;
                    minSpawnSeparation    = 150f;
                    break;
                case TrafficDensity.Medium:
                    maxVehicles           = 11;
                    targetVehiclesAhead   = 4;
                    aiTargetVehiclesAhead = 1;
                    minSpawnSeparation    = 120f;
                    break;
                case TrafficDensity.High:
                    maxVehicles           = 15;
                    targetVehiclesAhead   = 6;
                    aiTargetVehiclesAhead = 2;
                    minSpawnSeparation    = 90f;
                    break;
            }
        }

        // Applies a matched traffic density + AI avoidance preset in one call
        // Easy:   low density + forgiving AI dodge
        // Normal: medium density + standard AI dodge
        // Hard:   high density + sharp AI dodge
        public void ApplyGameDifficultyPreset(GameDifficulty difficulty)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy:
                    ApplyTrafficDensityPreset(TrafficDensity.Low);
                    ApplyAIDifficultyPreset(AIDifficulty.Easy);
                    break;
                case GameDifficulty.Normal:
                    ApplyTrafficDensityPreset(TrafficDensity.Medium);
                    ApplyAIDifficultyPreset(AIDifficulty.Normal);
                    break;
                case GameDifficulty.Hard:
                    ApplyTrafficDensityPreset(TrafficDensity.High);
                    ApplyAIDifficultyPreset(AIDifficulty.Hard);
                    break;
            }
        }

        AIDifficulty? _aiDifficulty;

        private void ApplyAIDifficultyPreset(AIDifficulty difficulty)
        {
            _aiDifficulty = difficulty;
            if (allRacerTransforms == null) return;
            int applied = 0;
            foreach (var t in allRacerTransforms)
            {
                if (t == null) continue;
                // includeInactive: a bike disabled at scene load still has to be configured, or enabling
                // it later drops it into the race on raw prefab values while the rest of the field is preset
                var aiLogic = t.GetComponentInChildren<BikeAILogic>(true);
                if (aiLogic != null) { aiLogic.ApplyDifficultyPreset(difficulty); applied++; }
            }
            if (applied > 0) Debug.Log($"[TrafficSpawner] {difficulty} difficulty applied to {applied} AI.", this);
        }

        void TestApplyDifficulty(AIDifficulty d)
        {
            if (!Application.isPlaying)
            { Debug.LogWarning("[TrafficSpawner] Enter Play mode to live switch difficulty.", this); return; }
            ApplyAIDifficultyPreset(d);
            Debug.Log($"[TrafficSpawner] AI difficulty → {d}", this);
        }

        void TestApplyDensity(TrafficDensity density)
        {
            if (!Application.isPlaying)
            { Debug.LogWarning("[TrafficSpawner] Enter Play mode to live switch traffic density.", this); return; }
            ApplyTrafficDensityPreset(density);
            Debug.Log($"[TrafficSpawner] Traffic density {density}", this);
        }

        [ContextMenu("Test ▸ AI Difficulty ▸ Easy")]   void TestDiffEasy()   => TestApplyDifficulty(AIDifficulty.Easy);
        [ContextMenu("Test ▸ AI Difficulty ▸ Normal")] void TestDiffNormal() => TestApplyDifficulty(AIDifficulty.Normal);
        [ContextMenu("Test ▸ AI Difficulty ▸ Hard")]   void TestDiffHard()   => TestApplyDifficulty(AIDifficulty.Hard);

        [ContextMenu("Test ▸ Traffic Density ▸ Low")]    void TestDensityLow()    => TestApplyDensity(TrafficDensity.Low);
        [ContextMenu("Test ▸ Traffic Density ▸ Medium")] void TestDensityMedium() => TestApplyDensity(TrafficDensity.Medium);
        [ContextMenu("Test ▸ Traffic Density ▸ High")]   void TestDensityHigh()   => TestApplyDensity(TrafficDensity.High);

        public void EditorPrePopulate()
        {
#if UNITY_EDITOR
            if (laneNetwork == null)
                laneNetwork = GetComponent<TrafficLaneNetwork>();

            if (laneNetwork == null || vehiclePrefabs == null || vehiclePrefabs.Length == 0)
            {
                return;
            }

            EditorClearPrePopulated();

            foreach (var road in laneNetwork.laneSplines)
            {
                foreach (var lane in road)
                {
                    if (lane == null) continue;

                    for (int i = 0; i < prePopulatePerLane; i++)
                    {
                        float offset = (float)i / prePopulatePerLane;

                        lane.Spline.Evaluate(offset, out var pos, out var tan, out var up);
                        Vector3 worldPos = lane.transform.TransformPoint((Vector3)(Unity.Mathematics.float3)pos);
                        Quaternion worldRot = Quaternion.LookRotation(
                            lane.transform.TransformDirection((Vector3)(Unity.Mathematics.float3)tan),
                            Vector3.up);

                        GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
                        GameObject vehicle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
                        vehicle.name = $"{prefab.name}_PrePop";

                        SplineMover existing = vehicle.GetComponent<SplineMover>();
                        if (existing != null) existing.enabled = false;

                        var marker = vehicle.AddComponent<PrePopulatedTraffic>();
                        marker.laneContainer = lane;
                        marker.startOffset = offset;
                        marker.speed = Random.Range(minSpeed, maxSpeed);

                        vehicle.transform.SetPositionAndRotation(worldPos, worldRot);
                        EditorUtility.SetDirty(vehicle);
                    }
                }
            }

            EditorUtility.SetDirty(gameObject);
#endif
        }

        public void EditorClearPrePopulated()
        {
            var markers = GetComponentsInChildren<PrePopulatedTraffic>();
            foreach (var m in markers)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(m.gameObject);
                else
#endif
                    Destroy(m.gameObject);
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(TrafficSpawner))]
    public class TrafficSpawnerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            var spawner = (TrafficSpawner)target;

            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
            if (GUILayout.Button("Pre populate Traffic", GUILayout.Height(36)))
            {
                Undo.RegisterFullObjectHierarchyUndo(spawner.gameObject, "Pre populate Traffic");
                spawner.EditorPrePopulate();
            }

            GUI.backgroundColor = new Color(1f, 0.6f, 0.4f);
            if (GUILayout.Button("Clear Pre populated", GUILayout.Height(28)))
            {
                Undo.RegisterFullObjectHierarchyUndo(spawner.gameObject, "Clear Pre populated Traffic");
                spawner.EditorClearPrePopulated();
            }

            GUI.backgroundColor = Color.white;
        }
    }
#endif
}
