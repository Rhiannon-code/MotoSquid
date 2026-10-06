using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotoSquid.Tests
{
    // MotoSquid Feature QA suite
    public class MotoSquidFeatureTests : MonoBehaviour
    {
        public const string PREF_KEY = "MotoSquid_RunFeatureTests";
        private const string SCENE   = "Main";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (PlayerPrefs.GetInt(PREF_KEY, 0) == 0) return;
            PlayerPrefs.SetInt(PREF_KEY, 0);
            PlayerPrefs.Save();
            var go = new GameObject("[MotoSquidFeatureTests]");
            DontDestroyOnLoad(go);
            go.AddComponent<MotoSquidFeatureTests>();
        }

        private readonly List<string> _passed  = new List<string>();
        private readonly List<string> _failed  = new List<string>();
        private readonly List<string> _skipped = new List<string>();
        private readonly List<Dictionary<string, string>> _bugs = new List<Dictionary<string, string>>();

        private void Pass(string name)
        {
            _passed.Add(name);
            Debug.Log($"  <color=green>✓</color>  {name}");
        }

        private void Skip(string name, string reason)
        {
            _skipped.Add(name);
            Debug.Log($"  -  {name} (skipped: {reason})");
        }

        private void Fail(string name, string reason, string category, string owner,
                          string steps = "", string expected = "", string notes = "")
        {
            _failed.Add(name);
            Debug.LogWarning($"  <color=red>✗</color>  {name}\n     {reason}");
            _bugs.Add(new Dictionary<string, string>
            {
                ["title"]             = name,
                ["category"]          = category,
                ["priority"]          = "Medium",
                ["assignedTo"]        = owner,
                ["sceneComponent"]    = SCENE,
                ["stepsToReproduce"]  = steps,
                ["expectedBehaviour"] = expected,
                ["actualBehaviour"]   = reason,
                ["notes"]             = notes,
            });
        }

        // Reflection helpers

        private static Type FindType(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                    if (t != null && t.Name == simpleName && (t.Namespace ?? "").StartsWith("MotoSquid")) return t;
            }
            return null;
        }

        private const BindingFlags ANY =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static bool HasMember(Type t, string member)
        {
            if (t == null) return false;
            // Array lookups avoid AmbiguousMatchException on overloaded methods/properties
            foreach (var m in t.GetMembers(ANY))
                if (m.Name == member) return true;
            return false;
        }

        private static object Singleton(Type t)
        {
            if (t == null) return null;
            var p = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (p != null) return p.GetValue(null);
            var f = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
            return f != null ? f.GetValue(null) : null;
        }

        // A declarative probe, feature is "present" if its type exists (and member, if given)
        private void Probe(string name, string typeName, string member, string owner, string category)
        {
            var t = FindType(typeName);
            if (t == null) { Skip(name, $"type '{typeName}' not implemented yet"); return; }
            if (!string.IsNullOrEmpty(member) && !HasMember(t, member))
            {
                Skip(name, $"'{typeName}.{member}' not implemented yet");
                return;
            }
            Pass(name);
        }

        private IEnumerator LoadMain()
        {
            if (SceneManager.GetActiveScene().name != SCENE)
                yield return SceneManager.LoadSceneAsync(SCENE);
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.2f);
            Debug.Log("══════════════════════════════════════════════════════");
            Debug.Log("  MotoSquid Feature QA Tests (new QoL/play-feel)");
            Debug.Log("══════════════════════════════════════════════════════");

            yield return LoadMain();

            Debug.Log("\n Play-Feel ");
            Probe("Feel_RumbleManagerExists",        "RumbleManager",      "Pulse",            "Unassigned", "Feel");
            yield return SettingsBackendRoundTrip();   // Behavioural
            Probe("Feel_HitStopSystemExists",        "HitStopController",  "",                 "Unassigned", "Feel");
            Probe("Feel_SpeedWindAudioExists",       "BikeAudioController", "windSource",       "Unassigned", "Audio");
            Probe("Feel_EngineBackfireExists",       "BikeController",     "backfireClip",     "Unassigned", "Audio");
            Probe("Feel_CameraDutchRollExists",      "CameraController",   "dutchRollStrength","Unassigned", "Feel");
            Probe("Feel_HighSpeedScreenFXExists",    "BikeSpeedFX",                 "currentIntensity", "Unassigned", "Feel");
            Probe("Feel_LandingImpactFXExists",      "LandingImpactFX",    "",                 "Unassigned", "Feel");
            Probe("Feel_PerfectStartExists",         "RaceManager",        "perfectStartWindow","Unassigned","Gameplay");
            Probe("Feel_SurfaceAudioExists",         "SurfaceAudio",       "",                 "Unassigned", "Audio");

            Debug.Log("\n Systems QoL: UI and flow ");
            yield return SaveFileExpanded();           // Behavioural
            Probe("QoL_LapSplitHUDExists",           "HUDManager",         "lapTimeText",      "Unassigned",   "UI");
            Probe("QoL_ResetPromptExists",           "ResetBike",          "resetPromptUI",    "Unassigned",   "UI");
            Probe("QoL_RestartRaceExists",           "PauseMenuManager",   "RestartRace",      "Unassigned",   "UI");
            Probe("QoL_NowPlayingSkipExists",        "MusicManager",         "SkipTrack",        "Unassigned",   "Audio");
            Probe("QoL_NearMissBoostLoopExists",     "BoostSystem",                 "AddChargeFromNearMiss","Unassigned","Gameplay");

            Debug.Log("\n Systems QoL: gameplay ");
            Probe("QoL_DifficultyScalingExists",     "BikeAILogic",        "ApplyDifficultyPreset", "Unassigned", "AI");
            Probe("QoL_GhostRacerExists",            "GhostRecorder",      "",                 "Unassigned", "Gameplay");

            PrintSummary();
            if (_bugs.Count > 0) FlushBugs();

            Destroy(gameObject);
        }

        // Behavioural the Rumble accessibility toggle actually drives the settings model
        private IEnumerator SettingsBackendRoundTrip()
        {
            const string N = "Settings_AccessibilityToggleRoundTrips";
            var accT = FindType("AccessibilityManager");
            if (accT == null) { Skip(N, "AccessibilityManager not present"); yield break; }
            var inst = Singleton(accT);
            if (inst == null) { Skip(N, "no AccessibilityManager instance in scene"); yield break; }

            var setRumble = accT.GetMethod("SetRumble", ANY);
            var prop      = accT.GetProperty("RumbleEnabled", ANY);
            if (setRumble == null || prop == null)
            { Skip(N, "SetRumble/RumbleEnabled missing"); yield break; }

            bool orig = (bool)prop.GetValue(inst);
            setRumble.Invoke(inst, new object[] { !orig });
            yield return null;
            bool flipped = (bool)prop.GetValue(inst);
            setRumble.Invoke(inst, new object[] { orig });   // restore
            if (flipped == !orig) Pass(N);
            else Fail(N, $"RumbleEnabled stayed {flipped} after SetRumble({!orig})",
                      "Settings", "Unassigned",
                      "Call AccessibilityManager.SetRumble(!RumbleEnabled)",
                      "RumbleEnabled reflects the new value");
        }

        // Behavioural: SaveData expanded beyond bestTime (last char/track, win count, etc.)
        private IEnumerator SaveFileExpanded()
        {
            const string N = "QoL_SaveFileExpanded";
            var smT = FindType("SaveManager");
            if (smT == null) { Skip(N, "SaveManager not present"); yield break; }
            var inst = Singleton(smT);
            var dataField = smT.GetField("currentData", ANY);
            if (inst == null || dataField == null) { Skip(N, "no SaveManager instance/currentData"); yield break; }
            var data = dataField.GetValue(inst);
            if (data == null) { Skip(N, "currentData is null"); yield break; }

            var dt = data.GetType();
            bool expanded = HasMember(dt, "lastSelectedCharacter")
                         || HasMember(dt, "lastSelectedTrack")
                         || HasMember(dt, "winCount")
                         || HasMember(dt, "totalRaces");
            if (expanded) Pass(N);
            else Skip(N, "SaveData not expanded yet (only bestTime/records present)");
            yield break;
        }

        private void PrintSummary()
        {
            int total = _passed.Count + _failed.Count + _skipped.Count;
            Debug.Log("══════════════════════════════════════════════════════");
            Debug.Log($"  Feature QA: {_passed.Count} present  {_failed.Count} broken  " +
                      $"{_skipped.Count} not-yet-implemented  ({total} total)");
            if (_failed.Count > 0)
            {
                var sb = new StringBuilder("  Broken (logged as bugs):\n");
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
                string jsonPath = Path.Combine(qaDir, "feature_findings.json");
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
                Debug.Log($"[MotoSquidFeatureTests] Failures written to {jsonPath}\n" +
                          $"Run:  cd {qaDir} && .venv/bin/python append_bugs.py feature_findings.json " +
                          $"../../MotoSquid_BugTracker.xlsx");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MotoSquidFeatureTests] Could not write findings JSON: {ex.Message}");
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
