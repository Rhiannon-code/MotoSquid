using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Race;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabCompletePlayer
    {
        const string DonorPath = "Assets/Prefabs/Player1.prefab";

        [MenuItem("Tools/Rig Lab/42. Complete Player Bikes (from donor)", false, 420)]
        static void Complete()
        {
            var donor = AssetDatabase.LoadAssetAtPath<GameObject>(DonorPath);
            if (donor == null) { Debug.LogError("Rig Lab: donor not found at " + DonorPath); return; }

            var found = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }
            var bikes = RigLabScope.Narrow(found);

            var gaps = new List<string>();
            int added = 0;

            foreach (var bike in bikes)
            {
                added += Copy<BikeAudioController>(donor, bike, gaps);
                added += Copy<EngineAudio>(donor, bike, gaps);
                added += Copy<SlipstreamSystem>(donor, bike, gaps);
                added += Copy<CameraController>(donor, bike, gaps);
                added += Copy<CheckpointTracker>(donor, bike, gaps);
                added += Copy<NearMissFX>(donor, bike, gaps);

                Repoint(bike, gaps);

                if (bike.bikeSuspension != null && bike.bikeSuspension.MaxDroop > 0.13f)
                {
                    Undo.RecordObject(bike, "Set droop");
                    bike.bikeSuspension.MaxDroop = 0.12f;
                    EditorUtility.SetDirty(bike);
                }
            }

            Debug.Log("Rig Lab: player set, " + added + " component(s) added across " + bikes.Length + " bike(s)." +
                      RigLabScope.ScopeNote(bikes.Length, found.Length) +
                      (gaps.Count > 0
                        ? "\n\nNeeds attention:\n   " + string.Join("\n   ", gaps.Distinct())
                        : "\n   everything resolved."));
        }

        static int Copy<T>(GameObject donor, BikeController bike, List<string> gaps) where T : Component
        {
            if (bike.GetComponentInChildren<T>(true) != null) return 0;

            var src = donor.GetComponentInChildren<T>(true);
            if (src == null)
            {
                gaps.Add("donor has no " + typeof(T).Name + " to copy from");
                return 0;
            }

            var dst = Undo.AddComponent(bike.gameObject, typeof(T));
            EditorUtility.CopySerialized(src, dst);
            EditorUtility.SetDirty(dst);
            return 1;
        }

        static void Repoint(BikeController bike, List<string> gaps)
        {
            var cam = bike.GetComponentInChildren<CameraController>(true);
            var boost = bike.GetComponentInChildren<BoostSystem>(true);

            var engine = bike.GetComponentInChildren<EngineAudio>(true);
            if (engine != null) { engine.cameraController = cam; EditorUtility.SetDirty(engine); }

            var near = bike.GetComponentInChildren<NearMissFX>(true);
            if (near != null)
            {
                near.cameraController = cam;
                near.boostSystem = boost;
                EditorUtility.SetDirty(near);
                if (near.postProcessVolume == null)
                    gaps.Add(bike.name + ": NearMissFX has no post process volume (scene asset, wire by hand)");
            }

            if (cam != null)
            {
                var so = new SerializedObject(cam);
                var p = so.FindProperty("bikeController");
                if (p != null) { p.objectReferenceValue = bike; so.ApplyModifiedPropertiesWithoutUndo(); }
                EditorUtility.SetDirty(cam);
            }

            var slip = bike.GetComponentInChildren<SlipstreamSystem>(true);
            if (slip != null)
            {
                var trails = bike.GetComponentsInChildren<TrailRenderer>(true);
                if (trails.Length > 0) { slip.trails = trails; EditorUtility.SetDirty(slip); }
                else gaps.Add(bike.name + ": SlipstreamSystem found no TrailRenderers on this bike");
            }

            var track = bike.GetComponentInChildren<CheckpointTracker>(true);
            if (track != null && track.routeGraph == null)
                gaps.Add(bike.name + ": CheckpointTracker has no RouteGraph, needs a track scene");
        }
    }
}
