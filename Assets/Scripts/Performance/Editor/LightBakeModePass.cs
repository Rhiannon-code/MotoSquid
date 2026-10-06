using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Policy = MotoSquid.Performance.LightBakePolicy;

namespace MotoSquid.Performance
{

    public static class LightBakeModePass
    {
        [MenuItem("MotoSquid/Lighting/Audit Light Bake Modes")]
        public static void Audit() => Run(false);

        [MenuItem("MotoSquid/Lighting/Apply Light Bake Modes")]
        public static void Apply() => Run(true);

        static void Run(bool apply)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[LightBake] Editor is compiling/importing. Wait for it to settle, then run again.");
                return;
            }

            int sceneChanged  = RunOnOpenScenes(apply);
            int prefabChanged = RunOnPrefabs(apply);

            Debug.Log($"[LightBake] {(apply ? "Applied" : "Audit (no changes written)")}: " +
                      $"{sceneChanged} light(s) in open scenes, {prefabChanged} light(s) in prefabs.");

            if (apply) AssetDatabase.SaveAssets();
        }

        static int RunOnOpenScenes(bool apply)
        {
            int changed = 0;

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var want = Policy.ModeFor(light);
                if (light.lightmapBakeType == want) continue;

                Debug.Log($"[LightBake] scene  {Policy.PathOf(light.transform)}  " +
                          $"{light.lightmapBakeType} -> {want}", light);
                changed++;
                if (!apply) continue;

                Undo.RecordObject(light, "Set Light Bake Mode");
                light.lightmapBakeType = want;
                EditorUtility.SetDirty(light);
                EditorSceneManager.MarkSceneDirty(light.gameObject.scene);
            }
            return changed;
        }

        static int RunOnPrefabs(bool apply)
        {
            int changed = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Policy.PrefabRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!apply)
                {
                    changed += CountPrefabViolations(path);
                    continue;
                }

                var root  = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;

                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    var want = Policy.ModeFor(light);
                    if (light.lightmapBakeType == want) continue;

                    Debug.Log($"[LightBake] prefab {path} :: {Policy.PathOf(light.transform)}  " +
                              $"{light.lightmapBakeType} -> {want}");
                    light.lightmapBakeType = want;
                    dirty = true;
                    changed++;
                }

                if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            return changed;
        }

        static int CountPrefabViolations(string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) return 0;

            int n = 0;
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (Policy.IsCorrect(light)) continue;

                Debug.Log($"[LightBake] prefab {path} :: {Policy.PathOf(light.transform)}  " +
                          $"{light.lightmapBakeType} -> {Policy.ModeFor(light)}", root);
                n++;
            }
            return n;
        }
    }
}
