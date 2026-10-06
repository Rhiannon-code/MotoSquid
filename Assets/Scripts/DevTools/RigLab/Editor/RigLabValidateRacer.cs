using MotoSquid.AI;
using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Combat;
using MotoSquid.Controls;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabValidateRacer
    {
        [MenuItem("Tools/Rig Lab/0b. Validate Racers (scene)", false, 2)]
        static void ValidateScene()
        {
            var roots = RigLabScope.BikeRoots().ToList();

            if (roots.Count == 0)
            {
                Debug.LogError("Rig Lab: no racers in the open scene. A racer is a GameObject carrying " +
                               "BikeController or BikeAIController, at any depth.");
                return;
            }
            Report(roots.Select(r => { var n = new List<string>(); return (r.name, Check(r, n), n); }).ToList(), "open scene");
        }

        [MenuItem("Tools/Rig Lab/0c. Validate Racer Prefabs", false, 3)]
        static void ValidatePrefabs()
        {
            var dirs = new[]
            {
                "Assets/MotoSquid/Resources/Racers",
                "Assets/Prefabs/Characters/AI",
                "Assets/Prefabs/Characters/Player",
            }.Where(AssetDatabase.IsValidFolder).ToArray();

            if (dirs.Length == 0) { Debug.LogWarning("Rig Lab: no racer prefab folders yet."); return; }

            var results = new List<(string, List<string>, List<string>)>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", dirs))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                var notes = new List<string>();
                try { results.Add((System.IO.Path.GetFileNameWithoutExtension(path), Check(root, notes), notes)); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Report(results, string.Join(" + ", dirs.Select(d => d.Split('/').Last())));
        }

        static void Report(List<(string name, List<string> problems, List<string> notes)> results, string where)
        {
            var sb = new System.Text.StringBuilder("RACER VALIDATION, " + where + "\n\n");
            int bad = 0;
            foreach (var (name, problems, notes) in results.OrderBy(r => r.name))
            {
                bool ok = problems.Count == 0;
                if (!ok) bad++;
                sb.Append(ok ? "  OK   " : "  FAIL ").Append(name).Append('\n');
                foreach (var x in problems) sb.Append("          ").Append(x).Append('\n');
                foreach (var x in notes) sb.Append("          note: ").Append(x).Append('\n');
            }
            sb.Append('\n').Append(results.Count - bad).Append(" of ").Append(results.Count).Append(" ready.");
            if (bad > 0) Debug.LogError(sb.ToString()); else Debug.Log(sb.ToString());
        }

        static List<string> Check(GameObject root, List<string> notes)
        {
            var p = new List<string>();
            var player = root.GetComponent<BikeController>();
            var ai = root.GetComponent<BikeAIController>();

            if (player == null && ai == null) { p.Add("no bike controller at all"); return p; }
            if (player != null && ai != null) p.Add("BOTH player and AI controllers, pick one");
            bool isAI = ai != null && player == null;

            Need<BikeAnimationController>(root, p, "the rider is never posed");
            Need<BikerRigReferences>(root, p, "no rig to pose it with");
            Need<BikeAnimationTargets>(root, p, "no pose targets to blend between");
            Need<WheelVisualSpin>(root, p, "the wheels never turn");
            Need<RagdollActivator>(root, p, "no ragdoll on a crash");
            Need<MeleeWeaponHolder>(root, p, "nothing can hold a weapon");
            Need<CombatSystem>(root, p, "no combat");

            if (isAI)
            {
                Reject<BikeInput>(root, p);
                Reject<CombatInput>(root, p);
                Reject<CameraController>(root, p);
                Need<BikeAILogic>(root, p, "the AI has no route logic");
            }
            else
            {
                Need<BikeInput>(root, p, "the player cannot steer it");
                Need<CameraController>(root, p, "nothing follows it");
                Reject<BikeAILogic>(root, p);
            }

            var audio = root.GetComponentInChildren<BikeAudioController>(true);
            if (audio == null) p.Add("no BikeAudioController, silent");
            else if (audio.gameObject.name != "Audios") p.Add("BikeAudioController is on '" + audio.gameObject.name + "', expected 'Audios'");

            var cam = root.GetComponentInChildren<CameraController>(true);
            if (cam != null && cam.gameObject.name != "Camera Controller")
                p.Add("CameraController is on '" + cam.gameObject.name + "', expected 'Camera Controller'");
            if (cam != null && (cam.cameras == null || cam.cameras.Length == 0))
                p.Add("CameraController has no cameras wired");

            var riders = RigLabRiders.AllOn(root);
            if (riders.Length == 0) p.Add("no rider found under bikeReferences.BikeModel");

            foreach (var anim in riders)
            {
                string who = anim.gameObject.name + ": ";

                if (isAI && anim.bikeAIRhiannon == null) p.Add(who + "bikeAIRhiannon unset, throws on Start");
                if (!isAI && anim.bikeControllerRhiannon == null) p.Add(who + "bikeControllerRhiannon unset");
                if (anim.hipTargetRig == null || anim.spineRootTargetRig == null) p.Add(who + "rig targets unset, run the rig builder");
                if (anim.hipIdleTarget == null || anim.hipNormalSpeedTarget == null || anim.hipHighSpeedTarget == null)
                    p.Add(who + "pose targets unset, the rider will not blend with speed");

                var builder = anim.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
                if (builder == null) p.Add(who + "no RigBuilder, its rig never evaluates");
                else
                {
                    if (builder.GetComponent<Animator>() == null)
                        p.Add(who + "RigBuilder with NO Animator on the same object, the rig is inert");

                    var layers = builder.layers;
                    if (layers == null || layers.Count == 0) p.Add(who + "RigBuilder has no rig layers");
                    else
                    {
                        var pose = layers.FirstOrDefault(l => l.rig != null && l.rig.name.StartsWith("Biker Rig"));
                        if (pose == null) p.Add(who + "RigBuilder has no Biker Rig layer");
                        else
                        {
                            if (!pose.active) p.Add(who + "its Biker Rig layer is inactive");
                            if (pose.rig.weight <= 0f) p.Add(who + "its Biker Rig weight is " + pose.rig.weight);
                            if (!pose.rig.transform.IsChildOf(anim.transform))
                                p.Add(who + "its Biker Rig lives under another rider");
                            if (!pose.rig.gameObject.activeInHierarchy && anim.gameObject.activeInHierarchy)
                                p.Add(who + "its Biker Rig object is disabled");
                        }
                    }
                }

                var set = anim.hipIdleTarget;
                while (set != null && !set.name.StartsWith("Biker Animation Targets")) set = set.parent;
                if (set != null && (set.localPosition != Vector3.zero || set.localRotation != Quaternion.identity))
                    p.Add(who + "its target set '" + set.name + "' is offset by " + set.localPosition.ToString("F3") +
                          ", the whole rider sits that far out while every target inside still reads correct");

                var refs = anim.GetComponentInChildren<BikerRigReferences>(true);
                if (refs == null) p.Add(who + "no BikerRigReferences");
                else if (refs.hipRig == null) p.Add(who + "hipRig unset, the hips are never placed");
                else if (refs.hipRig.data.constrainedObject == null)
                    p.Add(who + "hipRig constrains nothing, the rider floats in its imported pose");
            }

            foreach (var rag in root.GetComponentsInChildren<RagdollActivator>(true))
                if (rag.bikeController == null && rag.bikeAIRhiannon == null)
                    p.Add("RagdollActivator has neither controller, NullReference every frame");

            foreach (var combat in root.GetComponentsInChildren<CombatSystem>(true))
            {
                if (combat.rigDriver == null) p.Add("CombatSystem.rigDriver unset, attacks animate nothing (run 13)");
                if (combat.weaponHolder == null) p.Add("CombatSystem.weaponHolder unset");
                if (combat.bikeRigidbody == null) p.Add("CombatSystem.bikeRigidbody unset, no knockback");
            }

            var holder = root.GetComponentInChildren<MeleeWeaponHolder>(true);
            if (holder != null && holder.startingWeapon == null) notes.Add("no starting weapon (the matrix sets this per character)");

            if (player != null && player.bikeInput != null)
            {
                var i = player.bikeInput;
                if (i.Accelerate != 0f || i.Wheelie != 0f || i.SteeringLeft != 0f)
                    p.Add("stale bikeInput (accel " + i.Accelerate + ", wheelie " + i.Wheelie + ") , run 38");
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                { p.Add("missing script(s) on '" + t.name + "' - prefab cannot be saved (run 36)"); break; }

            return p;
        }

        static void Need<T>(GameObject root, List<string> p, string why) where T : Component
        {
            if (root.GetComponentInChildren<T>(true) == null) p.Add("no " + typeof(T).Name + " , " + why);
        }

        static void Reject<T>(GameObject root, List<string> p) where T : Component
        {
            if (root.GetComponentInChildren<T>(true) != null) p.Add(typeof(T).Name + " should not be on this role");
        }
    }
}
