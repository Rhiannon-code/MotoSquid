#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace MotoSquid.World
{
    public class TerrainTreeBatchAdder : EditorWindow
    {
        readonly List<string> folders    = new List<string> { "Assets/" };
        readonly List<string> foundGuids = new List<string>();

        Terrain targetTerrain;
        Vector2 scrollFolders;
        Vector2 scrollResults;
        bool    scanned;

        [MenuItem("Tools/Terrain Tree Batch Adder")]
        static void Open() => GetWindow<TerrainTreeBatchAdder>("Tree Batch Adder");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Terrain Tree Batch Adder", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawFolderList();
            EditorGUILayout.Space(4);

            targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", targetTerrain, typeof(Terrain), true);

            if (targetTerrain == null)
            {
                Terrain active = Selection.activeGameObject != null
                    ? Selection.activeGameObject.GetComponent<Terrain>()
                    : null;
                if (active != null)
                {
                    EditorGUILayout.HelpBox("Using selected Terrain in scene. Assign above to lock it.", MessageType.Info);
                    targetTerrain = active;
                }
                else
                {
                    EditorGUILayout.HelpBox("Select a Terrain in the scene or assign one above.", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Scan Folders for Prefabs"))
                Scan();

            if (scanned)
                DrawResults();
        }

        void DrawFolderList()
        {
            EditorGUILayout.LabelField("Prefab Folders", EditorStyles.boldLabel);
            scrollFolders = EditorGUILayout.BeginScrollView(scrollFolders, GUILayout.MaxHeight(120));

            for (int i = 0; i < folders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                folders[i] = EditorGUILayout.TextField(folders[i]);

                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Select Prefab Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        string dataPath = Application.dataPath;
                        if (picked.StartsWith(dataPath))
                            picked = "Assets" + picked.Substring(dataPath.Length);
                        folders[i] = picked;
                    }
                }

                if (GUILayout.Button(",", GUILayout.Width(20)) && folders.Count > 1)
                    folders.RemoveAt(i--);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Add Folder"))
                folders.Add("Assets/");
        }

        void Scan()
        {
            foundGuids.Clear();
            scanned = false;

            string[] validFolders = folders
                .Where(f => !string.IsNullOrWhiteSpace(f) && AssetDatabase.IsValidFolder(f))
                .ToArray();

            if (validFolders.Length == 0)
            {
                Debug.LogWarning("[TreeBatchAdder] No valid folders specified.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", validFolders);
            foundGuids.AddRange(guids.Distinct());
            scanned = true;
            Repaint();
        }

        void DrawResults()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Found {foundGuids.Count} prefab(s)", EditorStyles.boldLabel);

            scrollResults = EditorGUILayout.BeginScrollView(scrollResults, GUILayout.MaxHeight(200));
            foreach (string guid in foundGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);

            if (foundGuids.Count == 0)
                return;

            using (new EditorGUI.DisabledScope(targetTerrain == null))
            {
                if (GUILayout.Button($"Add All to Terrain ({foundGuids.Count} prefabs)"))
                    AddToTerrain();
            }
        }

        void AddToTerrain()
        {
            if (targetTerrain == null)
                return;

            TerrainData td = targetTerrain.terrainData;
            Undo.RecordObject(td, "Batch Add Tree Prototypes");

            HashSet<string> existingPaths = new HashSet<string>(
                td.treePrototypes
                  .Where(p => p.prefab != null)
                  .Select(p => AssetDatabase.GetAssetPath(p.prefab))
            );

            List<TreePrototype> updated = td.treePrototypes.ToList();
            int added = 0;

            foreach (string guid in foundGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (existingPaths.Contains(path))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                updated.Add(new TreePrototype { prefab = prefab });
                existingPaths.Add(path);
                added++;
            }

            td.treePrototypes = updated.ToArray();
            EditorUtility.SetDirty(td);

            Debug.Log($"[TreeBatchAdder] Added {added} tree prototype(s) to {targetTerrain.name}. " +
                      $"Total: {td.treePrototypes.Length}.");
        }
    }
}
#endif
