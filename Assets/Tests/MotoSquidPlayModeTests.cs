using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Core;
using MotoSquid.Race;
using MotoSquid.Traffic;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotoSquid.Tests
{
    public class MotoSquidPlayModeTests : MonoBehaviour
    {
        public const string PREF_KEY  = "MotoSquid_RunTests";
        private const string SCENE     = "Main";
        private const float  SCENE_TIMEOUT = 30f;

        // Boot

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (PlayerPrefs.GetInt(PREF_KEY, 0) == 0) return;
            PlayerPrefs.SetInt(PREF_KEY, 0);
            PlayerPrefs.Save();

            var go = new GameObject("[MotoSquidTests]");
            DontDestroyOnLoad(go);
            go.AddComponent<MotoSquidPlayModeTests>();
        }

        // Results

        private readonly List<string>               _passed  = new List<string>();
        private readonly List<string>               _failed  = new List<string>();
        private readonly List<string>               _skipped = new List<string>();
        private readonly List<Dictionary<string,string>> _bugs = new List<Dictionary<string,string>>();

        private void Pass(string name)
        {
            _passed.Add(name);
            Debug.Log($"  <color=green>✓</color>  {name}");
        }

        private void Fail(string name, string reason, string category = "Gameplay", string priority = "High",
                          string steps = "", string expected = "", string notes = "")
        {
            _failed.Add(name);
            Debug.LogWarning($"  <color=red>✗</color>  {name}\n     {reason}");
            _bugs.Add(new Dictionary<string, string>
            {
                ["title"]             = name,
                ["category"]          = category,
                ["priority"]          = priority,
                ["assignedTo"]        = "QA",
                ["sceneComponent"]    = SCENE,
                ["stepsToReproduce"]  = steps,
                ["expectedBehaviour"] = expected,
                ["actualBehaviour"]   = reason,
                ["notes"]             = notes,
            });
        }

        private void Skip(string name, string reason)
        {
            _skipped.Add(name);
            Debug.Log($"  -  {name} (skipped: {reason})");
        }

        // Helpers

        private IEnumerator LoadMain()
        {
            if (SceneManager.GetActiveScene().name != SCENE)
                yield return SceneManager.LoadSceneAsync(SCENE);
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator WaitUntil(System.Func<bool> cond, float timeout)
        {
            float t = 0f;
            while (!cond() && t < timeout) { yield return null; t += Time.deltaTime; }
        }

        private static void Throttle(BikeController b, float v = 1f) =>
            b.ProvideInput(v, 0f, 0f, 0f, 0f, 0f);

        private static void SteerLeft(BikeController b) =>
            b.ProvideInput(0.4f, 0f, 0f, 1f, 0f, 0f);

        private static void Release(BikeController b) =>
            b.ProvideInput(0f, 0f, 0f, 0f, 0f, 0f);

        private IEnumerator StartRace(RaceManager rm)
        {
            rm.StartCountdownState();
            rm.StartRacingState();
            yield return null;
        }

        // Entry

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.2f);
            Debug.Log("══════════════════════════════════════════════════════");
            Debug.Log("  MotoSquid Gameplay Tests");
            Debug.Log("══════════════════════════════════════════════════════");

            yield return RunSinglePlayerTests();
            yield return RunSplitScreenTests();

            PrintSummary();
            if (_bugs.Count > 0) FlushBugs();

            Destroy(gameObject);
        }

        // Single Player

        private IEnumerator RunSinglePlayerTests()
        {
            Debug.Log("\n Single Player ");
            yield return LoadMain();

            // 1. Acceleration
            {
                const string N = "Bike_AcceleratesWithThrottle";
                var bike = FindObjectOfType<BikeController>();
                if (!bike) { Skip(N, "BikeController not in scene"); goto SP2; }
                bike.canMove = true; Throttle(bike);
                yield return WaitUntil(() => bike.localBikeVelocity.z > 0.5f, 6f);
                float s = bike.localBikeVelocity.z; Release(bike);
                if (s > 0.5f) Pass(N);
                else Fail(N, $"Speed {s:F3} , bike did not accelerate",
                          "Physics","Critical","Set canMove=true; apply full throttle","Speed > 0.5");
            }

            SP2:
            // 2. Countdown lock
            {
                const string N = "Bike_LockedDuringCountdown";
                var bike = FindObjectOfType<BikeController>();
                if (!bike) { Skip(N, "BikeController not in scene"); goto SP3; }
                bike.canMove = false; Throttle(bike);
                yield return new WaitForSeconds(2f);
                float s = Mathf.Abs(bike.localBikeVelocity.z); Release(bike); bike.canMove = true;
                if (s < 2f) Pass(N);
                else Fail(N, $"Speed {s:F3} while canMove=false","Gameplay","High",
                          "Set canMove=false; apply throttle; wait 2s","Speed < 2");
            }

            SP3:
            // 3. Steering
            {
                const string N = "Bike_SteeringChangesHeading";
                var bike = FindObjectOfType<BikeController>();
                if (!bike) { Skip(N, "BikeController not in scene"); goto SP4; }
                bike.canMove = true;
                float yBefore = bike.transform.eulerAngles.y;
                SteerLeft(bike);
                yield return new WaitForSeconds(2f);
                float diff = Mathf.Abs(Mathf.DeltaAngle(yBefore, bike.transform.eulerAngles.y));
                Release(bike);
                if (diff > 2f) Pass(N);
                else Fail(N, $"Rotation delta {diff:F2}° bike did not turn",
                          "Physics","High","Apply left steer for 2s","Y angle changes > 2°");
            }

            SP4:
            yield return LoadMain();
            // 4. Race state machine
            {
                const string N = "RaceManager_TransitionsToRacing";
                var rm = FindObjectOfType<RaceManager>();
                if (!rm) { Skip(N, "RaceManager not in scene"); goto SP5; }
                yield return StartRace(rm);
                yield return WaitUntil(() => rm.State == RaceManager.RaceState.Racing, 5f);
                if (rm.State == RaceManager.RaceState.Racing) Pass(N);
                else Fail(N, $"State is {rm.State} not Racing","Gameplay","Critical",
                          "Call StartCountdownState then StartRacingState","State == Racing");
            }

            SP5:
            // 5. Bike unlocks on race start
            {
                const string N = "RaceManager_UnlocksPlayerBikeOnStart";
                var rm   = FindObjectOfType<RaceManager>();
                var bike = FindObjectOfType<BikeController>();
                if (!rm || !bike) { Skip(N, "Missing RaceManager or BikeController"); goto SP6; }
                bike.canMove = false;
                yield return StartRace(rm);
                yield return WaitUntil(() => bike.canMove, 5f);
                if (bike.canMove) Pass(N);
                else Fail(N, "canMove still false after race start","Gameplay","Critical",
                          "Set canMove=false; call StartRacingState","canMove == true");
            }

            SP6:
            yield return LoadMain();
            // 6. Checkpoint tracker
            {
                const string N = "CheckpointTracker_InitialisesCorrectly";
                var tracker = FindObjectOfType<CheckpointTracker>();
                if (!tracker) { Skip(N, "CheckpointTracker not in scene"); goto SP7; }
                yield return WaitUntil(() => tracker.IsReady, 10f);
                if (tracker.IsReady) Pass(N);
                else Fail(N, "IsReady=false after 10s","Gameplay","High",
                          "Load Main; wait 10s","IsReady == true",
                          "Check waypointPath assigned in Inspector");
            }

            SP7:
            // 7. Respawn
            {
                const string N = "ResetBike_RespawnsAboveGround";
                var bike  = FindObjectOfType<BikeController>();
                var reset = FindObjectOfType<ResetBike>();
                if (!bike || !reset) { Skip(N, "Missing BikeController or ResetBike"); goto SP8; }
                bike.transform.position = new Vector3(0f, -20f, 0f);
                yield return new WaitForFixedUpdate();
                reset.ResetCurrentBike();
                yield return WaitUntil(() => bike.transform.position.y >= -1f, 5f);
                float y = bike.transform.position.y;
                if (y >= -1f) Pass(N);
                else Fail(N, $"Respawned at y={y:F2} (underground)","Gameplay","High",
                          "Move bike to y=-20; call ResetCurrentBike","y >= -1");
            }

            SP8:
            yield return LoadMain();
            // 8. AI movement
            {
                const string N = "AIBike_MovesAfterRaceStart";
                var ai = FindObjectOfType<BikeAIController>();
                if (!ai) { Skip(N, "No AI bikes in scene (aiCount=0?)"); goto SP9; }
                var rm = FindObjectOfType<RaceManager>();
                Vector3 before = ai.transform.position;
                yield return StartRace(rm);
                yield return WaitUntil(() =>
                    Vector3.Distance(ai.transform.position, before) > 0.5f, 8f);
                float moved = Vector3.Distance(ai.transform.position, before);
                if (moved > 0.5f) Pass(N);
                else Fail(N, $"AI moved only {moved:F3} units","AI","High",
                          "Start race; wait 8s","AI moves > 0.5 units");
            }

            SP9:
            // 9. Traffic spawner configured
            {
                const string N = "TrafficSpawner_HasVehiclesConfigured";
                var spawner = FindObjectOfType<TrafficSpawner>();
                if (!spawner) { Skip(N, "TrafficSpawner not in scene"); goto SP10; }
                yield return null;
                if (spawner.maxVehicles > 0) Pass(N);
                else Fail(N, "maxVehicles == 0","Gameplay","High",
                          "Read TrafficSpawner.maxVehicles","maxVehicles > 0");
            }

            SP10:
            // 10. Timer accumulates
            {
                const string N = "Timer_AccumulatesTime";
                var timer = FindObjectOfType<TimerSystem>();
                if (!timer) { Skip(N, "TimerSystem not in scene"); goto SP11; }
                float t1 = timer.totalTime;
                yield return new WaitForSeconds(0.8f);
                float t2 = timer.totalTime;
                if (t2 > t1) Pass(N);
                else Fail(N, $"totalTime did not increase ({t1:F3}→{t2:F3})","Gameplay","High",
                          "Read totalTime; wait 0.8s; read again","totalTime increases");
            }

            SP11:
            // 11. SaveManager
            {
                const string N = "SaveManager_SaveAndLoad";
                var sm = FindObjectOfType<SaveManager>();
                if (!sm) { Skip(N, "SaveManager not in scene"); goto SPDONE; }
                sm.SaveLevelTime("QATest_Track", 77.7f);
                yield return null;
                float result = sm.GetBestTime("QATest_Track");
                if (Mathf.Abs(result - 77.7f) < 0.01f) Pass(N);
                else Fail(N, $"GetBestTime returned {result} not 77.7","Gameplay","High",
                          "SaveLevelTime; GetBestTime","Returns saved value");
            }

            SPDONE: ;
        }

        // Split Screen

        private IEnumerator RunSplitScreenTests()
        {
            Debug.Log("\n Split Screen ");
            yield return LoadMain();

            var bikes = FindObjectsOfType<BikeController>();
            var rm    = FindObjectOfType<RaceManager>();

            // 1. Both bikes exist
            {
                const string N = "SplitScreen_BothPlayerBikesExist";
                if (bikes.Length >= 2) Pass(N);
                else Fail(N, $"Found {bikes.Length} bikes, need >= 2","Multiplayer","Critical",
                          "Load Main in split-screen mode",">= 2 BikeController components");
            }

            // 2. Both cameras exist
            {
                const string N = "SplitScreen_BothCameraRigsExist";
                int camCount = FindObjectsOfType<CameraController>().Length;
                if (camCount >= 2) Pass(N);
                else Fail(N, $"Found {camCount} cameras, need >= 2","Multiplayer","High",
                          "Load Main in split-screen mode",">= 2 CameraController components");
            }

            if (bikes.Length < 2) { Skip("SplitScreen_InputIsolation", "Need >= 2 bikes"); goto SSDONE; }

            // 3. P1 input isolation
            {
                const string N = "SplitScreen_P1InputDoesNotMoveP2";
                bikes[0].canMove = true; bikes[1].canMove = false;
                Throttle(bikes[0]);
                yield return WaitUntil(() => bikes[0].localBikeVelocity.z > 0.5f, 6f);
                float p1s = bikes[0].localBikeVelocity.z;
                float p2s = Mathf.Abs(bikes[1].localBikeVelocity.z);
                Release(bikes[0]);
                if (p1s > 0.5f && p2s < 1.5f) Pass(N);
                else Fail(N, $"P1={p1s:F3} P2={p2s:F3}","Multiplayer","Critical",
                          "Apply throttle to P1 only","P1 moves, P2 stays still");
            }

            // 4. P2 input isolation
            {
                const string N = "SplitScreen_P2InputDoesNotMoveP1";
                bikes[0].canMove = false; bikes[1].canMove = true;
                Throttle(bikes[1]);
                yield return WaitUntil(() => bikes[1].localBikeVelocity.z > 0.5f, 6f);
                float p1s = Mathf.Abs(bikes[0].localBikeVelocity.z);
                float p2s = bikes[1].localBikeVelocity.z;
                Release(bikes[1]);
                if (p2s > 0.5f && p1s < 1.5f) Pass(N);
                else Fail(N, $"P1={p1s:F3} P2={p2s:F3}","Multiplayer","Critical",
                          "Apply throttle to P2 only","P2 moves, P1 stays still");
            }

            // 5. Both unlock on race start
            {
                const string N = "SplitScreen_BothBikesUnlockOnRaceStart";
                if (!rm) { Skip(N, "RaceManager not in scene"); goto SSDONE; }
                bikes[0].canMove = false; bikes[1].canMove = false;
                yield return StartRace(rm);
                yield return WaitUntil(() => bikes[0].canMove && bikes[1].canMove, 5f);
                if (bikes[0].canMove && bikes[1].canMove) Pass(N);
                else Fail(N, $"P1.canMove={bikes[0].canMove} P2.canMove={bikes[1].canMove}",
                          "Multiplayer","Critical","Start race; check both canMove","Both true");
            }

            SSDONE: ;
        }

        // Summary & export
        private void PrintSummary()
        {
            int total = _passed.Count + _failed.Count + _skipped.Count;
            Debug.Log("══════════════════════════════════════════════════════");
            Debug.Log($"  Results: {_passed.Count} passed  {_failed.Count} failed  " +
                      $"{_skipped.Count} skipped  ({total} total)");
            if (_failed.Count > 0)
            {
                var sb = new StringBuilder("  Failed:\n");
                foreach (var f in _failed) sb.AppendLine($"    ✗ {f}");
                Debug.LogWarning(sb.ToString());
            }
            Debug.Log("══════════════════════════════════════════════════════");
        }

        private void FlushBugs()
        {
            try
            {
                string qaDir     = Path.Combine(Application.dataPath, "../QA");
                string jsonPath  = Path.Combine(qaDir, "alt_findings.json");
                var    sb        = new StringBuilder("[\n");
                for (int i = 0; i < _bugs.Count; i++)
                {
                    var b = _bugs[i];
                    sb.AppendLine("  {");
                    foreach (var kv in b)
                        sb.AppendLine($"    \"{kv.Key}\": {Json(kv.Value)},");
                    // trim trailing comma from last line
                    int last = sb.Length - 1;
                    while (last > 0 && (sb[last] == '\n' || sb[last] == '\r')) last--;
                    if (sb[last] == ',') sb[last] = ' ';
                    sb.AppendLine(i < _bugs.Count - 1 ? "  }," : "  }");
                }
                sb.Append(']');
                File.WriteAllText(jsonPath, sb.ToString(), System.Text.Encoding.UTF8);
                Debug.Log($"[MotoSquidTests] Failures written to {jsonPath}\n" +
                          $"Run:  cd {qaDir} && .venv/bin/python append_bugs.py {jsonPath} " +
                          $"../../MotoSquid_BugTracker.xlsx");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MotoSquidTests] Could not write findings JSON: {ex.Message}");
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
