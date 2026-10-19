using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    // Curves a layout drawn along a straight line onto a curved centreline. A point `x` metres along the line
    // and `z` to its left lands `x` metres along the centreline and `z` to its left, so roads beside the line
    // stay beside the curve and tapers stay tapers. Far from the line that would fold (a point further out
    // than the curve's radius), so beyond `blendTo` points move rigidly with the centreline's frame at
    // `rigidStation`, blending in from `blendFrom`. A road crossing the line square at `rigidStation` stays
    // straight: its points sit on that frame's normal either way.
    [Serializable]
    public class RoadBend
    {
        [Tooltip("Curve this road's layout along a centreline: a feature drawn straight, then bent with its freeway.")]
        public bool enabled;
        public Vector2 start;
        [Tooltip("Direction the centreline starts in: degrees anticlockwise from east (+X), seen from above.")]
        public float heading;
        public List<LayoutLegs.Leg> centreline = new List<LayoutLegs.Leg>();
        [Tooltip("Metres along the centreline whose frame carries everything beyond the blend band.")]
        public float rigidStation;
        public float blendFrom = 60f;
        public float blendTo = 150f;

        [NonSerialized] PlanPath plan;

        public static RoadBend Along(Vector2 start, Vector2 direction, LayoutLegs.Leg[] centreline, float rigidStation, float blendFrom, float blendTo) =>
            new RoadBend
            {
                enabled = true,
                start = start,
                heading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
                centreline = new List<LayoutLegs.Leg>(centreline),
                rigidStation = rigidStation,
                blendFrom = blendFrom,
                blendTo = blendTo,
            };

        public Vector2 Map(Vector2 local)
        {
            if (plan == null)
            {
                float a = heading * Mathf.Deg2Rad;
                plan = LayoutLegs.Build(start, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), centreline);
            }
            Frame(local.x, out Vector2 at, out Vector2 along);
            Vector2 bent = at + Left(along) * local.y;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(blendFrom, blendTo, Mathf.Abs(local.y)));
            if (t <= 0f) return bent;
            Frame(rigidStation, out Vector2 origin, out Vector2 axis);
            Vector2 rigid = origin + axis * (local.x - rigidStation) + Left(axis) * local.y;
            return Vector2.Lerp(bent, rigid, t);
        }

        // The centreline's point and direction at `x`, carried on straight past either end.
        void Frame(float x, out Vector2 point, out Vector2 direction)
        {
            float s = Mathf.Clamp(x, 0f, plan.Length);
            plan.Evaluate(s, out point, out direction);
            point += direction * (x - s);
        }

        static Vector2 Left(Vector2 v) => new Vector2(-v.y, v.x);
    }
}
