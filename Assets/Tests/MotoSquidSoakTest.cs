using MotoSquid.Bike;
using MotoSquid.Controls;
using MotoSquid.Core;
using MotoSquid.Race;
using MotoSquid.Rider;
using MotoSquid.Track;
using MotoSquid.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotoSquid.Tests
{
    public class MotoSquidSoakTest : MonoBehaviour
    {
        public const string PREF_KEY           = "MotoSquid_RunSoakTest";
        public const string PREF_RACE_COUNT    = "MotoSquid_SoakRaceCount";
        public const string PREF_RACE_DURATION = "MotoSquid_SoakRaceDuration";

        private const string SCENE = "Main";
        private const float  UNDERGROUND_THRESHOLD = -2.0f;   // Y below this = underground bug
        private const float  STUCK_SPEED_THRESHOLD =  0.3f;   // M/s below this = potentially stuck
        private const float  STUCK_TIME_THRESHOLD  = 10.0f;   // Seconds before flagging as stuck
        private const float  SAMPLE_INTERVAL       =  0.5f;   // How often to check bike state

        // Boot

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (PlayerPrefs.GetInt(PREF_KEY, 0) == 0) return;
            PlayerPrefs.SetInt(PREF_KEY, 0);
            PlayerPrefs.Save();
            var go = new GameObject("[MotoSquidSoakTest]");
            DontDestroyOnLoad(go);
            go.AddComponent<MotoSquidSoakTest>();
        }

        // State

        private int   _raceCount;
        private float _raceDuration;

        private int _totalRaces;
        private int _totalAnomalies;
        private readonly List<Dictionary<string,string>> _bugs = new List<Dictionary<string,string>>();

        // Per bike stuck tracking
        private readonly Dictionary<int, float> _stuckTimers     = new Dictionary<int, float>();
        private readonly Dictionary<int, Vector3> _lastPositions = new Dictionary<int, Vector3>();

        // The autopilot player bike, monitored for anomalies alongside the AI bikes
        private BikeController _playerBike;

        // Aggregated reproduction tracking. Intermittent runtime bugs are confirmed by how
        // OFTEN they recur across the many soak races, so instead of logging a fresh row each
        // time we collapse repeats into one record per bug title and count reproductions
        private class ReproRecord
        {
            public string category, priority, scene, steps, expected, actual, notes;
            public int    racesReproduced;   // Distinct races the symptom appeared in
            public int    totalOccurrences;  // Total times observed across all races
        }
        private readonly Dictionary<string, ReproRecord> _repro = new Dictionary<string, ReproRecord>();
        private HashSet<string> _thisRaceReproduced;  // Bug titles already counted this race
        private int  _raceOccurrenceCount;            // Occurrences observed in the current race
        private bool _runtimeProbesDone;              // One shot runtime known-bug probes

        // Helpers

        private IEnumerator LoadMain()
        {
            if (SceneManager.GetActiveScene().name != SCENE)
                yield return SceneManager.LoadSceneAsync(SCENE);
            yield return new WaitForSeconds(1.0f);
        }

        private IEnumerator WaitUntil(System.Func<bool> cond, float timeout)
        {
            float t = 0f;
            while (!cond() && t < timeout) { yield return null; t += Time.deltaTime; }
        }

        private void RegisterBug(string title, string category, string priority, string scene,
                                 string steps, string expected, string actual, string notes)
        {
            _totalAnomalies++;
            _raceOccurrenceCount++;

            if (!_repro.TryGetValue(title, out var rec))
            {
                rec = new ReproRecord();
                _repro[title] = rec;
            }
            rec.category = category; rec.priority = priority; rec.scene = scene;
            rec.steps = steps; rec.expected = expected; rec.actual = actual; rec.notes = notes;
            rec.totalOccurrences++;

            if (_thisRaceReproduced != null && _thisRaceReproduced.Add(title))
            {
                rec.racesReproduced++;
                Debug.LogWarning($"  [SOAK] Reproduced: {title}\n     {actual}");
            }
        }
        private void CheckBikeAnomalies(int id, string bikeName, Vector3 pos, int raceNumber, float elapsed)
        {
            if (pos.y < UNDERGROUND_THRESHOLD)
            {
                RegisterBug(
                    $"Bike fell through the world: {bikeName}", "Physics", "High",
                    $"Main: {bikeName}",
                    $"Run the soak test and watch '{bikeName}'; it drops below the track (y < {UNDERGROUND_THRESHOLD}m). Intermittent, repeated races reproduce it.",
                    "Bike stays on the drivable surface for the whole race",
                    $"Race {raceNumber} @ {elapsed:F0}s: y = {pos.y:F2} at {pos}",
                    "Check ground/suspension raycasts and respawn in ResetBike");
            }

            if (_lastPositions.TryGetValue(id, out Vector3 lastPos))
            {
                float moved = Vector3.Distance(pos, lastPos) / SAMPLE_INTERVAL;
                if (moved < STUCK_SPEED_THRESHOLD)
                {
                    _stuckTimers[id] = _stuckTimers.GetValueOrDefault(id, 0f) + SAMPLE_INTERVAL;
                    if (_stuckTimers[id] >= STUCK_TIME_THRESHOLD)
                    {
                        RegisterBug(
                            $"Bike stuck / not progressing: {bikeName}", "Gameplay", "High",
                            $"Main — {bikeName}",
                            $"Run the soak test and watch '{bikeName}', it stops moving (<{STUCK_SPEED_THRESHOLD}m/s) for {STUCK_TIME_THRESHOLD:F0}s+. Intermittent, repeated races reproduce it.",
                            "Bike keeps racing without getting stuck",
                            $"Race {raceNumber} @ {elapsed:F0}s: <{STUCK_SPEED_THRESHOLD}m/s for {_stuckTimers[id]:F0}s at {pos}",
                            "Stuck against a barrier, on the wrong road, or failed to respawn, check waypoint following / collisions");
                        _stuckTimers[id] = 0f; // Reset to avoid spamming the same stall
                    }
                }
                else
                {
                    _stuckTimers[id] = 0f;
                }
            }
            _lastPositions[id] = pos;
        }
        private void RunRuntimeKnownBugProbesOnce()
        {
            if (_runtimeProbesDone) return;
            _runtimeProbesDone = true;

            if (FindObjectOfType<EndGameManager>() != null && SaveManager.Instance == null)
            {
                RegisterBug(
                    "EndGameManager_SaveManagerInstanceNullSafe", "Gameplay", "Critical",
                    "Main: EndGameManager",
                    "Enter the Main scene directly (without going through MainMenu) and finish a race",
                    "EndGameManager handles a null SaveManager gracefully",
                    "SaveManager.Instance is null in Main, end of race save/load may throw NullReferenceException",
                    "Add null guards around every SaveManager.Instance usage in EndGameManager");
            }
        }

        // Entry
        private IEnumerator Start()
        {
            _raceCount    = PlayerPrefs.GetInt(PREF_RACE_COUNT,    3);
            _raceDuration = PlayerPrefs.GetFloat(PREF_RACE_DURATION, 60f);

            yield return new WaitForSeconds(0.2f);
            Debug.Log("══════════════════════════════════════════════════════");
            Debug.Log($"  MotoSquid Soak Test, {_raceCount} race(s) × {_raceDuration}s each");
            Debug.Log("  Player + AI drive automatically, monitoring for anomalies");
            Debug.Log("  Also scanning for known bugs and reproducing them across races");
            Debug.Log("══════════════════════════════════════════════════════");

            // Deterministic static known bug checks, scan once (re running per race is wasteful)
            Debug.Log("\n Known bugs: static source scan");
            foreach (var bug in MotoSquidKnownBugTests.ScanStaticKnownBugs())
                _bugs.Add(bug);

            for (int race = 1; race <= _raceCount; race++)
                yield return RunSingleRace(race);

            BuildReproFindings();
            PrintSummary();
            if (_bugs.Count > 0) FlushBugs();
            Destroy(gameObject);
        }

        private void BuildReproFindings()
        {
            foreach (var kv in _repro)
            {
                var r = kv.Value;
                int pct = _totalRaces > 0 ? Mathf.RoundToInt(100f * r.racesReproduced / _totalRaces) : 0;
                string rate = $" [Reproduced in {r.racesReproduced}/{_totalRaces} races ({pct}%), " +
                              $"{r.totalOccurrences} total occurrence(s) across the soak.]";
                _bugs.Add(new Dictionary<string,string>
                {
                    ["title"]             = kv.Key,
                    ["category"]          = r.category,
                    ["priority"]          = r.priority,
                    ["assignedTo"]        = "QA (Soak Test)",
                    ["sceneComponent"]    = r.scene,
                    ["stepsToReproduce"]  = r.steps + rate,
                    ["expectedBehaviour"] = r.expected,
                    ["actualBehaviour"]   = r.actual,
                    ["notes"]             = r.notes,
                });
            }
        }

        // Single race
        private IEnumerator RunSingleRace(int raceNumber)
        {
            Debug.Log($"\n Race {raceNumber}/{_raceCount}  ");
            _stuckTimers.Clear();
            _lastPositions.Clear();
            _thisRaceReproduced  = new HashSet<string>();
            _raceOccurrenceCount = 0;

            yield return LoadMain();

            // Configure and start the race first, before wiring autopilot
            var rm = FindObjectOfType<RaceManager>();
            if (rm == null)
            {
                Debug.LogWarning($"  [SOAK] Race {raceNumber}: RaceManager not found, skipping");
                yield break;
            }

            // Start race with normal countdown so RaceManager positions bikes on the
            // grid and sets canMove at the right moment, not manually in the soak test
            rm.autoStartWithoutPlayer = true;
            rm.StartCountdownState();

            // Wait for the race to actually reach Racing state (countdown plays out naturally)
            yield return WaitUntil(
                () => rm.State == RaceManager.RaceState.Racing, 15f);

            if (rm.State != RaceManager.RaceState.Racing)
            {
                Debug.LogWarning($"  [SOAK] Race {raceNumber}: Never reached Racing state, skipping");
                yield break;
            }

            // Player bike and tracker are now positioned on the grid and canMove is set
            // by RaceManager, we must NOT override canMove here
            _playerBike = FindObjectOfType<BikeController>();
            if (_playerBike != null)
            {
                StartCoroutine(AutopilotCoroutine(_playerBike));
                Debug.Log($"  [SOAK] Race {raceNumber}: Player autopilot active");
            }
            else
            {
                Debug.LogWarning($"  [SOAK] Race {raceNumber}: No player bike found");
            }

            // Cheap runtime known bug probes, run once now that the scene is loaded
            RunRuntimeKnownBugProbesOnce();

            // Wait for AI bikes to spawn
            yield return WaitUntil(() => FindObjectOfType<BikeAIController>() != null, 5f);

            var aiBikes = FindObjectsOfType<BikeAIController>();
            Debug.Log($"  [SOAK] Race {raceNumber}: {aiBikes.Length} AI bike(s) racing");

            // Monitoring loop

            float elapsed = 0f;
            while (elapsed < _raceDuration)
            {
                yield return new WaitForSeconds(SAMPLE_INTERVAL);
                elapsed += SAMPLE_INTERVAL;

                // Re query in case bikes were spawned mid race
                aiBikes = FindObjectsOfType<BikeAIController>();

                foreach (var ai in aiBikes)
                {
                    if (ai == null) continue;
                    CheckBikeAnomalies(ai.GetInstanceID(), ai.gameObject.name,
                                       ai.transform.position, raceNumber, elapsed);
                }

                // Monitor the autopilot player bike too, the whole reason we drive it
                if (_playerBike != null)
                    CheckBikeAnomalies(_playerBike.GetInstanceID(), "Player (autopilot)",
                                       BikePos(_playerBike), raceNumber, elapsed);

                // Check: race completed early
                if (rm.State == RaceManager.RaceState.Finished)
                {
                    Debug.Log($"  [SOAK] Race {raceNumber}: Finished at {elapsed:F1}s ✓");
                    break;
                }
            }

            if (rm.State != RaceManager.RaceState.Finished)
                Debug.Log($"  [SOAK] Race {raceNumber}: Monitoring window elapsed ({_raceDuration:F0}s), " +
                          $"race may still be ongoing");

            _totalRaces++;
            Debug.Log($"  Race {raceNumber} complete, {_raceOccurrenceCount} anomaly occurrence(s) this race");
        }

        // Autopilot
        private IEnumerator AutopilotCoroutine(BikeController bike)
        {
            const float LAUNCH_TIME      = 4f;    // Straight line launch (mirrors BikeAILogic.launchStraightTime)
            const float RESPAWN_Y        = -1.5f;
            const float LOOK_AHEAD_BASE  = 14f;   // Metres ahead on the racing line at rest
            const float LOOK_AHEAD_PER_MS = 0.45f;// Extra look ahead per m/s, faster = look further = gentler steering
            const float FULL_STEER_ANGLE = 14f;   // Heading error (deg) at which steering runs at full duty
            const float STEER_DEADZONE   = 1.5f;  // Degree of error ignored entirely (kills micro jitter)
            const float SLOW_ANGLE       = 35f;   // Ease throttle above this heading error to take the corner
            const int   SEARCH_WINDOW    = 30;    // Route segments scanned forward each frame for the nearest point


            var humanInput = bike.GetComponentInParent<BikeInput>()
                          ?? bike.GetComponentInChildren<BikeInput>();
            if (humanInput != null)
            {
                humanInput.enabled = false;
                Debug.Log("  [SOAK] Player autopilot: disabled human BikeInput");
            }

            // Build the racing line polyline from the same route data the AI drives on.
            List<Vector3> route = BuildRoutePolyline();
            if (route == null || route.Count < 2)
            {
                Debug.LogWarning("  [SOAK] Player autopilot, no RouteGraph path found, driving straight");
                yield return StraightlineCoroutine(bike);
                yield break;
            }
            Debug.Log($"  [SOAK] Player autopilot, racing line built ({route.Count} points)");

            ResetBike reset = bike.GetComponent<ResetBike>()
                                    ?? FindObjectOfType<ResetBike>();
            Transform rotator = bike.bikeReferences?.Rotator;
            Rigidbody bikeRb  = bike.bikeReferences?.BikeRb;
            float     steerDuty = 0f;  // Duty cycle accumulator for proportional digital steering

            if (bikeRb != null && bikeRb.collisionDetectionMode == CollisionDetectionMode.Discrete)
            {
                bikeRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Debug.LogWarning(
                    "  [SOAK] Player bike Rigidbody was Discrete, switched the test bike to " +
                    "ContinuousSpeculative so high speed traffic hits register. NOTE: real " +
                    "players may tunnel through traffic at speed for the same reason; consider " +
                    "setting Collision Detection to Continuous/Speculative on the bike prefab.");
            }

            // Launch phase
            // Hold full throttle, dead straight, until the bike has cleared the start
            // line. Same behaviour the AI uses at lights out
            float launchTimer = 0f;
            while (bike != null && this != null && launchTimer < LAUNCH_TIME)
            {
                if (!bike.canMove) { yield return null; continue; }   // wait for race start
                bike.ProvideInput(1f, 0f, 0f, 0f, 0f, 0f);
                launchTimer += Time.deltaTime;
                yield return null;
            }
            Debug.Log("  [SOAK] Player autopilot: launch complete, following racing line");

            // Initial nearest point lock uses a full route search, afterwards we only
            // scan a forward window so we never snap backward at the start/finish join
            int progressIdx = NearestRouteIndex(route, BikePos(bike), 0, route.Count);

            while (bike != null && this != null)
            {
                if (!bike.canMove) { yield return null; continue; }

                Vector3 pos = BikePos(bike);
                if (pos.y < RESPAWN_Y)
                {
                    reset?.ResetCurrentBike();
                    yield return new WaitForSeconds(1.5f);
                    progressIdx = NearestRouteIndex(route, BikePos(bike), 0, route.Count);
                    continue;
                }


                float speedMs   = bikeRb != null
                    ? Vector3.ProjectOnPlane(bikeRb.linearVelocity, Vector3.up).magnitude
                    : 0f;
                float lookAhead = LOOK_AHEAD_BASE + speedMs * LOOK_AHEAD_PER_MS;

                // Follow the nearest point forward, then aim that distance ahead.
                progressIdx = NearestRouteIndex(route, pos, progressIdx, SEARCH_WINDOW);
                Vector3 aim = PointAhead(route, progressIdx, pos, lookAhead);

                Vector3 fwd = rotator != null ? rotator.forward : bike.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward; else fwd.Normalize();

                Vector3 toAim = aim - pos; toAim.y = 0f;
                float headingErr = toAim.sqrMagnitude > 0.001f
                    ? Vector3.SignedAngle(fwd, toAim, Vector3.up)
                    : 0f;


                float steerLeft = 0f, steerRight = 0f;
                if (Mathf.Abs(headingErr) > STEER_DEADZONE)
                {
                    float desired = Mathf.Clamp(headingErr / FULL_STEER_ANGLE, -1f, 1f);
                    steerDuty += Mathf.Abs(desired);
                    if (steerDuty >= 1f)
                    {
                        steerDuty -= 1f;
                        if (desired > 0f) steerRight = 1f;
                        else              steerLeft  = 1f;
                    }
                }
                else
                {
                    steerDuty = 0f;
                }

                float throttle = Mathf.Abs(headingErr) > SLOW_ANGLE ? 0.55f : 1f;

                bike.ProvideInput(throttle, 0f, 0f, steerLeft, steerRight, 0f);
                yield return null;
            }
        }

        // Racing line helpers

        private static Vector3 BikePos(BikeController bike)
        {
            var rb = bike.bikeReferences?.BikeRb;
            return rb != null ? rb.position : bike.transform.position;
        }

        private List<Vector3> BuildRoutePolyline()
        {
            var rg = FindObjectOfType<RouteGraph>();
            if (rg == null) return null;
            if (!rg.IsBaked) rg.Bake();
            if (!rg.IsBaked || rg.BakedSegments == null) return null;

            var order = new List<int>();
            if (rg.mainRouteIndices != null && rg.mainRouteIndices.Length > 0)
            {
                order.AddRange(rg.mainRouteIndices);
            }
            else
            {
                int seg = rg.startSegmentIndex;
                var seen = new HashSet<int>();
                for (int guard = 0; guard < rg.BakedSegments.Length + 2 && seg >= 0 && seen.Add(seg); guard++)
                {
                    order.Add(seg);
                    seg = FirstMainExit(rg, seg);
                }
            }

            var pts = new List<Vector3>();
            foreach (int si in order)
            {
                if (si < 0 || si >= rg.BakedSegments.Length) continue;
                var b = rg.BakedSegments[si];
                if (b?.positions == null) continue;
                foreach (var p in b.positions)
                {
                    // Skip a point that coincides with the previous one (shared segment joint)
                    if (pts.Count > 0 && (pts[pts.Count - 1] - p).sqrMagnitude < 0.01f) continue;
                    pts.Add(p);
                }
            }
            return pts;
        }

        private static int FirstMainExit(RouteGraph rg, int seg)
        {
            int[] exits = rg.GetExits(seg);
            int fallback = -1;
            foreach (int e in exits)
            {
                if (e < 0 || e >= rg.BakedSegments.Length) continue;
                if (fallback < 0) fallback = e;
                if (e < rg.segments.Length && (rg.segments[e].isShortcut || rg.segments[e].requiresTrigger))
                    continue;
                return e;
            }
            return fallback;
        }

        private static int NearestRouteIndex(List<Vector3> route, Vector3 pos, int startIdx, int window)
        {
            int n = route.Count;
            if (n < 2) return 0;
            window = Mathf.Min(window, n);
            float best = float.MaxValue;
            int bestIdx = ((startIdx % n) + n) % n;
            for (int k = 0; k < window; k++)
            {
                int i = (bestStart(startIdx, n) + k) % n;
                int j = (i + 1) % n;
                float d = SqrDistToSegment(pos, route[i], route[j]);
                if (d < best) { best = d; bestIdx = i; }
            }
            return bestIdx;
        }

        private static int bestStart(int startIdx, int n) => ((startIdx % n) + n) % n;

        private static Vector3 PointAhead(List<Vector3> route, int idx, Vector3 pos, float dist)
        {
            int n = route.Count;
            Vector3 a = route[idx];
            Vector3 b = route[(idx + 1) % n];
            Vector3 ab = b - a;
            float abLen = ab.magnitude;
            float t = abLen > 0.001f ? Mathf.Clamp01(Vector3.Dot(pos - a, ab) / (abLen * abLen)) : 0f;
            Vector3 cur = Vector3.Lerp(a, b, t);

            float remaining = dist;
            float remainOnSeg = abLen * (1f - t);
            if (remaining <= remainOnSeg)
                return cur + (abLen > 0.001f ? ab / abLen : Vector3.zero) * remaining;
            remaining -= remainOnSeg;

            int seg = (idx + 1) % n;
            for (int guard = 0; guard < n; guard++)
            {
                Vector3 p0 = route[seg];
                Vector3 p1 = route[(seg + 1) % n];
                Vector3 d  = p1 - p0;
                float len  = d.magnitude;
                if (remaining <= len)
                    return p0 + (len > 0.001f ? d / len : Vector3.zero) * remaining;
                remaining -= len;
                seg = (seg + 1) % n;
            }
            return route[(idx + 1) % n];
        }

        private static float SqrDistToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            float t = lenSq > 0.000001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq) : 0f;
            Vector3 proj = a + ab * t;
            return (p - proj).sqrMagnitude;
        }

        private IEnumerator StraightlineCoroutine(BikeController bike)
        {
            while (bike != null && this != null)
            {
                if (bike.canMove)
                    bike.ProvideInput(1f, 0f, 0f, 0f, 0f, 0f);
                yield return null;
            }
        }

        // Summary & export

        private void PrintSummary()
        {
            Debug.Log("══════════════════════════════════════════════════════");
            Debug.Log($"  Soak Test Complete");
            Debug.Log($"  Races run:        {_totalRaces}");
            Debug.Log($"  Total occurrences:{_totalAnomalies}");
            if (_repro.Count == 0)
                Debug.Log("  No reproducible anomalies detected across all races ✓");
            else
            {
                Debug.Log($"  Reproduced {_repro.Count} distinct bug(s), reproduction confidence:");
                foreach (var kv in _repro)
                {
                    var r = kv.Value;
                    int pct = _totalRaces > 0 ? Mathf.RoundToInt(100f * r.racesReproduced / _totalRaces) : 0;
                    Debug.Log($"    • {kv.Key} — {r.racesReproduced}/{_totalRaces} races ({pct}%), " +
                              $"{r.totalOccurrences} occurrence(s)");
                }
            }
            Debug.Log("══════════════════════════════════════════════════════");
        }

        private void FlushBugs()
        {
            try
            {
                string qaDir    = Path.Combine(Application.dataPath, "../QA");
                string jsonPath = Path.Combine(qaDir, "soak_findings.json");
                var    sb       = new StringBuilder("[\n");
                for (int i = 0; i < _bugs.Count; i++)
                {
                    var b = _bugs[i];
                    sb.AppendLine("  {");
                    foreach (var kv in b)
                        sb.AppendLine($"    \"{kv.Key}\": {Json(kv.Value)},");
                    int last = sb.Length - 1;
                    while (last > 0 && (sb[last] == '\n' || sb[last] == '\r')) last--;
                    if (sb[last] == ',') sb[last] = ' ';
                    sb.AppendLine(i < _bugs.Count - 1 ? "  }," : "  }");
                }
                sb.Append(']');
                File.WriteAllText(jsonPath, sb.ToString(), Encoding.UTF8);
                Debug.Log($"[MotoSquidSoak] Anomalies written to {jsonPath}\n" +
                          $"Run:  cd {qaDir} && .venv/bin/python append_bugs.py soak_findings.json " +
                          $"../../MotoSquid_BugTracker.xlsx");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MotoSquidSoak] Could not write findings: {ex.Message}");
            }
        }

        private static string Json(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                            .Replace("\n", "\\n").Replace("\r", "") + "\"";
        }
    }
}
