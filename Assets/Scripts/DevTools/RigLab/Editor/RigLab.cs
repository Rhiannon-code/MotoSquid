using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Combat;
using MotoSquid.Rider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLab
    {
        const string PresetFolder = "Assets/Prefabs/Characters";
        internal const string CustomBikeFolder = "Assets/MotoSquid/Data/RigLab/Bikes";
        const string BaseScene = "Assets/OutdoorsScene.unity";
        const string ReviewScene = "Assets/MotoSquid/Scenes/Tools/RigReview.unity";
        internal static readonly string[] ModelFolders =
        {
            "Assets/Models",
        };
        const string DataFolder = "Assets/MotoSquid/Data/RigLab/Data";

        internal const float Spacing = 7f;

        [MenuItem("Tools/Rig Lab/1. Build Review Scene", false, 0)]
        static void BuildReviewScene()
        {
            var presets = LoadPresets();
            if (presets.Count == 0)
            {
                EditorUtility.DisplayDialog("Rig Lab", "No bike presets found under\n" +
                                            PresetFolder + "\nor " + CustomBikeFolder, "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Rig Lab",
                    "This replaces " + ReviewScene + " with a fresh copy of " + Path.GetFileName(BaseScene) +
                    " holding " + presets.Count + " bikes.\n\nSave any work in the open scene first.",
                    "Build it", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ReviewScene));
            AssetDatabase.DeleteAsset(ReviewScene);
            if (!AssetDatabase.CopyAsset(BaseScene, ReviewScene))
            {
                Debug.LogError("Rig Lab: could not copy " + BaseScene + " to " + ReviewScene);
                return;
            }
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(ReviewScene, OpenSceneMode.Single);

            foreach (var stale in Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(stale.gameObject);

            BuildGround();

            var bikes = new List<Transform>();
            var labels = new List<string>();
            for (int i = 0; i < presets.Count; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(presets[i]);
                go.transform.position = new Vector3(i * Spacing, 0.6f, 0f);
                go.name = presets[i].name;

                foreach (var cam in go.GetComponentsInChildren<CameraController>(true))
                    Object.DestroyImmediate(cam.gameObject);

                bikes.Add(go.transform);
                labels.Add(presets[i].name);
            }

            var rigHost = new GameObject("Rig Review");
            var review = rigHost.AddComponent<RigReview>();
            review.bikes = bikes.ToArray();
            review.labels = labels.ToArray();
            review.reviewCamera = Object.FindFirstObjectByType<Camera>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ReviewScene);
            Debug.Log("Rig Lab: built " + ReviewScene + " with " + bikes.Count + " bikes. Press Play, use [ and ] to cycle.");
        }

        [MenuItem("Tools/Rig Lab/2. Swap Riders To Voodoo (open scene)", false, 1)]
        static void SwapRiders()
        {
            var rider = RigLabRiders.Characters()
                .FirstOrDefault(c => c.name.IndexOf("Voodoo", StringComparison.OrdinalIgnoreCase) >= 0);
            if (rider == null)
            {
                Debug.LogError("Rig Lab: no Humanoid Voodoo model under " + string.Join(", ", ModelFolders) +
                               ". Use '2. Swap Riders' to see what was found and why.");
                return;
            }

            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int done = 0, skipped = 0;
            foreach (var bike in bikes)
            {
                if (SwapRiderOn(bike, rider)) done++; else skipped++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Rig Lab: rigged " + done + " bike(s), skipped " + skipped + ". Run 'Report Rig Status' to verify.");
        }

        [MenuItem("Tools/Rig Lab/3. Report Rig Status (open scene)", false, 2)]
        static void ReportRigStatus()
        {
            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                              .OrderBy(b => b.transform.position.x).ToArray();
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }

            var report = new System.Text.StringBuilder("Rig Lab status\n");
            foreach (var bike in bikes)
            {
                report.Append("\n").Append(bike.name).Append("\n");

                var refs = bike.GetComponentInChildren<BikerRigReferences>(true);
                if (refs == null) { report.Append("   NO Biker Rig\n"); continue; }

                var anim = refs.GetComponentInParent<Animator>();
                report.Append("   rider      : ").Append(anim == null ? "NO Animator" : anim.gameObject.name)
                      .Append(anim != null && !anim.isHuman ? "   [NOT HUMANOID: bone lookup cannot work]" : "")
                      .Append("\n");
                report.Append("   avatar     : ").Append(anim == null || anim.avatar == null ? "NONE" : anim.avatar.name).Append("\n");
                report.Append("   controller : ").Append(anim == null || anim.runtimeAnimatorController == null
                                  ? "NONE" : anim.runtimeAnimatorController.name).Append("\n");

                var rb = refs.GetComponentInParent<RigBuilder>();
                report.Append("   rigBuilder : ").Append(rb == null ? "MISSING"
                                  : rb.layers.Count + " layer(s)").Append("\n");

                Bind(report, "hip", refs.hipRig == null ? null : refs.hipRig.data.constrainedObject);
                Bind(report, "spineRoot", refs.spineRootRig == null ? null : refs.spineRootRig.data.constrainedObject);
                Bind(report, "spineTip", refs.spineTipRig == null ? null : refs.spineTipRig.data.constrainedObject);
                Chain(report, "leftLeg", refs.LeftLegRig);
                Chain(report, "rightLeg", refs.RightLegRig);
                Chain(report, "leftHand", refs.LeftHandRig);
                Chain(report, "rightHand", refs.RightHandRig);
                Bind(report, "head", refs.headRig == null ? null : refs.headRig.data.constrainedObject);

                foreach (var ctrl in RigLabRiders.AllOn(bike))
                    report.Append("   rider ").Append(ctrl.gameObject.name)
                          .Append(ctrl.gameObject.activeInHierarchy ? " (active)" : "")
                          .Append(": ").Append(ctrl.bikeControllerRhiannon == null ? "bikeController NOT SET" : "ok").Append("\n");

                var targets = bike.bikeReferences.bikeAnimationTargets;
                report.Append("   bikeTargets: ").Append(targets == null ? "MISSING" : MissingTargets(targets)).Append("\n");
            }
            Debug.Log(report.ToString());
        }

        [MenuItem("Tools/Rig Lab/4. Export Bike Target Data (JSON)", false, 20)]
        static void ExportTargets()
        {
            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                              .OrderBy(b => b.name).ToArray();
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }

            Directory.CreateDirectory(DataFolder);
            var book = new TargetBook();
            foreach (var bike in bikes)
            {
                var entry = Capture(bike);
                if (entry != null) book.bikes.Add(entry);
            }
            string path = DataFolder + "/BikeTargets.json";
            File.WriteAllText(path, JsonUtility.ToJson(book, true));
            AssetDatabase.Refresh();
            Debug.Log("Rig Lab: wrote " + book.bikes.Count + " bike(s) to " + path);
        }

        static int PoseCount(string path)
        {
            if (!File.Exists(path)) return 0;
            try
            {
                var book = JsonUtility.FromJson<TargetBook>(File.ReadAllText(path));
                if (book == null || book.bikes == null) return 0;
                int n = 0;
                foreach (var b in book.bikes) if (b != null && b.poses != null) n += b.poses.Count;
                return n;
            }
            catch { return 0; }
        }

        [MenuItem("Tools/Rig Lab/5. Import Bike Target Data (JSON)", false, 21)]
        static void ImportTargets()
        {
            string exported = DataFolder + "/BikeTargets.json";
            string captured = DataFolder + "/LiveCapture.json";

            string path = null;
            int ne = PoseCount(exported), nc = PoseCount(captured);
            if (ne > 0 || nc > 0) path = ne >= nc ? exported : captured;

            if (ne > 0 && nc > 0 && ne != nc)
                Debug.Log("Rig Lab: " + Path.GetFileName(exported) + " holds " + ne + " pose(s), " +
                          Path.GetFileName(captured) + " holds " + nc + ", reading the fuller one.");

            if (path == null)
            {
                Debug.LogError("Rig Lab: no target data found.\n   looked for " + exported + "\n   and " + captured);
                return;
            }

            var book = JsonUtility.FromJson<TargetBook>(File.ReadAllText(path));
            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int applied = 0, written = 0, skipped = 0;
            if (book != null && book.bikes.Count > 0)
            {
                foreach (var entry in book.bikes)
                {
                    var bike = bikes.FirstOrDefault(b => b.name == entry.bike);
                    if (bike == null) { Debug.LogWarning("Rig Lab: no bike named " + entry.bike + " in scene"); continue; }
                    if (Apply(bike, entry)) applied++;
                }
            }
            else
            {
                var capture = JsonUtility.FromJson<FlatCapture>(File.ReadAllText(path));
                foreach (var pose in capture.poses)
                {
                    var bike = bikes.FirstOrDefault(b => b.name == pose.bike);
                    if (bike == null) { skipped++; continue; }
                    var tr = Resolve(bike, pose.target);
                    if (tr == null) { skipped++; continue; }
                    Undo.RecordObject(tr, "Rig Lab import");
                    tr.localPosition = pose.pos;
                    tr.localEulerAngles = pose.euler;
                    written++;
                }
                applied = bikes.Length;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Rig Lab: imported from " + Path.GetFileName(path) +
                      "  (" + written + " transform(s) written, " + skipped + " unresolved, " +
                      applied + " bike(s))");
        }

        [Serializable] class FlatPose { public string bike; public string target; public Vector3 pos; public Vector3 euler; }
        [Serializable] class FlatCapture { public string taken; public List<FlatPose> poses = new List<FlatPose>(); }

        static Transform Resolve(BikeController bike, string field)
        {
            if (field == "meleeHint") field = "meleeHintPose";

            var t = bike.bikeReferences.bikeAnimationTargets;
            if (t != null)
            {
                var f = typeof(BikeAnimationTargets).GetField(field);
                if (f != null && f.FieldType == typeof(Transform))
                {
                    var tr = f.GetValue(t) as Transform;
                    if (tr != null) return tr;
                }
            }

            var ctrl = RigLabRiders.ActiveOn(bike);
            var driver = ctrl != null ? ctrl.GetComponent<CombatRigDriver>() : null;
            if (driver != null)
            {
                var f = typeof(CombatRigDriver).GetField(field);
                if (f != null && f.FieldType == typeof(Transform))
                {
                    var tr = f.GetValue(driver) as Transform;
                    if (tr != null) return tr;
                }
            }

            var refs = bike.GetComponentInChildren<BikerRigReferences>(true);
            if (refs != null)
            {
                var f = typeof(BikerRigReferences).GetField(field);
                if (f != null && f.FieldType == typeof(Transform)) return f.GetValue(refs) as Transform;
            }
            return null;
        }

        internal static bool SwapRiderOn(BikeController bike, GameObject riderModel)
        {
            return SwapRiderOn(bike, riderModel, false);
        }

        internal static bool SwapRiderOn(BikeController bike, GameObject riderModel, bool keepExisting)
        {
            var bikeModel = bike.bikeReferences.BikeModel;
            if (bikeModel == null)
            {
                Debug.LogError("Rig Lab: " + bike.name + " has no BikeModel reference.", bike);
                return false;
            }

            var existing = bikeModel.GetComponentsInChildren<BikeAnimationController>(true).ToArray();
            foreach (var old in existing)
            {
                if (keepExisting)
                {
                    if (old.gameObject.name == riderModel.name) Object.DestroyImmediate(old.gameObject);
                    else old.gameObject.SetActive(false);
                }
                else Object.DestroyImmediate(old.gameObject);
            }

            if (!keepExisting)
                foreach (var old in bikeModel.GetComponentsInChildren<RigBuilder>(true).ToArray())
                    Object.DestroyImmediate(old.gameObject);

            var rider = (GameObject)PrefabUtility.InstantiatePrefab(riderModel);
            rider.name = riderModel.name;
            rider.transform.SetParent(bikeModel, false);
            rider.transform.localPosition = Vector3.zero;
            rider.transform.localRotation = Quaternion.identity;

            var anim = rider.GetComponent<Animator>();
            if (anim == null) anim = rider.AddComponent<Animator>();

            if (!anim.isHuman)
            {
                Debug.LogError("Rig Lab: " + riderModel.name + " has no Humanoid avatar. " +
                    "The vendor rig builder resolves bones through Animator.GetBoneTransform(HumanBodyBones), " +
                    "which only works on a Humanoid rig. Set the model's Rig to Humanoid (Create From This Model) and re-run.", rider);
                return false;
            }

            try
            {
                if (!BikerAnimationCreator.Build(anim, bike))
                    throw new InvalidOperationException("The rig builder refused - the console above says why.");
            }
            catch (Exception e)
            {
                Debug.LogError("Rig Lab: rig builder failed on " + bike.name + ": " + e, bike);
                return false;
            }
            return true;
        }

        static void Bind(System.Text.StringBuilder sb, string label, Transform bone)
        {
            sb.Append("   ").Append(label.PadRight(11)).Append(": ")
              .Append(bone == null ? "UNBOUND" : bone.name).Append("\n");
        }

        static void Chain(System.Text.StringBuilder sb, string label, TwoBoneIKConstraint ik)
        {
            if (ik == null) { sb.Append("   ").Append(label.PadRight(11)).Append(": NO CONSTRAINT\n"); return; }
            var d = ik.data;
            sb.Append("   ").Append(label.PadRight(11)).Append(": ")
              .Append(d.root == null ? "UNBOUND" : d.root.name).Append(" / ")
              .Append(d.mid == null ? "UNBOUND" : d.mid.name).Append(" / ")
              .Append(d.tip == null ? "UNBOUND" : d.tip.name)
              .Append("   maintainRot=").Append(d.maintainTargetRotationOffset ? "1" : "0")
              .Append("\n");
        }

        static string MissingTargets(BikeAnimationTargets t)
        {
            var missing = new List<string>();
            foreach (var f in typeof(BikeAnimationTargets).GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (f.FieldType == typeof(Transform) && (Transform)f.GetValue(t) == null) missing.Add(f.Name);
            return missing.Count == 0 ? "all 19 present" : missing.Count + " MISSING: " + string.Join(", ", missing);
        }

        [Serializable] public class Pose { public string name; public Vector3 pos; public Vector3 euler; }
        [Serializable] public class BikeEntry { public string bike; public List<Pose> poses = new List<Pose>(); }
        [Serializable] public class TargetBook { public List<BikeEntry> bikes = new List<BikeEntry>(); }

        static BikeEntry Capture(BikeController bike)
        {
            var t = bike.bikeReferences.bikeAnimationTargets;
            if (t == null) return null;
            var entry = new BikeEntry { bike = bike.name };

            foreach (var f in typeof(BikeAnimationTargets).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.FieldType != typeof(Transform)) continue;
                var tr = (Transform)f.GetValue(t);
                if (tr == null) continue;
                if (ScriptDriven(f.Name)) continue;
                    entry.poses.Add(new Pose { name = f.Name, pos = tr.localPosition, euler = tr.localEulerAngles });
            }

            var refs = bike.GetComponentInChildren<BikerRigReferences>(true);
            if (refs != null)
            {
                AddHint(entry, "leftLegHint", refs.leftLegHint);
                AddHint(entry, "rightLegHint", refs.rightLegHint);
                AddHint(entry, "leftHandHint", refs.leftHandHint);
                AddHint(entry, "rightHandHint", refs.rightHandHint);
            }
            return entry;
        }

        static void AddHint(BikeEntry entry, string name, Transform tr)
        {
            if (tr == null) return;
            entry.poses.Add(new Pose { name = name, pos = tr.localPosition, euler = tr.localEulerAngles });
        }

        static bool Apply(BikeController bike, BikeEntry entry)
        {
            var t = bike.bikeReferences.bikeAnimationTargets;
            var refs = bike.GetComponentInChildren<BikerRigReferences>(true);
            if (t == null) return false;

            foreach (var pose in entry.poses)
            {
                Transform tr = ResolveTargetField(t, pose.name)
                               ?? ResolveHint(refs, pose.name);
                if (tr == null) continue;
                Undo.RecordObject(tr, "Rig Lab import");
                tr.localPosition = pose.pos;
                tr.localEulerAngles = pose.euler;
            }
            return true;
        }

        static Transform ResolveTargetField(BikeAnimationTargets t, string field)
        {
            var f = typeof(BikeAnimationTargets).GetField(field, BindingFlags.Instance | BindingFlags.Public);
            return f == null || f.FieldType != typeof(Transform) ? null : (Transform)f.GetValue(t);
        }

        static Transform ResolveHint(BikerRigReferences refs, string field)
        {
            if (refs == null) return null;
            var f = typeof(BikerRigReferences).GetField(field, BindingFlags.Instance | BindingFlags.Public);
            return f == null || f.FieldType != typeof(Transform) ? null : (Transform)f.GetValue(refs);
        }

        static List<GameObject> LoadPresets()
        {
            Directory.CreateDirectory(CustomBikeFolder);
            var folders = new[] { PresetFolder, CustomBikeFolder }
                .Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length == 0) return new List<GameObject>();

            return AssetDatabase.FindAssets("t:Prefab", folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(go => go != null && go.GetComponent<BikeController>() != null)
                .ToList();
        }

        static void BuildGround()
        {
            if (GameObject.Find("Rig Lab Ground") != null) return;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Rig Lab Ground";
            ground.transform.position = new Vector3(40f, 0f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            ground.isStatic = true;
        }
    
        static bool ScriptDriven(string field)
        {
            return field == "leftlegReverseTarget" || field == "rightlegReverseTarget";
        }

}
}
