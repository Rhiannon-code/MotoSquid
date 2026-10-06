using MotoSquid.Bike;
using MotoSquid.Controls;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabZeroInput
    {
        [MenuItem("Tools/Rig Lab/38. Zero Stale Bike Input (scene)", false, 380)]
        static void ZeroScene()
        {
            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }

            int zeroed = 0, driven = 0;
            foreach (var bike in bikes)
            {
                if (bike.GetComponentInChildren<BikeInput>(true) != null) { driven++; continue; }
                if (bike.bikeInput == null) continue;

                var i = bike.bikeInput;
                if (i.Accelerate == 0f && i.Reverse == 0f && i.HandBrake == 0f &&
                    i.SteeringLeft == 0f && i.SteeringRight == 0f && i.Wheelie == 0f) continue;

                Undo.RecordObject(bike, "Zero stale bike input");
                Debug.Log("Rig Lab: " + bike.name + " had accel " + i.Accelerate + ", wheelie " + i.Wheelie +
                          ", steer " + i.SteeringLeft + "/" + i.SteeringRight + " , zeroed.", bike);
                i.Accelerate = i.Reverse = i.HandBrake = i.SteeringLeft = i.SteeringRight = i.Wheelie = 0f;
                EditorUtility.SetDirty(bike);
                zeroed++;
            }

            Debug.Log("Rig Lab: zeroed stale input on " + zeroed + " bike(s); " + driven +
                      " have a BikeInput driving them and were left alone.");
        }
    }
}
