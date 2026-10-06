using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabWheeliePivot
    {
        const float PivotZ = -0.49f;
        const string Dir = "Assets/Prefabs/Characters/New";

        [MenuItem("Tools/Rig Lab/37. Restore ABP Wheelie Pivots", false, 370)]
        static void Restore()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) { Debug.LogError("Rig Lab: " + Dir + " not found."); return; }

            int fixedCount = 0, already = 0, skipped = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var wheelie = FindDeep(root.transform, "Wheelie Transform");
                    var lean = FindDeep(root.transform, "Lean Transform");
                    if (wheelie == null || lean == null)
                    {
                        Debug.LogWarning("Rig Lab: " + System.IO.Path.GetFileName(path) +
                                         " has no Wheelie/Lean Transform, skipped.");
                        skipped++;
                        continue;
                    }

                    var wp = wheelie.localPosition;
                    var lp = lean.localPosition;
                    if (Mathf.Approximately(wp.z, PivotZ) && Mathf.Approximately(lp.z, -PivotZ)) { already++; continue; }

                    wheelie.localPosition = new Vector3(wp.x, wp.y, PivotZ);
                    lean.localPosition = new Vector3(lp.x, lp.y, -PivotZ);

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    fixedCount++;
                    Debug.Log("Rig Lab: " + System.IO.Path.GetFileName(path) +
                              " wheelie pivot " + wp.z.ToString("0.###") + " -> " + PivotZ.ToString("0.###") +
                              ", lean " + lp.z.ToString("0.###") + " -> " + (-PivotZ).ToString("0.###"));
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Rig Lab: wheelie pivots, " + fixedCount + " restored, " + already +
                      " already correct, " + skipped + " skipped.\n" +
                      "Reopen the scene so the instances pick this up.");
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var hit = FindDeep(t.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
