using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabBasePose
    {
        [MenuItem("Tools/Rig Lab/15. Capture Base Pose (all riders)", false, 180)]
        static void Capture()
        {
            if (Application.isPlaying) { Debug.LogError("Rig Lab: capture in edit mode."); return; }

            var bikes = RigLabScope.Bikes(true);
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in " + RigLabScope.Where() + "."); return; }

            int done = 0;
            foreach (var bike in bikes)
            {
                foreach (var ctrl in RigLabRiders.AllOn(bike))
                {
                var anim = ctrl.GetComponent<Animator>();
                if (anim == null) continue;

                var driven = DrivenBones(anim);
                var captured = new List<BasePose.BoneRotation>();

                foreach (var t in anim.GetComponentsInChildren<Transform>(true))
                {
                    if (t == anim.transform || driven.Contains(t)) continue;
                    if (t.GetComponentInParent<Rig>() != null) continue; 
                    captured.Add(new BasePose.BoneRotation
                    {
                        bone = t,
                        localRotation = t.localRotation,
                        localPosition = t.localPosition
                    });
                }

                var pose = anim.GetComponent<BasePose>();
                if (pose == null) pose = Undo.AddComponent<BasePose>(anim.gameObject);

                Undo.RecordObject(pose, "Capture base pose");
                pose.bones = captured.ToArray();
                EditorUtility.SetDirty(pose);
                done++;

                if (done == 1)
                    Debug.Log("Rig Lab: " + anim.gameObject.name + ", captured " + captured.Count +
                              " bone(s), skipped " + driven.Count + " driven by the rig.");
                }
            }

            RigLabScope.MarkDirty();
            Debug.Log("Rig Lab: base pose captured on " + done + " rider(s).\n" +
                      "The Animator Controller can now be cleared, step 16 checks nothing still needs it.");
        }

        static HashSet<Transform> DrivenBones(Animator anim)
        {
            var driven = new HashSet<Transform>();

            foreach (var c in anim.GetComponentsInChildren<TwoBoneIKConstraint>(true))
            {
                Add(driven, c.data.root); Add(driven, c.data.mid); Add(driven, c.data.tip);
                if (c.data.mid != null && c.data.tip != null)
                    foreach (Transform child in c.data.mid) Add(driven, child);
            }
            foreach (var c in anim.GetComponentsInChildren<MultiParentConstraint>(true))
                Add(driven, c.data.constrainedObject);
            foreach (var c in anim.GetComponentsInChildren<MultiRotationConstraint>(true))
                Add(driven, c.data.constrainedObject);
            foreach (var c in anim.GetComponentsInChildren<MultiAimConstraint>(true))
                Add(driven, c.data.constrainedObject);

            return driven;
        }

        static void Add(HashSet<Transform> set, Transform t) { if (t != null) set.Add(t); }

        [MenuItem("Tools/Rig Lab/16. Go Clip Free (clear Animator Controllers)", false, 181)]
        static void GoClipFree()
        {
            var bikes = RigLabScope.Bikes(true);
            var riders = bikes.Select(b => b.GetComponentInChildren<Animator>(true)).Where(a => a != null).ToList();
            if (riders.Count == 0) { Debug.LogWarning("Rig Lab: no riders found."); return; }

            int missing = riders.Count(r => r.GetComponent<BasePose>() == null ||
                                            r.GetComponent<BasePose>().bones.Length == 0);
            if (missing > 0)
            {
                Debug.LogError("Rig Lab: " + missing + " rider(s) have no captured base pose. Run step 15 first, " +
                               "or the undriven bones will snap to bind pose.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Rig Lab",
                    "Clear the Animator Controller on " + riders.Count + " rider(s)?\n\n" +
                    "Nothing in the procedural system reads a clip, RigBuilder builds its own PlayableGraph. " +
                    "This proves it: if the rider still poses correctly afterwards, no clip is load bearing.",
                    "Clear them", "Cancel"))
                return;

            foreach (var r in riders)
            {
                Undo.RecordObject(r, "Go clip free");
                r.runtimeAnimatorController = null;
                EditorUtility.SetDirty(r);
            }

            RigLabScope.MarkDirty();
            Debug.Log("Rig Lab: cleared the Animator Controller on " + riders.Count + " rider(s). " +
                      "Press Play, the rig runs entirely from scripts and serialized poses now.");
        }
    }
}
