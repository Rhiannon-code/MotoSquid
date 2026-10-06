using MotoSquid.Rider;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace MotoSquid.World
{
    public class RandomMaterialAssigner : EditorWindow
    {
        private string materialFolderPath = "Assets/Materials";
        private List<Material> loadedMaterials = new List<Material>();
        private Vector2 materialScrollPos;

        private bool assignToChildren = true;
        private bool affectAllSubmeshes = false;
        private int targetSubmeshIndex = 0;
        private bool useCustomSeed = false;
        private int randomSeed = 0;

        private Dictionary<GameObject, Material[]> originalMaterials = new Dictionary<GameObject, Material[]>();

        [MenuItem("Tools/Random Material Assigner")]
        public static void OpenWindow()
        {
            var window = GetWindow<RandomMaterialAssigner>("Random Material Assigner");
            window.minSize = new Vector2(420, 520);
        }

        void OnGUI()
        {
            GUILayout.Label("Random Material Assigner", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── FOLDER ──────────────────────────────────────
            EditorGUILayout.LabelField("Material Source", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            materialFolderPath = EditorGUILayout.TextField("Folder Path", materialFolderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string chosen = EditorUtility.OpenFolderPanel("Select Material Folder", "Assets", "");
                if (!string.IsNullOrEmpty(chosen) && chosen.StartsWith(Application.dataPath))
                    materialFolderPath = "Assets" + chosen.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Load Materials from Folder"))
                LoadMaterials();

            if (loadedMaterials.Count > 0)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField($"Loaded: {loadedMaterials.Count} material(s)", EditorStyles.miniBoldLabel);
                materialScrollPos = EditorGUILayout.BeginScrollView(materialScrollPos, GUILayout.Height(72));
                foreach (var mat in loadedMaterials)
                    EditorGUILayout.ObjectField(mat, typeof(Material), false);
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No materials loaded yet.", MessageType.Info);
            }

            EditorGUILayout.Space(6);

            // ── TARGETS ─────────────────────────────────────
            EditorGUILayout.LabelField("Target Objects", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Selected in Hierarchy: {Selection.gameObjects.Length} object(s)");
            assignToChildren = EditorGUILayout.Toggle("Include Children", assignToChildren);

            EditorGUILayout.Space(6);

            // ── OPTIONS ─────────────────────────────────────
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            affectAllSubmeshes = EditorGUILayout.Toggle("Affect All Submeshes", affectAllSubmeshes);
            if (!affectAllSubmeshes)
                targetSubmeshIndex = EditorGUILayout.IntField("  Submesh Index", Mathf.Max(0, targetSubmeshIndex));

            useCustomSeed = EditorGUILayout.Toggle("Use Custom Seed", useCustomSeed);
            if (useCustomSeed)
                randomSeed = EditorGUILayout.IntField("  Seed", randomSeed);

            EditorGUILayout.Space(8);

            // ── BUTTONS ─────────────────────────────────────
            GUI.enabled = loadedMaterials.Count > 0 && Selection.gameObjects.Length > 0;
            EditorGUILayout.BeginHorizontal();

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Assign Randomly", GUILayout.Height(38)))
                AssignMaterials();

            GUI.backgroundColor = new Color(0.8f, 0.5f, 0.4f);
            if (GUILayout.Button("Revert All", GUILayout.Height(38)))
                RevertMaterials();

            GUI.backgroundColor = prev;
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;

            if (originalMaterials.Count > 0)
                EditorGUILayout.HelpBox($"Revert available for {originalMaterials.Count} renderer(s). Ctrl+Z also works.", MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "1.  Set folder path → Load Materials\n" +
                "2.  Select buildings in the Hierarchy\n" +
                "3.  Assign Randomly\n" +
                "Submesh Index 0 = main body, 1 = windows, etc.",
                MessageType.None);
        }

        // ── LOAD ────────────────────────────────────────────────
        void LoadMaterials()
        {
            loadedMaterials.Clear();
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { materialFolderPath });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null) loadedMaterials.Add(mat);
            }

            if (loadedMaterials.Count == 0)
                EditorUtility.DisplayDialog("No Materials Found",
                    $"No materials found in:\n{materialFolderPath}", "OK");
            else
                Debug.Log($"[RandomMaterialAssigner] Loaded {loadedMaterials.Count} materials.");
        }

        // ── ASSIGN ──────────────────────────────────────────────
        void AssignMaterials()
        {
            if (loadedMaterials.Count == 0 || Selection.gameObjects.Length == 0) return;

            Random.State prevState = Random.state;
            if (useCustomSeed) Random.InitState(randomSeed);

            // Collect renderers
            List<Renderer> renderers = new List<Renderer>();
            foreach (GameObject go in Selection.gameObjects)
            {
                if (assignToChildren)
                    renderers.AddRange(go.GetComponentsInChildren<Renderer>(true));
                else
                {
                    Renderer r = go.GetComponent<Renderer>();
                    if (r != null) renderers.Add(r);
                }
            }
            renderers = renderers.Distinct().ToList();

            if (renderers.Count == 0)
            {
                EditorUtility.DisplayDialog("No Renderers", "No Renderer components found on selected objects.", "OK");
                return;
            }

            // Store originals for revert
            originalMaterials.Clear();
            foreach (Renderer r in renderers)
                originalMaterials[r.gameObject] = r.sharedMaterials.ToArray();

            // Undo support
            Undo.RecordObjects(renderers.Cast<Object>().ToArray(), "Random Material Assignment");

            foreach (Renderer r in renderers)
            {
                Material picked = loadedMaterials[Random.Range(0, loadedMaterials.Count)];

                if (affectAllSubmeshes)
                {
                    Material[] mats = Enumerable.Repeat(picked, r.sharedMaterials.Length).ToArray();
                    r.sharedMaterials = mats;
                }
                else
                {
                    Material[] mats = r.sharedMaterials;
                    int idx = Mathf.Min(targetSubmeshIndex, mats.Length - 1);
                    mats[idx] = picked;
                    r.sharedMaterials = mats;
                }
                EditorUtility.SetDirty(r);
            }

            Random.state = prevState;
            Debug.Log($"[RandomMaterialAssigner] Assigned to {renderers.Count} renderer(s).");
        }

        // ── REVERT ──────────────────────────────────────────────
        void RevertMaterials()
        {
            if (originalMaterials.Count == 0) return;

            foreach (var kvp in originalMaterials)
            {
                if (kvp.Key == null) continue;
                Renderer r = kvp.Key.GetComponent<Renderer>();
                if (r == null) continue;
                Undo.RecordObject(r, "Revert Material Assignment");
                r.sharedMaterials = kvp.Value;
                EditorUtility.SetDirty(r);
            }

            originalMaterials.Clear();
            Debug.Log("[RandomMaterialAssigner] All materials reverted.");
        }
    }
}
