using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Policy = MotoSquid.Performance.LightBakePolicy;

namespace MotoSquid.Performance
{

    [InitializeOnLoad]
    public static class LightBakeGuard
    {
        public struct Violation
        {
            public string Where;
            public LightmapBakeType Is;
            public LightmapBakeType Want;
            public Object Context;

            public override string ToString() => $"{Where}  is {Is}, should be {Want}";
        }

        static LightBakeGuard()
        {
            Lightmapping.bakeStarted -= OnBakeStarted;
            Lightmapping.bakeStarted += OnBakeStarted;
        }

        [MenuItem("MotoSquid/Lighting/Validate Light Bake Modes")]
        public static void ValidateAll()
        {
            var scene = ScanOpenScenes();
            var prefabs = ScanPrefabs();

            foreach (var v in scene) Debug.LogWarning($"[LightBake] scene  {v}", v.Context);
            foreach (var v in prefabs) Debug.LogWarning($"[LightBake] prefab {v}", v.Context);

            if (scene.Count == 0 && prefabs.Count == 0)
            {
                Debug.Log("[LightBake] Clean: every light in the open scenes and under " +
                          $"{Policy.PrefabRoot} matches the bake policy.");
                return;
            }

            Debug.LogError($"[LightBake] {scene.Count} scene light(s) and {prefabs.Count} prefab light(s) " +
                           "violate the bake policy. Run MotoSquid/Lighting/Apply Light Bake Modes to fix.");
        }

        static void OnBakeStarted()
        {
            var violations = ScanOpenScenes();
            if (violations.Count == 0) return;

            foreach (var v in violations) Debug.LogError($"[LightBake] {v}", v.Context);

            Lightmapping.Cancel();
            Debug.LogError($"[LightBake] Bake cancelled: {violations.Count} light(s) in the open scenes " +
                           "would bake incorrectly. Run MotoSquid/Lighting/Apply Light Bake Modes, then bake again.");
        }

        public static List<Violation> ScanOpenScenes()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return lights.Where(l => !Policy.IsCorrect(l)).Select(l => new Violation
            {
                Where   = Policy.PathOf(l.transform),
                Is      = l.lightmapBakeType,
                Want    = Policy.ModeFor(l),
                Context = l,
            }).ToList();
        }

        public static List<Violation> ScanPrefabs()
        {
            var found = new List<Violation>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Policy.PrefabRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;

                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (Policy.IsCorrect(light)) continue;
                    found.Add(new Violation
                    {
                        Where   = $"{path} :: {Policy.PathOf(light.transform)}",
                        Is      = light.lightmapBakeType,
                        Want    = Policy.ModeFor(light),
                        Context = root,
                    });
                }
            }
            return found;
        }
    }
}
