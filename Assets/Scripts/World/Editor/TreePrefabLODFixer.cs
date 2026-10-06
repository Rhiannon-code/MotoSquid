#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace MotoSquid.World
{
    public class TreePrefabLODFixer : EditorWindow
    {
        string[] searchFolders = new[]
        {
            "Assets/Prefabs/Trees"
        };

        Vector2 scroll;
        List<string> preview = new List<string>();
        bool scanned;

        [MenuItem("Tools/Tree Prefab LOD Fixer")]
        static void Open() => GetWindow<TreePrefabLODFixer>("Tree LOD Fixer");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Tree Prefab LOD Fixer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Adds a single LOD LODGroup to tree prefab roots that are missing one.\n" +
                "Required for Unity terrain Paint Trees to instance the prefabs.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            if (GUILayout.Button("1. Scan for Broken Prefabs"))
                Scan();

            if (scanned)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"{preview.Count} prefab(s) need fixing:", EditorStyles.boldLabel);

                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(300));
                foreach (string p in preview)
                    EditorGUILayout.LabelField(p, EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(4);

                using (new EditorGUI.DisabledScope(preview.Count == 0))
                {
                    if (GUILayout.Button($"2. Fix All ({preview.Count} prefabs)"))
                        FixAll();
                }

                if (preview.Count == 0)
                    EditorGUILayout.HelpBox("All tree prefabs already have an LOD Group.", MessageType.None);
            }
        }

        void Scan()
        {
            preview.Clear();
            scanned = false;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", searchFolders);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                if (NeedsFix(prefab))
                    preview.Add(path);
            }

            scanned = true;
            Repaint();
        }

        static bool NeedsFix(GameObject root)
        {
            if (root.GetComponent<LODGroup>() != null) return false;
            if (root.GetComponent<MeshRenderer>() != null) return false;
            return root.GetComponentsInChildren<MeshRenderer>().Length > 0;
        }

        void FixAll()
        {
            int fixed_ = 0;
            foreach (string path in preview.ToList())
            {
                if (FixPrefab(path))
                    fixed_++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TreeLODFixer] Fixed {fixed_} prefab(s). Rescan to verify.");
            Scan();
        }

        static bool FixPrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return false;

            try
            {
                MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0) return false;

                LODGroup group = root.AddComponent<LODGroup>();
                LOD[] lods = new LOD[]
                {
                    new LOD(0.01f, renderers)
                };
                group.SetLODs(lods);
                group.RecalculateBounds();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
