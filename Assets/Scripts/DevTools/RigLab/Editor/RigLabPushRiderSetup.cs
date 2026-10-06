using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.DevTools
{
    public class RigLabPushRiderSetup : EditorWindow
    {
        GameObject sourceBike;
        bool fitKnees = true;
        int referenceRider;
        string[] riders = new string[0];
        string status = "";
        Vector2 scroll;

        [MenuItem("Tools/Rig Lab/17. Push Rider Setup To Prefabs", false, 195)]
        static void Open()
        {
            GetWindow<RigLabPushRiderSetup>("Push Rider Setup").minSize = new Vector2(440, 320);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Push Rider Setup To Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Takes every rider's targets and hints off ONE tuned bike in the scene and writes them " +
                "into each bike prefab behind the scene's bikes, matching riders by name. The player " +
                "and AI bikes are separate prefabs, so both get it.\n" +
                "Reverse leg targets are skipped, the walk cycle owns those.", MessageType.None);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                var next = (GameObject)EditorGUILayout.ObjectField("Tuned bike", sourceBike, typeof(GameObject), true);
                if (next != sourceBike) { sourceBike = next; RefreshRiders(); }

                if (GUILayout.Button("Use the selected bike"))
                {
                    sourceBike = Selection.activeGameObject != null
                        ? RigLabScope.BikeRoots().FirstOrDefault(
                              b => Selection.activeGameObject.transform.IsChildOf(b.transform))
                        : null;
                    RefreshRiders();
                }

                EditorGUILayout.LabelField("Riders found", riders.Length == 0 ? "none" : string.Join(", ", riders),
                                           EditorStyles.miniLabel);

                EditorGUILayout.Space();
                fitKnees = EditorGUILayout.Toggle("Fit knee hints to match one rider", fitKnees);
                if (fitKnees && riders.Length > 0)
                {
                    referenceRider = Mathf.Clamp(referenceRider, 0, riders.Length - 1);
                    referenceRider = EditorGUILayout.Popup("Knees like", referenceRider, riders);
                    EditorGUILayout.LabelField(" ", "each rider's hint = its own foot + that rider's hint-to-foot offset",
                                               EditorStyles.miniLabel);
                }

                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(sourceBike == null || riders.Length == 0))
                    if (GUILayout.Button("Push to bike prefabs"))
                        EditorApplication.delayCall += Push;
            }

            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Leave Play mode, scene edits made in Play do not survive it.",
                                        MessageType.Warning);

            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.Space();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.HelpBox(status, MessageType.None);
                EditorGUILayout.EndScrollView();
            }
        }

        void RefreshRiders()
        {
            riders = sourceBike == null
                ? new string[0]
                : RigLabRiders.AllOn(sourceBike).Select(r => r.gameObject.name).ToArray();

            for (int i = 0; i < riders.Length; i++)
                if (riders[i].IndexOf("bliss", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    referenceRider = i;
        }

        static IEnumerable<string> SetupFields()
        {
            foreach (var f in typeof(BikeAnimationController)
                         .GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (f.FieldType == typeof(Transform) && !f.Name.EndsWith("Rig") &&
                    !RigLabPoseTuner.ScriptDriven(f.Name))
                    yield return f.Name;

            yield return "leftLegHint";
            yield return "rightLegHint";
            yield return "leftHandHint";
            yield return "rightHandHint";
            yield return "headLookAtTarget";
        }

        struct Placed { public Vector3 pos; public Quaternion rot; }
        const string SetRootKey = "#targetSetRoot";

        static Transform SetRootOf(BikeAnimationController rider)
        {
            var t = RigLabPoseTuner.Resolve(rider, "hipIdleTarget");
            while (t != null && !t.name.StartsWith("Biker Animation Targets")) t = t.parent;
            return t;
        }

        void Push()
        {
            try { status = Run(); }
            catch (System.Exception e) { status = "FAILED: " + e.Message; Debug.LogException(e); }
            Repaint();
        }

        string Run()
        {
            var source = RigLabRiders.AllOn(sourceBike);
            if (source.Length == 0) return "No riders on '" + sourceBike.name + "'.";

            var setup = new Dictionary<string, Dictionary<string, Placed>>();
            foreach (var rider in source)
            {
                var one = new Dictionary<string, Placed>();
                foreach (var field in SetupFields())
                {
                    var tr = RigLabPoseTuner.Resolve(rider, field);
                    if (tr != null) one[field] = new Placed { pos = tr.localPosition, rot = tr.localRotation };
                }
                var setRoot = SetRootOf(rider);
                if (setRoot != null)
                    one[SetRootKey] = new Placed { pos = setRoot.localPosition, rot = setRoot.localRotation };

                setup[rider.gameObject.name] = one;
            }

            string kneeReport = fitKnees ? FitKnees(source, setup) : "";

            var paths = RigLabScope.BikeRoots()
                .Select(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot)
                .Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p).ToList();
            if (paths.Count == 0)
                return "No bike prefab instances in the open scene, so there is nothing to write to.";

            if (!EditorUtility.DisplayDialog("Rig Lab",
                    "Writing " + setup.Count + " rider(s) from '" + sourceBike.name + "' into:\n\n   " +
                    string.Join("\n   ", paths) +
                    "\n\nEvery scene that instances these prefabs changes with them. Commit first.",
                    "Write the prefabs", "Cancel"))
                return "Cancelled.";

            var log = new System.Text.StringBuilder(kneeReport);
            int written = 0;
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = WriteOne(root, setup, log, path);
                    if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                    written += n;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();

            if (written == 0)
            {
                Debug.LogError("Rig Lab: wrote NOTHING. No rider on those prefabs is named like the " +
                               "source bike's riders (" + string.Join(", ", setup.Keys) + ").\n" + log);
                return "Nothing matched, see the console.";
            }

            Debug.Log("Rig Lab: pushed " + written + " transform(s) into " + paths.Count + " prefab(s).\n" +
                      log + SettingsReport(source, paths) + MaskReport(setup));
            return "Pushed " + written + " transform(s) into " + paths.Count +
                   " prefab(s). See the console for the per rider breakdown.";
        }

        string FitKnees(BikeAnimationController[] source, Dictionary<string, Dictionary<string, Placed>> setup)
        {
            string refName = riders[Mathf.Clamp(referenceRider, 0, riders.Length - 1)];
            var reference = source.FirstOrDefault(r => r.gameObject.name == refName);
            if (reference == null) return "";

            var refHint = RigLabPoseTuner.Resolve(reference, "leftLegHint");
            var refFoot = RigLabPoseTuner.Resolve(reference, "leftLegTarget");
            if (refHint == null || refFoot == null) return "   knee fit skipped, " + refName + " has no leg hint or target\n";
            if (refHint.parent != refFoot.parent)
                return "   knee fit skipped, " + refName + "'s hint and foot target are not siblings\n";

            Vector3 offset = refHint.localPosition - refFoot.localPosition;
            var log = new System.Text.StringBuilder("   knee hints fitted to " + refName +
                                                    ", offset from foot " + offset.ToString("F3") + "\n");

            foreach (var rider in source)
            {
                var foot = RigLabPoseTuner.Resolve(rider, "leftLegTarget");
                var left = RigLabPoseTuner.Resolve(rider, "leftLegHint");
                var right = RigLabPoseTuner.Resolve(rider, "rightLegHint");
                if (foot == null || left == null || right == null) continue;

                Vector3 want = foot.localPosition + offset;
                var one = setup[rider.gameObject.name];
                one["leftLegHint"] = new Placed { pos = want, rot = left.localRotation };
                one["rightLegHint"] = new Placed { pos = new Vector3(-want.x, want.y, want.z), rot = right.localRotation };

                log.Append("      ").Append(rider.gameObject.name).Append("  ")
                   .Append(left.localPosition.ToString("F3")).Append(" -> ").Append(want.ToString("F3")).Append('\n');
            }
            return log.ToString();
        }

        static int WriteOne(GameObject root, Dictionary<string, Dictionary<string, Placed>> setup,
                            System.Text.StringBuilder log, string path)
        {
            log.Append('\n').Append(System.IO.Path.GetFileNameWithoutExtension(path)).Append('\n');

            var riders = RigLabRiders.AllOn(root);
            if (riders.Length == 0)
            {
                log.Append("   SKIPPED: no rider found, is bikeReferences.BikeModel wired?\n");
                return 0;
            }

            int n = 0;
            foreach (var rider in riders)
            {
                Dictionary<string, Placed> want;
                if (!setup.TryGetValue(rider.gameObject.name, out want))
                {
                    log.Append("   ").Append(rider.gameObject.name).Append("  not on the source bike, left alone\n");
                    continue;
                }

                int m = 0, absent = 0;
                foreach (var pair in want)
                {
                    var tr = pair.Key == SetRootKey
                        ? SetRootOf(rider)
                        : RigLabPoseTuner.Resolve(rider, pair.Key);
                    if (tr == null) { absent++; continue; }
                    if (pair.Key == SetRootKey && tr.localPosition != pair.Value.pos)
                        log.Append("      ").Append(rider.gameObject.name).Append(" target set was offset ")
                           .Append(tr.localPosition.ToString("F3")).Append(", now ")
                           .Append(pair.Value.pos.ToString("F3")).Append('\n');
                    tr.localPosition = pair.Value.pos;
                    tr.localRotation = pair.Value.rot;
                    m++;
                }

                log.Append("   ").Append(rider.gameObject.name).Append("  ").Append(m).Append(" transform(s)")
                   .Append(absent > 0 ? "  (" + absent + " absent here)" : "").Append('\n');
                n += m;
            }

            foreach (var name in setup.Keys)
                if (!riders.Any(r => r.gameObject.name == name))
                    log.Append("   ").Append(name).Append(" is on the source bike but not on this prefab\n");

            return n;
        }

        static string SettingsReport(BikeAnimationController[] source, List<string> paths)
        {
            var want = new Dictionary<string, float>();
            foreach (var f in typeof(BikeAnimationController)
                         .GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (f.FieldType == typeof(float) && source.Length > 0)
                    want[f.Name] = (float)f.GetValue(source[0]);

            var lines = new List<string>();
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var rider in RigLabRiders.AllOn(root))
                        foreach (var pair in want)
                        {
                            var f = typeof(BikeAnimationController).GetField(pair.Key);
                            float has = (float)f.GetValue(rider);
                            if (Mathf.Abs(has - pair.Value) > 1e-4f)
                                lines.Add("   " + System.IO.Path.GetFileNameWithoutExtension(path) + "  " +
                                          rider.gameObject.name + "." + pair.Key + " = " + has +
                                          "   (tuned bike has " + pair.Value + ")");
                        }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            return lines.Count == 0
                ? "\nRider settings match the tuned bike everywhere.\n"
                : "\nThese rider SETTINGS differ from the tuned bike and are not pushed, set them by hand " +
                  "if they should match:\n" + string.Join("\n", lines) + "\n";
        }

        static string MaskReport(Dictionary<string, Dictionary<string, Placed>> setup)
        {
            var lines = new List<string>();
            foreach (var bike in RigLabScope.BikeRoots())
            {
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(bike);
                var mods = root != null ? PrefabUtility.GetPropertyModifications(root) : null;
                if (mods == null) continue;

                int count = mods.Count(m => m.target is Transform && m.propertyPath.StartsWith("m_Local") &&
                                            IsRiderPart((Transform)m.target));
                if (count > 0) lines.Add("   " + bike.name + "  " + count + " override(s) on rider transforms");
            }

            return lines.Count == 0
                ? "\nNo instance override masks what was written."
                : "\nThese instances still override rider transforms. The values now match the prefab, so " +
                  "reverting them loses nothing and stops them masking later prefab edits:\n" +
                  string.Join("\n", lines);
        }

        static bool IsRiderPart(Transform tr)
        {
            for (var t = tr; t != null; t = t.parent)
                if (t.name.StartsWith("Biker Animation Targets") || t.name.StartsWith("Biker Rig"))
                    return true;
            return false;
        }
    }
}
