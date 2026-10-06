using MotoSquid.Bike;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    internal static class RigLabScope
    {
        internal static BikeController[] Bikes() => Bikes(false);
        internal static BikeController[] Bikes(bool forWriting)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                return stage.prefabContentsRoot.GetComponentsInChildren<BikeController>(true);

            var found = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (!forWriting) return found;

            var open = found.Where(b => !PrefabUtility.IsPartOfPrefabInstance(b.gameObject)).ToArray();
            var blocked = found.Where(b => PrefabUtility.IsPartOfPrefabInstance(b.gameObject)).ToArray();
            if (blocked.Length > 0)
                Debug.LogWarning("Rig Lab: " + blocked.Length + " bike(s) skipped, they are prefab INSTANCES and " +
                                 "editing them here would only make overrides. Open the prefab instead:\n   " +
                                 string.Join("\n   ", blocked.Select(b => b.name)));
            return open;
        }

        // Player bikes carry BikeController, AI bikes carry BikeAIController and never both, so
        // anything that works on any racer has to ask for roots rather than for the player type
        internal static GameObject[] BikeRoots() => BikeRoots(false);

        internal static GameObject[] BikeRoots(bool forWriting)
        {
            var found = AllBikeRoots();
            if (!forWriting) return found;

            var open = found.Where(g => !PrefabUtility.IsPartOfPrefabInstance(g)).ToArray();
            var blocked = found.Where(PrefabUtility.IsPartOfPrefabInstance).ToArray();
            if (blocked.Length > 0)
                Debug.LogWarning("Rig Lab: " + blocked.Length + " bike(s) skipped, they are prefab INSTANCES and " +
                                 "editing them here would only make overrides. Open the prefab instead:\n   " +
                                 string.Join("\n   ", blocked.Select(g => g.name)));
            return open;
        }

        static GameObject[] AllBikeRoots()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                return Roots(stage.prefabContentsRoot.GetComponentsInChildren<BikeController>(true),
                             stage.prefabContentsRoot.GetComponentsInChildren<BikeAIController>(true));

            return Roots(Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                         Object.FindObjectsByType<BikeAIController>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        static GameObject[] Roots(BikeController[] player, BikeAIController[] ai) =>
            player.Select(b => b.gameObject).Concat(ai.Select(b => b.gameObject)).Distinct().ToArray();

        // The two controllers declare a BikeReferences each, so the model is the only shared handle.
        internal static Transform BikeModelOf(GameObject root)
        {
            if (root == null) return null;

            var player = root.GetComponent<BikeController>();
            if (player != null) return player.bikeReferences != null ? player.bikeReferences.BikeModel : null;

            var ai = root.GetComponent<BikeAIController>();
            return ai != null && ai.bikeReferences != null ? ai.bikeReferences.BikeModel : null;
        }

        internal static bool IsSelected(GameObject go)
        {
            foreach (var s in Selection.gameObjects)
                if (s == go || go.transform.IsChildOf(s.transform) || s.transform.IsChildOf(go.transform)) return true;
            return false;
        }

        // A scene holding other anchored bikes is the normal case, so the batch tools narrow to
        // whatever is selected and fall back to every bike only when nothing relevant is picked
        internal static T[] Narrow<T>(T[] all) where T : Component
        {
            var picked = all.Where(c => c != null && IsSelected(c.gameObject)).ToArray();
            return picked.Length > 0 ? picked : all;
        }

        internal static GameObject[] Narrow(GameObject[] all)
        {
            var picked = all.Where(g => g != null && IsSelected(g)).ToArray();
            return picked.Length > 0 ? picked : all;
        }

        internal static string ScopeNote(int used, int found) =>
            used == found
                ? "\n   scope: every bike in " + Where() + "."
                : "\n   scope: " + used + " of " + found + " bike(s), narrowed by the Hierarchy selection.";

        internal static void ClearSelectionForEdit() => Selection.objects = new Object[0];

        internal static void MarkDirty()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            EditorSceneManager.MarkSceneDirty(stage != null ? stage.scene : EditorSceneManager.GetActiveScene());
        }

        internal static string Where() =>
            PrefabStageUtility.GetCurrentPrefabStage() != null ? "the open prefab" : "the open scene";
    }
}
