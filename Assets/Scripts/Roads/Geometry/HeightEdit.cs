using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    // Hand edits to a road's height points that keep the ride rules: no grade steeper than the road's limit (or
    // than it already was), and no rounding overlapping its neighbour's, which is what keeps crests smooth enough
    // to stay grounded. A height that breaks them is taken as far towards the request as the rules allow.
    public static class HeightEdit
    {
        const float SameStation = 1f;
        const int Search = 24;

        public enum Result { Applied, Limited, Refused }

        public static Result SetHeight(List<HeightPoint> points, float station, float height, float radius, float maxGrade, out float applied)
        {
            var original = new List<HeightPoint>(points);
            int k = points.FindIndex(p => Mathf.Abs(p.station - station) < SameStation);
            if (k < 0)
            {
                float current = VerticalProfile.FromPoints(points).Height(station, out _);
                k = points.FindIndex(p => p.station > station);
                if (k < 0) k = points.Count;
                points.Insert(k, new HeightPoint { station = station, height = current, radius = radius });
            }
            float from = points[k].height;
            applied = from;
            // A new point inside an existing rounding would cut that rounding short before it moved at all.
            if (Overlaps(points, k)) return Restore(points, original);

            var limits = GradeLimits(points, k, maxGrade);
            bool Valid(float t)
            {
                Set(points, k, Mathf.Lerp(from, height, t));
                return !Overlaps(points, k) && WithinGrades(points, k, limits);
            }

            if (Valid(1f)) { applied = height; return Result.Applied; }
            float lo = 0f, hi = 1f;
            for (int i = 0; i < Search; i++)
            {
                float mid = (lo + hi) / 2f;
                if (Valid(mid)) lo = mid; else hi = mid;
            }
            if (lo <= 0f) return Restore(points, original);
            Valid(lo);
            applied = points[k].height;
            return Result.Limited;
        }

        static Result Restore(List<HeightPoint> points, List<HeightPoint> original)
        {
            points.Clear();
            points.AddRange(original);
            return Result.Refused;
        }

        // Moves every height point with the stretch of road between the two nodes it sat between, so a hill or a
        // bridge stays where it was on the ground when nodes before it are dragged.
        public static void Remap(List<HeightPoint> points, IReadOnlyList<float> before, IReadOnlyList<float> after)
        {
            if (before.Count < 2 || before.Count != after.Count) return;
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                int j = 0;
                while (j < before.Count - 2 && p.station > before[j + 1]) j++;
                float span = before[j + 1] - before[j];
                float f = span > 0f ? (p.station - before[j]) / span : 0f;
                p.station = p.station > before[before.Count - 1]
                    ? p.station - before[before.Count - 1] + after[after.Count - 1]
                    : after[j] + f * (after[j + 1] - after[j]);
                points[i] = p;
            }
        }

        static void Set(List<HeightPoint> points, int k, float height)
        {
            var p = points[k];
            p.height = height;
            points[k] = p;
        }

        static float Grade(List<HeightPoint> points, int i) =>
            (points[i + 1].height - points[i].height) / Mathf.Max(points[i + 1].station - points[i].station, 1e-3f);

        static float Rounding(List<HeightPoint> points, int i) =>
            i <= 0 || i >= points.Count - 1 ? 0f : points[i].radius * Mathf.Abs(Grade(points, i) - Grade(points, i - 1));

        // Whether any rounding near the edited point runs into its neighbour's or past the road's ends.
        static bool Overlaps(List<HeightPoint> points, int k)
        {
            for (int i = Mathf.Max(0, k - 2); i <= Mathf.Min(points.Count - 2, k + 1); i++)
                if (Rounding(points, i) / 2f + Rounding(points, i + 1) / 2f > points[i + 1].station - points[i].station)
                    return true;
            return false;
        }

        static float[] GradeLimits(List<HeightPoint> points, int k, float maxGrade)
        {
            var limits = new float[2];
            if (k > 0) limits[0] = Mathf.Max(maxGrade, Mathf.Abs(Grade(points, k - 1)));
            if (k < points.Count - 1) limits[1] = Mathf.Max(maxGrade, Mathf.Abs(Grade(points, k)));
            return limits;
        }

        static bool WithinGrades(List<HeightPoint> points, int k, float[] limits) =>
            (k == 0 || Mathf.Abs(Grade(points, k - 1)) <= limits[0] + 1e-5f) &&
            (k == points.Count - 1 || Mathf.Abs(Grade(points, k)) <= limits[1] + 1e-5f);
    }
}
