using MotoSquid.Bike;
using MotoSquid.Rider;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabBikeAnchors
    {
        [MenuItem("Tools/Rig Lab/19b. Create Bike Anchors (all bikes)", false, 191)]
        static void Create()
        {
            var found = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }
            var bikes = RigLabScope.Narrow(found);

            int made = 0, seeded = 0;
            foreach (var bike in bikes)
            {
                var model = bike.bikeReferences != null ? bike.bikeReferences.BikeModel : null;
                var donor = bike.bikeReferences != null ? bike.bikeReferences.bikeAnimationTargets : null;
                if (model == null) continue;

                var anchors = bike.GetComponent<BikeAnchors>()
                           ?? Undo.AddComponent<BikeAnchors>(bike.gameObject);
                Undo.RecordObject(anchors, "Create bike anchors");

                var host = Child(model, "Rider Anchors");

                bool fresh = anchors.seat == null;
                anchors.seat = Keep(anchors.seat, host, "Seat");
                anchors.pegLeft = Keep(anchors.pegLeft, host, "Peg Left");
                anchors.pegRight = Keep(anchors.pegRight, host, "Peg Right");
                anchors.gripLeft = Keep(anchors.gripLeft, host, "Grip Left");
                anchors.gripRight = Keep(anchors.gripRight, host, "Grip Right");
                anchors.footDownLeft = Keep(anchors.footDownLeft, host, "Foot Down Left");

                if (fresh && donor != null)
                {
                    Seed(anchors.pegLeft, donor, "leftlegInMotionTarget");
                    Seed(anchors.pegRight, donor, "rightlegInMotionTarget");
                    Seed(anchors.gripLeft, donor, "leftHandTarget");
                    Seed(anchors.gripRight, donor, "rightHandTarget");
                    Seed(anchors.footDownLeft, donor, "leftlegIdleTarget");

                    var normal = Get(donor, "hipNormalSpeedTarget");
                    if (normal != null)
                    {
                        Undo.RecordObject(anchors.seat, "Create bike anchors");
                        anchors.seat.SetPositionAndRotation(
                            normal.position - normal.up * anchors.seatClearance, normal.rotation);

                        anchors.hipNormal = Vector3.zero;
                        anchors.hipIdle = Offset(anchors.seat, normal, Get(donor, "hipIdleTarget"));
                        anchors.hipHigh = Offset(anchors.seat, normal, Get(donor, "hipHighSpeedTarget"));

                        anchors.spineIdleTuck = Tuck(anchors.seat, Get(donor, "spineIdleTarget"));
                        anchors.spineNormalTuck = Tuck(anchors.seat, Get(donor, "spineNormalSpeedTarget"));
                        anchors.spineHighTuck = Tuck(anchors.seat, Get(donor, "spineHighSpeedTarget"));
                    }
                    seeded++;
                }

                EditorUtility.SetDirty(anchors);
                made++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Rig Lab: anchors on " + made + " bike(s), " + seeded + " seeded from existing targets." +
                      RigLabScope.ScopeNote(bikes.Length, found.Length) + "\n" +
                      "Select a bike to see them, then drag each onto the real surface:\n" +
                      "   Seat on the seat's TOP face, pegs on the peg tops, grips at the grip centres.\n" +
                      "Sinking is fixed by placing them on the surface and letting seatClearance lift the rider.");
        }

        static Transform Keep(Transform existing, Transform host, string name)
        {
            return existing != null ? existing : Child(host, name);
        }

        static Transform Get(BikeAnimationTargets donor, string field)
        {
            var f = typeof(BikeAnimationTargets).GetField(field);
            return f != null ? f.GetValue(donor) as Transform : null;
        }

        static Vector3 Offset(Transform seat, Transform normal, Transform pose)
        {
            if (pose == null || normal == null) return Vector3.zero;
            return seat.InverseTransformVector(pose.position - normal.position);
        }

        static float Tuck(Transform seat, Transform spine)
        {
            if (spine == null) return 0f;
            float x = (Quaternion.Inverse(seat.rotation) * spine.rotation).eulerAngles.x;
            return x > 180f ? x - 360f : x;
        }

        static void Seed(Transform anchor, BikeAnimationTargets donor, string field)
        {
            if (anchor == null) return;
            var f = typeof(BikeAnimationTargets).GetField(field);
            var src = f != null ? f.GetValue(donor) as Transform : null;
            if (src == null) return;
            Undo.RecordObject(anchor, "Create bike anchors");
            anchor.SetPositionAndRotation(src.position, src.rotation);
        }

        static Transform Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create bike anchors");
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
