using MotoSquid.AI;
using MotoSquid.Bike;
using MotoSquid.Combat;
using MotoSquid.Controls;
using MotoSquid.Core;
using MotoSquid.Rider;
using MotoSquid.Track;
using MotoSquid.Traffic;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace MotoSquid.Race
{
    [DefaultExecutionOrder(10)]
    public class RaceManager : MonoBehaviour
    {

        [Header("Race Route")]
        public RouteGraph routeGraph;

        [Header("Start Line")]
        public Transform startLine;        // Assign the start line object, its forward = race direction
        public bool      flipGridDirection; // Flip if bikes face the wrong way

        public int totalLaps = 3;

        [Header("Racers")]
        public Transform playerTransform;
        public BikeController playerBike;

        [Header("Split Screen/Player 2")]
        public Transform playerTransform2;
        public BikeController playerBike2;

        public BikeAIController[] aiOpponents;

        [Header("AI Spawning")]
        public GameObject aiPrefab;
        public int aiCount = 7;

        [Header("Countdown")]
        public float countdownDuration = 3f;

        [Header("AI Start Line Burnout")]
        [Range(0f, 1f)] public float aiBurnoutChance = 0.25f;  // Per AI chance to burn out on the grid

        [Header("Debug")]
        public bool autoStartWithoutPlayer = false;
        public bool positionPlayerOnGrid = true;
        public bool skipCountdown = false;

        [Header("Grid Spawn")]
        public float spawnHeightAboveGround = 0.7f;
        public LayerMask groundSnapMask = ~0;
        public float groundRayUp   = 40f;
        public float groundRayDown = 60f;
        // Placed exactly on the surface, the chassis sits low enough that the wheel rays start underneath
        // it and report airborne, which silently killed the start line burnout (it needs both wheels
        // grounded). A small clearance the bike settles out of before the fade is up.
        public float gridClearance = 0.5f;
        // Everything except the racers and traffic, so a missed mask still lands the bike on the track
        public LayerMask groundSnapFallbackMask = ~0;
        public enum RaceState { Waiting, Countdown, Racing, Finished }
        public RaceState State { get; private set; } = RaceState.Waiting;

        // Resolved from GameSession in Start (fall back to Inspector defaults if no session exists)
        [Header("Mode (defaults; overridden by GameSession at runtime)")]
        public TrackType trackType = TrackType.PointToPoint;
        public GameMode  gameMode  = GameMode.Race;
        public TrackType ActiveTrackType => trackType;
        public GameMode  ActiveGameMode  => gameMode;

        // Wall clock seconds since GO (used by the end screen for Race mode results, TimeTrial uses the timer)
        float _raceStartTime;
        public float RaceElapsedTime =>
            (State == RaceState.Waiting || State == RaceState.Countdown) ? 0f : Time.time - _raceStartTime;

        public IReadOnlyList<RacerInfo> Standings => _racers;

        public class RacerInfo
        {
            public Transform   transform;
            public BikeAIController ai;   // Null for the player
            public int         laps;
            public float       splineT;
            public float       lastT;
            public int         position;           // 1-based
            public bool        finished;
            public bool        beforeStart;        // Still behind the start line, ranked by grid slot

            // Lap timing (seconds). lapStartTime is seeded at GO and after each lap
            public float       lapStartTime;
            public float       lastLapTime = -1f;
            public float       bestLapTime = -1f;
            public float       finishTime  = -1f;   // RaceElapsedTime when this racer crossed the finish; -1 = DNF

            public float TotalProgress => laps + splineT;
        }

        public float GetLastLapTime(Transform racer)
        {
            foreach (var r in _racers)
                if (r.transform == racer) return r.lastLapTime;
            return -1f;
        }

        public float GetBestLapTime(Transform racer)
        {
            foreach (var r in _racers)
                if (r.transform == racer) return r.bestLapTime;
            return -1f;
        }

        public int GetPosition(Transform racer)
        {
            foreach (var r in _racers)
                if (r.transform == racer) return r.position;
            return -1;
        }

        public int GetLaps(Transform racer)
        {
            foreach (var r in _racers)
                if (r.transform == racer) return r.laps;
            return 0;
        }


        // Fires once, when the intro fly-in has settled onto the player view and the grid wakes -
        // before the countdown. Start taunts belong here, not on OnRaceStart, which is "GO!"
        public System.Action                     OnGridWarmedUp;
        public System.Action                     OnRaceStart;
        public System.Action<RacerInfo>          OnRacerFinished;
        public System.Action<List<RacerInfo>>    OnRaceComplete;
        // (racer, lapTime, isPersonalBest) fired each time a racer completes a lap
        public System.Action<RacerInfo, float, bool> OnLapCompleted;

        private readonly List<RacerInfo> _racers      = new();
        private readonly List<RacerInfo> _finishOrder = new();
        private bool  _isSplitScreen;

        // Start line plane, cached by SetupStartingGrid so the grid can be ranked before anyone crosses it
        private Vector3 _startLineWorld;
        private Vector3 _startLineForward;

        // Cached delegate avoids a heap allocation every FixedUpdate when sorting standings
        private readonly Comparison<RacerInfo> _standingsComparison =
            (a, b) => b.TotalProgress.CompareTo(a.TotalProgress);

        void Start()
        {
            if (routeGraph == null)
            {
                Debug.LogError("[RaceManager] No Route Graph assigned, cannot track race progress.", this);
                enabled = false;
                return;
            }

            if (!routeGraph.IsBaked) routeGraph.Bake();

            _isSplitScreen = GameSession.Instance != null && GameSession.Instance.IsSplitScreen;

            // Pull the selected track/game mode from the persistent session (menu/track select sets these)
            if (GameSession.Instance != null)
            {
                trackType = GameSession.Instance.SelectedTrackType;
                gameMode  = GameSession.Instance.SelectedGameMode;
            }

            // Time Trial is solo + ghost, no live AI opponents (same treatment as split screen)
            bool disableLiveAI = _isSplitScreen || gameMode == GameMode.TimeTrial;

            Time.maximumDeltaTime = 0.1f;

            if (disableLiveAI)
            {
                var allAI = FindObjectsByType<BikeAIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var ai in allAI)
                    if (ai != null) ai.gameObject.SetActive(false);
                aiOpponents = System.Array.Empty<BikeAIController>();
                aiCount     = 0;
            }
            else
            {
                // Spawn AI bikes from prefab if configured and no scene bikes are already assigned
                bool hasSceneBikes = aiOpponents != null && aiOpponents.Length > 0 && aiOpponents[0] != null;
                bool canRandomise = FindFirstObjectByType<AIRacerRandomiser>() != null;
                if ((aiPrefab != null || canRandomise) && aiCount > 0 && !hasSceneBikes)
                    SpawnAIBikes();

                var sceneAI = FindObjectsByType<BikeAIController>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
                var merged = new System.Collections.Generic.List<BikeAIController>();
                if (aiOpponents != null)
                    foreach (var ai in aiOpponents)
                        if (ai != null && !merged.Contains(ai)) merged.Add(ai);
                foreach (var ai in sceneAI)
                    if (!merged.Contains(ai))
                    {
                        merged.Add(ai);
                        Debug.Log($"[RaceManager] Auto-registered unlisted AI bike '{ai.name}'.", ai);
                    }
                aiOpponents = merged.ToArray();
            }

            // Silence traffic through the camera fly in too (unmuted in WarmUpGrid)
            // A StartGameManager with no intro camera warms the grid inside its own Start, which runs before
            // this one, and WarmUpGrid never runs twice, so muting here would silence the whole race
            TrafficEngineSound.GlobalMuted = !_gridWarmedUp;

            if (playerBike  != null) { ApplyPreRaceLock(playerBike);  MuteEngine(playerBike,  !_gridWarmedUp); }
            else Debug.LogWarning("[RaceManager] No player bike bound at Start, so the grid lock could " +
                                  "not be applied here. Relying on the bind-time lock instead.", this);

            if (playerBike2 != null) { ApplyPreRaceLock(playerBike2); MuteEngine(playerBike2, !_gridWarmedUp); }

            // Register player 1
            if (playerTransform != null)
            {
                _racers.Add(new RacerInfo
                {
                    transform = playerTransform,
                    ai        = null,
                    splineT   = SampleT(playerTransform.position),
                });
            }

            // Register player 2 (split screen only)
            if (_isSplitScreen && playerTransform2 != null)
            {
                _racers.Add(new RacerInfo
                {
                    transform = playerTransform2,
                    ai        = null,
                    splineT   = SampleT(playerTransform2.position),
                });
            }

            // Register AI opponents (single player only)
            foreach (var ai in aiOpponents)
            {
                if (ai == null) continue;

                // Wire race manager reference into the logic component
                if (ai.aiLogic != null)
                {
                    ai.aiLogic.raceManager      = this;
                    ai.aiLogic.playerTransform  = playerTransform;

                    // BikeAILogic.Awake falls back to FindFirstObjectByType, which skips inactive
                    // objects, and a spawned AI with no route graph never leaves the grid
                    if (routeGraph != null) ai.aiLogic.routeGraph = routeGraph;

                    if (ai.aiLogic.routeGraph == null)
                        Debug.LogError($"[RaceManager] '{ai.name}' has no RouteGraph, it cannot navigate " +
                                       "the track. Assign one on this RaceManager.", ai);
                }


                ai.canAccelerate = false;
                ai.SetCanMove(false);
                ai.isRevving     = _gridWarmedUp;
                MuteEngine(ai, !_gridWarmedUp);
                // Roll whether this AI burns out on the grid this race (for a launch shove at GO)
                ai.willStartBurnout = UnityEngine.Random.value < aiBurnoutChance;

                Transform t = ai.bikeReferences.Rotator != null
                    ? ai.bikeReferences.Rotator
                    : ai.transform;

                _racers.Add(new RacerInfo
                {
                    transform  = t,
                    ai         = ai,
                    splineT    = SampleT(t.position),
                });
            }

            // Seed last T so first frame lap detection is clean
            foreach (var r in _racers)
                r.lastT = r.splineT;

            // Setup starting grid, position bikes in lanes with proper spacing
            SetupStartingGrid();

            var traffic = FindFirstObjectByType<TrafficSpawner>();
            if (traffic != null)
            {
                var racers = new List<Transform>();
                // P2 is not playerTransform, so without this the spawner never builds a band around them
                if (_isSplitScreen && playerTransform2 != null) racers.Add(playerTransform2);
                foreach (var ai in aiOpponents) if (ai != null) racers.Add(ai.transform);
                traffic.allRacerTransforms = racers.ToArray();
                if (traffic.playerTransform == null) traffic.playerTransform = playerTransform;
                traffic.RefreshAnchors();
            }

            var racerBodies = new HashSet<Rigidbody>();
            if (playerBike != null)
            {
                var playerRb = playerBike.GetComponentInChildren<Rigidbody>();
                if (playerRb != null) racerBodies.Add(playerRb);
            }
            if (_isSplitScreen && playerBike2 != null)
            {
                var p2Rb = playerBike2.GetComponentInChildren<Rigidbody>();
                if (p2Rb != null) racerBodies.Add(p2Rb);
            }
            foreach (var ai in aiOpponents)
            {
                if (ai?.bikeReferences.BikeRb != null)
                    racerBodies.Add(ai.bikeReferences.BikeRb);
            }
            foreach (var ai in aiOpponents)
            {
                if (ai?.aiLogic != null)
                {
                    // excludedBodies, traffic avoidance skips these (all racers move fast, handle separately)
                    ai.aiLogic.excludedBodies = racerBodies;
                    // racerBodies, used by the dedicated overtaking system
                    ai.aiLogic.racerBodies = racerBodies;
                }
            }

            foreach (var ai in aiOpponents)
            {
                if (ai?.bikeReferences?.BikeRb != null)
                    ai.bikeReferences.BikeRb.maxDepenetrationVelocity = 2f;
            }

            if (autoStartWithoutPlayer && playerTransform == null)
                StartRacingState();

            if (skipCountdown)
                StartRacingState();
        }

        void FixedUpdate()
        {
            if (State != RaceState.Racing) return;

            UpdateProgress();
            UpdateStandings();
        }


        void UpdateProgress()
        {
            foreach (var r in _racers)
            {
                if (r.finished || r.transform == null) continue;

                float t = SampleProgress(r);

                if (trackType == TrackType.Circuit && r.lastT > 0.75f && t < 0.25f)
                {
                    r.laps++;

                    // Record the lap split and fire the event (drives the lap time HUD popup)
                    float lapTime = Time.time - r.lapStartTime;
                    r.lapStartTime = Time.time;
                    r.lastLapTime  = lapTime;
                    bool isBest = r.bestLapTime < 0f || lapTime < r.bestLapTime;
                    if (isBest) r.bestLapTime = lapTime;
                    OnLapCompleted?.Invoke(r, lapTime, isBest);

                    if (r.laps >= totalLaps)
                        FinishRacer(r);
                }

                r.splineT = t;
                r.lastT   = t;
            }
        }

        public void ReportFinish(Transform racer)
    {
        Debug.Log("TrackType: " + trackType + " | State: " + State + " | Racer null: " + (racer == null));
        if (trackType != TrackType.PointToPoint || State != RaceState.Racing || racer == null) return;

        Transform racerRoot = racer.root;
        Debug.Log("ReportFinish called. Racer root: " + racerRoot.name + " | Registered racers: " + _racers.Count);
    
        foreach (var r in _racers)
        {
            Debug.Log("Checking against: " + r.transform?.root?.name);
            if (r.finished || r.transform == null) continue;
            if (r.transform.root == racerRoot) { FinishRacer(r); return; }
        }
    
        Debug.Log("No match found");
    }

        void FinishRacer(RacerInfo r)
        {
            if (r.finished) return;

            r.finished   = true;
            r.finishTime = RaceElapsedTime;
            _finishOrder.Add(r);

            if (r.ai != null)
                r.ai.canAccelerate = false;

            OnRacerFinished?.Invoke(r);

            // The first racer across ends it, AI included; everyone else is ranked behind by progress
            if (State != RaceState.Finished)
                EndRace();
        }

        void EndRace()
        {
            State = RaceState.Finished;

            // Stop all remaining AI racers
            foreach (var remaining in _racers)
                if (remaining.ai != null)
                    remaining.ai.canAccelerate = false;

            // Anyone who didn't cross is ranked behind the finishers by track progress
            var ranked = new List<RacerInfo>(_racers);
            ranked.Sort(_standingsComparison);
            foreach (var rest in ranked)
                if (!_finishOrder.Contains(rest))
                    _finishOrder.Add(rest);

            OnRaceComplete?.Invoke(_finishOrder);
        }

        public float GetBestHumanFinishTime()
        {
            float best = -1f;
            foreach (var r in _racers)
                if (r.ai == null && r.finished && r.finishTime >= 0f)
                    if (best < 0f || r.finishTime < best) best = r.finishTime;
            return best;
        }

        void UpdateStandings()
        {
            _racers.Sort(_standingsComparison);
            for (int i = 0; i < _racers.Count; i++)
                _racers[i].position = i + 1;
        }


        float SampleT(Vector3 worldPos)
        {
            if (routeGraph != null && routeGraph.IsBaked)
                return routeGraph.FindNearestProgress(worldPos, out _, out _, out _);
            return 0f;
        }

        // Route progress is clamped to 0-1, so every grid slot behind the start line reads the same value
        // and the standings sort kept registration order - the player first. Rank by signed distance back
        // down the grid until a racer crosses, which runs continuously into real progress at GO.
        float SampleProgress(RacerInfo r)
        {
            if (r.beforeStart)
            {
                float behind = Vector3.Dot(r.transform.position - _startLineWorld, _startLineForward);
                if (behind < 0f) return behind / routeGraph.MainRouteLength;
                r.beforeStart = false;
            }

            return r.ai != null && r.ai.aiLogic != null
                ? r.ai.aiLogic.SplineT
                : SampleT(r.transform.position);
        }

        void SpawnAIBikes()
        {
            var randomiser = FindFirstObjectByType<AIRacerRandomiser>();
            var combos = randomiser != null ? randomiser.Draw(aiCount) : null;
            bool randomised = combos != null && combos.Length == aiCount;

            var spawned = new System.Collections.Generic.List<BikeAIController>(aiCount);
            var log = new System.Text.StringBuilder();

            for (int i = 0; i < aiCount; i++)
            {
                GameObject prefab = randomised && combos[i].bikePrefab != null ? combos[i].bikePrefab : aiPrefab;
                if (prefab == null)
                {
                    Debug.LogError($"[RaceManager] No prefab for AI slot {i}, and no fallback aiPrefab.", this);
                    continue;
                }

                GameObject go = Instantiate(prefab);
                go.name = $"BikeAI_{i}_{prefab.name}";

                // Instantiate drops into the ACTIVE scene, which during an additive load is still the
                // loading scene, and that scene is unloaded a few seconds later taking the grid with it
                if (go.scene != gameObject.scene)
                    SceneManager.MoveGameObjectToScene(go, gameObject.scene);

                var ai = go.GetComponent<BikeAIController>();
                if (ai == null)
                {
                    Debug.LogError($"[RaceManager] '{prefab.name}' is missing BikeAIController.", go);
                    Destroy(go);
                    continue;
                }

                if (randomised && !string.IsNullOrEmpty(combos[i].character))
                {
                    var switcher = go.GetComponentInChildren<RiderSwitch>(true);
                    if (switcher == null)
                        Debug.LogError($"[RaceManager] '{go.name}' has no RiderSwitch, it keeps its authored rider.", go);
                    else if (!switcher.SetRider(combos[i].character))
                        Debug.LogWarning($"[RaceManager] '{go.name}' carries no rider for '{combos[i].character}'.", go);
                }

                log.Append("\n   ").Append(go.name)
                   .Append(randomised ? "  as " + combos[i].character : "  (authored rider)");
                spawned.Add(ai);
            }

            aiOpponents = spawned.ToArray();
            Debug.Log($"[RaceManager] Spawned {spawned.Count} AI bike(s)" +
                      (randomised ? ", bike and character randomised" : " from the single aiPrefab") +
                      $" into scene '{gameObject.scene.name}'." + log);
        }

        void SetupStartingGrid()
        {
            bool hasPlayers = (positionPlayerOnGrid && playerBike != null) || (_isSplitScreen && playerBike2 != null);
            bool hasAI      = aiOpponents != null && aiOpponents.Length > 0;
            if (!hasPlayers && !hasAI) return;

            // Resolve start position, priority start line object, route graph, spline
            Vector3 startWorld;
            Vector3 forward;

            if (startLine != null)
            {
                startWorld = startLine.position;
                forward    = Vector3.ProjectOnPlane(startLine.forward, Vector3.up).normalized;

                // A start line whose Z points straight up flattens to zero, and every slot offset is
                // a multiple of it, so the whole grid would collapse onto one point
                if (forward.sqrMagnitude < 0.001f)
                {
                    Debug.LogError($"[RaceManager] '{startLine.name}' has no usable forward direction: its " +
                                   "blue Z axis points straight up or down, so every racer would stack on one " +
                                   "spot. Rotate it so Z runs down the track. Using world forward meanwhile.",
                                   startLine);
                    forward = Vector3.forward;
                }
            }
            else if (routeGraph != null && routeGraph.IsBaked &&
                     routeGraph.startSegmentIndex >= 0 &&
                     routeGraph.startSegmentIndex < routeGraph.BakedSegments.Length)
            {
                var seg = routeGraph.BakedSegments[routeGraph.startSegmentIndex];
                if (seg.positions == null || seg.positions.Length < 2)
                {
                    Debug.LogWarning("[RaceManager] Route graph start segment has no waypoints.", this);
                    return;
                }
                startWorld = seg.positions[0];
                Vector3 dir = seg.positions[1] - seg.positions[0];
                dir.y   = 0f;
                forward = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            }
            else
            {
                Debug.LogWarning("[RaceManager] No start line, route graph, or race spline assigned cannot set up starting grid.", this);
                return;
            }

            if (flipGridDirection) forward = -forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            _startLineWorld   = startWorld;
            _startLineForward = forward;

            // Spacing configuration, staggered two wide race track style
            float laneSpacing  = 3.5f;   // Metres between lane centres
            float slotSpacing  = 6.0f;   // Metres each successive bike steps back, keeps them clear at launch
            float startOffset  = 10.0f;  // How far behind the start line the first slot sits

            // Place player at the rear of the grid, one slot behind the last AI bike
            if (positionPlayerOnGrid && playerBike != null)
            {
                int playerSlot = aiOpponents != null ? aiOpponents.Length : 0;
                Vector3 playerPos = startWorld
                    - forward * (startOffset + playerSlot * slotSpacing);
                playerPos = SnapToGround(playerPos);
                Quaternion playerRot = Quaternion.LookRotation(forward, Vector3.up);

                Rigidbody playerRb = playerBike.bikeReferences.BikeRb;
                if (playerRb != null)
                {
                    playerRb.linearVelocity  = Vector3.zero;
                    playerRb.angularVelocity = Vector3.zero;
                    playerRb.position        = playerPos;
                    playerRb.rotation        = playerRot;
                }
                playerBike.transform.SetPositionAndRotation(playerPos, playerRot);
                if (playerBike.bikeReferences.Rotator != null)
                    playerBike.bikeReferences.Rotator.rotation = playerRot;

            }

            // In split screen, place P2 beside P1 but staggered one slot back
            if (_isSplitScreen && playerBike2 != null)
            {
                Vector3 p2Pos = startWorld - forward * (startOffset + slotSpacing) + right * laneSpacing;
                p2Pos = SnapToGround(p2Pos);
                Quaternion p2Rot = Quaternion.LookRotation(forward, Vector3.up);

                Rigidbody p2Rb = playerBike2.bikeReferences.BikeRb;
                if (p2Rb != null)
                {
                    p2Rb.linearVelocity  = Vector3.zero;
                    p2Rb.angularVelocity = Vector3.zero;
                    p2Rb.position        = p2Pos;
                    p2Rb.rotation        = p2Rot;
                }
                playerBike2.transform.SetPositionAndRotation(p2Pos, p2Rot);
                if (playerBike2.bikeReferences.Rotator != null)
                    playerBike2.bikeReferences.Rotator.rotation = p2Rot;

            }

            // Position each AI bike alternating left/right, each one a slot further back
            for (int i = 0; i < aiOpponents.Length; i++)
            {
                var ai = aiOpponents[i];
                if (ai == null || ai.aiLogic == null) continue;

                // Alternate lanes, even indices left (-0.5), odd right (+0.5)
                float laneOffset = (i % 2 == 0) ? -0.5f : 0.5f;

                Vector3 position = startWorld
                    - forward * (startOffset + i * slotSpacing)  // Each bike one slot further back
                    + right   * (laneOffset  * laneSpacing);     // Alternate left/right

                position = SnapToGround(position);

                // Teleport via Rigidbody so physics stays in sync with the transform
                Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
                Rigidbody rb = ai.bikeReferences.BikeRb;
                if (rb != null)
                {
                    rb.linearVelocity  = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position        = position;
                    rb.rotation        = targetRotation;
                }
                ai.transform.SetPositionAndRotation(position, targetRotation);

                if (ai.bikeReferences.Rotator != null)
                    ai.bikeReferences.Rotator.rotation = targetRotation;

                // Prevent ragdoll activation during grid settling
                if (ai.bikeReferences.ragdollActivator != null)
                    ai.bikeReferences.ragdollActivator.IsInvulnerable = true;

            }

            // Re sync spline tracking to match grid positions after teleport
            foreach (var ai in aiOpponents)
            {
                if (ai?.aiLogic != null)
                    ai.aiLogic.ResetTracking();
            }

            foreach (var r in _racers)
                r.beforeStart = true;

            SeedStandings();
        }

        // FixedUpdate only sorts while Racing, so without this the HUD shows registration order until GO
        public void SeedStandings()
        {
            foreach (var r in _racers)
            {
                if (r.transform == null) continue;
                r.splineT = SampleProgress(r);
                r.lastT   = r.splineT;
            }
            UpdateStandings();
        }

        public bool logGridSnap = true;

        Vector3 SnapToGround(Vector3 position)
        {
            // 3 m was never enough: the grid points sit ~11 m up and the surface under them is metres
            // lower, so every bike missed its snap and fell in from height at the lights.
            Vector3 origin = position + Vector3.up * groundRayUp;
            float   dist   = groundRayUp + groundRayDown;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, dist,
                                 groundSnapMask, QueryTriggerInteraction.Ignore))
            {
                // The project has two layers both named "Road" (8 and 12) and the track is on the second,
                // so a mask built by name silently excludes the surface it was meant to find.
                if (!Physics.Raycast(origin, Vector3.down, out hit, dist,
                                     groundSnapFallbackMask, QueryTriggerInteraction.Ignore))
                {
                    position.y += spawnHeightAboveGround;
                    Debug.LogWarning($"[RaceManager] SnapToGround found no surface within {dist:F0} m " +
                                     $"below y={origin.y:F2}. Bike placed at y={position.y:F2} and will " +
                                     "fall to whatever is actually below it.", this);
                    return position;
                }

                if (logGridSnap)
                    Debug.Log($"[RaceManager] SnapToGround fell back off groundSnapMask onto " +
                              $"'{hit.collider.name}' (layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}). " +
                              "Add that layer to groundSnapMask.", this);
            }

            position.y = hit.point.y + spawnHeightAboveGround + gridClearance;

            if (logGridSnap)
                Debug.Log($"[RaceManager] SnapToGround HIT '{hit.collider.name}' " +
                          $"(layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}) " +
                          $"at y={hit.point.y:F2}, placed y={position.y:F2}.", this);

            return position;
        }

        // Will call this when the "3, 2, 1" starts
        public void StartCountdownState()
        {
            if (State != RaceState.Waiting) return;
            State = RaceState.Countdown;
            WarmUpGrid();
        }

        private bool _gridWarmedUp;
        public bool GridWarmedUp => _gridWarmedUp;

        // Bikes are spawned at runtime, so a bind can land either side of this component's Start: going
        // straight into Main 2 bound before it, coming through the menus bound after, and that second
        // case left canMove true so the start line burnout could never fire. Applied at bind time as
        // well as at Start, so the order stops mattering.
        public void ApplyPreRaceLock(BikeController bike)
        {
            if (bike == null || State != RaceState.Waiting) return;

            // canMove is what the start line burnout reads, and is safe to set whenever the bind lands
            bike.canMove = false;

            var input = bike.GetComponentInChildren<BikeInput>(true);
            if (input == null) return;

            // Silencing input is NOT safe to re-apply: a bind that lands after WarmUpGrid would put back
            // a lock that nothing clears until GO, which is what stopped the grid revving entirely.
            if (_gridWarmedUp) input.UnlockForCountdown();
            else               input.LockForPreRace();
        }

        public void WarmUpGrid()
        {
            if (_gridWarmedUp) return;
            _gridWarmedUp = true;
            OnGridWarmedUp?.Invoke();

            TrafficEngineSound.GlobalMuted = false;
            MuteEngine(playerBike,  false);
            MuteEngine(playerBike2, false);
            playerBike ?.GetComponentInChildren<BikeInput>(true)?.UnlockForCountdown();
            playerBike2?.GetComponentInChildren<BikeInput>(true)?.UnlockForCountdown();

            foreach (var ai in aiOpponents)
                if (ai != null) { MuteEngine(ai, false); ai.isRevving = true; }
        }

        // Mute/unmute a bike's engine via its BikeAudioController child (player or AI)
        static void MuteEngine(Component bike, bool muted)
        {
            if (bike != null) bike.GetComponentInChildren<BikeAudioController>()?.SetEngineMuted(muted);
        }

        // Will call this on "GO!"
        public void StartRacingState()
        {
            if (State == RaceState.Racing || State == RaceState.Finished) return;

            WarmUpGrid();

            foreach (var ai in aiOpponents)
            {
                if (ai == null) continue;
                ai.canAccelerate = true;
                ai.SetCanMove(true);
                ai.isRevving     = false;
                ai.ApplyStartBurnoutLaunch();   // Getaway shove for AIs that burned out on the grid
                if (ai.bikeReferences.ragdollActivator != null)
                    ai.bikeReferences.ragdollActivator.IsInvulnerable = false;
            }

            if (playerBike  != null)
            {
                playerBike.canMove = true;
                playerBike.GetComponentInChildren<BikeInput>()?.SetFullControl();
            }
            if (playerBike2 != null)
            {
                playerBike2.canMove = true;
                playerBike2.GetComponentInChildren<BikeInput>()?.SetFullControl();
            }

            State = RaceState.Racing;

            // Seed every racer's lap clock at GO so lap 1 is timed from the start line
            _raceStartTime = Time.time;
            foreach (var r in _racers)
                r.lapStartTime = Time.time;

            OnRaceStart?.Invoke();
        }

    }
}
