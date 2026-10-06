using MotoSquid.Bike;
using MotoSquid.Combat;
using MotoSquid.Race;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabAddSystems
    {
        const string ActionsPath = "Assets/MotoSquid/Data/Controls/BikeInputActions.inputactions";

        [MenuItem("Tools/Rig Lab/40. Add Combat + Reset + Boost To Review Bikes", false, 400)]
        static void Add()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null) { Debug.LogError("Rig Lab: input actions not found at " + ActionsPath); return; }

            var found = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }
            var bikes = RigLabScope.Narrow(found);

            var gaps = new List<string>();
            int combat = 0, reset = 0, boost = 0;

            foreach (var bike in bikes)
            {
                var rider = RigLabRiders.ActiveOn(bike);
                var cs = bike.GetComponentInChildren<CombatSystem>(true)
                      ?? Undo.AddComponent<CombatSystem>(bike.gameObject);
                Undo.RecordObject(cs, "Add combat");
                cs.bikeRigidbody = bike.bikeReferences != null ? bike.bikeReferences.BikeRb : null;
                if (rider != null)
                {
                    cs.riderAnimator = rider.GetComponent<Animator>();
                    cs.rigDriver = rider.GetComponent<CombatRigDriver>();
                }
                cs.audioController = bike.GetComponentInChildren<BikeAudioController>(true);
                cs.ragdollActivator = bike.GetComponentInChildren<RagdollActivator>(true);
                cs.weaponHolder = bike.GetComponentInChildren<MeleeWeaponHolder>(true);
                EditorUtility.SetDirty(cs);
                combat++;

                if (cs.rigDriver == null)
                    gaps.Add(bike.name + ": no CombatRigDriver on the rider - run 13, or Q/E will animate nothing");
                if (cs.bikeRigidbody == null)
                    gaps.Add(bike.name + ": bikeReferences.BikeRb is empty - knockback will not apply");

                var ci = bike.GetComponentInChildren<CombatInput>(true)
                      ?? Undo.AddComponent<CombatInput>(bike.gameObject);
                Undo.RecordObject(ci, "Add combat");
                ci.combatSystem = cs;
                Wire(ci, actions);

                var rb = bike.GetComponentInChildren<ResetBike>(true)
                      ?? Undo.AddComponent<ResetBike>(bike.gameObject);
                Undo.RecordObject(rb, "Add reset");
                rb.bikeController = bike;
                Wire(rb, actions);
                reset++;

                var bs = bike.GetComponentInChildren<BoostSystem>(true)
                      ?? Undo.AddComponent<BoostSystem>(bike.gameObject);
                Undo.RecordObject(bs, "Add boost");
                bs.bikeController = bike;
                Wire(bs, actions);
                boost++;

                if (bs.postProcessVolume == null)
                    gaps.Add(bike.name + ": BoostSystem has no post process volume, boost works, no visual");
            }

            Debug.Log("Rig Lab: combat on " + combat + ", reset on " + reset + ", boost on " + boost +
                      " bike(s)." + RigLabScope.ScopeNote(bikes.Length, found.Length) +
                      "\n   Q / E melee, Z / C kick, CapsLock boost, R reset" +
                      (gaps.Count > 0 ? "\n\nUnwired:\n   " + string.Join("\n   ", gaps.Distinct()) : "\n   everything wired."));
        }

        /// The three components all hide inputActions behind [SerializeField], so it goes in by name.
        static void Wire(Component c, InputActionAsset actions)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty("inputActions");
            if (p != null) p.objectReferenceValue = actions;
            var d = so.FindProperty("playerDeviceIndex");
            if (d != null) d.intValue = -1;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c);
        }
    }
}
