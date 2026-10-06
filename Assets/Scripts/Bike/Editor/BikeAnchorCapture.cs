using MotoSquid.DevTools;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Bike
{
    public static class BikeAnchorCapture
    {
        const string Tag = "[AnchorCapture]";
        const string BikeFolder = "Assets/Prefabs/Characters";

        static readonly string[] Anchors =
        {
            "SeatAnchor", "GripAnchor_L", "GripAnchor_R", "FootPegGrip_L", "FootPegGrip_R",
        };

        [MenuItem("MotoSquid/Characters/Capture Anchors To Bike Prefab (Selected)")]
        public static void Capture()
        {
            var selection = Selection.activeGameObject;
            if (selection == null)
            {
                Debug.LogError($"{Tag} Select the built character in the scene (the prefab root).");
                return;
            }

            var bikePaths = AssetDatabase.FindAssets("t:Prefab", new[] { BikeFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToDictionary(System.IO.Path.GetFileNameWithoutExtension, p => p);

            var art = selection.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => bikePaths.ContainsKey(t.name) && FindDeep(t, "SeatAnchor") != null);

            if (art == null)
            {
                Debug.LogError($"{Tag} {selection.name}: no child matches a bike prefab in {BikeFolder}. " +
                               "Select the character root, not the rider.");
                return;
            }

            var path = bikePaths[art.name];
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int written = 0;
                foreach (var name in Anchors)
                {
                    var live = FindDeep(art, name);
                    var target = FindDeep(root.transform, name);
                    if (live == null || target == null)
                    {
                        Debug.LogWarning($"{Tag} '{name}' missing on {(live == null ? selection.name : art.name)}, skipped.");
                        continue;
                    }

                    var movedBy = Vector3.Distance(target.localPosition, live.localPosition);
                    var turnedBy = Quaternion.Angle(target.localRotation, live.localRotation);
                    if (movedBy < 0.0005f && turnedBy < 0.05f) continue;

                    target.localPosition = live.localPosition;
                    target.localRotation = live.localRotation;
                    written++;

                    Debug.Log($"{Tag} {art.name}.{name}: moved {movedBy * 100f:0.0} cm, turned {turnedBy:0.0} deg.");
                }

                if (written == 0)
                {
                    Debug.Log($"{Tag} {art.name}: nothing differed from the prefab, nothing written.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"{Tag} Wrote {written} anchor(s) to {System.IO.Path.GetFileName(path)}. " +
                          "Every character on this bike inherits them at the next rebuild.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = FindDeep(c, name);
                if (f != null) return f;
            }

            return null;
        }
    }
}
