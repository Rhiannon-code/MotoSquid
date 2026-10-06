using MotoSquid.Bike;
using MotoSquid.Combat;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabCombatRig
    {
        static readonly HumanBodyBones[] Driven =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        };

        [MenuItem("Tools/Rig Lab/13. Build Combat Rig Layer (all bikes)", false, 160)]
        static void Build()
        {
            var bikes = RigLabScope.Bikes(true);
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in " + RigLabScope.Where() + "."); return; }

            RigLabRiderTargets.Split();

            var tuning = Tuning();
            var grips = Grips();
            int built = 0;
            foreach (var bike in bikes) if (BuildFor(bike, tuning, grips)) built++;

            RigLabScope.MarkDirty();
            Debug.Log("Rig Lab: combat rig built on " + built + " bike(s). Run '0. Check Everything' to confirm.");
        }

        static bool BuildFor(BikeController bike, CombatTuning tuning,
                             WeaponGrips gripsAsset)
        {
            var riders = RigLabRiders.AllOn(bike);
            if (riders.Length == 0) return Fail(bike, "has no rider");

            bool all = true;
            foreach (var r in riders) if (!BuildForRider(bike, r, tuning, gripsAsset)) all = false;

            var sw = bike.GetComponent<RiderSwitch>()
                  ?? Undo.AddComponent<RiderSwitch>(bike.gameObject);
            Undo.RecordObject(sw, "Build combat rig");
            sw.riders = riders.Select(r => r.gameObject).ToArray();
            EditorUtility.SetDirty(sw);

            return all;
        }

        static bool BuildForRider(BikeController bike, BikeAnimationController ctrl,
                                  CombatTuning tuning, WeaponGrips gripsAsset)
        {

            var anim = ctrl.GetComponent<Animator>();
            var rigBuilder = ctrl.GetComponent<RigBuilder>();
            if (anim == null || rigBuilder == null) return Fail(bike, "rider has no Animator / RigBuilder");
            if (!anim.isHuman) return Fail(bike, "rider is not Humanoid");

            var rideRefs = ctrl.GetComponentInChildren<BikerRigReferences>(true);
            if (rideRefs == null) return Fail(bike, "rider has no BikerRigReferences");

            RestoreRide(rideRefs);

            var rigGo = Child(ctrl.transform, "Combat Rig");
            var rig = rigGo.GetComponent<Rig>() ?? Undo.AddComponent<Rig>(rigGo.gameObject);
            rig.weight = 0f;

            if (!rigBuilder.layers.Exists(l => l.rig == rig))
            {
                Undo.RecordObject(rigBuilder, "Build combat rig");
                rigBuilder.layers.Add(new RigLayer(rig, true));
                EditorUtility.SetDirty(rigBuilder);
            }

            var refs = rigGo.GetComponent<CombatRigReferences>()
                    ?? Undo.AddComponent<CombatRigReferences>(rigGo.gameObject);
            Undo.RecordObject(refs, "Build combat rig");
            refs.rig = rig;

            var poseGo = Child(rigGo, "Pose Overrides");
            var list = new List<CombatRigReferences.BoneOverride>();
            foreach (var b in Driven)
            {
                var bone = anim.GetBoneTransform(b);
                if (bone == null) continue;
                list.Add(MakeOverride(poseGo, ctrl.transform, bone, b));
            }
            refs.overrides = list.ToArray();

            var anchorGo = Child(rigGo, "Anchors");
            refs.leftHand = MakeIK(anchorGo, "Left Hand", anim, HumanBodyBones.LeftUpperArm,
                                   HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                                   rideRefs.leftHandTarget, rideRefs.leftHandHint);
            refs.rightHand = MakeIK(anchorGo, "Right Hand", anim, HumanBodyBones.RightUpperArm,
                                    HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                                    rideRefs.rightHandTarget, rideRefs.rightHandHint);
            refs.leftFoot = MakeIK(anchorGo, "Left Foot", anim, HumanBodyBones.LeftUpperLeg,
                                   HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                                   rideRefs.leftLegTarget, rideRefs.leftLegHint);
            refs.rightFoot = MakeIK(anchorGo, "Right Foot", anim, HumanBodyBones.RightUpperLeg,
                                    HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                                    rideRefs.rightLegTarget, rideRefs.rightLegHint);

            Order(rigGo, "Pose Overrides", "Anchors");
            EditorUtility.SetDirty(refs);

            var driver = ctrl.GetComponent<CombatRigDriver>()
                      ?? Undo.AddComponent<CombatRigDriver>(ctrl.gameObject);
            Undo.RecordObject(driver, "Build combat rig");

            foreach (var combat in bike.GetComponentsInChildren<CombatSystem>(true))
            {
                Undo.RecordObject(combat, "Build combat rig");
                combat.rigDriver = driver;
                EditorUtility.SetDirty(combat);
            }

            driver.rideRefs = rideRefs;
            driver.combatRefs = refs;
            driver.tuning = tuning;
            Fill(driver.meleeLeft, null, "AS_Long_Weapon_Hold", "AS_Long_Weapon_Left",
                 CombatRigDriver.Limb.LeftHand);
            Fill(driver.meleeRight, null, "AS_Long_Weapon_Hold", "AS_Long_Weapon_Right",
                 CombatRigDriver.Limb.LeftHand);
            Fill(driver.kickLeft, "AS_Kick_Left_Start", "AS_Kick_Left_Loop", "AS_Kick_Left_End",
                 CombatRigDriver.Limb.LeftFoot);
            Fill(driver.kickRight, "AS_Kick_Right_Start", "AS_Kick_Right_Loop", "AS_Kick_Right_End",
                 CombatRigDriver.Limb.RightFoot);

            Fill(driver.hitFront, null, null, "AS_Get_Hit_Front",
                 CombatRigDriver.Limb.LeftHand, CombatRigDriver.Body.Whole);
            Fill(driver.hitBack, null, null, "AS_Get_Hit_Back",
                 CombatRigDriver.Limb.LeftHand, CombatRigDriver.Body.Whole);
            Fill(driver.hitLeft, null, null, "AS_Get_Hit_Left",
                 CombatRigDriver.Limb.LeftHand, CombatRigDriver.Body.Whole);
            Fill(driver.hitRight, null, null, "AS_Get_Hit_Right",
                 CombatRigDriver.Limb.LeftHand, CombatRigDriver.Body.Whole);

            Fill(driver.lookBackLeft, "AS_Look_Back_Left_Start", "AS_Look_Back_Left_Loop", "AS_Look_Back_Left_End",
                 CombatRigDriver.Limb.LeftHand, CombatRigDriver.Body.UpperOnly);
            Fill(driver.lookBackRight, "AS_Look_Back_Right_Start", "AS_Look_Back_Right_Loop", "AS_Look_Back_Right_End",
                 CombatRigDriver.Limb.LeftHand, CombatRigDriver.Body.UpperOnly);
            EditorUtility.SetDirty(driver);

            var weapon = ctrl.GetComponent<CombatWeapon>()
                      ?? Undo.AddComponent<CombatWeapon>(ctrl.gameObject);
            Undo.RecordObject(weapon, "Build combat rig");
            weapon.driver = driver;
            weapon.grips = gripsAsset;
            weapon.hand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            if (weapon.model == null) weapon.model = WeaponFor(ctrl.name, gripsAsset);
            EditorUtility.SetDirty(weapon);

            var splay = ctrl.GetComponent<RiderLeanSplay>()
                     ?? Undo.AddComponent<RiderLeanSplay>(ctrl.gameObject);
            Undo.RecordObject(splay, "Build combat rig");
            splay.controller = ctrl;
            splay.rig = rideRefs;
            EditorUtility.SetDirty(splay);

            return true;
        }

        static readonly string[] Racers = { "Voodoo", "Bliss", "Mace", "Slick", "Soul", "Torq" };

        static GameObject WeaponFor(string riderName, WeaponGrips grips)
        {
            if (grips == null || grips.grips.Length == 0) return null;

            foreach (var racer in Racers)
            {
                if (riderName.IndexOf(racer, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (var g in grips.grips)
                    if (g.model != null &&
                        g.model.name.IndexOf(racer, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return g.model;
            }
            return grips.grips[0].model;
        }

        static WeaponGrips Grips()
        {
            const string path = "Assets/MotoSquid/Data/Combat/WeaponGrips.asset";
            var a = AssetDatabase.LoadAssetAtPath<WeaponGrips>(path);
            bool made = a == null;
            if (made)
            {
                a = ScriptableObject.CreateInstance<WeaponGrips>();
                AssetDatabase.CreateAsset(a, path);
            }

            var have = new HashSet<GameObject>();
            foreach (var g in a.grips) if (g.model != null) have.Add(g.model);

            var list = new List<WeaponGrips.Grip>(a.grips);
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models/Weapons" }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go == null || have.Contains(go)) continue;
                list.Add(new WeaponGrips.Grip { model = go });
            }

            if (list.Count != a.grips.Length)
            {
                a.grips = list.ToArray();
                EditorUtility.SetDirty(a);
                AssetDatabase.SaveAssets();
                Debug.Log("Rig Lab: " + path + " now lists " + a.grips.Length +
                          " weapon(s). Nudge each grip there while Play runs.", a);
            }
            return a;
        }

        static void RestoreRide(BikerRigReferences r)
        {
            var rig = r.GetComponent<Rig>();
            if (rig != null) { Undo.RecordObject(rig, "Build combat rig"); rig.weight = 1f; EditorUtility.SetDirty(rig); }

            foreach (var c in new Component[] { r.hipRig, r.spineRootRig, r.spineTipRig, r.headRig,
                                                r.LeftHandRig, r.RightHandRig, r.LeftLegRig, r.RightLegRig })
            {
                var con = c as IRigConstraint;
                if (con == null) continue;
                Undo.RecordObject(c, "Build combat rig");
                con.weight = 1f;
                EditorUtility.SetDirty(c);
            }
        }

        static CombatRigReferences.BoneOverride MakeOverride(Transform parent, Transform root,
                                                                     Transform bone, HumanBodyBones id)
        {
            var go = Child(parent, id.ToString());

            var stale = go.Find("Source");
            if (stale != null) Undo.DestroyObjectImmediate(stale.gameObject);

            var c = go.GetComponent<OverrideTransform>() ?? Undo.AddComponent<OverrideTransform>(go.gameObject);
            c.data.constrainedObject = bone;
            c.data.sourceObject = null;
            c.data.space = OverrideTransformData.Space.Local;
            c.data.position = Vector3.zero;
            c.data.rotation = bone.localRotation.eulerAngles;
            c.data.positionWeight = 0f;  
            c.data.rotationWeight = 1f;
            c.weight = 0f;

            return new CombatRigReferences.BoneOverride
            {
                bonePath = PathOf(root, bone), constraint = c, group = GroupOf(id)
            };
        }

        static CombatTuning Tuning()
        {
            const string path = "Assets/MotoSquid/Data/Combat/CombatTuning.asset";
            var t = AssetDatabase.LoadAssetAtPath<CombatTuning>(path);
            if (t != null) return t;

            t = ScriptableObject.CreateInstance<CombatTuning>();
            AssetDatabase.CreateAsset(t, path);
            AssetDatabase.SaveAssets();
            Debug.Log("Rig Lab: created " + path + ", tune the combat layer there, in Play mode.", t);
            return t;
        }

        static CombatTuning.Group GroupOf(HumanBodyBones b)
        {
            switch (b)
            {
                case HumanBodyBones.Hips:
                    return CombatTuning.Group.Hips;
                case HumanBodyBones.Spine:
                case HumanBodyBones.Chest:
                case HumanBodyBones.UpperChest:
                    return CombatTuning.Group.Spine;
                case HumanBodyBones.Neck:
                case HumanBodyBones.Head:
                    return CombatTuning.Group.Head;
                case HumanBodyBones.LeftShoulder:
                case HumanBodyBones.LeftUpperArm:
                case HumanBodyBones.LeftLowerArm:
                case HumanBodyBones.LeftHand:
                    return CombatTuning.Group.LeftArm;
                case HumanBodyBones.RightShoulder:
                case HumanBodyBones.RightUpperArm:
                case HumanBodyBones.RightLowerArm:
                case HumanBodyBones.RightHand:
                    return CombatTuning.Group.RightArm;
                case HumanBodyBones.LeftUpperLeg:
                case HumanBodyBones.LeftLowerLeg:
                case HumanBodyBones.LeftFoot:
                    return CombatTuning.Group.LeftLeg;
                default:
                    return CombatTuning.Group.RightLeg;
            }
        }

        static TwoBoneIKConstraint MakeIK(Transform parent, string name, Animator anim,
                                          HumanBodyBones root, HumanBodyBones mid, HumanBodyBones tip,
                                          Transform rideTarget, Transform rideHint)
        {
            var go = Child(parent, name);
            var ik = go.GetComponent<TwoBoneIKConstraint>() ?? Undo.AddComponent<TwoBoneIKConstraint>(go.gameObject);

            var target = Child(go, "Target");
            var hint = Child(go, "Hint");
            if (rideTarget != null) target.SetPositionAndRotation(rideTarget.position, rideTarget.rotation);
            if (rideHint != null) hint.position = rideHint.position;

            var d = ik.data;
            d.root = anim.GetBoneTransform(root);
            d.mid = anim.GetBoneTransform(mid);
            d.tip = anim.GetBoneTransform(tip);
            d.target = target;
            d.hint = hint;
            d.targetPositionWeight = 1f;
            d.targetRotationWeight = 0f;
            d.hintWeight = 1f;
            d.maintainTargetPositionOffset = false;
            d.maintainTargetRotationOffset = false;
            ik.data = d;
            ik.weight = 0f;
            return ik;
        }

        static void Fill(CombatRigDriver.ActionSet set, string start, string loop, string end,
                         CombatRigDriver.Limb acting,
                         CombatRigDriver.Body body = CombatRigDriver.Body.ActingLimb)
        {
            if (set.start == null) set.start = Pose(start);
            if (set.loop == null) set.loop = Pose(loop);
            if (set.end == null) set.end = Pose(end);
            set.acting = acting;
            set.body = body;
        }

        static FullPose Pose(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var t = AssetDatabase.LoadAssetAtPath<FullPose>("Assets/MotoSquid/Data/RigLab/FullPoses/" + name + ".asset");
            if (t == null) Debug.LogWarning("Rig Lab: no pose '" + name + "', run 22. Full Skeleton Sample + Verify.");
            return t;
        }

        static string PathOf(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static Transform Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Build combat rig");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Order(Transform parent, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                var t = parent.Find(names[i]);
                if (t != null) t.SetSiblingIndex(i);
            }
        }

        static bool Fail(Object ctx, string why)
        {
            Debug.LogError("Rig Lab: " + ctx.name + " " + why, ctx);
            return false;
        }
    }
}
