using System.Collections.Generic;
using UnityEngine;
using static MotoSquid.Roads.LayoutLegs;

namespace MotoSquid.Roads
{
    // A route given as corner points becomes straights and arcs: each corner is a straight-arc-straight fillet, as
    // round as the shorter of its two legs allows (half of it each side), up to maxRadius. With `keep`, leg i must
    // keep that many metres straight (a feature sits on it), and its corners share only what is left.
    public static class RoadFillet
    {
        public static (Vector2 start, Vector2 direction, Leg[] legs) Fillet(IReadOnlyList<Vector2> raw, float minRadius, float maxRadius,
                                                                            IReadOnlyList<float> keep = null)
        {
            var pts = new List<Vector2> { raw[0] };
            var kept = new List<float>();
            for (int i = 1; i < raw.Count; i++)
            {
                if (Vector2.Distance(raw[i], pts[pts.Count - 1]) > 1f) { pts.Add(raw[i]); kept.Add(keep != null ? keep[i - 1] : 0f); }
                else if (keep != null && kept.Count > 0) kept[kept.Count - 1] = Mathf.Max(kept[kept.Count - 1], keep[i - 1]);
            }
            if (pts.Count < 2) { pts.Add(raw[raw.Count - 1] + Vector2.right); kept.Add(0f); }

            int n = pts.Count;
            var length = new float[n - 1];
            var dir = new Vector2[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                length[i] = Vector2.Distance(pts[i], pts[i + 1]);
                dir[i] = (pts[i + 1] - pts[i]) / length[i];
            }
            var turn = new float[n];
            var radius = new float[n];
            var tangent = new float[n];
            for (int i = 1; i < n - 1; i++)
            {
                turn[i] = Vector2.SignedAngle(dir[i - 1], dir[i]);
                float half = Mathf.Abs(turn[i]) * Mathf.Deg2Rad / 2f;
                // Treating a small turn as straight sends the rest of the road a little off course; with `keep` the
                // road's straights must land exactly where its features are, so only rounding noise is ignored.
                if (Mathf.Abs(turn[i]) < (keep == null ? 0.5f : 1e-4f)) { turn[i] = 0f; continue; }
                float fit = keep == null
                    ? Mathf.Min(length[i - 1], length[i]) / 2f / Mathf.Tan(half)
                    : Mathf.Min(Free(i - 1), Free(i)) / Mathf.Tan(half);
                radius[i] = Mathf.Min(maxRadius, fit);
                tangent[i] = radius[i] * Mathf.Tan(half);
            }

            // What a leg leaves for the corners at its two ends: the road's own ends aren't corners.
            float Free(int leg) => Mathf.Max(0f, length[leg] - kept[leg]) / ((leg > 0 ? 1 : 0) + (leg + 1 < n - 1 ? 1 : 0));

            var legs = new List<Leg>();
            for (int i = 0; i < n - 1; i++)
            {
                float straight = length[i] - tangent[i] - tangent[i + 1];
                if (straight > 0.01f) legs.Add(Leg.S(straight));
                if (i + 1 < n - 1 && turn[i + 1] != 0f) legs.Add(Leg.A(radius[i + 1], turn[i + 1]));
            }
            return (pts[0], dir[0], legs.ToArray());
        }
    }
}
