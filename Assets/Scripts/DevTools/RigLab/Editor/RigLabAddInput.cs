using MotoSquid.Bike;
using MotoSquid.Controls;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabAddInput
    {
        const string ActionsPath = "Assets/MotoSquid/Data/Controls/BikeInputActions.inputactions";

        [MenuItem("Tools/Rig Lab/39. Add Bike Input To Review Bikes", false, 390)]
        static void Add()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null)
            {
                Debug.LogError("Rig Lab: input actions not found at " + ActionsPath);
                return;
            }
            if (actions.FindActionMap("Bike") == null)
            {
                Debug.LogError("Rig Lab: " + ActionsPath + " has no 'Bike' action map, BikeInput asks " +
                               "for it by name and throws without it.");
                return;
            }

            var found = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }
            var bikes = RigLabScope.Narrow(found);

            int added = 0, rewired = 0;
            foreach (var bike in bikes)
            {
                var input = bike.GetComponent<BikeInput>();
                if (input == null) { input = Undo.AddComponent<BikeInput>(bike.gameObject); added++; }
                else rewired++;

                Undo.RecordObject(input, "Wire bike input");
                var so = new SerializedObject(input);
                so.FindProperty("inputActions").objectReferenceValue = actions;
                so.FindProperty("playerDeviceIndex").intValue = -1;
                so.FindProperty("lockToKeyboard").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                input.bikeControllerRhiannon = bike;
                input.SetFullControl();

                EditorUtility.SetDirty(input);
            }

            Debug.Log("Rig Lab: bike input, " + added + " added, " + rewired + " rewired, across " +
                      bikes.Length + " bike(s)." + RigLabScope.ScopeNote(bikes.Length, found.Length) + "\n" +
                      "   W accelerate, S reverse, A/D steer, Space brake, LeftShift wheelie\n" +
                      "   accelOnly cleared: the review scene has no countdown to clear it.\n" +
                      "Press Play and ride: the pose should shift idle -> normal -> high with speed.");
        }
    }
}
