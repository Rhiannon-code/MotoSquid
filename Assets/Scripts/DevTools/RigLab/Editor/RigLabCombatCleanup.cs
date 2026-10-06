using MotoSquid.Bike;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabCombatCleanup
    {
        [MenuItem("Tools/Rig Lab/14. Remove Unused Combat Targets", false, 161)]
        static void Cleanup()
        {
            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }

            var doomed = new List<GameObject>();
            var kept = new List<string>();

            foreach (var bike in bikes)
            {
                var bikeModel = bike.bikeReferences.BikeModel;
                if (bikeModel == null) continue;
                var host = bikeModel.Find("Combat Pose Targets");
                if (host == null) continue;

                var referenced = new HashSet<Transform>();
                foreach (var ctrl in bike.GetComponentsInChildren<BikeAnimationController>(true))
                    foreach (var f in typeof(BikeAnimationController).GetFields(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (f.FieldType != typeof(Transform)) continue;
                        var tr = f.GetValue(ctrl) as Transform;
                        if (tr != null) referenced.Add(tr);
                    }

                foreach (Transform child in host)
                {
                    if (referenced.Contains(child)) { if (!kept.Contains(child.name)) kept.Add(child.name); }
                    else doomed.Add(child.gameObject);
                }
            }

            if (doomed.Count == 0)
            {
                Debug.Log("Rig Lab: nothing to remove, every combat target is referenced.\n   kept: " +
                          string.Join(", ", kept.OrderBy(n => n)));
                return;
            }

            var names = doomed.Select(g => g.name).Distinct().OrderBy(n => n).ToList();
            bool ok = EditorUtility.DisplayDialog("Rig Lab",
                "Remove " + doomed.Count + " unreferenced target(s) across " + bikes.Length + " bike(s)?\n\n" +
                "DELETING:\n  " + string.Join("\n  ", names) + "\n\n" +
                "KEEPING:\n  " + string.Join("\n  ", kept.OrderBy(n => n)),
                "Delete", "Cancel");
            if (!ok) return;

            foreach (var go in doomed) Undo.DestroyObjectImmediate(go);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Rig Lab: removed " + doomed.Count + " unused target(s).\n   " + string.Join(", ", names));
        }
    }
}
