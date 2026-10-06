using MotoSquid.Core;
using MotoSquid.Race;
using MotoSquid.Traffic;
using MotoSquid.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotoSquid.Tests
{
    public class MotoSquidKnownBugTests : MonoBehaviour
    {
        public const string PREF_KEY = "MotoSquid_RunBugTests";
        private const string SCENE   = "Main";

        // Boot
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (PlayerPrefs.GetInt(PREF_KEY, 0) == 0) return;
            PlayerPrefs.SetInt(PREF_KEY, 0);
            PlayerPrefs.Save();
            var go = new GameObject("[MotoSquidBugTests]");
            DontDestroyOnLoad(go);
            go.AddComponent<MotoSquidKnownBugTests>();
        }

        // Result tracking
        private readonly List<string> _passed  = new List<string>();
        private readonly List<string> _failed  = new List<string>();
        private readonly List<Dictionary<string,string>> _bugs = new List<Dictionary<string,string>>();

        private void Pass(string name, string note = "")
        {
            _passed.Add(name);
            Debug.Log($"  <color=green>✓</color>  [BUG-FIXED] {name}" +
                      (string.IsNullOrEmpty(note) ? "" : $"\n     {note}"));
        }

        private void Fail(string name, string reproduce, string actual,
                          string category = "Gameplay", string priority = "High",
                          string expected = "", string notes = "")
        {
            _failed.Add(name);
            Debug.LogWarning($"  <color=red>✗</color>  [BUG PRESENT] {name}\n     {actual}");
            _bugs.Add(new Dictionary<string,string>
            {
                ["title"]             = name,
                ["category"]          = category,
                ["priority"]          = priority,
                ["assignedTo"]        = "QA",
                ["sceneComponent"]    = SCENE,
                ["stepsToReproduce"]  = reproduce,
                ["expectedBehaviour"] = expected,
                ["actualBehaviour"]   = actual,
                ["notes"]             = notes,
            });
        }

        private void Skip(string name, string reason) =>
            Debug.Log($"  -  {name} (skipped: {reason})");

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

        private static string ReadScript(string projectRelativePath)
        {
            string full = Path.Combine(Application.dataPath, projectRelativePath);
            if (!File.Exists(full)) full = FindScript(Path.GetFileName(projectRelativePath));
            return full != null ? File.ReadAllText(full) : null;
        }

        // Scripts move between folders, so the file name is the stable key; ours win over vendor copies.
        private static string FindScript(string fileName)
        {
            foreach (var root in new[] { Path.Combine(Application.dataPath, "MotoSquid"), Application.dataPath })
            {
                if (!Directory.Exists(root)) continue;
                var hits = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                if (hits.Length > 0) return hits[0];
            }
            return null;
        }

        // Entry
        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.2f);
            Debug.Log("══════════════════════════════════════════════════════");
            Debug.Log("  MotoSquid Known Bug Tests");
            Debug.Log("══════════════════════════════════════════════════════");

            yield return RunBugTests();

            PrintSummary();
            if (_bugs.Count > 0) FlushBugs();
            Destroy(gameObject);
        }

        private IEnumerator RunBugTests()
        {
            yield return LoadMain();

            // Static code analysis bugs

            Debug.Log("\n Code Quality (static analysis) ");

            {
                const string N    = "BikeController_WheelRotationUsesFixedDeltaTime";
                const string FILE = "MotoSquid/Scripts/Bike/Core/BikeController.cs";
                string src = ReadScript(FILE);
                if (src == null) { Skip(N, "Script not found"); goto BUG002; }

                // Find all FixedUpdate bodies and scan for Time.deltaTime
                bool found = false;
                int  fIdx  = src.IndexOf("void FixedUpdate");
                while (fIdx >= 0)
                {
                    int braceStart = src.IndexOf('{', fIdx);
                    if (braceStart < 0) break;
                    int depth = 1, pos = braceStart + 1;
                    while (pos < src.Length && depth > 0)
                    {
                        if (src[pos] == '{') depth++;
                        else if (src[pos] == '}') depth--;
                        pos++;
                    }
                    string body = src.Substring(braceStart, pos - braceStart);
                    if (body.Contains("Time.deltaTime"))
                    {
                        found = true;
                        break;
                    }
                    fIdx = src.IndexOf("void FixedUpdate", pos);
                }

                if (found)
                    Fail(N,
                        "Open BikeController.cs and search for 'Time.deltaTime' inside FixedUpdate",
                        "Time.deltaTime used inside FixedUpdate for wheel rotation, " +
                        "causes framerate dependent physics at lines ~1069, 1080, 1084",
                        "Physics", "High",
                        "FixedUpdate wheel rotation uses Time.fixedDeltaTime",
                        "Replace Time.deltaTime with Time.fixedDeltaTime on wheel spin calculations");
                else
                    Pass(N, "All wheel rotation in FixedUpdate uses fixedDeltaTime");
            }

            BUG002:
            {
                const string N    = "MusicManager_PlaylistUsesSafeEndDetection";
                const string FILE = "MotoSquid/Scripts/Audio/Music/MusicManager.cs";
                string src = ReadScript(FILE);
                if (src == null) { Skip(N, "Script not found"); goto BUG003; }

                bool hasUnsafeCheck = src.Contains("musicSource.time == 0") ||
                                      src.Contains("musicSource.time==0");
                if (hasUnsafeCheck)
                    Fail(N,
                        "Open MusicManager.cs:~69; start a race and let a track finish",
                        "Exact float equality 'musicSource.time == 0' used for end-of-track " +
                        "detection, unreliable, playlist may never advance",
                        "Audio", "Critical",
                        "Playlist advances to next track reliably",
                        "Replace with '!musicSource.isPlaying' check");
                else
                    Pass(N, "Unsafe time==0 check removed; isPlaying used instead");
            }

            BUG003:
            {
                const string N    = "EndGameManager_LoadsCorrectSceneName";
                const string FILE = "MotoSquid/Scripts/UI/Menus/EndGameManager.cs";
                string src = ReadScript(FILE);
                if (src == null) { Skip(N, "Script not found"); goto BUG005; }

                bool hasWrongName = src.Contains("LoadScene(\"MainMenu\")") ||
                                    src.Contains("LoadScene( \"MainMenu\" )");
                if (hasWrongName)
                    Fail(N,
                        "Complete a race, observe scene that loads after end screen",
                        "EndGameManager calls SceneManager.LoadScene(\"MainMenu\"), " +
                        "scene does not exist; causes MissingReferenceException",
                        "Gameplay", "Critical",
                        "Returns to 'Main' scene",
                        "Change LoadScene(\"MainMenu\") to LoadScene(\"Main\")");
                else
                    Pass(N, "Scene name is 'Main'");
            }

            BUG005:
            {
                const string N    = "ColourBlindFilter_DontDestroyOnRoot";
                const string FILE = "MotoSquid/Scripts/Settings/ColourBlindFilter.cs";
                string src = ReadScript(FILE);
                if (src == null) { Skip(N, "Script not found"); goto BUG006; }

                // Check whether there's a root object guard before DontDestroyOnLoad(gameObject)
                bool hasDDOL  = src.Contains("DontDestroyOnLoad(gameObject)");
                bool hasGuard = src.Contains("transform.parent == null") ||
                                src.Contains("transform.root == transform") ||
                                src.Contains("gameObject.transform.parent == null");
                if (hasDDOL && !hasGuard)
                    Fail(N,
                        "Attach ColourBlindFilter to a child GameObject; enter Play Mode",
                        "DontDestroyOnLoad(gameObject) called without checking " +
                        "transform.parent == null, Unity logs an error if called on a non root",
                        "Code Quality", "Medium",
                        "DontDestroyOnLoad only called on root GameObjects",
                        "Add 'if (transform.parent != null) transform.SetParent(null);' before the call");
                else if (!hasDDOL)
                    Pass(N, "DontDestroyOnLoad(gameObject) not found, may have been removed");
                else
                    Pass(N, "Root-object guard present before DontDestroyOnLoad");
            }

            BUG006:
            // Runtime behaviour bugs

            Debug.Log("\n Runtime Behaviour ");

            {
                const string N = "RaceManager_P1P2EqualForwardDistanceOnGrid";
                var rm = FindObjectOfType<RaceManager>();
                if (!rm || rm.playerBike == null || rm.playerBike2 == null)
                {
                    Skip(N, "Need both playerBike and playerBike2 assigned in RaceManager");
                    goto BUG007;
                }

                // Allow Start/Awake to run grid setup
                yield return new WaitForSeconds(0.3f);

                Vector3 fwd = rm.startLine != null
                    ? Vector3.ProjectOnPlane(rm.startLine.forward, Vector3.up).normalized
                    : Vector3.forward;

                float p1Fwd = Vector3.Dot(rm.playerBike.transform.position,  fwd);
                float p2Fwd = Vector3.Dot(rm.playerBike2.transform.position, fwd);
                float diff  = Mathf.Abs(p1Fwd - p2Fwd);

                if (diff > 1.0f)
                    Fail(N,
                        "Load Main scene in split-screen mode; observe P1 and P2 starting positions",
                        $"P1 and P2 are {diff:F2}m apart along the forward axis, unfair advantage",
                        "Multiplayer", "High",
                        "P1 and P2 start at equal forward distance, offset only laterally",
                        "Check startOffset multiplier in SetupStartingGrid() for P2");
                else
                    Pass(N, $"P1 and P2 forward distance difference: {diff:F2}m (within 1m tolerance)");
            }

            BUG007:
            {
                const string N = "EndGameManager_SaveManagerInstanceNullSafe";
                var egm = FindObjectOfType<EndGameManager>();
                if (egm == null) { Skip(N, "EndGameManager not in scene"); goto BUG008; }

                bool exceptionThrown = false;
                try
                {
                    // If SaveManager.Instance is null, does EndGameManager crash?
                    // We read whether there's a live Instance
                    var sm = SaveManager.Instance;
                    if (sm == null)
                    {
                        // Instance IS null, trigger path that uses it
                        // We can't safely call StartEndSequence without full setup,
                        // so we just flag that the risk exists
                        Fail(N,
                            "Complete a race when SaveManager has not been initialised " +
                            "(e.g. enter Main directly without going through MainMenu)",
                            "SaveManager.Instance is null in the current scene, " +
                            "any EndGame save/load code may throw NullReferenceException",
                            "Gameplay", "Critical",
                            "EndGameManager handles null SaveManager gracefully",
                            "Add null checks around all SaveManager.Instance usages in EndGameManager");
                    }
                    else
                    {
                        Pass(N, "SaveManager.Instance is live, null path not triggered");
                    }
                }
                catch (System.Exception e)
                {
                    exceptionThrown = true;
                    Fail(N,
                        "Load Main and trigger end-of-race sequence without SaveManager initialised",
                        $"Exception thrown: {e.GetType().Name}: {e.Message}",
                        "Gameplay", "Critical",
                        "No exception on null SaveManager",
                        "Add null guard before SaveManager.Instance access");
                }
                _ = exceptionThrown;
            }

            BUG008:
            {
                const string N = "TrafficSpawner_NoInfiniteSpawnWithoutRace";
                var spawner = FindObjectOfType<TrafficSpawner>();
                if (spawner == null) { Skip(N, "TrafficSpawner not in scene"); goto BUGDONE; }

                // Count traffic vehicles before and after waiting without starting the race
                yield return new WaitForSeconds(5f);
                int count = FindObjectsOfType<VehicleFollowing>().Length;

                // Traffic should not spawn if the race hasn't started
                if (count > 0)
                    Fail(N,
                        "Load Main scene; do NOT start the race; wait 5 seconds",
                        $"{count} traffic vehicle(s) spawned despite race not reaching Racing state, " +
                        "indicates spawn coroutine runs unconditionally",
                        "Performance", "Medium",
                        "Traffic only spawns after race reaches Racing state",
                        "Add a RaceState.Racing check at the top of the traffic spawn coroutine");
                else
                    Pass(N, "No traffic spawned before race start");
            }

            BUGDONE: ;
        }

        // Reusable static analysis (shared with the soak test)
        public static List<Dictionary<string,string>> ScanStaticKnownBugs()
        {
            var present = new List<Dictionary<string,string>>();

            void Report(string name, bool isPresent, string passNote, Dictionary<string,string> bug)
            {
                if (isPresent)
                {
                    present.Add(bug);
                    Debug.LogWarning($"  <color=red>✗</color>  [KNOWN BUG PRESENT] {name}\n     {bug["actualBehaviour"]}");
                }
                else
                {
                    Debug.Log($"  <color=green>✓</color>  [known bug fixed] {name}" +
                              (string.IsNullOrEmpty(passNote) ? "" : $" — {passNote}"));
                }
            }

            Dictionary<string,string> Mk(string name, string repro, string actual, string category,
                string priority, string expected, string notes) => new Dictionary<string,string>
            {
                ["title"] = name, ["category"] = category, ["priority"] = priority, ["assignedTo"] = "QA",
                ["sceneComponent"] = SCENE, ["stepsToReproduce"] = repro, ["expectedBehaviour"] = expected,
                ["actualBehaviour"] = actual, ["notes"] = notes,
            };

            {
                const string N = "BikeController_WheelRotationUsesFixedDeltaTime";
                string src = ReadScript("MotoSquid/Scripts/Bike/Core/BikeController.cs");
                if (src == null) Debug.Log($"  -  {N} (skipped: script not found)");
                else
                {
                    bool found = false;
                    int fIdx = src.IndexOf("void FixedUpdate");
                    while (fIdx >= 0)
                    {
                        int braceStart = src.IndexOf('{', fIdx);
                        if (braceStart < 0) break;
                        int depth = 1, pos = braceStart + 1;
                        while (pos < src.Length && depth > 0)
                        {
                            if (src[pos] == '{') depth++;
                            else if (src[pos] == '}') depth--;
                            pos++;
                        }
                        if (src.Substring(braceStart, pos - braceStart).Contains("Time.deltaTime")) { found = true; break; }
                        fIdx = src.IndexOf("void FixedUpdate", pos);
                    }
                    Report(N, found, "wheel rotation in FixedUpdate uses fixedDeltaTime",
                        Mk(N, "Open BikeController.cs and search for 'Time.deltaTime' inside FixedUpdate",
                           "Time.deltaTime used inside FixedUpdate for wheel rotation, framerate dependent physics (~lines 1069, 1080, 1084)",
                           "Physics", "High", "FixedUpdate wheel rotation uses Time.fixedDeltaTime",
                           "Replace Time.deltaTime with Time.fixedDeltaTime on wheel-spin calculations"));
                }
            }


            {
                const string N = "MusicManager_PlaylistUsesSafeEndDetection";
                string src = ReadScript("MotoSquid/Scripts/Audio/Music/MusicManager.cs");
                if (src == null) Debug.Log($"  -  {N} (skipped: script not found)");
                else
                {
                    bool bad = src.Contains("musicSource.time == 0") || src.Contains("musicSource.time==0");
                    Report(N, bad, "isPlaying used instead of time==0",
                        Mk(N, "Open MusicManager.cs:~69; start a race and let a track finish",
                           "Exact float equality 'musicSource.time == 0' for end of track detection, unreliable, playlist may never advance",
                           "Audio", "Critical", "Playlist advances to next track reliably",
                           "Replace with '!musicSource.isPlaying' check"));
                }
            }

            {
                const string N = "EndGameManager_LoadsCorrectSceneName";
                string src = ReadScript("MotoSquid/Scripts/UI/Menus/EndGameManager.cs");
                if (src == null) Debug.Log($"  -  {N} (skipped: script not found)");
                else
                {
                    bool bad = src.Contains("LoadScene(\"MainMenu\")") || src.Contains("LoadScene( \"MainMenu\" )");
                    Report(N, bad, "scene name is 'Main'",
                        Mk(N, "Complete a race; observe the scene that loads after the end screen",
                           "EndGameManager calls SceneManager.LoadScene(\"MainMenu\"), scene does not exist; MissingReferenceException",
                           "Gameplay", "Critical", "Returns to 'Main' scene",
                           "Change LoadScene(\"MainMenu\") to LoadScene(\"Main\")"));
                }
            }

            {
                const string N = "ColourBlindFilter_DontDestroyOnRoot";
                string src = ReadScript("MotoSquid/Scripts/Settings/ColourBlindFilter.cs");
                if (src == null) Debug.Log($"  -  {N} (skipped: script not found)");
                else
                {
                    bool hasDDOL = src.Contains("DontDestroyOnLoad(gameObject)");
                    bool hasGuard = src.Contains("transform.parent == null") || src.Contains("transform.root == transform")
                                 || src.Contains("gameObject.transform.parent == null");
                    if (!hasDDOL)
                        Debug.Log($"  <color=green>✓</color>  [known bug fixed] {N} — DontDestroyOnLoad(gameObject) not found");
                    else
                        Report(N, !hasGuard, "root-object guard present before DontDestroyOnLoad",
                            Mk(N, "Attach ColourBlindFilter to a child GameObject; enter Play Mode",
                               "DontDestroyOnLoad(gameObject) called without a transform.parent == null guard — Unity errors on non root",
                               "Code Quality", "Medium", "DontDestroyOnLoad only called on root GameObjects",
                               "Add 'if (transform.parent != null) transform.SetParent(null);' before the call"));
                }
            }

            // Extended marker keyed signature scan

            // Read each file once even when it hosts several markers.
            var fileCache = new Dictionary<string, string>();
            string Src(string f)
            {
                if (!fileCache.TryGetValue(f, out var s)) { s = ReadScript(f); fileCache[f] = s; }
                return s;
            }

            // require == true  → bug PRESENT when signature is ABSENT (fix reverted)
            // require == false → bug PRESENT when signature is FOUND  (bad pattern returned)
            void Check(string title, string file, bool require, string signature,
                       string category, string priority, string repro, string actual,
                       string expected, string notes)
            {
                string src = Src(file);
                if (src == null) { Debug.Log($"  -  {title} (skipped: {file} not found)"); return; }
                bool found      = src.Contains(signature);
                bool bugPresent = require ? !found : found;
                Report(title, bugPresent, require ? "fix signature present" : "bad pattern absent",
                    Mk(title, repro, actual, category, priority, expected, notes));
            }

            Check("BUG-004_BurnoutRotationInFixedUpdate", "BikeBurnout.cs", true,
                "refs.Rotator.Rotate(Vector3.up, rotationAmount * rotationDirection * burnoutLerp, Space.World)",
                "Physics", "High", "Burnout while turning at varying frame rates",
                "Burnout rotation re-applies a stale Update value scaled by dt, juddery at low fps",
                "Burnout rotation computed and applied in FixedUpdate", "Keep the Rotator.Rotate call in HandleBurnoutAndRotation");

            Check("BUG-005_SplitScreenVcamLayersDistinct", "SplitScreenSetup.cs", true,
                "p1VirtualCamLayer.value == 0 || p2VirtualCamLayer.value == 0 || p1Layer == p2Layer",
                "Multiplayer", "High", "Enter split-screen with unassigned/identical vcam layers",
                "Both Cinemachine brains share vcams and the views bleed into each other",
                "Loud error when P1/P2 vcam layers are unassigned or identical", "Keep the layer validation guard in SetupCinemachine");

            Check("BUG-007_TrafficSpawnerNullRaceManagerGuard", "TrafficSpawner.cs", true,
                "if (raceManager == null)",
                "Code Quality", "Medium", "Destroy the RaceManager mid-race while traffic is spawning",
                "Coroutine reads raceManager.State on a dead reference — MissingReferenceException",
                "Coroutine yield-breaks cleanly when raceManager is gone", "Keep the null guard at the top of the spawn loop");

            Check("BUG-008_MusicManagerPersistsAcrossScenes", "AdaptiveMusicManager.cs", true,
                "DontDestroyOnLoad(gameObject)",
                "Audio", "Medium", "Load a new scene while music is playing",
                "Music object destroyed on scene load, leaving Instance dangling",
                "Music survives scene loads via DontDestroyOnLoad", "Keep DontDestroyOnLoad in Awake");

            Check("BUG-009_P2GridSteppedBack", "RaceManager.cs", true,
                "startWorld - forward * (startOffset + slotSpacing) + right * laneSpacing",
                "Multiplayer", "High", "Start a split-screen race with no AI on the grid",
                "P1 and P2 sit at the same forward depth and collide on launch",
                "P2 is offset back one slot as well as laterally", "Keep the slotSpacing term in the P2 grid position");

            Check("BUG-012_TimeoutPassesSurvivedTime", "Timer/TimerSystem.cs", true,
                "endGameManager.StartEndSequence(totalTime, false)",
                "Gameplay", "High", "Let the timer run out instead of finishing",
                "End screen always shows 00:00.00 on a timeout",
                "End screen shows the real survived time", "Pass totalTime (not 0) to StartEndSequence");

            Check("BUG-013_CancelBonusResetOnTimeout", "Timer/TimerSystem.cs", true,
                "CancelInvoke(nameof(ResetBonusState))",
                "Audio", "Medium", "Collect a bonus on the frame the timer expires",
                "ResetBonusState (and its audio) fires after the game has ended",
                "Pending ResetBonusState invoke is cancelled on game over", "Keep CancelInvoke in the timeout path");

            Check("BUG-014_FinishLineNullGuards", "Finish_Startline/FinishLine.cs", true,
                "if (timerSystem == null || endGameManager == null)",
                "Code Quality", "High", "Enter the finish trigger with timerSystem/endGameManager unassigned",
                "NullReferenceException when a player crosses the line",
                "Clear error and graceful return on missing refs", "Keep the null guard in OnTriggerEnter");

            Check("BUG-015_EndGameFadePanelGuard", "EndGame Screen/EndGameManager.cs", true,
                "if (fadePanel == null)",
                "Gameplay", "High", "Finish a race with fadePanel unassigned",
                "Coroutine crashes mid-sequence and can leave the screen stuck black",
                "Coroutine yield-breaks with a clear error", "Keep the fadePanel null guard");

            Check("BUG-016_EndGamePlayerCamError", "EndGame Screen/EndGameManager.cs", true,
                "SetPlayer() was never called",
                "Gameplay", "Medium", "Finish a race with skipStartLineSpawn enabled",
                "End sequence silently aborts with no end screen",
                "Explicit error explaining SetPlayer() was never called", "Keep the diagnostic LogError");

            Check("BUG-017_StartTeleportKinematic", "Finish_Startline/StartGameManager.cs", true,
                "bool wasKinematic = rb.isKinematic;",
                "Physics", "Medium", "Watch the bike snap to the grid at race start",
                "Setting rb.position on a dynamic body causes a one-frame visual pop",
                "Teleport is instant (briefly kinematic)", "Keep the kinematic toggle around the reposition");

            Check("BUG-018_CursorLockedDuringGameplay", "Finish_Startline/StartGameManager.cs", true,
                "Cursor.lockState = CursorLockMode.Locked;",
                "Gameplay", "Low", "Play with a second monitor and move the mouse",
                "CursorLockMode.None lets the cursor leave the window during gameplay",
                "Cursor confined to the window", "Keep CursorLockMode.Locked at race start");

            Check("BUG-019_StopAndGetTimeNoShadow", "Timer/TimerSystem.cs", true,
                "out float elapsedTime",
                "Code Quality", "Medium", "Review StopAndGetTime signature",
                "out param named totalTime shadows the class field (contributed to BUG-012)",
                "out param is named elapsedTime", "Keep the renamed out parameter");

            Check("BUG-020_MusicNullClipGuard", "Music/MusicManager.cs", true,
                "if (bufferedClips[bufferIndex] != null)",
                "Audio", "High", "Use custom music with a file that fails to load",
                "Null clip assigned and null.name dereferenced in UpdateUI",
                "Failed clips are skipped, not dereferenced", "Keep the buffered-clip null guards");

            Check("BUG-021_BestTimeTextGuard", "EndGame Screen/EndGameManager.cs", true,
                "if (bestTimeText == null)",
                "Gameplay", "Medium", "Finish a race with bestTimeText unassigned",
                "NullReferenceException when writing the best-time readout",
                "Clear error and return on missing bestTimeText", "Keep the bestTimeText null guard");

            Check("BUG-022_ObjectFloatKinematic", "ObjectFloat.cs", true,
                "if (rb != null) rb.isKinematic = true;",
                "Physics", "Medium", "Attach ObjectFloat to an object with a Rigidbody",
                "Writing transform.position each frame fights the Rigidbody (jitter, broken CCD)",
                "Float driven kinematically via MovePosition", "Keep the isKinematic setup in Awake");

            Check("BUG-023_CountdownBeepPlayOneShot", "Finish_Startline/StartGameManager.cs", true,
                "audioSource.PlayOneShot(countingBeeps)",
                "Audio", "Low", "Start several races in quick succession",
                "!isPlaying guard silently drops a countdown beep",
                "Every countdown beep plays", "Keep PlayOneShot for the counting beeps");

            Check("BUG-024_GoBeepPlayOneShot", "Finish_Startline/StartGameManager.cs", true,
                "audioSource.PlayOneShot(goBeep)",
                "Audio", "Medium", "Start a race and listen for the GO cue",
                "!isPlaying guard could swallow the GO beep if a count beep was still playing",
                "GO cue always sounds", "Keep PlayOneShot for the GO beep");

            Check("BUG-025_BestTimeUsesSharedFormatter", "EndGame Screen/EndGameManager.cs", true,
                "FormatTime(finalTime)",
                "UI", "Medium", "Set a new best time and read the end screen",
                "Display scraped/string-replaced out of scoreText — breaks if the prefix changes",
                "Best time formatted directly from finalTime", "Keep FormatTime(finalTime) as the source of truth");

            Check("BUG-026_CountdownClampsToZero", "Timer/TimerSystem.cs", true,
                "Mathf.CeilToInt(Mathf.Max(0f, timerDuration))",
                "UI", "Low", "Watch the countdown reach zero",
                "CeilToInt of a tiny positive residual shows '1' on the final frame",
                "Timer reads 0 when effectively expired", "Keep the Mathf.Max(0f, ...) clamp");

            {
                const string T = "BUG-027_MenuMusicCachesAudioSourceFirst";
                string src = Src("Menu/MainMusicManager.cs");
                if (src == null) Debug.Log($"  -  {T} (skipped: file not found)");
                else
                {
                    int cache = src.IndexOf("audioSource = GetComponent<AudioSource>()");
                    int chk   = src.IndexOf("Instance == null");
                    bool bug  = cache < 0 || (chk >= 0 && cache > chk);
                    Report(T, bug, "audioSource cached before the instance check",
                        Mk(T, "Review MainMusicManager.Awake()",
                           "audioSource cached after (or instead of before) the duplicate-instance check — can be left null",
                           "Audio", "Medium", "audioSource cached as the first line of Awake()",
                           "Move 'audioSource = GetComponent<AudioSource>();' above the Instance == null check"));
                }
            }


            Check("BUG-028_OpenFolderDesktopGuard", "Menu/OpenUserMusicFolder.cs", true,
                "#if UNITY_STANDALONE || UNITY_EDITOR",
                "Code Quality", "Low", "Trigger 'open music folder' on a mobile build",
                "Application.OpenURL on a file:// path is a no-op / nonsensical on Android/iOS",
                "Folder open guarded to desktop, with a clear log elsewhere", "Keep the platform #if guard");

            Check("BUG-029_HudNeutralGearGuard", "HUDManager.cs", true,
                "Mathf.Max(1, bike.currentGear)",
                "UI", "High", "Sit in neutral (gear 0) and watch the HUD",
                "gearSpeeds[gear-1] = gearSpeeds[-1] crashes the HUD",
                "Neutral clamps to first gear for the readout", "Keep the Mathf.Max(1, currentGear) clamp");

            Check("BUG-030_PauseMenuInputActionsGuard", "PauseMenuManager.cs", true,
                "if (inputActions == null)",
                "Code Quality", "High", "Enter Play Mode with inputActions unassigned",
                "NullReferenceException in Awake leaves pause permanently broken",
                "Component disables itself with a clear error", "Keep the inputActions null guard");

            Check("BUG-031_FadeOutRevealsScene", "SceneLoader.cs", true,
                "fadePanel.alpha = 1f - (elapsed / fadeDuration);",
                "UI", "High", "Load a scene through SceneLoader",
                "FadeOut ramps 0→1, hiding the new scene behind a black panel",
                "Panel fades from opaque to transparent", "Keep the '1f - (elapsed/fadeDuration)' ramp");

            Check("BUG-032_CameraOffsetSingleOwner", "CameraController.cs", true,
                "ShouldLookOrbitDriveOffset",
                "Visual", "Medium", "Use look-orbit while ground-rush is active",
                "GroundRush and LookOrbit both write m_FollowOffset and conflict",
                "One predicate decides the offset owner per frame", "Keep ShouldLookOrbitDriveOffset and its guard");

            Check("BUG-033_SettingsFlushedOnQuit", "SettingsManager.cs", true,
                "SaveAllToPrefs(); PlayerPrefs.Save();",
                "Code Quality", "Medium", "Change a setting then quit/alt-tab abnormally",
                "SaveAllToPrefs was private and never invoked — settings lost",
                "Full UI state flushed on quit and pause", "Keep OnApplicationQuit/Pause calling SaveAllToPrefs");

            Check("BUG-034_NoColourBlindModeClobber", "SettingsManager.cs", false,
                "SetInt(\"ColourBlindMode\"",
                "Accessibility", "High", "Pick a colour-blind mode, then quit",
                "SettingsManager writes a 0/1 toggle that clobbers the chosen mode (e.g. Tritanopia)",
                "Only AccessibilityManager persists ColourBlindMode", "Do not write the ColourBlindMode key from SettingsManager");

            Check("BUG-035_NoScreenShakeKeyClobber", "SettingsManager.cs", false,
                "SetInt(\"ScreenShake\",",
                "Accessibility", "Medium", "Set screen-shake, then restart",
                "Writing a separate 'ScreenShake' int means the value never restores (real key is ScreenShakeScale)",
                "Only AccessibilityManager persists screen-shake", "Do not write a separate ScreenShake key");

            Check("BUG-036_FlameVfxNullGuard", "FlameSequenceController.cs", true,
                "if (vfx == null) yield break;",
                "Code Quality", "Medium", "Trigger the flame sequence before vfx is assigned",
                "NullReferenceException when Play() runs with no vfx",
                "Coroutine yield-breaks when vfx is null", "Keep the vfx null guard");

            Check("BUG-037_StarWheelPivotGuard", "StarWheelSpin.cs", true,
                "if (pivot == null) return;",
                "Code Quality", "Low", "Leave pivot unassigned and enter Play Mode",
                "NullReferenceException every frame in Update",
                "Update returns early when pivot is null", "Keep the pivot null guard");

            Check("BUG-038_ProgressClamped", "RouteGraph.cs", true,
                "return Mathf.Clamp01(prog);",
                "Gameplay", "High", "Cross the finish line exactly",
                "Mathf.Repeat(1,1)=0 reports a finishing racer at the start, corrupting standings",
                "Progress clamped to [0,1]", "Keep Mathf.Clamp01 instead of Mathf.Repeat at progress=1");

            Check("BUG-039_TwoGamepadsDisableKeyboard", "SplitScreenCharacterSelectManager.cs", true,
                "p1KeyboardEnabled = false;",
                "Multiplayer", "Medium", "Character-select with two gamepads connected",
                "Keyboard still drives P1 and a stray Enter confirms P1 unexpectedly",
                "Keyboard ignored for P1 when both players use pads", "Keep the keyboard-disable branch");

            Check("BUG-040_BrightnessViaHdrpExposure", "AccessibilityManager.cs", true,
                "EnsureColorVolume",
                "Accessibility", "Medium", "Drag the brightness slider on PC",
                "Screen.brightness is a no-op on desktop, so the slider does nothing",
                "Brightness drives HDRP exposure on PC", "Keep the runtime HDRP color volume");

            Check("BUG-041_GreyscaleViaSaturation", "AccessibilityManager.cs", true,
                "saturation.value",
                "Accessibility", "Medium", "Enable Achromatopsia mode",
                "A UI tint overlay can't desaturate the scene",
                "True greyscale via ColorAdjustments.saturation = -100", "Keep the saturation override");

            Check("BUG-042_StarWheelClassNameMatchesFile", "StarWheelSpin.cs", true,
                "class StarWheelSpin",
                "Code Quality", "Medium", "Inspect the StarWheelSpin component",
                "Class name mismatch breaks the MonoBehaviour resolve (asset-DB warning)",
                "Class name matches StarWheelSpin.cs", "Keep the class name in sync with the file");

            Check("BUG-043_LeanAngleNormalised", "BikeSuspensionForces.cs", true,
                "rawLeanZ > 180f ? rawLeanZ - 360f",
                "Physics", "Medium", "Lean hard right while grounded",
                "Wrapped 0-360 euler makes a right lean read ~340° and can reverse the suspension force",
                "Lean angle normalised to [-180,180]", "Keep the rawLeanZ normalisation (also in BikeAIController)");

            Check("BUG-044_TrafficSpeedUnitsMetresPerSecond", "TrafficSpawner.cs", true,
                "Metres per second (NOT km/h)",
                "Gameplay", "Low", "Tune traffic min/max speed",
                "Values are consumed as m/s by SplineMover, not km/h — easy to mis-set",
                "Speed fields documented as m/s", "Keep the m/s tooltips on min/maxSpeed");

            Check("BUG-045_NoEngineVolumeResetOnGearChange", "BikeController.cs", true,
                "// BUG-045",
                "Audio", "Low", "Shift gears repeatedly while accelerating",
                "Resetting engine volume to 0.5 on every gear change causes an audible pop",
                "Volume left to UpdateEngineSound's per-frame interpolation",
                "Documentation-anchored: keep the BUG-045 note in CurrentGearProperty; do not re-add a volume reset there");

            Check("BUG-046_KnockbackRigidbodyGuard", "KnockbackObject.cs", true,
                "if (rb == null)",
                "Code Quality", "Medium", "Add KnockbackObject to an object with no Rigidbody",
                "GetComponent returns null and rb usage NullRefs here and in LaunchSelf",
                "Component disables itself with a clear error", "Keep the rb null guard");

            Check("BUG-047_LapDetectionBand", "RaceManager.cs", true,
                "r.lastT > 0.75f && t < 0.25f",
                "Gameplay", "High", "Respawn just before the line and cross the next frame",
                "Narrow 0.85/0.15 band misses the crossing and the lap isn't counted",
                "Wider 0.75/0.25 band still implies a real backward wrap", "Keep the widened lap band");

            Check("BUG-048_CompleteFinalStandings", "RaceManager.cs", true,
                "ranked.Sort(_standingsComparison)",
                "Gameplay", "High", "Finish a race and read OnRaceComplete standings",
                "Race ends on first finisher; others never added, standings has a single entry",
                "Remaining racers appended ordered by progress", "Keep the ranked.Sort + append on finish");

            Check("BUG-049_FrictionDividedByFixedDelta", "BikeTyreGrip.cs", true,
                "set.frictionCoefficient / dt",
                "Physics", "Medium", "Drive at different physics timesteps",
                "Friction not normalised by dt becomes frame-rate dependent",
                "Per-step lateral velocity change is dt-independent", "Keep the / Time.fixedDeltaTime (also in BikeAIController)");

            Check("BUG-050_CombatInstancesCleared", "CombatSystem.cs", true,
                "_allInstances.Clear()",
                "Code Quality", "Medium", "Enter Play Mode repeatedly with domain reload off",
                "Static instance list survives between sessions, leaving stale/duplicate entries",
                "List cleared on SubsystemRegistration", "Keep the ClearInstances RuntimeInitializeOnLoad");

            Check("BUG-051_GearSoundOnAnyChange", "BikeController.cs", true,
                "!bikeAudio.gearShiftSound.isPlaying && bikeIsGrounded",
                "Audio", "Low", "Downshift during engine braking (no throttle)",
                "Sound only fired under throttle, so engine-braking downshifts were silent",
                "Sound fires on any forward grounded gear change", "Keep the gear-change sound condition");

            Check("BUG-052_AiSpawnSequenceReset", "BikeAILogic.cs", true,
                "static void ResetSpawnSequence()",
                "Gameplay", "Medium", "Run several races in a player build",
                "Static counter carries over between races, skewing AI lane distribution",
                "Counter reset on SubsystemRegistration (runs in builds)", "Keep the RuntimeInitializeOnLoad reset");

            Check("BUG-053_RagdollFirstFrameGuard", "RagdollActivator.cs", true,
                "if (!isRagdollActivated && BikeRb != null)",
                "Code Quality", "Medium", "Let FixedUpdate run before Start",
                "BikeRb chain may still be null on the first physics frame — NullRef",
                "Velocity capture guarded against a null BikeRb", "Keep the BikeRb null guard in FixedUpdate");

            Check("BUG-054_OcclusionIgnoresTrafficLayer", "TrafficSpawner.cs", true,
                "& ~(1 << trafficLayer)",
                "Gameplay", "Medium", "Queue traffic so one vehicle sits in front of a spawn point",
                "A traffic vehicle occludes the spawn point and wrongly suppresses spawning",
                "Occlusion judged against static geometry only", "Keep the Traffic-layer strip from the occlusion mask");

            Check("BUG-055_SafeLookRotation", "BikeAnimationController.cs", true,
                "SafeLookRotation",
                "Code Quality", "Medium", "Land a punch/kick exactly on the rig target",
                "Quaternion.LookRotation on a zero-length dir logs an error and snaps to identity",
                "Falls back to the existing rotation", "Keep the SafeLookRotation helper");

            Check("BUG-056_ColliderHandleChangeFlag", "ColliderAdjustment.cs", true,
                "SetColliderCenter(collider, collider.transform.InverseTransformPoint(center))",
                "Code Quality", "Low", "Move a collider handle in the editor",
                "Inner EndChangeCheck consumed the flag, so MirrorCollider never ran on a Move",
                "Handle just applies the centre; outer EndChangeCheck mirrors", "Do not call EndChangeCheck inside the handle apply");

            Check("BUG-057_CombatCooldownsInFixedUpdate", "CombatSystem.cs", true,
                "_punchTimer - Time.fixedDeltaTime",
                "Gameplay", "Medium", "Run combat AI at a low physics rate",
                "Cooldowns ticked in Update drift from the FixedUpdate AI clock, missing attack windows",
                "Cooldowns tick in FixedUpdate", "Keep the cooldown decrement in FixedUpdate");

            Check("BUG-058_ResetBikeDeadRefGuard", "ResetBike.cs", true,
                "if (rb == null) yield break;",
                "Code Quality", "Medium", "Destroy the bike during a reset",
                "rb becomes a dead reference and touching it throws MissingReferenceException",
                "Coroutine yield-breaks if rb is gone", "Keep the rb null guard after the yield");

            Check("BUG-059_BoostForceInFixedUpdate", "BoostSystem.cs", true,
                "bikeController.transform.forward * boostForce",
                "Physics", "High", "Boost at different frame rates",
                "Boost applied in Update with ForceMode.Force AND a dt multiplier, double-scaled, frame-rate dependent",
                "Boost flagged in Update, applied in FixedUpdate (Acceleration)", "Keep the FixedUpdate AddForce");


            Check("BUG-061_PhysicsMeshDestroyed", "RoadColliderFixer.cs", true,
                "DestroyPhysicsMesh(physics)",
                "Performance", "Medium", "Cook road colliders that contain degenerate meshes",
                "Freshly-built mesh leaks unmanaged memory for every degenerate collider",
                "Unused physics mesh destroyed", "Keep the DestroyPhysicsMesh call");

            Check("BUG-062_SingleSplineCentred", "TrafficLaneNetwork.cs", true,
                "road.laneWidth * (lane - (road.laneAmount - 1) * 0.5f)",
                "Gameplay", "Medium", "Generate traffic splines with perLaneSplines off",
                "The single spline is pushed to the far edge instead of the centreline",
                "Single-spline mode forces a centred offset", "Keep the perLaneSplines ternary on laneOffset");

            Check("BUG-063_OncomingSideTest", "TrafficLaneNetwork.cs", true,
                "bool isOncoming = leftHandTraffic ? laneOffset < 0f : laneOffset > 0f;",
                "Gameplay", "Low", "Check which side oncoming traffic drives on",
                "Axis-convention assumption can put oncoming traffic on the wrong side",
                "Oncoming side derived from leftHandTraffic + laneOffset", "Keep the isOncoming computation");

            Check("BUG-064_NearMissTrueElapsed", "BoostSystem.cs", true,
                "_nearMissElapsed += Time.deltaTime;",
                "Gameplay", "Medium", "Trigger near-misses at high fps",
                "Swept distance uses 3× the current frame dt, under covering at high fps",
                "True elapsed time accumulated between checks", "Keep _nearMissElapsed accumulation");

            Check("BUG-065_NearMissSoundOnce", "BoostSystem.cs", true,
                "if (anyNearMiss)",
                "Audio", "Low", "Pass several vehicles in one near-miss check",
                "Near-miss sound plays once per qualifying vehicle (overlapping spam)",
                "Sound plays once per check", "Keep the single anyNearMiss PlayOneShot");

            Check("BUG-066_RegenSourcesAreBooleans", "BoostUI.cs", true,
                "AnyRegenActive => m_WheelieRegen || m_DriftRegen",
                "UI", "Medium", "Enable the boost UI mid-regen",
                "An int counter can be driven negative by an End with no Start, sticking the bar",
                "Each regen source tracked as a bool", "Keep AnyRegenActive over a counter");

            Check("BUG-067_BoostGlowDrains", "BoostWheelGlow.cs", true,
                "// BUG-067",
                "Visual", "Low", "Watch the wheel glow while boosting",
                "Boosting branch was identical to charging, so the glow tracked the meter instead of draining",
                "While boosting the glow eases to the default/spent look",
                "Documentation-anchored: keep the BUG-067 note; the boosting branch must target defaultIntensity/Colour, not the meter");

            Check("BUG-068_PerSubmeshEmptyCheck", "RoadColliderFixer.cs", true,
                "physics.GetTriangles(s).Length > 0",
                "Performance", "Medium", "Cook a collider whose submeshes are individually empty",
                "physics.triangles concatenation hides a per-submesh empty/degenerate case",
                "Each submesh inspected individually", "Keep the per-submesh GetTriangles loop");

            Check("BUG-069_WheelieComboMultipliesAward", "BoostSystem.cs", true,
                "award *= wheelieNearMissBoostMultiplier;",
                "Gameplay", "Medium", "Near-miss during a wheelie with a nearly full meter",
                "Multiplying the already-updated meter just clamps to 1.0 and wastes the bonus",
                "Multiplier applied to the award before it's added", "Keep the award *= multiplier order");

            Check("BUG-070_BlurTimerDecrementBeforeSample", "NearMissFX.cs", true,
                "m_BlurTimer - Time.deltaTime",
                "Visual", "Low", "Watch the near miss blur fade out",
                "Sampling before decrement reads full blur on the zero frame; fade starts a frame late",
                "Timer decremented before sampling", "Keep the decrement above the targetBlur sample");

            Debug.Log($"  Static known-bug scan: {present.Count} still present");
            return present;
        }

        // Summary & export

        private void PrintSummary()
        {
            int total = _passed.Count + _failed.Count;
            Debug.Log("══════════════════════════════════════════════════════");
            Debug.Log($"  Bug Tests: {_passed.Count} fixed  {_failed.Count} still present  ({total} total)");
            if (_failed.Count > 0)
            {
                var sb = new StringBuilder("  Still present:\n");
                foreach (var f in _failed) sb.AppendLine($"    ✗ {f}");
                Debug.LogWarning(sb.ToString());
            }
            Debug.Log("══════════════════════════════════════════════════════");
        }

        private void FlushBugs()
        {
            try
            {
                string qaDir    = Path.Combine(Application.dataPath, "../QA");
                string jsonPath = Path.Combine(qaDir, "bug_findings.json");
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
                Debug.Log($"[MotoSquidBugTests] Bug details written to {jsonPath}\n" +
                          $"Run:  cd {qaDir} && .venv/bin/python append_bugs.py bug_findings.json " +
                          $"../../MotoSquid_BugTracker.xlsx");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MotoSquidBugTests] Could not write bug JSON: {ex.Message}");
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
