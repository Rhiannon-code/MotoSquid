using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{

    public static class RigLabRiderTargets
    {
        const string Prefix = RiderSwitch.SetPrefix;

        [MenuItem("Tools/Rig Lab/2c. Give Each Rider Its Own Targets", false, 3)]
        internal static void Split()
        {
            var bikes = RigLabScope.Bikes(true);
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in " + RigLabScope.Where() + "."); return; }

            var riderFields = typeof(BikeAnimationController)
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f => f.FieldType == typeof(Transform) && !f.Name.EndsWith("Rig"))
                .ToArray();

            int made = 0, reused = 0, killed = 0;
            foreach (var bike in bikes)
            {
                var donor = bike.bikeReferences != null ? bike.bikeReferences.bikeAnimationTargets : null;
                if (donor == null) continue;
                var parent = donor.transform.parent;
                if (parent == null) continue;

                var riders = RigLabRiders.AllOn(bike);
                var wanted = new HashSet<string>(riders.Select(r => Prefix + r.gameObject.name));

                var seen = new HashSet<string>();
                foreach (var child in parent.Cast<Transform>().ToList())
                {
                    if (!child.name.StartsWith(Prefix)) continue;
                    if (!wanted.Contains(child.name) || !seen.Add(child.name))
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);
                        killed++;
                    }
                }

                foreach (var ctrl in riders)
                {
                    string name = Prefix + ctrl.gameObject.name;
                    var set = parent.Find(name);
                    if (set == null)
                    {
                        var copy = Object.Instantiate(donor.gameObject, parent);
                        copy.name = name;
                        copy.transform.localPosition = donor.transform.localPosition;
                        copy.transform.localRotation = donor.transform.localRotation;
                        copy.transform.localScale = donor.transform.localScale;
                        Undo.RegisterCreatedObjectUndo(copy, "Split rider targets");
                        set = copy.transform;
                        made++;
                    }
                    else reused++;

                    Undo.RecordObject(ctrl, "Split rider targets");
                    foreach (var f in riderFields)
                    {
                        var donorField = typeof(BikeAnimationTargets).GetField(f.Name);
                        var donorTr = donorField != null ? donorField.GetValue(donor) as Transform : null;
                        if (donorTr == null) continue;
                        var mine = Mirror(donor.transform, set, donorTr);
                        if (mine != null) f.SetValue(ctrl, mine);
                    }
                    EditorUtility.SetDirty(ctrl);
                }
            }

            int hidden = 0;
            foreach (var bike in bikes)
            {
                var donor = bike.bikeReferences != null ? bike.bikeReferences.bikeAnimationTargets : null;
                if (donor == null || donor.transform.parent == null) continue;
                var parent = donor.transform.parent;

                Undo.RecordObject(donor.gameObject, "Split rider targets");
                donor.gameObject.SetActive(false);
                hidden++;

                foreach (var ctrl in RigLabRiders.AllOn(bike))
                {
                    var set = parent.Find(Prefix + ctrl.gameObject.name);
                    if (set == null) continue;
                    bool on = ctrl.gameObject.activeInHierarchy;
                    if (set.gameObject.activeSelf == on) continue;
                    Undo.RecordObject(set.gameObject, "Split rider targets");
                    set.gameObject.SetActive(on);
                    if (!on) hidden++;
                }
            }

            RigLabScope.MarkDirty();
            Debug.Log("Rig Lab: " + hidden + " inactive target set(s) hidden.\n" +
                      "Rig Lab: rider targets, " + made + " created, " + reused + " reused, " +
                      killed + " duplicate/orphan set(s) removed.\n" +
                      "'Biker Animation Targets' is the donor and stays untouched. Re-run after adding a character.");
        }

        static Transform Mirror(Transform donor, Transform target, Transform node)
        {
            if (node == donor) return target;
            if (!node.IsChildOf(donor)) return null;

            var parts = new List<string>();
            for (var t = node; t != null && t != donor; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return target.Find(string.Join("/", parts));
        }
    }
}
