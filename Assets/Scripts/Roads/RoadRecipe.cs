using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace MotoSquid.Roads
{
    [Serializable]
    public struct HeightPoint
    {
        [Tooltip("Metres along the road.")]
        public float station;
        public float height;
        [Tooltip("Radius of the rounding at this point (m). Crests under ~1,100 m throw the bike at top speed.")]
        public float radius;
    }

    // A road written as numbers: where it starts, its straights and arcs, and its height points. It becomes
    // spline knots - one wherever a leg or a height rounding starts or ends, every 30° round an arc, and at most
    // 100 m apart so a dragged node only bends the road near it - with tangents that make the spline follow the
    // numbers exactly.
    [Serializable]
    public class RoadRecipe
    {
        const float MaxArcStep = 30f;
        public const float NodeSpacing = 100f;
        const float SameStation = 0.5f;
        const float BentKnotSpacing = 20f;
        const float SlopeProbe = 0.5f;

        public Vector2 start;
        [Tooltip("Direction the road starts in: degrees anticlockwise from east (+X), seen from above.")]
        public float heading;
        public List<LayoutLegs.Leg> legs = new List<LayoutLegs.Leg>();
        [Tooltip("Straight grades between these points, rounded by parabolic curves of each point's radius.")]
        public List<HeightPoint> heights = new List<HeightPoint>();
        [Tooltip("The feature this road belongs to is drawn straight and bent onto a curved freeway. Every road of the feature carries the same bend.")]
        public RoadBend bend = new RoadBend();

        public bool IsEmpty => legs.Count == 0;

        public static RoadRecipe From(Vector2 start, Vector2 direction, IEnumerable<LayoutLegs.Leg> legs, VerticalProfile profile)
        {
            var recipe = new RoadRecipe
            {
                start = start,
                heading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
                legs = new List<LayoutLegs.Leg>(legs)
            };
            recipe.heights = profile.HeightPoints(recipe.Plan().Length);
            return recipe;
        }

        public PlanPath Plan()
        {
            float a = heading * Mathf.Deg2Rad;
            return LayoutLegs.Build(start, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), legs);
        }

        public VerticalProfile Profile() => VerticalProfile.FromPoints(heights);

        public BezierKnot[] Knots()
        {
            var plan = Plan();
            var profile = Profile();
            if (bend != null && bend.enabled) return BentKnots(plan, profile);
            var stations = Stations(plan, profile);
            var knots = new BezierKnot[stations.Count];
            for (int i = 0; i < stations.Count; i++)
            {
                float s = stations[i];
                plan.Evaluate(s, out Vector2 xz, out Vector2 dir);
                float y = profile.Height(s, out float slope);
                var tangent = new float3(dir.x, slope, dir.y);
                // The end knots get a handle on their open side too: it shapes nothing, but Unity orients a
                // Continuous knot from its out handle, and a zero one would twist the road's last stretch.
                float handleIn = i > 0 ? Handle(plan, stations[i - 1], s) : Handle(plan, s, stations[i + 1]);
                float handleOut = i < stations.Count - 1 ? Handle(plan, s, stations[i + 1]) : handleIn;
                knots[i] = new BezierKnot(new float3(xz.x, y, xz.y), -tangent * handleIn, tangent * handleOut);
            }
            return knots;
        }

        // Bent, the road is no longer straights and arcs, so it is followed with a knot at least every 20 m and
        // tangents taken from the bent curve itself. Its height points are read along the bent road, so grades
        // and roundings stay as designed where the bend stretches it (they would kink where the stretch changes);
        // every rounding's ends get a knot of their own, measured along the bent road too.
        BezierKnot[] BentKnots(PlanPath plan, VerticalProfile profile)
        {
            // Bent distance at every metre of the unbent road.
            int steps = Mathf.Max(1, Mathf.CeilToInt(plan.Length));
            var bentAt = new float[steps + 1];
            plan.Evaluate(0f, out Vector2 first, out _);
            Vector2 previous = bend.Map(first);
            for (int k = 1; k <= steps; k++)
            {
                plan.Evaluate(plan.Length * k / steps, out Vector2 xz, out _);
                Vector2 bent = bend.Map(xz);
                bentAt[k] = bentAt[k - 1] + Vector2.Distance(previous, bent);
                previous = bent;
            }
            float Bent(float s)
            {
                float f = Mathf.Clamp(s / plan.Length * steps, 0f, steps);
                int k = Mathf.Min((int)f, steps - 1);
                return Mathf.Lerp(bentAt[k], bentAt[k + 1], f - k);
            }
            float Unbent(float distance)
            {
                int k = 0;
                while (k < steps - 1 && bentAt[k + 1] < distance) k++;
                float span = bentAt[k + 1] - bentAt[k];
                return plan.Length * (k + (span > 0f ? Mathf.Clamp01((distance - bentAt[k]) / span) : 0f)) / steps;
            }

            var breaks = LegStations(plan);
            foreach (float d in profile.Breaks())
                if (d > 0f && d < bentAt[steps]) breaks.Add(Unbent(d));
            breaks = Distinct(breaks, plan.Length);
            var stations = new List<float>();
            for (int i = 0; i < breaks.Count; i++)
            {
                stations.Add(breaks[i]);
                if (i == breaks.Count - 1) break;
                int extra = Mathf.FloorToInt((breaks[i + 1] - breaks[i]) / BentKnotSpacing);
                for (int k = 1; k <= extra; k++) stations.Add(breaks[i] + (breaks[i + 1] - breaks[i]) * k / (extra + 1));
            }

            var points = new Vector2[stations.Count];
            var directions = new Vector2[stations.Count];
            for (int i = 0; i < stations.Count; i++)
            {
                plan.Evaluate(stations[i], out Vector2 xz, out Vector2 dir);
                points[i] = bend.Map(xz);
                directions[i] = (bend.Map(xz + dir * SlopeProbe) - bend.Map(xz - dir * SlopeProbe)).normalized;
            }

            var knots = new BezierKnot[stations.Count];
            for (int i = 0; i < stations.Count; i++)
            {
                float y = profile.Height(Bent(stations[i]), out float slope);
                var tangent = new float3(directions[i].x, slope, directions[i].y);
                float handleIn = i > 0 ? Vector2.Distance(points[i - 1], points[i]) / 3f : Vector2.Distance(points[i], points[i + 1]) / 3f;
                float handleOut = i < stations.Count - 1 ? Vector2.Distance(points[i], points[i + 1]) / 3f : handleIn;
                knots[i] = new BezierKnot(new float3(points[i].x, y, points[i].y), -tangent * handleIn, tangent * handleOut);
            }
            return knots;
        }

        // Where each leg ends, and every 30° round an arc.
        List<float> LegStations(PlanPath plan)
        {
            var all = new List<float> { 0f, plan.Length };
            float at = 0f;
            foreach (var leg in legs)
            {
                float length = leg.radius > 0f ? leg.radius * Mathf.Abs(leg.degrees) * Mathf.Deg2Rad : leg.straight;
                int pieces = leg.radius > 0f ? Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(leg.degrees) / MaxArcStep)) : 1;
                for (int k = 1; k <= pieces; k++) all.Add(at + length * k / pieces);
                at += length;
            }
            return all;
        }

        static List<float> Distinct(List<float> all, float length)
        {
            all.Sort();
            var stations = new List<float>();
            foreach (float s in all)
                if (stations.Count == 0 || s - stations[stations.Count - 1] > SameStation) stations.Add(s);
            stations[stations.Count - 1] = length;
            return stations;
        }

        List<float> Stations(PlanPath plan, VerticalProfile profile)
        {
            var all = LegStations(plan);
            foreach (float s in profile.Breaks())
                if (s > 0f && s < plan.Length) all.Add(s);
            var breaks = Distinct(all, plan.Length);
            var stations = new List<float>();
            for (int i = 0; i < breaks.Count - 1; i++)
            {
                int pieces = Mathf.CeilToInt((breaks[i + 1] - breaks[i]) / NodeSpacing);
                for (int k = 0; k < pieces; k++) stations.Add(breaks[i] + (breaks[i + 1] - breaks[i]) * k / pieces);
            }
            stations.Add(plan.Length);
            return stations;
        }

        // Handle length that makes a cubic Bézier follow a circular arc (4/3 tan(θ/4) r), or a third of a straight.
        public static float Handle(PlanPath plan, float from, float to)
        {
            float span = to - from;
            float curvature = Mathf.Abs(plan.CurvatureAt((from + to) / 2f));
            return curvature < 1e-9f ? span / 3f : 4f / 3f * Mathf.Tan(span * curvature / 4f) / curvature;
        }
    }
}
