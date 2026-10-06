using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabMissingScripts
    {
        [MenuItem("Tools/Rig Lab/36. Remove Missing Scripts (scene + bike prefabs)", false, 360)]
        static void Clean()
        {
            int scene = 0, assets = 0;

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (n == 0) continue;
                Undo.RegisterCompleteObjectUndo(go, "Remove missing scripts");
                scene += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            }

            foreach (var dir in new[] { "Assets/Prefabs" })
            {
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        int removed = root.GetComponentsInChildren<Transform>(true)
                            .Sum(t => GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject));
                        if (removed > 0)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            assets += removed;
                            Debug.Log("Rig Lab: " + System.IO.Path.GetFileName(path) + ", removed " +
                                      removed + " missing script(s).");
                        }
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Rig Lab: removed " + scene + " missing script(s) from the open scene and " +
                      assets + " from bike prefabs." +
                      (assets > 0 ? "\nRe-run the wheelie pivot fit, prefabs that failed to save can now be written." : ""));
        }
    }
}
