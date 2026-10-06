#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MotoSquid.World
{
    public class TreePrefabOrientationFixer : EditorWindow
    {
        static readonly string[] SearchFolders = { "Assets/Prefabs/Trees" };

        Vector3 rotationToApply = new Vector3(-90f, 0f, 0f);
        bool    previewDone;
        int     prefabCount;
        Vector2 scroll;
        List<string> paths = new List<string>();

        [MenuItem("Tools/Tree Prefab Orientation Fixer")]
        static void Open() => GetWindow<TreePrefabOrientationFixer>("Tree Orientation Fixer");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Tree Prefab Orientation Fixer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sets the root Transform rotation on every tree prefab.\n" +
                "Use -90 X to correct Z-up (Blender) models. Use 0,0,0 to reset.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            rotationToApply = EditorGUILayout.Vector3Field("Root Rotation (Euler)", rotationToApply);

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preset: -90 X  (Z-up fix)"))  rotationToApply = new Vector3(-90, 0, 0);
            if (GUILayout.Button("Preset:  90 X"))               rotationToApply = new Vector3( 90, 0, 0);
            if (GUILayout.Button("Preset: Reset (0,0,0)"))       rotationToApply = Vector3.zero;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            if (GUILayout.Button("1. Scan Prefabs"))
                Scan();

            if (previewDone)
            {
                EditorGUILayout.LabelField($"Found {prefabCount} tree prefab(s):", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(200));
                foreach (string p in paths)
                    EditorGUILayout.LabelField(p, EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Will set root rotation to: {rotationToApply}", EditorStyles.helpBox);
                EditorGUILayout.Space(4);

                if (GUILayout.Button($"2. Apply to All {prefabCount} Prefabs", GUILayout.Height(30)))
                    ApplyAll();
            }
        }

        void Scan()
        {
            paths.Clear();
            previewDone = false;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
            foreach (string guid in guids)
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            prefabCount = paths.Count;
            previewDone = true;
            Repaint();
        }

        void ApplyAll()
        {
            int fixed_ = 0;
            Quaternion rot = Quaternion.Euler(rotationToApply);

            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                try
                {
                    root.transform.localRotation = rot;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    fixed_++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TreeOrientationFixer] Applied rotation {rotationToApply} to {fixed_} prefab(s).");
            EditorUtility.DisplayDialog("Done", $"Rotation {rotationToApply} applied to {fixed_} prefabs.\n\nClick Refresh in the Paint Trees panel.", "OK");
        }
    }
}
#endif
