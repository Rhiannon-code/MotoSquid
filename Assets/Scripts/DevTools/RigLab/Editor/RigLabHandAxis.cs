using MotoSquid.Bike;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabHandAxis
    {
        const string RefPath = "Assets/MotoSquid/Data/RigLab/Data/ReferenceHandAxis.json";
        const string BaselinePath = "Assets/MotoSquid/Data/RigLab/Data/HandTargetBaseline.json";

        [System.Serializable]
        class HandAxis { public Quaternion left = Quaternion.identity; public Quaternion right = Quaternion.identity; public string rider; }

        [System.Serializable] class Entry { public string bike; public string target; public Vector3 euler; }
        [System.Serializable] class Baseline { public List<Entry> entries = new List<Entry>(); }

        static readonly string[] LeftFields = { "leftHandTarget", "leftHandReverseTarget" };
        static readonly string[] RightFields = { "rightHandTarget", "rightHandReverseTarget" };

        [MenuItem("Tools/Rig Lab/7. Capture Reference Hand Axis (mannequin scene)", false, 60)]
        static void Capture()
        {
            var bike = Object.FindFirstObjectByType<BikeController>();
            var anim = bike == null ? null : bike.GetComponentInChildren<Animator>(true);
            if (anim == null || !anim.isHuman) { Debug.LogError("Rig Lab: need a Humanoid rider in the open scene."); return; }

            Quaternion l, r;
            if (!ReadBindHands(anim, out l, out r)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(RefPath));
            File.WriteAllText(RefPath, JsonUtility.ToJson(new HandAxis { left = l, right = r, rider = anim.gameObject.name }, true));
            AssetDatabase.Refresh();
            Debug.Log("Rig Lab: reference hand axis from " + anim.gameObject.name +
                      "\n   left  bind euler " + l.eulerAngles.ToString("0.00") +
                      "\n   right bind euler " + r.eulerAngles.ToString("0.00") +
                      "\n   (read from mesh bindposes, not live transforms)");
        }

        [MenuItem("Tools/Rig Lab/8. Apply Hand Axis Correction (all bikes)", false, 61)]
        static void Apply()
        {
            if (!File.Exists(RefPath)) { Debug.LogError("Rig Lab: no reference at " + RefPath + ", run step 7 on the mannequin scene first."); return; }
            var reference = JsonUtility.FromJson<HandAxis>(File.ReadAllText(RefPath));

            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }

            var baseline = File.Exists(BaselinePath)
                ? JsonUtility.FromJson<Baseline>(File.ReadAllText(BaselinePath))
                : null;
            bool capturing = baseline == null;
            if (capturing) baseline = new Baseline();

            var lookup = new Dictionary<string, Entry>();
            foreach (var e in baseline.entries) lookup[e.bike + "/" + e.target] = e;

            Quaternion dl = Quaternion.identity, dr = Quaternion.identity;
            bool haveDelta = false;
            int touched = 0;

            foreach (var bike in bikes)
            {
                var anim = bike.GetComponentInChildren<Animator>(true);
                if (anim == null || !anim.isHuman) continue;

                if (!haveDelta)
                {
                    Quaternion l, r;
                    if (!ReadBindHands(anim, out l, out r)) return;
                    dl = Quaternion.Inverse(reference.left) * l;
                    dr = Quaternion.Inverse(reference.right) * r;
                    haveDelta = true;
                    Debug.Log("Rig Lab: rider " + anim.gameObject.name +
                              "\n   left  bind euler " + l.eulerAngles.ToString("0.00") +
                              "   delta " + dl.eulerAngles.ToString("0.00") +
                              "\n   right bind euler " + r.eulerAngles.ToString("0.00") +
                              "   delta " + dr.eulerAngles.ToString("0.00"));
                }

                var t = bike.bikeReferences.bikeAnimationTargets;
                if (t == null) continue;
                foreach (var f in LeftFields) touched += Rotate(bike, t, f, dl, lookup, baseline, capturing);
                foreach (var f in RightFields) touched += Rotate(bike, t, f, dr, lookup, baseline, capturing);
            }

            if (!haveDelta) { Debug.LogError("Rig Lab: no Humanoid rider found to measure."); return; }

            if (capturing)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath));
                File.WriteAllText(BaselinePath, JsonUtility.ToJson(baseline, true));
                AssetDatabase.Refresh();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Rig Lab: corrected " + touched + " hand target(s). Re-running is safe, always applied from baseline.");
        }

        [MenuItem("Tools/Rig Lab/8b. Revert Hand Axis Correction", false, 62)]
        static void Revert()
        {
            if (!File.Exists(BaselinePath)) { Debug.LogWarning("Rig Lab: no hand target baseline stored."); return; }
            var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText(BaselinePath));
            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int n = 0;
            foreach (var e in baseline.entries)
            {
                foreach (var bike in bikes)
                {
                    if (bike.name != e.bike) continue;
                    var t = bike.bikeReferences.bikeAnimationTargets;
                    if (t == null) continue;
                    var f = typeof(BikeAnimationTargets).GetField(e.target);
                    var tr = f == null ? null : f.GetValue(t) as Transform;
                    if (tr == null) continue;
                    Undo.RecordObject(tr, "Revert hand axis");
                    tr.localEulerAngles = e.euler;
                    n++;
                }
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Rig Lab: restored " + n + " hand target(s) to their shipped rotations.");
        }

        static int Rotate(BikeController bike, BikeAnimationTargets targets, string field,
                          Quaternion delta, Dictionary<string, Entry> lookup, Baseline baseline, bool capturing)
        {
            var f = typeof(BikeAnimationTargets).GetField(field);
            if (f == null) return 0;
            var tr = f.GetValue(targets) as Transform;
            if (tr == null) return 0;

            string key = bike.name + "/" + field;
            Entry entry;
            if (!lookup.TryGetValue(key, out entry))
            {
                if (!capturing) return 0;
                entry = new Entry { bike = bike.name, target = field, euler = tr.localEulerAngles };
                baseline.entries.Add(entry);
                lookup[key] = entry;
            }

            Undo.RecordObject(tr, "Hand axis correction");
            tr.localRotation = Quaternion.Euler(entry.euler) * delta;
            return 1;
        }

        static bool ReadBindHands(Animator anim, out Quaternion left, out Quaternion right)
        {
            left = right = Quaternion.identity;
            var lh = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            var rh = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (lh == null || rh == null) { Debug.LogError("Rig Lab: hand bones not mapped on " + anim.gameObject.name); return false; }

            bool gotL = false, gotR = false;
            foreach (var smr in anim.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                var bones = smr.bones;
                var binds = mesh.bindposes;
                if (bones == null || binds == null || binds.Length != bones.Length) continue;

                for (int i = 0; i < bones.Length; i++)
                {
                    if (!gotL && bones[i] == lh) { left = binds[i].inverse.rotation; gotL = true; }
                    if (!gotR && bones[i] == rh) { right = binds[i].inverse.rotation; gotR = true; }
                }
                if (gotL && gotR) break;
            }

            if (!gotL || !gotR)
            {
                Debug.LogError("Rig Lab: could not find the hand bones in any SkinnedMeshRenderer's bone list on " +
                               anim.gameObject.name + ", cannot read bind pose.");
                return false;
            }
            return true;
        }
    }
}
