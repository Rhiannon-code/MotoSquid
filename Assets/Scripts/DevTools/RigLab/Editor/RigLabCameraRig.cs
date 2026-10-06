using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Combat;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabCameraRig
    {
        const string DonorPath = "Assets/Prefabs/Characters/Voodoo_Player.prefab";

        [MenuItem("Tools/Rig Lab/47. Wire AI Bike References (scene)", false, 470)]
        static void WireAI()
        {
            var foundAi = Object.FindObjectsByType<BikeAIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (foundAi.Length == 0) { Debug.LogWarning("Rig Lab: no AI bikes in the open scene."); return; }
            var ais = RigLabScope.Narrow(foundAi);

            int rag = 0, anim = 0;
            foreach (var ai in ais)
            {
                foreach (var r in ai.GetComponentsInChildren<RagdollActivator>(true))
                    if (r.bikeAIRhiannon == null) { r.bikeAIRhiannon = ai; EditorUtility.SetDirty(r); rag++; }

                foreach (var a in ai.GetComponentsInChildren<BikeAnimationController>(true))
                    if (a.bikeAIRhiannon == null) { a.bikeAIRhiannon = ai; EditorUtility.SetDirty(a); anim++; }
            }
            Debug.Log("Rig Lab: AI wiring, " + rag + " RagdollActivator(s), " + anim +
                      " BikeAnimationController(s), across " + ais.Length + " AI bike(s)." +
                      RigLabScope.ScopeNote(ais.Length, foundAi.Length));
        }

        [MenuItem("Tools/Rig Lab/46. Build Camera Rig + Fix Placements", false, 460)]
        static void Build()
        {
            var donor = AssetDatabase.LoadAssetAtPath<GameObject>(DonorPath);
            if (donor == null) { Debug.LogError("Rig Lab: donor not found at " + DonorPath); return; }

            var donorRig = FindDeep(donor.transform, "Camera Controller");
            if (donorRig == null) { Debug.LogError("Rig Lab: donor has no 'Camera Controller' subtree."); return; }

            var found = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }
            var bikes = RigLabScope.Narrow(found);

            var gaps = new List<string>();
            int rigs = 0, moved = 0;

            foreach (var bike in bikes)
            {
                rigs += BuildRig(bike, donorRig, gaps);
                moved += FixPlacements(bike, gaps);
            }

            Debug.Log("Rig Lab: " + rigs + " camera rig(s) built, " + moved + " component(s) moved, across " +
                      bikes.Length + " bike(s)." + RigLabScope.ScopeNote(bikes.Length, found.Length) +
                      (gaps.Count > 0 ? "\n\nNeeds attention:\n   " + string.Join("\n   ", gaps.Distinct()) : "\n   everything resolved."));
        }

        static int BuildRig(BikeController bike, Transform donorRig, List<string> gaps)
        {
            var rigRoot = FindDeep(bike.transform, "Camera Controller");
            foreach (var stray in bike.GetComponents<CameraController>())
                Object.DestroyImmediate(stray);

            if (rigRoot != null) return 0;

            var rig = Object.Instantiate(donorRig.gameObject, bike.transform);
            rig.name = "Camera Controller";
            Undo.RegisterCreatedObjectUndo(rig, "Build camera rig");
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;

            gaps.AddRange(RigLabFitCameras.AimCameras(bike, rig.transform));

            var cc = rig.GetComponent<CameraController>();
            if (cc != null)
            {
                var so = new SerializedObject(cc);
                var p = so.FindProperty("bikeController");
                if (p != null) p.objectReferenceValue = bike;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cc);
            }
            else gaps.Add(bike.name + ": copied rig has no CameraController");

            return 1;
        }

        static int FixPlacements(BikeController bike, List<string> gaps)
        {
            int n = 0;
            var audios = FindDeep(bike.transform, "Audios");
            if (audios == null) gaps.Add(bike.name + ": no 'Audios' object, audio left on the root");
            else
            {
                n += Move<BikeAudioController>(bike.transform, audios);
                n += Move<EngineAudio>(bike.transform, audios);
            }

            var art = bike.bikeReferences != null && bike.bikeReferences.BikeModel != null
                    ? bike.bikeReferences.BikeModel : null;
            if (art != null) n += Move<WheelVisualSpin>(bike.transform, art);

            var audio = bike.GetComponentInChildren<BikeAudioController>(true);
            foreach (var combat in bike.GetComponentsInChildren<CombatSystem>(true))
            { combat.audioController = audio; EditorUtility.SetDirty(combat); }

            var cam = bike.GetComponentInChildren<CameraController>(true);
            var engine = bike.GetComponentInChildren<EngineAudio>(true);
            if (engine != null) { engine.cameraController = cam; EditorUtility.SetDirty(engine); }

            foreach (var rag in bike.GetComponentsInChildren<RagdollActivator>(true))
            {
                if (rag.bikeController == null)
                {
                    rag.bikeController = bike;
                    EditorUtility.SetDirty(rag);
                    n++;
                }
            }

            return n;
        }

        static int Move<T>(Transform root, Transform target) where T : Component
        {
            var src = root.GetComponent<T>();
            if (src == null || target == null) return 0;
            if (target.GetComponent<T>() != null) { Object.DestroyImmediate(src); return 1; }

            var dst = Undo.AddComponent(target.gameObject, typeof(T));
            EditorUtility.CopySerialized(src, dst);
            Object.DestroyImmediate(src);
            EditorUtility.SetDirty(dst);
            return 1;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var hit = FindDeep(t.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
