using MotoSquid.Combat;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public class RigLabPoseTuner : EditorWindow
    {
        const string CapturePath = "Assets/MotoSquid/Data/RigLab/Data/LiveCapture.json";

        [System.Serializable] class Pose { public string bike; public string target; public Vector3 pos; public Vector3 euler; }
        [System.Serializable] class Capture { public string taken; public List<Pose> poses = new List<Pose>(); }

        enum Forced
        {
            Off, Idle, NormalSpeed, HighSpeed,
        }

        Forced forced = Forced.Off;
        bool activeBikeOnly = true;
        static bool autoCapture = true;
        Vector2 scroll;
        string status = "";
        string[] captureBikes = new string[0];
        int captureBikeIndex;
        bool captureBikesRead;

        readonly Dictionary<BikeAnimationController, Vector2> savedThresholds =
            new Dictionary<BikeAnimationController, Vector2>();

        [MenuItem("Tools/Rig Lab/11. Live Pose Tuner", false, 120)]
        static void Open()
        {
            GetWindow<RigLabPoseTuner>("Pose Tuner").minSize = new Vector2(400, 330);
        }

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            autoCapture = EditorPrefs.GetBool("RigLab.AutoCapture", true);
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode && autoCapture)
                DoCapture(true);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Live Pose Tuner", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play mode to tune. Drag the targets under\n" +
                    "Bike Model > Biker Animation Targets  (NOT the ones under Biker Rig -\n" +
                    "those are overwritten every frame by the controller).", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Drag targets in the Scene view and the rider follows. Capture before you stop.",
                    MessageType.None);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.LabelField("Force pose while stationary", EditorStyles.boldLabel);
                var next = (Forced)EditorGUILayout.EnumPopup("Pose", forced);
                if (next != forced) { forced = next; ApplyForcedPose(); RefreshHighlight(); }

                else if (forced != Forced.Off && Event.current.type == EventType.Layout) ApplyForcedPose();
                EditorGUILayout.LabelField("Tunes", ActiveTargetsHint(), EditorStyles.miniLabel);

                activeBikeOnly = EditorGUILayout.Toggle("Active bike only", activeBikeOnly);
                if (GUILayout.Button(activeBikeOnly ? "Select this pose's targets (active bike)"
                                                    : "Select this pose's targets (all bikes)"))
                    SelectPoseTargets();
            }

            EditorGUILayout.Space();
            bool gizmos = EditorGUILayout.Toggle("Draw target gizmos", RigLabTargetGizmos.Enabled);
            if (gizmos != RigLabTargetGizmos.Enabled)
            {
                RigLabTargetGizmos.Enabled = gizmos;
                SceneView.RepaintAll();
            }

            autoCapture = EditorGUILayout.Toggle("Auto capture on leaving Play", autoCapture);
            EditorPrefs.SetBool("RigLab.AutoCapture", autoCapture);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
                if (GUILayout.Button("Capture now")) DoCapture(false);

            EditorGUILayout.Space();
            bool have = File.Exists(CapturePath);
            EditorGUILayout.LabelField("Captured", have ? Taken() : "nothing captured yet");

            using (new EditorGUI.DisabledScope(!have || Application.isPlaying))
                if (GUILayout.Button("Write captured values into the scene"))
                    ApplyCapture();

            using (new EditorGUI.DisabledScope(!have || Application.isPlaying))
            {
                if (!captureBikesRead && have && Event.current.type == EventType.Layout)
                    RefreshCaptureBikes();

                if (captureBikes.Length > 0)
                {
                    captureBikeIndex = Mathf.Clamp(captureBikeIndex, 0, captureBikes.Length - 1);
                    captureBikeIndex = EditorGUILayout.Popup("Tuned bike", captureBikeIndex, captureBikes);
                }

                if (GUILayout.Button("Write that bike's riders into the bike PREFABS"))
                {
                    string bike = captureBikes.Length > 0 ? captureBikes[captureBikeIndex] : null;
                    EditorApplication.delayCall += () => WriteToPrefabs(bike);
                }
            }

            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(status, MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            if (GUILayout.Button("Restore speed thresholds to stock (" + StockNormal + " / " + StockHigh + ")"))
                RestoreThresholds();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Knee hints  (edit mode)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Knee hints cannot be dragged in Play: RiderLeanSplay rewrites them every frame from " +
                "a rest read once at Start, and it only splays them with lean, so at zero lean the " +
                "knees sit wherever the rest is. Too narrow and they sink into the chassis.\n" +
                "This sets each rider's rest hint from its OWN foot target plus the margin, so riders " +
                "of different widths do not share one value.", MessageType.None);

            kneeMargin = EditorGUILayout.Slider("Margin past the foot (m)", kneeMargin, 0.05f, 0.45f);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("Fit knee hints from foot targets"))
                    EditorApplication.delayCall += FitKneeHints;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Leg target offset  (TEMPORARY)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "For a character whose foot mesh is not weighted to its foot bone: the rig places the " +
                "bone correctly and no mesh follows, so the visible foot misses the peg. Shifting the " +
                "leg targets lands the mesh where it should be. The ankle will not articulate, that " +
                "needs a re skin.\n" +
                "Applies to the ACTIVE rider's Idle and InMotion leg targets, both sides. Reverse " +
                "targets are skipped because the walk cycle owns them. Additive, so Ctrl+Z reverts.",
                MessageType.None);

            legOffset = EditorGUILayout.Vector3Field("Offset (m)", legOffset);
            using (new EditorGUI.DisabledScope(legOffset == Vector3.zero))
                if (GUILayout.Button("Shift active rider's leg targets")) ShiftLegTargets();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Notes", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(
                ", Leg hints are owned by RiderLeanSplay and rest at whatever the prefab holds,\n" +
                "  so they are fitted in edit mode above, not dragged live.\n" +
                ", Hand hints are not touched by the controller, live dragging works.\n" +
                ", Spine targets are ROTATION ONLY, the spine rigs ignore their position.\n" +
                "  Rotate them (E), dragging does nothing.\n" +
                ", After hand tuning, press 'Forget baseline' in Rider Fit so it re-reads\n" +
                "  from your new values instead of the shipped ones.",
                EditorStyles.wordWrappedMiniLabel, GUILayout.Height(70));
            EditorGUILayout.EndScrollView();
        }

        string ActiveTargetsHint()
        {
            switch (forced)
            {
                case Forced.Idle: return "hip/spine/leg Idle targets";
                case Forced.NormalSpeed: return "hip/spine Normal + leg InMotion";
                case Forced.HighSpeed: return "hip/spine High + leg InMotion";
                default: return "whatever the current speed selects";
            }
        }

        void ApplyForcedPose()
        {
            foreach (var ctrl in Object.FindObjectsByType<BikeAnimationController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!savedThresholds.ContainsKey(ctrl))
                {
                    bool forcedAlready = Mathf.Abs(ctrl.normalSpeedThreshold) > 9000f ||
                                         Mathf.Abs(ctrl.highSpeedThreshold) > 9000f;
                    savedThresholds[ctrl] = forcedAlready
                        ? new Vector2(StockNormal, StockHigh)
                        : new Vector2(ctrl.normalSpeedThreshold, ctrl.highSpeedThreshold);
                }

                var original = savedThresholds[ctrl];
                switch (forced)
                {
                    case Forced.Idle: ctrl.normalSpeedThreshold = 9999f; ctrl.highSpeedThreshold = 9999f; ctrl.forcedSpeedBlend = -1f; break;
                    case Forced.NormalSpeed: ctrl.normalSpeedThreshold = -9999f; ctrl.highSpeedThreshold = 9999f; ctrl.forcedSpeedBlend = 0f; break;
                    case Forced.HighSpeed: ctrl.normalSpeedThreshold = -9999f; ctrl.highSpeedThreshold = -9999f; ctrl.forcedSpeedBlend = 1f; break;
                    default: ctrl.normalSpeedThreshold = original.x; ctrl.highSpeedThreshold = original.y; ctrl.forcedSpeedBlend = -1f; break;
                }

                ApplyToDriver(ctrl);
            }
            status = forced == Forced.Off ? "Thresholds restored." : "Forcing " + forced + " on every bike.";
        }

        void RefreshHighlight()
        {
            RigLabTargetGizmos.Highlight = new HashSet<string>(FieldsForPose());
            SceneView.RepaintAll();
        }

        void ApplyToDriver(BikeAnimationController ctrl)
        {
        }

        void SelectPoseTargets()
        {
            var picked = new List<Object>();
            foreach (var bike in RigLabScope.BikeRoots())
            {
                if (activeBikeOnly && !bike.activeInHierarchy) continue;
                var ctrl = RigLabRiders.ActiveOn(bike);
                if (ctrl == null) continue;
                var driver = ctrl.GetComponent<CombatRigDriver>();
                foreach (var name in FieldsForPose())
                {
                    Transform tr = null;
                    var f = typeof(BikeAnimationController).GetField(name);
                    if (f != null) tr = f.GetValue(ctrl) as Transform;
                    if (tr == null && driver != null)
                    {
                        var cf = typeof(CombatRigDriver).GetField(name);
                        if (cf != null && cf.FieldType == typeof(Transform)) tr = cf.GetValue(driver) as Transform;
                    }
                    if (tr != null) picked.Add(tr.gameObject);
                }
            }
            Selection.objects = picked.ToArray();
            status = picked.Count == 0
                ? "Nothing selected, is the bike you are looking at active?"
                : "Selected " + picked.Count + " target(s).";
        }

        Vector3 legOffset;
        float kneeMargin = 0.2f;
        const float StockNormal = 1.0f, StockHigh = 60.0f;

        void FitKneeHints()
        {
            var found = RigLabScope.BikeRoots(true);
            var roots = RigLabScope.Narrow(found);
            var log = new System.Text.StringBuilder();
            int n = 0;

            foreach (var root in roots)
                foreach (var rider in RigLabRiders.AllOn(root))
                {
                    var refs = rider.GetComponentInChildren<BikerRigReferences>(true);
                    if (refs == null || refs.leftLegHint == null || refs.rightLegHint == null ||
                        refs.leftLegTarget == null) continue;

                    float x = Mathf.Abs(refs.leftLegTarget.localPosition.x) + kneeMargin;
                    Undo.RecordObject(refs.leftLegHint, "Fit knee hints");
                    Undo.RecordObject(refs.rightLegHint, "Fit knee hints");
                    log.Append("   ").Append(rider.gameObject.name).Append("  hint x ")
                       .Append(refs.leftLegHint.localPosition.x.ToString("0.000")).Append(" -> ")
                       .Append((-x).ToString("0.000")).Append('\n');

                    refs.leftLegHint.localPosition = With(refs.leftLegHint.localPosition, -x);
                    refs.rightLegHint.localPosition = With(refs.rightLegHint.localPosition, x);
                    EditorUtility.SetDirty(refs.leftLegHint);
                    EditorUtility.SetDirty(refs.rightLegHint);
                    n++;
                }

            RigLabScope.MarkDirty();
            status = n == 0
                ? "No writable rider found. Open a bike prefab, the open scene's bikes are instances."
                : "Fitted knee hints on " + n + " rider(s) in " + RigLabScope.Where() + "." +
                  RigLabScope.ScopeNote(roots.Length, found.Length);
            if (n == 0) Debug.LogError("Rig Lab: " + status);
            else Debug.Log("Rig Lab: " + status + "\n" + log);
            Repaint();
        }

        static Vector3 With(Vector3 v, float x) { return new Vector3(x, v.y, v.z); }

        void RestoreThresholds()
        {
            int n = 0;
            foreach (var ctrl in Object.FindObjectsByType<BikeAnimationController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(ctrl, "Restore thresholds");
                ctrl.normalSpeedThreshold = StockNormal;
                ctrl.highSpeedThreshold = StockHigh;
                ctrl.forcedSpeedBlend = -1f;
                EditorUtility.SetDirty(ctrl);
                n++;
            }

            savedThresholds.Clear();
            forced = Forced.Off;
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            status = "Restored thresholds on " + n + " controller(s).";
            Debug.Log("Rig Lab: " + status + "  Save the scene to keep it.");
            Repaint();
        }

        static readonly string[] LegFields =
            { "leftlegIdleTarget", "leftlegInMotionTarget", "rightlegIdleTarget", "rightlegInMotionTarget" };

        void ShiftLegTargets()
        {
            int moved = 0;
            foreach (var bike in RigLabScope.BikeRoots())
            {
                if (activeBikeOnly && !bike.activeInHierarchy) continue;
                var ctrl = RigLabRiders.ActiveOn(bike);
                if (ctrl == null) continue;

                foreach (var name in LegFields)
                {
                    var f = typeof(BikeAnimationController).GetField(name);
                    var tr = f != null ? f.GetValue(ctrl) as Transform : null;
                    if (tr == null) continue;
                    Undo.RecordObject(tr, "Shift leg targets");
                    tr.localPosition += legOffset;
                    EditorUtility.SetDirty(tr);
                    moved++;
                }
                status = "Shifted " + moved + " leg target(s) on " + ctrl.gameObject.name + ".";
            }

            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Rig Lab: " + status + (Application.isPlaying
                ? "  [Play: capture and write it in edit mode to keep it]" : ""));
            Repaint();
        }

        string[] FieldsForPose()
        {
            switch (forced)
            {
                case Forced.Idle:
                    return new[] { "hipIdleTarget", "spineIdleTarget", "leftlegIdleTarget", "rightlegIdleTarget",
                                   "leftHandTarget", "rightHandTarget" };
                case Forced.NormalSpeed:
                    return new[] { "hipNormalSpeedTarget", "spineNormalSpeedTarget", "leftlegInMotionTarget",
                                   "rightlegInMotionTarget", "leftHandTarget", "rightHandTarget" };
                case Forced.HighSpeed:
                    return new[] { "hipHighSpeedTarget", "spineHighSpeedTarget", "leftlegInMotionTarget",
                                   "rightlegInMotionTarget", "leftHandTarget", "rightHandTarget" };
                default:
                    return new[] { "leftHandTarget", "rightHandTarget" };
            }
        }

        void DoCapture(bool automatic)
        {
            var capture = new Capture { taken = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };

            foreach (var bike in RigLabScope.BikeRoots())
            {
                foreach (var rider in RigLabRiders.AllOn(bike))
                {
                    string key = bike.name + " / " + rider.gameObject.name;
                    foreach (var name in CapturedFields(rider))
                    {
                        var tr = Resolve(rider, name);
                        if (tr == null) continue;
                        capture.poses.Add(new Pose { bike = key, target = name,
                                                     pos = tr.localPosition, euler = tr.localEulerAngles });
                    }
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CapturePath));
            File.WriteAllText(CapturePath, JsonUtility.ToJson(capture, true));
            RefreshCaptureBikes();
            status = (automatic ? "Auto captured " : "Captured ") + capture.poses.Count +
                     " transform(s) at " + capture.taken + ".";
            Debug.Log("Rig Lab: " + status + (automatic ? "  Press 'Write captured values into the scene' to keep them." : ""));
            Repaint();
        }

        static IEnumerable<string> CapturedFields(BikeAnimationController rider)
        {
            foreach (var f in typeof(BikeAnimationController).GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (f.FieldType == typeof(Transform) && !f.Name.EndsWith("Rig") && !ScriptDriven(f.Name))
                    yield return f.Name;

            yield return "leftHandHint";
            yield return "rightHandHint";

            foreach (var f in typeof(CombatRigDriver).GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (f.FieldType == typeof(Transform) && (f.Name.StartsWith("melee") || f.Name.StartsWith("kick")))
                    yield return f.Name;
        }

        internal static Transform Resolve(BikeAnimationController rider, string field)
        {
            var f = typeof(BikeAnimationController).GetField(field);
            if (f != null && f.FieldType == typeof(Transform)) return f.GetValue(rider) as Transform;

            var driver = rider.GetComponent<CombatRigDriver>();
            var df = driver != null ? typeof(CombatRigDriver).GetField(field) : null;
            if (df != null && df.FieldType == typeof(Transform)) return df.GetValue(driver) as Transform;

            var refs = rider.GetComponentInChildren<BikerRigReferences>(true);
            var rf = refs != null ? typeof(BikerRigReferences).GetField(field) : null;
            if (rf != null && rf.FieldType == typeof(Transform)) return rf.GetValue(refs) as Transform;

            return null;
        }

        void ApplyCapture()
        {
            var capture = JsonUtility.FromJson<Capture>(File.ReadAllText(CapturePath));
            var bikes = RigLabScope.BikeRoots();

            int n = 0, unmatched = 0;
            foreach (var pose in capture.poses)
            {
                var bike = bikes.FirstOrDefault(b => b.name == BikeOf(pose.bike));
                if (bike == null) { unmatched++; continue; }

                var rider = RigLabRiders.AllOn(bike)
                                .FirstOrDefault(r => pose.bike == bike.name + " / " + r.gameObject.name)
                            ?? RigLabRiders.ActiveOn(bike);
                if (rider == null) { unmatched++; continue; }

                var tr = Resolve(rider, pose.target);
                if (tr == null) continue;

                Undo.RecordObject(tr, "Apply captured pose");
                tr.localPosition = pose.pos;
                tr.localEulerAngles = pose.euler;
                n++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            status = "Wrote " + n + " transform(s) from the " + capture.taken + " capture." +
                     (unmatched > 0 ? "  " + unmatched + " entry(s) matched no bike in this scene." : "");

            // Writing nothing used to report success. It is a failure, and it should say so.
            if (n == 0)
                Debug.LogError("Rig Lab: wrote NOTHING from the " + capture.taken + " capture, " +
                               capture.poses.Count + " entries, none matched a bike/rider in this scene.\n" +
                               "Capture again in Play, then write in edit mode.");
            else Debug.Log("Rig Lab: " + status);
        }

        void RefreshCaptureBikes()
        {
            try
            {
                var capture = JsonUtility.FromJson<Capture>(File.ReadAllText(CapturePath));
                captureBikes = capture.poses.Select(p => BikeOf(p.bike)).Distinct().OrderBy(n => n).ToArray();
            }
            catch { captureBikes = new string[0]; }
            captureBikesRead = true;
        }

        static string BikeOf(string key)
        {
            int slash = key.IndexOf(" / ", System.StringComparison.Ordinal);
            return slash < 0 ? key : key.Substring(0, slash);
        }

        static string RiderOf(string key)
        {
            int slash = key.IndexOf(" / ", System.StringComparison.Ordinal);
            return slash < 0 ? null : key.Substring(slash + 3);
        }

        void WriteToPrefabs(string bikeName)
        {
            try { status = WritePrefabs(bikeName); }
            catch (System.Exception e)
            {
                status = "FAILED: " + e.Message;
                Debug.LogException(e);
            }
            Repaint();
        }

        static string WritePrefabs(string bikeName)
        {
            if (string.IsNullOrEmpty(bikeName)) return "No captured bike chosen.";

            var capture = JsonUtility.FromJson<Capture>(File.ReadAllText(CapturePath));
            var wanted = new Dictionary<string, List<Pose>>();
            int bikeLevel = 0;
            foreach (var pose in capture.poses)
            {
                if (BikeOf(pose.bike) != bikeName) continue;
                string rider = RiderOf(pose.bike);
                if (rider == null) { bikeLevel++; continue; }
                if (!wanted.TryGetValue(rider, out var list)) wanted[rider] = list = new List<Pose>();
                list.Add(pose);
            }
            if (wanted.Count == 0)
                return "The " + capture.taken + " capture holds no rider entries for '" + bikeName + "'.";

            // Every bike carries riders with the same names, so without narrowing this writes one
            // bike's tuning into every other bike's prefab
            var found = RigLabScope.BikeRoots();
            var roots = RigLabScope.Narrow(found);

            var paths = roots
                .Select(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot)
                .Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p).ToList();
            if (paths.Count == 0)
                return "No bike prefab instances in " + RigLabScope.Where() +
                       ", so there is nothing to write to." +
                       (roots.Length > 0
                            ? "  The bike(s) here are unpacked, not instances: save with Replace instead."
                            : "  Open the scene you tuned.");

            if (!EditorUtility.DisplayDialog("Rig Lab",
                    "Writing " + wanted.Count + " tuned rider(s) from '" + bikeName + "' into:\n\n   " +
                    string.Join("\n   ", paths) +
                    "\n\n" + (roots.Length == found.Length
                        ? "Nothing is selected, so EVERY bike prefab in the scene is listed. Select the one "
                          + "bike you tuned and run this again to narrow it."
                        : roots.Length + " of " + found.Length + " bike(s), narrowed by the Hierarchy selection.") +
                    "\n\nEvery scene that instances these prefabs changes with them. Commit first.",
                    "Write the prefabs", "Cancel"))
                return "Cancelled.";

            var log = new System.Text.StringBuilder();
            int written = 0;
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = WriteOne(root, wanted, log, path);
                    if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                    written += n;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();

            string report = "Wrote " + written + " transform(s) into " + paths.Count + " prefab(s)." +
                            (bikeLevel > 0 ? "  " + bikeLevel + " bike-level entry(s) skipped, they name no rider." : "");

            if (written == 0)
            {
                Debug.LogError("Rig Lab: wrote NOTHING into the prefabs from the " + capture.taken +
                               " capture. No rider on those prefabs is named like the captured riders (" +
                               string.Join(", ", wanted.Keys) + ").\n" + log);
                return report + "  Nothing matched, see the console.";
            }

            Debug.Log("Rig Lab: " + report + log + MaskingReport());
            return report + "  See the console for the per-rider breakdown.";
        }

        static int WriteOne(GameObject root, Dictionary<string, List<Pose>> wanted,
                            System.Text.StringBuilder log, string path)
        {
            log.Append('\n').Append(Path.GetFileNameWithoutExtension(path)).Append('\n');

            var riders = RigLabRiders.AllOn(root);
            if (riders.Length == 0)
            {
                log.Append("   SKIPPED: no rider found, is bikeReferences.BikeModel wired?\n");
                return 0;
            }

            int n = 0;
            foreach (var rider in riders)
            {
                if (!wanted.TryGetValue(rider.gameObject.name, out var poses))
                {
                    log.Append("   ").Append(rider.gameObject.name).Append("  not in the capture, left alone\n");
                    continue;
                }

                int m = 0, absent = 0;
                foreach (var pose in poses)
                {
                    var tr = Resolve(rider, pose.target);
                    if (tr == null) { absent++; continue; }
                    tr.localPosition = pose.pos;
                    tr.localEulerAngles = pose.euler;
                    m++;
                }

                log.Append("   ").Append(rider.gameObject.name).Append("  ").Append(m).Append(" transform(s)")
                   .Append(absent > 0 ? "  (" + absent + " captured field(s) absent here)" : "").Append('\n');
                n += m;
            }

            foreach (var name in wanted.Keys)
                if (!riders.Any(r => r.gameObject.name == name))
                    log.Append("   ").Append(name).Append(" is in the capture but not on this prefab\n");

            return n;
        }

        static string MaskingReport()
        {
            var lines = new List<string>();
            foreach (var bike in RigLabScope.BikeRoots())
            {
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(bike);
                var mods = root != null ? PrefabUtility.GetPropertyModifications(root) : null;
                if (mods == null) continue;

                foreach (var m in mods)
                {
                    var tr = m.target as Transform;
                    if (tr == null || !m.propertyPath.StartsWith("m_Local")) continue;
                    if (!HierarchyPath(tr).Contains("Biker Animation Targets")) continue;
                    lines.Add("   " + bike.name + "  " + tr.name + "." + m.propertyPath);
                }
            }

            return lines.Count == 0
                ? "\nNo instance override in the open scene masks what was written."
                : "\nSTILL MASKED. Revert these instance overrides or the prefab values will not show:\n" +
                  string.Join("\n", lines.Distinct());
        }

        static string HierarchyPath(Transform tr)
        {
            var parts = new List<string>();
            for (var t = tr; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        string Taken()
        {
            try { return JsonUtility.FromJson<Capture>(File.ReadAllText(CapturePath)).taken; }
            catch { return "unreadable"; }
        }
    
        internal static bool ScriptDriven(string field)
        {
            return field == "leftlegReverseTarget" || field == "rightlegReverseTarget";
        }

}
}
