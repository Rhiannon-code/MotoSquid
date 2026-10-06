using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Bike
{
    public static class DummyBikeBuilder
    {
        const string Tag       = "[DummyBikeBuilder]";
        const string OutputDir = "Assets/Prefabs/Ragdolls";
        const string ModelName = "Bike Model";

        static readonly string[] StripPrefixes =
        {
            "Biker Animation Targets",
            "Biker Rig",
            "Combat Poses",
            "Rider Anchors",
            "Rescued FX",
        };

        [MenuItem("MotoSquid/Characters/Create Dummy Bike From Selected")]
        public static void Create()
        {
            var selection = Selection.activeGameObject;
            if (selection == null)
            {
                Debug.LogError($"{Tag} Select a bike prefab in the Project window first.");
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(selection);
            bool   fromAsset  = !string.IsNullOrEmpty(sourcePath);
            var    root       = fromAsset ? PrefabUtility.LoadPrefabContents(sourcePath) : selection;

            try
            {
                Build(root, selection.name);
            }
            finally
            {
                if (fromAsset) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void Build(GameObject root, string sourceName)
        {
            var model = FindDeep(root.transform, ModelName);
            if (model == null)
            {
                Debug.LogError($"{Tag} '{sourceName}' has no '{ModelName}' child, so there is no bike art to copy.");
                return;
            }

            var body = model.Find("Body Mesh");
            if (body == null || body.childCount == 0)
            {
                Debug.LogError($"{Tag} '{sourceName}' has no 'Body Mesh' with art under it, cannot name the dummy.");
                return;
            }

            string name = "DummyBike_" + body.GetChild(0).name;
            string path = $"{OutputDir}/{name}.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null &&
                !EditorUtility.DisplayDialog("Dummy bike already exists",
                    $"{path} already exists.\n\nOverwrite it? Every RagdollActivator pointing at it will " +
                    "pick up the new version.", "Overwrite", "Cancel"))
            {
                return;
            }

            var copy = Object.Instantiate(model.gameObject);
            try
            {
                copy.name = name;
                copy.transform.localScale = model.lossyScale;
                Strip(copy);
                AddPhysics(copy, root.GetComponent<Rigidbody>());

                if (!AssetDatabase.IsValidFolder(OutputDir))
                {
                    Debug.LogError($"{Tag} Output folder '{OutputDir}' does not exist.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(copy, path);
                Debug.Log($"{Tag} Wrote {path}. Assign it to Dummy Bike Prefab on the RagdollActivator " +
                          $"of every bike using this art.", AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        static void Strip(GameObject copy)
        {
            foreach (var child in copy.GetComponentsInChildren<Transform>(true)
                         .Where(t => t != copy.transform)
                         .Where(t => t != null &&
                                     (StripPrefixes.Any(p => t.name.StartsWith(p)) ||
                                      t.GetComponent<Animator>() != null))
                         .ToList())
            {
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }

            foreach (var light in copy.GetComponentsInChildren<Light>(true).ToList())
                if (light != null) Object.DestroyImmediate(light.gameObject);

            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true).ToList())
                if (behaviour != null) Object.DestroyImmediate(behaviour);
        }

        static void AddPhysics(GameObject copy, Rigidbody source)
        {
            var rb = copy.AddComponent<Rigidbody>();
            rb.mass          = source != null ? source.mass : 150f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            AddWheelCollider(copy, "Front Wheel");
            AddWheelCollider(copy, "Rear Wheel");
        }

        static void AddWheelCollider(GameObject copy, string wheelName)
        {
            var wheel = FindDeep(copy.transform, wheelName);
            if (wheel == null)
            {
                Debug.LogWarning($"{Tag} No '{wheelName}' under the bike art, that wheel gets no collider, " +
                                 "so the dummy will sink through the road on that end.", copy);
                return;
            }

            var renderer = wheel.GetComponentInChildren<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"{Tag} '{wheelName}' has no MeshRenderer, cannot size its collider.", copy);
                return;
            }

            wheel.rotation = Quaternion.identity;
            renderer.transform.rotation = Quaternion.identity;
            wheel.gameObject.AddComponent<SphereCollider>().radius = renderer.bounds.extents.y;
            wheel.localRotation = Quaternion.identity;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var found = FindDeep(c, name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
