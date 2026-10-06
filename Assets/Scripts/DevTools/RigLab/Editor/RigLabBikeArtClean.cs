using MotoSquid.Rider;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabBikeArtClean
    {
        [MenuItem("Tools/Rig Lab/31. Clean Bike Art (selected prefabs)", false, 310)]
        static void CleanSelected()
        {
            var prefabs = Selection.objects.OfType<GameObject>()
                .Where(go => PrefabUtility.IsPartOfPrefabAsset(go))
                .ToArray();

            if (prefabs.Length == 0)
            {
                Debug.LogError("Rig Lab: select one or more bike art prefabs in the Project window.");
                return;
            }

            int changed = 0;
            foreach (var asset in prefabs)
            {
                var path = AssetDatabase.GetAssetPath(asset);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = Strip(root);
                    if (n > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changed++;
                        Debug.Log("Rig Lab: cleaned " + asset.name + " , " + n + " authored item(s) removed.", asset);
                    }
                    else Debug.Log("Rig Lab: " + asset.name + " was already clean.", asset);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Rig Lab: bike art clean, " + changed + " of " + prefabs.Length + " prefab(s) changed.");
        }

        static int Strip(GameObject root)
        {
            int n = 0;

            foreach (var c in root.GetComponentsInChildren<BikeRiderAnimator>(true).ToArray())
            { Object.DestroyImmediate(c, true); n++; }

            foreach (var c in root.GetComponentsInChildren<AnimatedRootPin>(true).ToArray())
            { Object.DestroyImmediate(c, true); n++; }

            foreach (var a in root.GetComponentsInChildren<Animator>(true))
                if (a.runtimeAnimatorController != null)
                { a.runtimeAnimatorController = null; n++; }

            return n;
        }
    }
}
