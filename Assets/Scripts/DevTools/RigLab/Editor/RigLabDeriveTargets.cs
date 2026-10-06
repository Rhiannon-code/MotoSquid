using MotoSquid.Bike;
using MotoSquid.Rider;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabDeriveTargets
    {
        [MenuItem("Tools/Rig Lab/19c. Derive Targets From Anchors (all bikes)", false, 192)]
        static void Derive()
        {
            var found = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }
            var bikes = RigLabScope.Narrow(found);

            int riders = 0, skipped = 0;
            foreach (var bike in bikes)
            {
                var a = bike.GetComponent<BikeAnchors>();
                if (a == null || !a.IsComplete)
                {
                    skipped++;
                    Debug.LogWarning("Rig Lab: " + bike.name + " has no complete anchors, run 19b and place them.", bike);
                    continue;
                }

                Vector3 seat = a.seat.position + a.seat.up * a.seatClearance;

                foreach (var ctrl in RigLabRiders.AllOn(bike))
                {
                    Undo.RecordObject(ctrl, "Derive targets");

                    Place(ctrl.leftHandTarget, a.gripLeft.position, a.gripLeft.rotation);
                    Place(ctrl.rightHandTarget, a.gripRight.position, a.gripRight.rotation);

                    Place(ctrl.leftlegInMotionTarget, a.pegLeft.position, a.pegLeft.rotation);
                    Place(ctrl.rightlegInMotionTarget, a.pegRight.position, a.pegRight.rotation);
                    Place(ctrl.rightlegIdleTarget, a.pegRight.position, a.pegRight.rotation);
                    Place(ctrl.leftlegIdleTarget,
                          a.footDownLeft != null ? a.footDownLeft.position : a.pegLeft.position,
                          a.footDownLeft != null ? a.footDownLeft.rotation : a.pegLeft.rotation);

                    Place(ctrl.hipIdleTarget, seat + a.seat.TransformVector(a.hipIdle), a.seat.rotation);
                    Place(ctrl.hipNormalSpeedTarget, seat + a.seat.TransformVector(a.hipNormal), a.seat.rotation);
                    Place(ctrl.hipHighSpeedTarget, seat + a.seat.TransformVector(a.hipHigh), a.seat.rotation);

                    Lean(ctrl.spineIdleTarget, a.seat, a.spineIdleTuck);
                    Lean(ctrl.spineNormalSpeedTarget, a.seat, a.spineNormalTuck);
                    Lean(ctrl.spineHighSpeedTarget, a.seat, a.spineHighTuck);

                    EditorUtility.SetDirty(ctrl);
                    riders++;
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Rig Lab: derived targets for " + riders + " rider(s)" +
                      (skipped > 0 ? ", skipped " + skipped + " bike(s) with no anchors" : "") + "." +
                      RigLabScope.ScopeNote(bikes.Length, found.Length) + "\n" +
                      "Reverse and air targets are left alone, they have no anchor and are rare states.\n" +
                      "Per-character reach is Rider Fit's job, not this one.");
        }

        static void Place(Transform t, Vector3 pos, Quaternion rot)
        {
            if (t == null) return;
            Undo.RecordObject(t, "Derive targets");
            t.SetPositionAndRotation(pos, rot);
            EditorUtility.SetDirty(t);
        }

        static void Lean(Transform t, Transform seat, float degrees)
        {
            if (t == null) return;
            Undo.RecordObject(t, "Derive targets");
            t.rotation = seat.rotation * Quaternion.Euler(degrees, 0f, 0f);
            EditorUtility.SetDirty(t);
        }
    }
}
