#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Performance
{
    public class OcclusionStaticHelper : EditorWindow
    {
        static readonly string[] s_OccluderPrefixes = {
            "Building", "Road", "Sidewalk", "Pavement", "Wall",
            "Flinders", "MCG", "StarWheel", "Eureka", "Tunnel",
            "Ground", "Track", "Barrier", "Fence", "Bridge",
            "Melbourne", "City", "Block", "Structure"
        };

        // Name fragments that should NEVER be touched
        static readonly string[] s_ExcludeFragments = {
            "Bike", "Player", "AI", "Traffic", "Car", "Van", "Truck", "Train",
            "Camera", "Manager", "Spawner", "UI", "Canvas", "HUD", "Light",
            "Spline", "Waypoint", "Trigger", "Collider", "Volume"
        };

        [MenuItem("Tools/Occlusion Static Helper")]
        static void Open() => GetWindow<OcclusionStaticHelper>("Occlusion Static");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Occlusion Static Helper", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Marks environment meshes (buildings, roads, walls…) as both " +
                "Occluder Static and Occludee Static so the occlusion bake has " +
                "solid geometry to work with. Bikes and dynamic objects are excluded.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            if (GUILayout.Button("Preview (no changes)"))
                Run(dryRun: true);

            GUI.color = Color.green;
            if (GUILayout.Button("Apply Occluder Static to Environment"))
                Run(dryRun: false);
            GUI.color = Color.white;
        }

        static void Run(bool dryRun)
        {
            var candidates = new List<GameObject>();

            foreach (var mr in FindObjectsByType<MeshRenderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                GameObject go = mr.gameObject;

                if (ShouldExclude(go.name)) continue;
                if (!HasOccluderPrefix(go.name) && !HasOccluderPrefix(GetRootName(go))) continue;

                candidates.Add(go);
            }

            if (candidates.Count == 0)
            {
                Debug.Log("[Occlusion Helper] No matching environment objects found.");
                return;
            }

            Debug.Log($"[Occlusion Helper] {(dryRun ? "Would mark" : "Marking")} " +
                      $"{candidates.Count} object(s) as Occluder + Occludee Static.");

            if (dryRun)
            {
                foreach (var go in candidates)
                    Debug.Log($"  • {go.name}", go);
                return;
            }

            Undo.RecordObjects(candidates.ToArray(), "Set Occluder Static");

            const StaticEditorFlags flags =
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

            foreach (var go in candidates)
            {
                StaticEditorFlags current = GameObjectUtility.GetStaticEditorFlags(go);
                GameObjectUtility.SetStaticEditorFlags(go, current | flags);
            }

            Debug.Log("[Occlusion Helper] Done. Now bake: Window → Rendering → Occlusion Culling → Bake.");
        }

        static bool HasOccluderPrefix(string name)
        {
            foreach (var prefix in s_OccluderPrefixes)
                if (name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        static bool ShouldExclude(string name)
        {
            foreach (var frag in s_ExcludeFragments)
                if (name.IndexOf(frag, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        static string GetRootName(GameObject go)
        {
            Transform t = go.transform;
            while (t.parent != null) t = t.parent;
            return t.name;
        }
    }
}
#endif
