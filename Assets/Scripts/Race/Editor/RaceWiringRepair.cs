using MotoSquid.Bike;
using MotoSquid.Cameras;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotoSquid.Race
{
    public static class RaceWiringRepair
    {
        const string Tag = "[WiringRepair]";
        static readonly string[] CameraNames     = { "ThirdPerson", "FirstPerson" };
        static readonly string[] LookBehindNames = { "ThirdPerson_LookBehind", "FirstPerson_LookBehind" };
        static readonly bool[]   IsFirstPerson   = { false, true };

        static readonly string[] PrefabFolders =
        {
            "Assets/Prefabs",
            "Assets/Prefabs/Characters",
        };

        [MenuItem("MotoSquid/Characters/Repair Camera Wiring (Donors + All Characters)")]
        public static void RepairCameras()
        {
            if (!EditorUtility.DisplayDialog("Repair camera wiring",
                    "Assigns cameras[] and lookBehindCameras[] on every prefab carrying a CameraController, " +
                    "donors first.\n\nphysicalCamera is left alone, it is a scene object, not part of the prefab.\n\n" +
                    "Commit first. Continue?", "Repair", "Cancel"))
            {
                return;
            }

            var paths = PrefabFolders
                .Where(AssetDatabase.IsValidFolder)
                .SelectMany(f => AssetDatabase.FindAssets("t:Prefab", new[] { f }))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderByDescending(p => p.EndsWith("/Player1.prefab") || p.EndsWith("/Voodoo.prefab"))
                .ToList();

            int repaired = 0, clean = 0;
            foreach (var path in paths)
            {
                var result = RepairPrefab(path);
                if (result == Result.Repaired) repaired++;
                else if (result == Result.AlreadyFine) clean++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Camera wiring: repaired {repaired} prefab(s), {clean} already correct. " +
                      "Rebuild characters so the built prefabs pick up the donor fix, then assign " +
                      "physicalCamera on each scene instance.");
        }

        enum Result { NoController, AlreadyFine, Repaired, Failed }

        static Result RepairPrefab(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<CameraController>(true) == null)
                return Result.NoController;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var controller = root.GetComponentInChildren<CameraController>(true);
                if (controller == null) return Result.NoController;

                bool changed = false;
                changed |= Assign(root, controller, CameraNames, nameof(CameraController.cameras), path);
                changed |= Assign(root, controller, LookBehindNames, nameof(CameraController.lookBehindCameras), path);

                if (!changed) return Result.AlreadyFine;

                EditorUtility.SetDirty(controller);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return Result.Repaired;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool Assign(GameObject root, CameraController controller, string[] names,
            string field, string path)
        {
            var current = field == nameof(CameraController.cameras)
                ? controller.cameras
                : controller.lookBehindCameras;

            var wanted = names.Select(n =>
            {
                var t = FindDeep(root.transform, n);
                return t != null ? t.GetComponent<CinemachineCamera>() : null;
            }).ToArray();

            if (current != null && current.Length == wanted.Length &&
                !current.Where((c, i) => c != wanted[i]).Any()) return false;

            var found = wanted;
            var missing = names.Where((n, i) => found[i] == null).ToList();

            if (missing.Count == names.Length && field == nameof(CameraController.cameras))
            {
                var any = root.GetComponentsInChildren<CinemachineCamera>(true);
                if (any.Length >= names.Length)
                {
                    found = any.Take(names.Length).ToArray();
                    missing.Clear();
                    Debug.LogWarning($"{Tag} {System.IO.Path.GetFileName(path)}: no {string.Join("/", names)}, " +
                                     $"used '{found[0].name}' and '{found[1].name}' instead. Rename them if the " +
                                     "starting view is wrong; index 0 is the one you start in.");
                }
            }

            if (missing.Count > 0)
            {
                Debug.LogWarning($"{Tag} {System.IO.Path.GetFileName(path)}: {field} left alone, " +
                                 $"no CinemachineCamera named {string.Join(", ", missing)}.");
                return false;
            }

            if (field == nameof(CameraController.cameras))
            {
                controller.cameras = found;

                if (controller.cameraIsFirstPerson == null ||
                    controller.cameraIsFirstPerson.Length != IsFirstPerson.Length)
                {
                    controller.cameraIsFirstPerson = (bool[])IsFirstPerson.Clone();
                    Debug.Log($"{Tag} {System.IO.Path.GetFileName(path)}: cameraIsFirstPerson -> [false, true].");
                }
            }
            else controller.lookBehindCameras = found;

            Debug.Log($"{Tag} {System.IO.Path.GetFileName(path)}: {field} -> {string.Join(", ", names)}.");
            return true;
        }

        [MenuItem("MotoSquid/Characters/Repair Race Wiring (Open Scene)")]
        public static void RepairScene()
        {
            var player = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include,
                    FindObjectsSortMode.InstanceID)
                .FirstOrDefault(b => b.GetComponentInChildren<CameraController>(true) != null);

            if (player == null)
            {
                Debug.LogError($"{Tag} No player bike in the open scene (a BikeController with a CameraController under it).");
                return;
            }

            int fixes = 0;
            var playerCam = FindDeep(player.transform, CameraNames[0]);

            foreach (var start in Object.FindObjectsByType<StartGameManager>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var so = new SerializedObject(start);
                fixes += SetIfNull(so, "playerObject", player.gameObject);
                if (playerCam != null)
                    fixes += SetIfNull(so, "playerObjectCam", playerCam.GetComponent<CinemachineCamera>());
                so.ApplyModifiedProperties();
            }

            foreach (var race in Object.FindObjectsByType<RaceManager>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var so = new SerializedObject(race);
                fixes += SetIfNull(so, "playerTransform", player.transform);

                var ai = so.FindProperty("aiOpponents");
                if (ai != null && ai.isArray && ai.arraySize > 0 &&
                    Enumerable.Range(0, ai.arraySize)
                        .All(i => ai.GetArrayElementAtIndex(i).objectReferenceValue == null))
                {
                    Debug.Log($"{Tag} {race.name}: cleared {ai.arraySize} dead aiOpponents slot(s), " +
                              "AI will spawn from aiPrefab instead.");
                    ai.arraySize = 0;
                    fixes++;
                }

                so.ApplyModifiedProperties();
            }

            if (fixes > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"{Tag} Scene wiring: {fixes} field(s) repaired against '{player.name}'. " +
                      (fixes > 0 ? "Save the scene." : "Nothing was null."));
        }

        static int SetIfNull(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p == null || p.objectReferenceValue != null || value == null) return 0;
            p.objectReferenceValue = value;
            Debug.Log($"{Tag} {so.targetObject.name}.{field} -> {value.name}");
            return 1;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = FindDeep(c, name);
                if (f != null) return f;
            }

            return null;
        }
    }
}
