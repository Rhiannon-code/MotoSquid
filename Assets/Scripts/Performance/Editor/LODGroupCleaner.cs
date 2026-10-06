using MotoSquid.AI;
using MotoSquid.Bike;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Performance
{
    public class LODGroupCleaner : EditorWindow
    {
        [MenuItem("Tools/LOD Group Cleaner")]
        static void Open() => GetWindow<LODGroupCleaner>("LOD Cleaner");

        void OnGUI()
        {
            EditorGUILayout.LabelField("LOD Group Cleaner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Finds every _LODGroup object that is a descendant of a bike " +
                "(BikeController or BikeAILogic) and unwinds it, " +
                "restoring the original LOD0 mesh to its correct place in the hierarchy.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            if (GUILayout.Button("Preview (no changes)"))
                Run(dryRun: true);

            GUI.color = Color.green;
            if (GUILayout.Button("Clean Up Bike LODs"))
                Run(dryRun: false);
            GUI.color = Color.white;
        }

        static void Run(bool dryRun)
        {
            // Collect every _LODGroup in the scene whose ancestor is a bike
            var toClean = new List<LODGroup>();

            foreach (var lg in FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                if (!lg.gameObject.name.EndsWith("_LODGroup")) continue;
                if (IsDescendantOfBike(lg.transform))
                    toClean.Add(lg);
            }

            if (toClean.Count == 0)
            {
                Debug.Log("[LOD Cleaner] No bike LODGroups found.");
                return;
            }

            Debug.Log($"[LOD Cleaner] Found {toClean.Count} bike LODGroup(s) to {(dryRun ? "report" : "remove")}.");

            if (dryRun)
            {
                foreach (var lg in toClean)
                    Debug.Log($"  • {lg.gameObject.name} (parent: {lg.transform.parent?.name})", lg.gameObject);
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clean Bike LODGroups");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var lg in toClean)
            {
                GameObject lodGroupGO = lg.gameObject;
                Transform  lodParent  = lodGroupGO.transform.parent;
                int        siblingIdx = lodGroupGO.transform.GetSiblingIndex();
            
                var originals = new List<Transform>();
                for (int i = 0; i < lodGroupGO.transform.childCount; i++)
                {
                    Transform child = lodGroupGO.transform.GetChild(i);
                    if (!child.name.EndsWith("_LOD1") && !child.name.EndsWith("_LOD2"))
                        originals.Add(child);
                }

                for (int i = 0; i < originals.Count; i++)
                {
                    Undo.SetTransformParent(originals[i], lodParent, "Restore LOD0");
                    originals[i].SetSiblingIndex(siblingIdx + i);
                }

                // Now the LODGroup only contains LOD1/LOD2 safe to destroy
                Undo.DestroyObjectImmediate(lodGroupGO);
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[LOD Cleaner] Cleaned {toClean.Count} bike LODGroup(s). Save the scene (Ctrl+S).");
        }

        static bool IsDescendantOfBike(Transform t)
        {
            Transform current = t.parent;
            while (current != null)
            {
                if (current.GetComponent<BikeController>() != null) return true;
                if (current.GetComponent<BikeAILogic>()    != null) return true;
                current = current.parent;
            }
            return false;
        }
    }
}
#endif
