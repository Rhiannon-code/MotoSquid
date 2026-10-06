using MotoSquid.Bike;
using MotoSquid.Cameras;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.Race
{
    public static class MakePlayerSwap
    {
        const string Tag = "[PlayerSwap]";

        [MenuItem("MotoSquid/Characters/Make Selected The Player (Open Scene)")]
        public static void MakeSelected()
        {
            var next = Selection.activeGameObject;
            if (next == null)
            {
                Debug.LogError($"{Tag} Select the bike you want to race with.");
                return;
            }

            if (next.GetComponentInChildren<BikeController>(true) == null)
            {
                Debug.LogError($"{Tag} '{next.name}' has no BikeController, it is not a rideable bike.");
                return;
            }

            var starts = Object.FindObjectsByType<StartGameManager>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var races = Object.FindObjectsByType<RaceManager>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (starts.Length == 0 && races.Length == 0)
            {
                Debug.LogError($"{Tag} no StartGameManager or RaceManager in the open scene.");
                return;
            }

            var previous = starts.Select(s => new SerializedObject(s).FindProperty("playerObject"))
                .Where(p => p != null)
                .Select(p => p.objectReferenceValue as GameObject)
                .FirstOrDefault(g => g != null && g != next);

            if (previous != null)
            {
                next.tag = previous.tag;
                SetLayerRecursively(next, previous.layer);
                Debug.Log($"{Tag} took tag '{previous.tag}' and layer {previous.layer} from '{previous.name}'.");
            }

            var cam = FindCamera(next);
            var controller = next.GetComponentInChildren<CameraController>(true);

            foreach (var start in starts)
            {
                var so = new SerializedObject(start);
                Force(so, "playerObject", next);
                if (cam != null) Force(so, "playerObjectCam", cam);
                if (controller != null) Force(so, "p1CameraController", controller);
                so.ApplyModifiedProperties();
            }

            foreach (var race in races)
            {
                var so = new SerializedObject(race);
                Force(so, "playerTransform", next.transform);
                so.ApplyModifiedProperties();
            }

            if (controller != null)
            {
                var brains = Object.FindObjectsByType<CinemachineBrain>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (brains.Length == 1)
                {
                    var so = new SerializedObject(controller);
                    if (Force(so, "physicalCamera", brains[0].GetComponent<Camera>()) > 0)
                        Debug.Log($"{Tag} physicalCamera -> '{brains[0].name}'.");
                    so.ApplyModifiedProperties();
                }
                else
                {
                    Debug.LogWarning($"{Tag} {brains.Length} CinemachineBrain(s) in the scene, " +
                                     "physicalCamera left for you to set by hand.");
                }
            }

            if (previous != null)
            {
                Undo.RecordObject(previous, "Stand down old player");
                previous.SetActive(false);
                Debug.Log($"{Tag} disabled the previous player '{previous.name}'.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"{Tag} '{next.name}' is now the player. Save the scene, then press Play.");
        }

        static CinemachineCamera FindCamera(GameObject root)
        {
            var cams = root.GetComponentsInChildren<CinemachineCamera>(true);
            return cams.FirstOrDefault(c => c.name == "ThirdPerson") ?? cams.FirstOrDefault();
        }

        static int Force(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p == null || value == null) return 0;
            if (p.objectReferenceValue == value) return 0;
            p.objectReferenceValue = value;
            Debug.Log($"{Tag} {so.targetObject.name}.{field} -> {value.name}");
            return 1;
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursively(c.gameObject, layer);
        }
    }
}
