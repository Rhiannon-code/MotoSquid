using MotoSquid.Rider;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.DevTools
{
    public static class RigLabRestoreReverse
    {
        [MenuItem("Tools/Rig Lab/18. Restore Reverse Leg Targets", false, 202)]
        static void Restore()
        {
            if (Application.isPlaying) { Debug.LogError("Rig Lab: restore in edit mode."); return; }

            var log = new System.Text.StringBuilder();
            int fixedCount = 0, clean = 0, riders = 0;

            var found = RigLabScope.BikeRoots(true);
            var scoped = RigLabScope.Narrow(found);

            foreach (var root in scoped)
                foreach (var rider in RigLabRiders.AllOn(root))
                {
                    var down = rider.leftlegIdleTarget;
                    if (down == null || rider.leftlegReverseTarget == null || rider.rightlegReverseTarget == null)
                        continue;

                    riders++;
                    var want = down.localPosition;
                    fixedCount += Place(rider.leftlegReverseTarget, want, down.localRotation, rider, log, ref clean);
                    fixedCount += Place(rider.rightlegReverseTarget, new Vector3(-want.x, want.y, want.z),
                                        down.localRotation, rider, log, ref clean);
                }

            RigLabScope.MarkDirty();

            if (riders == 0)
            {
                Debug.LogError("Rig Lab: no writable rider found. Open a bike prefab, the open scene's " +
                               "bikes are instances and would only take overrides.");
                return;
            }

            Debug.Log("Rig Lab: reverse targets, " + fixedCount + " reset, " + clean + " already correct, " +
                      riders + " rider(s) in " + RigLabScope.Where() + "." +
                      RigLabScope.ScopeNote(scoped.Length, found.Length) + "\n" + log +
                      "\nStride size is reverseStepHeight / reverseStepDistance on each rider, not part of this.");
        }

        static int Place(Transform tr, Vector3 want, Quaternion rot,
                         BikeAnimationController rider, System.Text.StringBuilder log, ref int clean)
        {
            if ((tr.localPosition - want).sqrMagnitude < 1e-8f) { clean++; return 0; }

            log.Append("   ").Append(rider.gameObject.name).Append('/').Append(tr.name).Append("  ")
               .Append(tr.localPosition.ToString("F3")).Append(" -> ").Append(want.ToString("F3")).Append('\n');

            Undo.RecordObject(tr, "Restore reverse leg targets");
            tr.localPosition = want;
            tr.localRotation = rot;
            EditorUtility.SetDirty(tr);
            return 1;
        }
    }
}
