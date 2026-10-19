using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    // Carries a road on from a dead end to the nearest road it can join: one turn at connector radius, then a
    // straight onto the other road's centreline, at a height that road has there and that the road's own grade
    // limit can reach in the distance. It meets the other road at an angle, so the join is a compact T.
    public static class DeadEndRouter
    {
        const float Search = 1500f;
        const float CandidateStep = 10f;
        const float MinApproach = 30f;
        const float MinStraight = 20f;
        const float HeightMargin = 20f;
        const float MaxTurn = 270f;

        public sealed class Route
        {
            public RoadPath Target;
            public float TargetStation;
            public Vector2 Start;
            public Vector2 Direction;
            public float StartHeight;
            public float EndHeight;
            public LayoutLegs.Leg[] Legs;
            public float Length;

            public PlanPath Plan() => LayoutLegs.Build(Start, Direction, Legs);
        }

        public static Route Find(RoadPath road, bool atStart, IReadOnlyList<RoadPath> paths, float maxGrade, float verticalRadius)
        {
            int end = atStart ? 0 : road.Count - 1;
            Vector3 from = road.Centre[end];
            var p = new Vector2(from.x, from.z);
            Vector3 f = atStart ? -road.Forward[0] : road.Forward[end];
            var d = new Vector2(f.x, f.z).normalized;

            Route best = null;
            foreach (var other in paths)
            {
                if (other == road) continue;
                int step = Mathf.Max(1, Mathf.RoundToInt(CandidateStep / other.Spacing));
                for (int i = 0; i < other.Count; i += step)
                {
                    Vector3 to = other.Centre[i];
                    var q = new Vector2(to.x, to.z);
                    float flat = (q - p).magnitude;
                    float rise = to.y - from.y;
                    if (flat > Search || flat < MinStraight || Mathf.Abs(rise) > maxGrade * flat) continue;
                    foreach (float side in new[] { 1f, -1f })
                    {
                        if (!Turn(p, d, q, side, out float degrees, out float straight)) continue;
                        float length = RoadDesign.ConnectorRadius * Mathf.Abs(degrees) * Mathf.Deg2Rad + straight;
                        if (best != null && length >= best.Length) continue;
                        if (VerticalProfile.ChangeLength(rise, maxGrade, verticalRadius) > length - HeightMargin) continue;
                        float approach = Vector2.Angle(Rotate(d, degrees), new Vector2(other.Forward[i].x, other.Forward[i].z));
                        if (approach < MinApproach || approach > 180f - MinApproach) continue;
                        var legs = new List<LayoutLegs.Leg>();
                        if (Mathf.Abs(degrees) > 0.01f) legs.Add(LayoutLegs.Leg.A(RoadDesign.ConnectorRadius, degrees));
                        legs.Add(LayoutLegs.Leg.S(straight));
                        best = new Route
                        {
                            Target = other, TargetStation = i * other.Spacing, Start = p, Direction = d,
                            StartHeight = from.y, EndHeight = to.y, Legs = legs.ToArray(), Length = length
                        };
                    }
                }
            }
            return best;
        }

        // The turn at connector radius (left for side +1) after which the road points straight at q, and the
        // straight still to run. False when q is inside the turn or the turn would pass 270°.
        static bool Turn(Vector2 p, Vector2 d, Vector2 q, float side, out float degrees, out float straight)
        {
            float r = RoadDesign.ConnectorRadius;
            Vector2 centre = p + new Vector2(-d.y, d.x) * (side * r);
            float Lean(float a, out Vector2 at, out Vector2 dir)
            {
                at = centre + Rotate(p - centre, side * a);
                dir = Rotate(d, side * a);
                Vector2 ahead = q - at;
                return (dir.x * ahead.y - dir.y * ahead.x) * side;
            }

            degrees = 0f;
            straight = 0f;
            Vector2 pos, heading;
            float lean = Lean(0f, out pos, out heading);
            if (lean <= 0f)
            {
                if (side < 0f || Mathf.Abs(lean) > 1e-3f * (q - p).magnitude || Vector2.Dot(heading, q - p) <= 0f) return false;
                straight = (q - p).magnitude;
                return straight >= MinStraight;
            }
            float lo = 0f, hi = -1f;
            for (float a = 1f; a <= MaxTurn; a += 1f)
            {
                if (Lean(a, out pos, out heading) <= 0f) { hi = a; break; }
                lo = a;
            }
            if (hi < 0f) return false;
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) / 2f;
                if (Lean(mid, out pos, out heading) > 0f) lo = mid; else hi = mid;
            }
            Lean(hi, out pos, out heading);
            if (Vector2.Dot(heading, q - pos) <= 0f) return false;
            degrees = side * hi;
            straight = (q - pos).magnitude;
            return straight >= MinStraight;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
