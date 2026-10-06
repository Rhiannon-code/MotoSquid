using MotoSquid.Bike;
using MotoSquid.UI;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabResetReview
    {
        [MenuItem("Tools/Rig Lab/49. Reset Review Scene To Prefabs", false, 490)]
        static void Reset()
        {
            var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.parent == null && t.GetComponent<BikeController>() != null)
                .Select(t => t.gameObject)
                .Where(PrefabUtility.IsPartOfPrefabInstance)
                .ToList();

            var picked = Selection.gameObjects.Where(roots.Contains).ToList();
            if (picked.Count > 0) roots = picked;

            if (roots.Count == 0) { Debug.LogWarning("Rig Lab: no bike prefab instances in the open scene."); return; }

            if (!EditorUtility.DisplayDialog("Rig Lab",
                    "Discard every inspector change made to " + roots.Count + " bike(s) in this scene and " +
                    "take the prefab's values instead.\n\nKept: where each bike sits, and whether it is " +
                    "switched off.\nLost: anything else tweaked on the instance rather than the prefab.\n\n" +
                    "The prefabs themselves are not touched.",
                    "Revert them", "Cancel"))
                return;

            Selection.objects = new Object[0];

            var log = new System.Text.StringBuilder();
            foreach (var go in roots)
            {
                var pos = go.transform.position;
                var rot = go.transform.rotation;
                bool on = go.activeSelf;
                int had = PrefabUtility.GetObjectOverrides(go).Count
                        + PrefabUtility.GetAddedComponents(go).Count
                        + PrefabUtility.GetAddedGameObjects(go).Count;

                PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);

                go.transform.SetPositionAndRotation(pos, rot);
                go.SetActive(on);
                log.Append("   ").Append(go.name).Append("  ").Append(had)
                   .Append(" override(s) discarded").Append(on ? "" : "  (left switched off)").Append('\n');
            }

            foreach (var review in Object.FindObjectsByType<RigReview>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!review.enabled) continue;
                Undo.RecordObject(review, "Reset review scene");
                review.enabled = false;
                log.Append("   RigReview disabled, it writes the camera transform every frame and fights the brain\n");
            }

            log.Append(Brain());

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            log.Append(saved ? "   scene saved\n" : "   SCENE NOT SAVED: save it by hand (Ctrl+S)\n");
            Debug.Log("Rig Lab: review scene reset.\n" + log);
        }

        static string Brain()
        {
            var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (cam == null) return "   NO CAMERA in the scene, the bike's virtual cameras have nothing to drive\n";

            var brain = cam.GetComponent<CinemachineBrain>();
            var note = "";
            if (brain == null)
            {
                brain = Undo.AddComponent<CinemachineBrain>(cam.gameObject);
                note += "   CinemachineBrain added to '" + cam.name + "'\n";
            }

            if (brain.DefaultBlend.Style != CinemachineBlendDefinition.Styles.Cut)
            {
                Undo.RecordObject(brain, "Reset review scene");
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                note += "   DefaultBlend set to Cut on '" + cam.name + "'\n";
            }

            EditorUtility.SetDirty(brain);
            return note.Length > 0 ? note : "   brain on '" + cam.name + "' already correct\n";
        }
    }
}
