using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Controls;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabStripSystems
    {
        [MenuItem("Tools/Rig Lab/35. Strip Game Systems From Review Bikes", false, 350)]
        static void Strip()
        {
            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }

            if (!EditorUtility.DisplayDialog("Rig Lab",
                    "Remove CameraController and BikeInput from " + bikes.Length + " bike(s)?\n\n" +
                    "The rig, the rider and the bike's own physics are left alone. Add the systems " +
                    "back one at a time once each is confirmed.",
                    "Strip them", "Cancel"))
                return;

            int cams = 0, inputs = 0;
            foreach (var bike in bikes)
            {
                foreach (var c in bike.GetComponentsInChildren<CameraController>(true).ToArray())
                { Undo.DestroyObjectImmediate(c); cams++; }

                foreach (var c in bike.GetComponentsInChildren<BikeInput>(true).ToArray())
                { Undo.DestroyObjectImmediate(c); inputs++; }
            }

            Debug.Log("Rig Lab: stripped " + cams + " CameraController(s) and " + inputs +
                      " BikeInput(s) from " + bikes.Length + " bike(s).\n" +
                      "The rig is untouched. Press Play, the rider should still pose.");
        }
    }
}
