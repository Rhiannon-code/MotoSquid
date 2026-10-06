using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
namespace MotoSquid.DevTools
{
    public class MotoSquidQAWindow : EditorWindow
    {
        // Paths
        static string ProjectRoot    => Path.GetDirectoryName(Application.dataPath);
        static string QADir          => Path.Combine(ProjectRoot, "QA");
        static string FindingsJson   => Path.Combine(QADir, "qa_findings.json");
        static string BugTrackerPath => Path.Combine(Path.GetDirectoryName(ProjectRoot), "MotoSquid_BugTracker.xlsx");
        static string PythonExe      => Path.Combine(QADir, ".venv/bin/python");
        static string AppendScript   => Path.Combine(QADir, "append_bugs.py");

        // Prefab/asset folders to scan (third party assets excluded to avoid noise)
        static readonly string[] ScanFolders = { "Assets/MotoSquid", "Assets/Prefabs" };
        static readonly string   ScriptRoot  = "Assets/MotoSquid/Scripts";

        // State
        class Finding
        {
            public bool   export = true;
            public string title, category, priority, assignedTo;
            public string sceneComponent, stepsToReproduce, expectedBehaviour, actualBehaviour, notes;
        }

        readonly List<Finding> _findings = new List<Finding>();
        Vector2 _scroll;
        string  _status = "Click 'Run Checks' to scan the project.";

        // Window
        [MenuItem("MotoSquid/QA Checker")]
        public static void Open() => GetWindow<MotoSquidQAWindow>("MotoSquid QA");

        void OnGUI()
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField("MotoSquid QA Checker", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Scans: prefabs, open scenes, collaborator scripts", EditorStyles.miniLabel);
            GUILayout.Space(6);

            if (GUILayout.Button("Run All Checks", GUILayout.Height(32)))
                RunAllChecks();

            GUILayout.Space(4);
            var msgType = _findings.Count > 0 ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(_status, msgType);

            if (_findings.Count == 0) return;

            GUILayout.Space(6);

            // Select/deselect all row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Findings ({_findings.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("All",  GUILayout.Width(40))) foreach (var f in _findings) f.export = true;
            if (GUILayout.Button("None", GUILayout.Width(44))) foreach (var f in _findings) f.export = false;
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var f in _findings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                f.export = EditorGUILayout.Toggle(f.export, GUILayout.Width(18));
                var priorityColor = f.priority == "Critical" ? Color.red
                                  : f.priority == "High"     ? new Color(1f, 0.5f, 0f)
                                  : f.priority == "Medium"   ? Color.yellow
                                                             : Color.white;
                var prevColor = GUI.color;
                GUI.color = priorityColor;
                EditorGUILayout.LabelField($"[{f.priority}]", GUILayout.Width(66));
                GUI.color = prevColor;
                EditorGUILayout.LabelField($"[{f.category}]  {f.title}", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(f.sceneComponent, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(f.actualBehaviour, EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.EndVertical();
                GUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(6);
            int selectedCount = _findings.Count(f => f.export);
            GUI.enabled = selectedCount > 0;
            if (GUILayout.Button($"Export {selectedCount} Selected: Bug Tracker", GUILayout.Height(30)))
                ExportToBugTracker();
            GUI.enabled = true;
        }

        // Main scan
        void RunAllChecks()
        {
            _findings.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("MotoSquid QA", "Scanning prefabs...", 0f);
                CheckPrefabs();

                EditorUtility.DisplayProgressBar("MotoSquid QA", "Scanning open scenes...", 0.75f);
                CheckOpenScenes();

                EditorUtility.DisplayProgressBar("MotoSquid QA", "Scanning collaborator scripts...", 0.9f);
                CheckScripts();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            int critical = _findings.Count(f => f.priority == "Critical");
            int high     = _findings.Count(f => f.priority == "High");
            _status = _findings.Count == 0
                ? "No issues found."
                : $"Scan complete, {_findings.Count} issue(s) found ({critical} Critical, {high} High).";

            Repaint();
        }

        // Check: Prefabs
        void CheckPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", ScanFolders);

            for (int i = 0; i < guids.Length; i++)
            {
                string path   = AssetDatabase.GUIDToAssetPath(guids[i]);
                string display = path.Replace("Assets/", "");
                EditorUtility.DisplayProgressBar("MotoSquid QA", $"Prefab: {Path.GetFileName(path)}", 0.75f * i / guids.Length);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                string assignee = "Unassigned";
                bool   isCam    = path.ToLower().Contains("camera") || prefab.name.ToLower().Contains("camera");

                // 1. Stray Camera in non camera prefab
                if (!isCam)
                {
                    foreach (var cam in prefab.GetComponentsInChildren<Camera>(true))
                    {
                        string goName = cam.gameObject.name;
                        if (goName.ToLower().Contains("camera") || goName.ToLower() == "cam") continue;

                        Add(new Finding
                        {
                            title            = $"Stray Camera component in prefab: {prefab.name}",
                            category         = "Performance",
                            priority         = "High",
                            assignedTo       = assignee,
                            sceneComponent   = $"{display} → {goName}",
                            stepsToReproduce = "Place multiple instances of this prefab in the scene and enter Play Mode",
                            expectedBehaviour = "No unnecessary HDRP render passes",
                            actualBehaviour  = $"Camera on '{goName}' creates one full HDRP render pass per prefab instance",
                            notes            = "Remove the Camera component, HDRP spawns a full render pipeline pass per Camera at startup"
                        });
                    }
                }

                // 2. VisualEffect enabled in Edit Mode
                foreach (var vfx in prefab.GetComponentsInChildren<VisualEffect>(true))
                {
                    if (!vfx.enabled) continue;
                    Add(new Finding
                    {
                        title            = $"VFX component enabled in Edit Mode: {prefab.name}",
                        category         = "Performance",
                        priority         = "Medium",
                        assignedTo       = assignee,
                        sceneComponent   = $"{display} to {vfx.gameObject.name}",
                        stepsToReproduce = "Open the project; observe GPU usage in Edit Mode",
                        expectedBehaviour = "VFX should not simulate until Play Mode",
                        actualBehaviour  = $"VisualEffect '{vfx.gameObject.name}' is enabled and simulates in Edit Mode",
                        notes            = "Disable the component in Awake with an [ExecuteInEditMode] guard, or stagger startup with Random.Range delay"
                    });
                }

                // 3. Missing script references
                int missing = prefab.GetComponentsInChildren<Transform>(true)
                                   .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if (missing > 0)
                {
                    Add(new Finding
                    {
                        title            = $"Missing script reference(s) in prefab: {prefab.name}",
                        category         = "Gameplay",
                        priority         = "Critical",
                        assignedTo       = assignee,
                        sceneComponent   = display,
                        stepsToReproduce = "Select the prefab in the Project window and inspect its components",
                        expectedBehaviour = "All MonoBehaviour scripts resolve correctly",
                        actualBehaviour  = $"{missing} missing script stub(s), class not found",
                        notes            = "Reimport the script or reattach the MonoBehaviour; check if the class was renamed"
                    });
                }

                // 4. Null material slot on Renderer
                foreach (var rend in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (!rend.sharedMaterials.Any(m => m == null)) continue;
                    Add(new Finding
                    {
                        title            = $"Null material slot on '{rend.gameObject.name}' in {prefab.name}",
                        category         = "Art / Rendering",
                        priority         = "High",
                        assignedTo       = assignee,
                        sceneComponent   = $"{display} → {rend.gameObject.name}",
                        stepsToReproduce = "Place prefab in scene and view in Game or Scene view",
                        expectedBehaviour = "Object renders with correct material",
                        actualBehaviour  = "Material slot is null, object renders as pink (HDRP error material)",
                        notes            = "Reassign the material in the Inspector"
                    });
                    break; // One finding per prefab is enough
                }

                // 5. AudioListener in non camera/player prefab
                if (!prefab.name.ToLower().Contains("camera") && !prefab.name.ToLower().Contains("player"))
                {
                    var listeners = prefab.GetComponentsInChildren<AudioListener>(true);
                    if (listeners.Length > 0)
                    {
                        Add(new Finding
                        {
                            title            = $"AudioListener in non-camera prefab: {prefab.name}",
                            category         = "Audio",
                            priority         = "High",
                            assignedTo       = assignee,
                            sceneComponent   = $"{display} to {listeners[0].gameObject.name}",
                            stepsToReproduce = "Place multiple instances of this prefab in a scene, enter Play Mode",
                            expectedBehaviour = "No duplicate AudioListener warnings",
                            actualBehaviour  = "Multiple prefab instances each carry an AudioListener, Unity warns and uses only one",
                            notes            = "Remove AudioListener from this prefab; it belongs only on the Main Camera"
                        });
                    }
                }
            }
        }

        // Check: Open Scenes
        void CheckOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                GameObject[] roots = scene.GetRootGameObjects();

                // Multiple AudioListeners
                var listeners = roots.SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).ToList();
                if (listeners.Count > 1)
                {
                    string names = string.Join(", ", listeners.Select(l => l.gameObject.name));
                    Add(new Finding
                    {
                        title            = $"Multiple AudioListeners in scene: {scene.name}",
                        category         = "Audio",
                        priority         = "High",
                        assignedTo       = "Unassigned",
                        sceneComponent   = scene.path,
                        stepsToReproduce = "Enter Play Mode in this scene",
                        expectedBehaviour = "Single AudioListener in scene",
                        actualBehaviour  = $"{listeners.Count} AudioListeners found: {names}",
                        notes            = "Remove extra AudioListeners; keep only the one on the Main Camera"
                    });
                }

                // Missing scripts in scene
                var missingObjects = new List<string>();
                int totalMissing   = 0;
                foreach (var root in roots)
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                        if (n <= 0) continue;
                        totalMissing += n;
                        missingObjects.Add(t.gameObject.name);
                    }
                }

                if (totalMissing > 0)
                {
                    string list = string.Join(", ", missingObjects.Take(5));
                    if (missingObjects.Count > 5) list += $" (+{missingObjects.Count - 5} more)";

                    Add(new Finding
                    {
                        title            = $"Missing script references in scene: {scene.name}",
                        category         = "Gameplay",
                        priority         = "Critical",
                        assignedTo       = "Unassigned",
                        sceneComponent   = scene.path,
                        stepsToReproduce = "Enter Play Mode; observe MissingReferenceException in console",
                        expectedBehaviour = "All scripts resolve correctly",
                        actualBehaviour  = $"{totalMissing} missing script(s) on: {list}",
                        notes            = "Check if scripts were renamed or deleted; reattach in Inspector"
                    });
                }
            }
        }

        // Check: Scripts (TODO/FIXME)
        void CheckScripts()
        {
            string scriptDir = Path.Combine(Application.dataPath, "MotoSquid", "Scripts");
            if (!Directory.Exists(scriptDir)) return;

            string[] csFiles    = Directory.GetFiles(scriptDir, "*.cs", SearchOption.AllDirectories);
            var      todoPattern = new Regex(@"//\s*(TODO|FIXME|HACK|XXX)[\s:\-]+(.+)", RegexOptions.IgnoreCase);

            foreach (string file in csFiles)
            {
                string relPath  = "Assets" + file.Replace(Application.dataPath, "").Replace('\\', '/');
                string assignee = "Unassigned";
                string[] lines  = File.ReadAllLines(file);

                for (int lineNum = 0; lineNum < lines.Length; lineNum++)
                {
                    Match m = todoPattern.Match(lines[lineNum]);
                    if (!m.Success) continue;

                    string tag      = m.Groups[1].Value.ToUpper();
                    string message  = m.Groups[2].Value.Trim();
                    string shortMsg = message.Length > 80 ? message.Substring(0, 80) + "…" : message;

                    Add(new Finding
                    {
                        title            = $"{tag}: {shortMsg}",
                        category         = "Code Quality",
                        priority         = tag is "FIXME" or "HACK" ? "Medium" : "Low",
                        assignedTo       = assignee,
                        sceneComponent   = $"{relPath}:{lineNum + 1}",
                        stepsToReproduce = $"Open {Path.GetFileName(file)} at line {lineNum + 1}",
                        expectedBehaviour = "Code is complete and correct",
                        actualBehaviour  = $"{tag} comment left in code: \"{message}\"",
                        notes            = message
                    });
                }
            }
        }

        // Export
        void ExportToBugTracker()
        {
            var toExport = _findings.Where(f => f.export).ToList();
            if (toExport.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing Selected", "Tick at least one finding to export.", "OK");
                return;
            }

            if (!File.Exists(PythonExe))
            {
                EditorUtility.DisplayDialog("Python Not Found",
                    $"Expected venv at:\n{PythonExe}\n\nRun: uv venv QA/.venv && uv pip install openpyxl --python QA/.venv/bin/python",
                    "OK");
                return;
            }

            Directory.CreateDirectory(QADir);
            File.WriteAllText(FindingsJson, SerializeFindings(toExport), Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName               = PythonExe,
                Arguments              = $"\"{AppendScript}\" \"{FindingsJson}\" \"{BugTrackerPath}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            using var proc = Process.Start(psi);
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode == 0)
            {
                _status = $"Exported, {stdout.Trim()}";
                EditorUtility.DisplayDialog("Export Complete",
                    $"{stdout.Trim()}\n\nFile: {BugTrackerPath}", "OK");
                foreach (var f in toExport) f.export = false;
            }
            else
            {
                _status = "Export failed, see console";
                UnityEngine.Debug.LogError($"[MotoSquid QA] Export error:\n{stderr}");
                EditorUtility.DisplayDialog("Export Failed",
                    $"Python script returned an error. Check the Console for details.\n\n{stderr.Split('\n').FirstOrDefault()}", "OK");
            }

            Repaint();
        }

        // Helpers
        void Add(Finding f) => _findings.Add(f);


        static string SerializeFindings(List<Finding> findings)
        {
            var sb = new StringBuilder("[\n");
            for (int i = 0; i < findings.Count; i++)
            {
                var f = findings[i];
                sb.AppendLine("  {");
                sb.AppendLine($"    \"title\": {J(f.title)},");
                sb.AppendLine($"    \"category\": {J(f.category)},");
                sb.AppendLine($"    \"priority\": {J(f.priority)},");
                sb.AppendLine($"    \"assignedTo\": {J(f.assignedTo)},");
                sb.AppendLine($"    \"sceneComponent\": {J(f.sceneComponent)},");
                sb.AppendLine($"    \"stepsToReproduce\": {J(f.stepsToReproduce)},");
                sb.AppendLine($"    \"expectedBehaviour\": {J(f.expectedBehaviour)},");
                sb.AppendLine($"    \"actualBehaviour\": {J(f.actualBehaviour)},");
                sb.AppendLine($"    \"notes\": {J(f.notes)}");
                sb.Append(i < findings.Count - 1 ? "  },\n" : "  }\n");
            }
            sb.Append(']');
            return sb.ToString();
        }

        static string J(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\")
                           .Replace("\"", "\\\"")
                           .Replace("\n", "\\n")
                           .Replace("\r", "") + "\"";
        }
    }
}
