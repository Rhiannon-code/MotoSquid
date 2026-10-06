using System.Text;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Core
{
    public static class RemoveMissingScripts
    {
        [MenuItem("MotoSquid/Fix/Remove Missing Scripts From Selected Prefabs")]
        static void Clean()
        {
            var prefabs = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
            if (prefabs == null || prefabs.Length == 0)
            {
                EditorUtility.DisplayDialog("Remove Missing Scripts",
                    "Select one or more prefabs in the Project window first.", "OK");
                return;
            }

            int totalRemoved = 0, touched = 0;
            var log = new StringBuilder();

            foreach (var asset in prefabs)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) { log.AppendLine($"x {asset.name}: could not open"); continue; }

                try
                {
                    int removed = 0;
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

                    if (removed > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        totalRemoved += removed;
                        touched++;
                        log.AppendLine($"+ {asset.name}: removed {removed}");
                    }
                    else log.AppendLine($", {asset.name}: none found");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Remove Missing Scripts",
                $"Removed {totalRemoved} missing component(s) from {touched} prefab(s).\n\n{log}", "OK");
        }
    }
}
