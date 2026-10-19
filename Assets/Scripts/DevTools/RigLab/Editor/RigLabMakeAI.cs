using MotoSquid.AI;
using MotoSquid.Audio;
using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Combat;
using MotoSquid.Controls;
using MotoSquid.Race;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Cinemachine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabMakeAI
    {
        const string OutDir = "Assets/Prefabs/Characters/AI";

        [MenuItem("MotoSquid/Rig Lab/43. Make AI Variants Of Review Bikes", false, 430)]
        static void Make()
        {
            var found = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }
            var bikes = RigLabScope.Narrow(found);

            if (!EditorUtility.DisplayDialog("Rig Lab",
                    "Write an AI variant of " + bikes.Length + " bike(s) into\n" + OutDir +
                    "\n\n" + string.Join("\n", bikes.Select(b => "   " + b.name)) +
                    "\n\nSame BikeController, the player's input and camera swapped for a BikeAILogic brain. The scene bikes are not changed.", "Make them", "Cancel"))
                return;

            if (!AssetDatabase.IsValidFolder(OutDir))
                AssetDatabase.CreateFolder("Assets/Prefabs/Characters", "AI");

            var gaps = new List<string>();
            int made = 0;

            foreach (var bike in bikes)
            {
                var path = OutDir + "/" + bike.name + "_AI.prefab";
                var copy = Object.Instantiate(bike.gameObject);
                copy.name = bike.name + "_AI";
                try
                {
                    if (Convert(copy, gaps))
                    {
                        PrefabUtility.SaveAsPrefabAsset(copy, path);
                        made++;
                        Debug.Log("Rig Lab: " + copy.name + " -> " + path);
                    }
                }
                finally { Object.DestroyImmediate(copy); }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rig Lab: " + made + " of " + bikes.Length + " AI variant(s) written to " + OutDir +
                      RigLabScope.ScopeNote(bikes.Length, found.Length) +
                      (gaps.Count > 0 ? "\n\nNeeds attention:\n   " + string.Join("\n   ", gaps.Distinct()) : "") +
                      "\n\nBikeAILogic still needs a RouteGraph, which only a track scene has.");
        }

        internal static bool Convert(GameObject root, List<string> gaps)
        {
            var bike = root.GetComponent<BikeController>();
            if (bike == null) { gaps.Add(root.name + ": no BikeController to make an AI of"); return false; }

            Strip<BikeInput>(root);
            Strip<CombatInput>(root);
            StripCameraRig(root);
            Strip<CheckpointTracker>(root);
            Strip<NearMissFX>(root);
            Strip<BoostSystem>(root);
            // The loop reads the BoostSystem in its parents, so stripping one without the other
            // leaves every AI logging a null reference on Start
            Strip<BoostAudioLoop>(root);
            Strip<VoiceBarkController>(root);

            var logic = Ensure<BikeAILogic>(root);
            // A mask left at Nothing turns traffic avoidance and wall avoidance off while their toggles still read on
            if (logic.trafficLayerMask == 0) logic.trafficLayerMask = LayerMask.GetMask("Traffic");
            if (logic.barrierLayerMask == 0) logic.barrierLayerMask = LayerMask.GetMask("Barriers", "TurnBarrier");
            var boost = Ensure<AIBoostSystem>(root);
            var cai = Ensure<CombatAI>(root);

            var combat = root.GetComponentInChildren<CombatSystem>(true);
            if (combat != null && cai != null)
            {
                var so = new SerializedObject(cai);
                var p = so.FindProperty("combatSystem");
                if (p != null) { p.objectReferenceValue = combat; so.ApplyModifiedPropertiesWithoutUndo(); }
            }

            // RaceManager only wires the AI chain when the brain already knows its bike, so a miss is silent
            logic.controller    = bike;
            logic.bikeTransform = bike.bikeReferences != null && bike.bikeReferences.Rotator != null
                                ? bike.bikeReferences.Rotator : root.transform;
            boost.bikeController = bike;

            foreach (var rag in root.GetComponentsInChildren<RagdollActivator>(true))
            {
                if (rag.characterAnimator != null) continue;
                var switcher = root.GetComponentInChildren<RiderSwitch>(true);
                var rider = switcher != null ? switcher.ActiveRider : null;
                if (rider != null)
                    rag.characterAnimator = rider.GetComponent<Animator>()
                                            ?? rider.GetComponentInChildren<Animator>(true);
            }

            gaps.Add(root.name + "_AI: BikeAILogic needs a RouteGraph wiring in the track scene");
            return true;
        }

    // Stripping only the component left the GameObject and its CinemachineCameras behind, and with
    // no controller to deactivate them every AI put two extra vcams on the brain at the player's
    // priority, so the race could open inside an AI's head
    static void StripCameraRig(GameObject root)
    {
        foreach (var c in root.GetComponentsInChildren<CameraController>(true).ToArray())
            if (c != null) Object.DestroyImmediate(c.gameObject);

        foreach (var vcam in root.GetComponentsInChildren<CinemachineCamera>(true).ToArray())
            if (vcam != null) Object.DestroyImmediate(vcam.gameObject);
    }

        static void Strip<T>(GameObject root) where T : Component
        {
            foreach (var c in root.GetComponentsInChildren<T>(true).ToArray())
                if (c != null) Object.DestroyImmediate(c);
        }

        static T Ensure<T>(GameObject root) where T : Component
        {
            var existing = root.GetComponentInChildren<T>(true);
            return existing != null ? existing : root.AddComponent<T>();
        }
    }
}
