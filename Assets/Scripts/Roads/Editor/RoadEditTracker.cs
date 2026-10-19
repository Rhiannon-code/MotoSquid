using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace MotoSquid.Roads
{
    // Notices a road's knots being moved by hand (Scene view spline tools, the Spline inspector) and marks
    // the road hand-shaped and edited, so its recipe stops driving it and Update Roads leaves it alone.
    [InitializeOnLoad]
    static class RoadEditTracker
    {
        static RoadEditTracker() => Spline.Changed += OnSplineChanged;

        static void OnSplineChanged(Spline spline, int knot, SplineModification modification)
        {
            if (Road.IsReshaping || Undo.isProcessing) return;
            foreach (var road in Object.FindObjectsByType<Road>(FindObjectsSortMode.None))
            {
                if (!road.TryGetComponent(out SplineContainer container)) continue;
                bool owns = false;
                foreach (var s in container.Splines) owns |= s == spline;
                if (!owns || road.handShaped) continue;
                Undo.RecordObject(road, "Hand-shape road");
                road.handShaped = true;
                road.edited = true;
                EditorUtility.SetDirty(road);
            }
        }
    }
}
