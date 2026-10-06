using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabBikePrefab
    {
        const string Reference = "Assets/Prefabs/Characters/Futuristic_Bike.prefab";
        const string OutDir = "Assets/Prefabs/Characters";

        static readonly string[] Anchors =
            { "SeatAnchor", "GripAnchor_L", "GripAnchor_R", "FootPegGrip_L", "FootPegGrip_R" };

        [MenuItem("Tools/Rig Lab/33. Make Bike Prefab With Anchors (selected models)", false, 330)]
        static void Make()
        {
            var models = Selection.objects.OfType<GameObject>()
                .Where(go => PrefabUtility.IsPartOfPrefabAsset(go))
                .ToArray();
            if (models.Length == 0)
            {
                Debug.LogError("Rig Lab: select one or more bike FBX models in the Project window.");
                return;
            }

            var refAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Reference);
            if (refAsset == null) { Debug.LogError("Rig Lab: reference bike not found at " + Reference); return; }

            var refGo = (GameObject)PrefabUtility.InstantiatePrefab(refAsset);
            Vector3[] norm;
            try
            {
                if (!Frame(refGo.transform, out var rRear, out var rAxis, out var rLen))
                { Debug.LogError("Rig Lab: reference bike has no Front_Wheel/Back_Wheel."); return; }

                norm = new Vector3[Anchors.Length];
                for (int i = 0; i < Anchors.Length; i++)
                {
                    var a = FindDeep(refGo.transform, Anchors[i]);
                    if (a == null) { Debug.LogError("Rig Lab: reference bike has no '" + Anchors[i] + "'."); return; }
                    var d = a.position - rRear;
                    norm[i] = new Vector3(d.x / rLen, d.y / rLen, Vector3.Dot(d, rAxis) / rLen);
                }
            }
            finally { Object.DestroyImmediate(refGo); }

            int made = 0;
            foreach (var model in models)
            {
                var outPath = OutDir + "/" + model.name + ".prefab";
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(outPath);
                var go = existing != null
                    ? PrefabUtility.LoadPrefabContents(outPath)
                    : (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    if (!Frame(go.transform, out var rear, out var axis, out var len))
                    {
                        Debug.LogError("Rig Lab: " + model.name + " has no Front_Wheel/Back_Wheel, cannot fit anchors.", model);
                        continue;
                    }

                    var host = FindDeep(go.transform, "Rider Anchors");
                    if (host == null)
                    {
                        host = new GameObject("Rider Anchors").transform;
                        host.SetParent(go.transform, false);
                    }

                    int added = 0;
                    for (int i = 0; i < Anchors.Length; i++)
                    {
                        if (FindDeep(go.transform, Anchors[i]) != null) continue;
                        var t = new GameObject(Anchors[i]).transform;
                        t.SetParent(host, false);
                        t.position = rear + axis * (norm[i].z * len)
                                   + Vector3.up * (norm[i].y * len)
                                   + go.transform.right * (norm[i].x * len);
                        t.rotation = go.transform.rotation;
                        added++;
                    }

                    if (added == 0) { Debug.Log("Rig Lab: " + model.name + " already has all five anchors."); continue; }

                    PrefabUtility.SaveAsPrefabAsset(go, outPath);
                    made++;
                    Debug.Log("Rig Lab: " + model.name + " -> " + outPath + " (" + added + " anchor(s) added)" +
                              "\n   fitted from " + System.IO.Path.GetFileNameWithoutExtension(Reference) +
                              ", CHECK THEM BY EYE, a proportional fit is not a correct fit.");
                }
                finally
                {
                    if (existing != null) PrefabUtility.UnloadPrefabContents(go);
                    else Object.DestroyImmediate(go);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rig Lab: " + made + " of " + models.Length + " bike prefab(s) created. " +
                      "Point each CharacterDefinition's Bike Prefab at the PREFAB, not the FBX.");
        }

        static bool Frame(Transform root, out Vector3 rear, out Vector3 axis, out float len)
        {
            rear = Vector3.zero; axis = Vector3.forward; len = 0f;
            var f = FindDeep(root, "Front_Wheel");
            var b = FindDeep(root, "Back_Wheel") ?? FindDeep(root, "Rear_Wheel");
            if (f == null || b == null) return false;
            rear = b.position;
            var d = f.position - b.position;
            len = d.magnitude;
            if (len < 0.0001f) return false;
            axis = d / len;
            return true;
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
