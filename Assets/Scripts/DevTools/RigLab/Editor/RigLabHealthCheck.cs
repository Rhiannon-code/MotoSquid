using MotoSquid.Bike;
using MotoSquid.Combat;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabHealthCheck
    {
        [MenuItem("Tools/Rig Lab/0. Check Everything", false, -1)]
        static void Check()
        {
            var sb = new System.Text.StringBuilder();
            int fail = 0, warn = 0;

            var bikes = RigLabScope.Bikes();
            sb.Append("RIG LAB HEALTH CHECK\n\n");
            sb.Append(Line(bikes.Length > 0, "bikes in scene", bikes.Length.ToString(), "run 1. Build Review Scene", ref fail));
            if (bikes.Length == 0) { Debug.LogError(sb.ToString()); return; }

            var poses = AssetDatabase.FindAssets("t:FullPose")
                .Select(g => AssetDatabase.LoadAssetAtPath<FullPose>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).ToList();
            sb.Append(Line(poses.Count > 0, "captured FullPose assets", poses.Count.ToString(),
                           "run 22. Full Skeleton Sample + Verify", ref fail));

            var needed = new List<FullPose>();
            var unwired = new List<string>();
            foreach (var bike in bikes)
                foreach (var ctrl in RigLabRiders.AllOn(bike))
                {
                    var d = ctrl.GetComponent<CombatRigDriver>();
                    if (d == null) continue;
                    foreach (var f in typeof(CombatRigDriver).GetFields(
                                 System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                    {
                        if (f.FieldType != typeof(CombatRigDriver.ActionSet)) continue;
                        var set = f.GetValue(d) as CombatRigDriver.ActionSet;
                        if (set == null) continue;
                        if (set.end == null && !unwired.Contains(f.Name)) unwired.Add(f.Name);
                        foreach (var pose in new[] { set.start, set.loop, set.end })
                            if (pose != null && !needed.Contains(pose)) needed.Add(pose);
                    }
                }

            sb.Append(Line(unwired.Count == 0, "poses the driver needs",
                           unwired.Count == 0 ? needed.Count + " referenced, every action wired"
                                              : "no pose on: " + string.Join(", ", unwired),
                           "run 22 for the missing clips, then 13", ref fail));

            foreach (var p in needed)
            {
                bool sane = p.frameCount > 1 && p.BoneCount > 50 && p.rotations.Length == p.frameCount * p.BoneCount;
                if (!sane)
                    sb.Append(Line(false, "pose " + p.name, p.frameCount + " frames x " + p.BoneCount + " bones",
                                   "re-run 22", ref fail));
            }

            sb.Append('\n');
            foreach (var bike in bikes)
            {
                var onBike = RigLabRiders.AllOn(bike);
                if (onBike.Length == 0) { sb.Append("  ").Append(bike.name).Append("   NO RIDER  -> run 2. Swap Riders\n"); fail++; continue; }

                foreach (var ctrl in onBike)
                {

                var anim = ctrl.GetComponent<Animator>();
                var rigBuilder = ctrl.GetComponent<RigBuilder>();
                var driver = ctrl.GetComponent<CombatRigDriver>();
                var basePose = ctrl.GetComponent<BasePose>();
                var rideRefs = ctrl.GetComponentInChildren<BikerRigReferences>(true);

                var problems = new List<string>();
                if (anim == null || !anim.isHuman) problems.Add("rider not Humanoid");
                if (rigBuilder == null) problems.Add("no RigBuilder");
                if (basePose == null || basePose.bones.Length == 0) problems.Add("no base pose (15)");
                var weapon = ctrl.GetComponent<CombatWeapon>();
                if (weapon == null) problems.Add("no CombatWeapon (13)");
                else if (!weapon.Ready) problems.Add("weapon unset: " +
                         (weapon.model == null ? "no model" : weapon.hand == null ? "no hand bone" : "no driver") + " (13)");

                if (driver == null) problems.Add("no CombatRigDriver (13)");
                else
                {
                    if (driver.rideRefs == null) problems.Add("driver.rideRefs unset (13)");
                    if (driver.tuning == null) problems.Add("driver.tuning unset (13)");
                    if (driver.combatRefs == null) problems.Add("driver.combatRefs unset (13)");
                    else
                    {
                        var c = driver.combatRefs;
                        if (c.rig == null) problems.Add("combat rig unset (13)");
                        if (c.overrides.Length == 0) problems.Add("no pose overrides (13)");
                        if (c.leftHand == null || c.rightHand == null ||
                            c.leftFoot == null || c.rightFoot == null) problems.Add("anchors unset (13)");

                        int badOvr = c.overrides.Count(o => o.constraint == null ||
                                                            string.IsNullOrEmpty(o.bonePath) ||
                                                            o.constraint.data.constrainedObject == null);
                        if (badOvr > 0) problems.Add(badOvr + " override(s) incomplete (13)");

                        foreach (var ik in new[] { c.leftHand, c.rightHand, c.leftFoot, c.rightFoot })
                            if (ik != null && (ik.data.root == null || ik.data.mid == null || ik.data.tip == null))
                            { problems.Add("an anchor has unbound bones (13)"); break; }

                        if (driver.meleeRight != null && driver.meleeRight.end != null)
                        {
                            var pose = driver.meleeRight.end;
                            int unmapped = c.overrides.Count(o => pose.IndexOf(o.bonePath) < 0);
                            if (unmapped > 0)
                                problems.Add(unmapped + "/" + c.overrides.Length +
                                             " overrides name bones the pose does not contain (re-run 22 then 13)");
                        }
                    }
                    if (driver.meleeRight == null || driver.meleeRight.end == null) problems.Add("melee poses unset (13)");
                    if (driver.kickLeft == null || driver.kickLeft.end == null) problems.Add("kick poses unset (13)");
                }

                var rideRig = rideRefs != null ? rideRefs.GetComponent<Rig>() : null;
                if (rideRig != null && rideRig.weight < 0.99f)
                    problems.Add("ride rig weight is " + rideRig.weight.ToString("0.00") + ", she will sag (13 resets it)");


                if (problems.Count == 0) sb.Append("  OK   ").Append(bike.name).Append("  ").Append(ctrl.gameObject.name).Append('\n');
                else { fail++; sb.Append("  FAIL ").Append(bike.name).Append("  ").Append(ctrl.gameObject.name).Append("\n          ")
                                 .Append(string.Join("\n          ", problems)).Append('\n'); }
                }
            }

            sb.Append('\n');
            if (fail == 0 && warn == 0)
                sb.Append("Everything checks out. Press Play, then Q / E / Z / C.");
            else
                sb.Append(fail).Append(" bike(s) or asset(s) not ready. Fix the steps named above, then run this again.");

            if (fail > 0) Debug.LogError(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        static string Line(bool ok, string what, string detail, string fix, ref int fail)
        {
            if (!ok) fail++;
            return (ok ? "  OK   " : "  FAIL ") + what.PadRight(28) + detail +
                   (ok ? "" : "   -> " + fix) + "\n";
        }
    }
}
