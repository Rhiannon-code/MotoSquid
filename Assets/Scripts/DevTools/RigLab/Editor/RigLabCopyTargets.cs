using MotoSquid.Bike;
using MotoSquid.Rider;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotoSquid.DevTools
{
    public class RigLabCopyTargets : EditorWindow
    {
        BikeAnimationController from;
        BikeController destination;
        bool copyHints = true;

        [MenuItem("Tools/Rig Lab/10. Copy Targets Between Bikes", false, 100)]
        static void Open()
        {
            GetWindow<RigLabCopyTargets>("Copy Targets").minSize = new Vector2(360, 170);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Copies the 19 targets from one RIDER to every rider on another bike, or to the other " +
                "riders on the same bike, to tune one character and propagate.\n" +
                "Reads and writes each rider's own target set, not the bike's donor, which nothing has " +
                "used since every rider was given its own.", MessageType.None);

            EditorGUILayout.Space();
            from = (BikeAnimationController)EditorGUILayout.ObjectField("From rider", from, typeof(BikeAnimationController), true);
            destination = (BikeController)EditorGUILayout.ObjectField("To bike", destination, typeof(BikeController), true);
            copyHints = EditorGUILayout.Toggle("Also copy IK hints", copyHints);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(from == null || destination == null))
                if (GUILayout.Button("Copy"))
                    Copy();
        }

        void Copy()
        {
            var fields = typeof(BikeAnimationController).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f => f.FieldType == typeof(Transform) && !f.Name.EndsWith("Rig")).ToArray();

            int n = 0, riders = 0;
            foreach (var to in RigLabRiders.AllOn(destination))
            {
                if (to == from) continue;

                foreach (var f in fields)
                {
                    var a = f.GetValue(from) as Transform;
                    var b = f.GetValue(to) as Transform;
                    if (a == null || b == null) continue;
                    Undo.RecordObject(b, "Copy targets");
                    b.localPosition = a.localPosition;
                    b.localRotation = a.localRotation;
                    n++;
                }

                if (copyHints)
                {
                    var ra = from.GetComponentInChildren<BikerRigReferences>(true);
                    var rb = to.GetComponentInChildren<BikerRigReferences>(true);
                    if (ra != null && rb != null)
                    {
                        n += CopyHint(ra.leftLegHint, rb.leftLegHint);
                        n += CopyHint(ra.rightLegHint, rb.rightLegHint);
                        n += CopyHint(ra.leftHandHint, rb.leftHandHint);
                        n += CopyHint(ra.rightHandHint, rb.rightHandHint);
                    }
                }
                riders++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Rig Lab: copied " + n + " transform(s) from " + from.gameObject.name +
                      " to " + riders + " rider(s) on " + destination.name + ".");
        }

        static int CopyHint(Transform a, Transform b)
        {
            if (a == null || b == null) return 0;
            Undo.RecordObject(b, "Copy targets");
            b.localPosition = a.localPosition;
            return 1;
        }
    }
}
