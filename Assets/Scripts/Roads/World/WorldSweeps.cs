using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MotoSquid.Roads.LayoutLegs;

namespace MotoSquid.Roads
{
    // Breaks up a world road's long straights: on each stretch clear of its interchanges, a technical bend set where
    // the World Plan asks for one, otherwise long sweeps, alternating sides. Every set ends back on the straight's
    // own line, so nothing beyond it moves; a set is only used where its bulge stays clear of the other roads and
    // inside the map.
    public static class WorldSweeps
    {
        const float Clearance = 200f;
        const float EndClear = 300f;
        const float Spacing = 600f;
        const float TechnicalReach = 400f;
        const float MapEdge = 7400f;
        static readonly (System.Func<float, float, Leg[]> make, float degrees)[] Sweeps =
        {
            (RoadSweeps.Long, 25f), (RoadSweeps.Long, 32f), (RoadSweeps.Long, 18f),
            (RoadSweeps.Medium, 20f), (RoadSweeps.Medium, 14f),
        };

        public static Leg[] Add(string name, Vector2 start, Vector2 direction, Leg[] legs, List<(float from, float to)> kept,
                                bool sweeps, IReadOnlyList<Vector2> technical, IReadOnlyList<IReadOnlyList<Vector2>> others, List<string> report)
        {
            var plan = Build(start, direction, legs);
            var usedTechnical = new bool[technical.Count];
            var result = new List<Leg>();
            int sweepCount = 0, technicalCount = 0;
            float at = 0f;
            foreach (var leg in legs)
            {
                float length = leg.radius > 0f ? leg.radius * Mathf.Abs(leg.degrees) * Mathf.Deg2Rad : leg.straight;
                if (leg.radius > 0f) { result.Add(leg); at += length; continue; }
                float emitted = at;
                foreach (var (from, to) in Free(at, at + length, kept, plan.Length))
                {
                    float pos = from;
                    while (pos < to)
                    {
                        var choice = Technical(plan, pos, to, technical, usedTechnical, technicalCount, others)
                                  ?? (sweeps ? Sweep(plan, pos, to, sweepCount, others) : null);
                        if (choice == null) break;
                        var (place, set, isTechnical) = choice.Value;
                        result.Add(Leg.S(place - emitted));
                        result.AddRange(set);
                        emitted = place + RoadSweeps.Forward(set);
                        pos = emitted + Spacing;
                        if (isTechnical) technicalCount++; else sweepCount++;
                    }
                }
                if (at + length - emitted > 0.01f) result.Add(Leg.S(at + length - emitted));
                at += length;
            }
            report?.Add($"sweeps {name}: {sweepCount} long sweep(s), {technicalCount} technical set(s)");
            for (int i = 0; i < technical.Count; i++)
                if (!usedTechnical[i]) report?.Add($"{name}: no clear straight near the technical bend point ({technical[i].x:0}, {technical[i].y:0}).");
            return result.ToArray();
        }

        // The parts of a straight [from, to] that are clear of every interchange and of the road's ends.
        static IEnumerable<(float from, float to)> Free(float from, float to, List<(float from, float to)> kept, float length)
        {
            float a = Mathf.Max(from, EndClear), end = Mathf.Min(to, length - EndClear);
            foreach (var (k0, k1) in kept.Where(k => k.to > a && k.from < end).OrderBy(k => k.from))
            {
                if (k0 > a) yield return (a, k0);
                a = Mathf.Max(a, k1);
            }
            if (end > a) yield return (a, end);
        }

        static (float place, Leg[] set, bool technical)? Technical(PlanPath plan, float from, float to, IReadOnlyList<Vector2> points,
                                                                 bool[] used, int count, IReadOnlyList<IReadOnlyList<Vector2>> others)
        {
            plan.Evaluate(from, out Vector2 a, out Vector2 dir);
            for (int i = 0; i < points.Count; i++)
            {
                if (used[i]) continue;
                float along = Vector2.Dot(points[i] - a, dir), across = Mathf.Abs(Cross(dir, points[i] - a));
                if (across > TechnicalReach || along < 0f || along > to - from) continue;
                var baseSet = RoadSweeps.Technical[count % RoadSweeps.Technical.Length];
                foreach (float side in new[] { 1f, -1f })
                {
                    var set = RoadSweeps.Mirrored(baseSet, side);
                    float forward = RoadSweeps.Forward(set);
                    float place = Mathf.Clamp(from + along - forward / 2f, from, to - forward);
                    if (place < from || !Clear(plan, place, set, others)) continue;
                    used[i] = true;
                    return (place, set, true);
                }
            }
            return null;
        }

        static (float place, Leg[] set, bool technical)? Sweep(PlanPath plan, float from, float to, int count, IReadOnlyList<IReadOnlyList<Vector2>> others)
        {
            // Long sweeps first (varying their angle from one to the next), medium ones where a long one won't fit.
            for (int d = 0; d < Sweeps.Length; d++)
            {
                var (make, degrees) = d < 3 ? Sweeps[(count + d) % 3] : Sweeps[d];
                foreach (float side in count % 2 == 0 ? new[] { 1f, -1f } : new[] { -1f, 1f })
                {
                    var set = make(degrees, side);
                    float forward = RoadSweeps.Forward(set);
                    if (forward > to - from) continue;
                    // A stretch with room for only one sits it in the middle; a long one starts it early to leave room for more.
                    float place = to - from < 2f * forward + Spacing ? from + (to - from - forward) / 2f : from;
                    if (Clear(plan, place, set, others)) return (place, set, false);
                }
            }
            return null;
        }

        static bool Clear(PlanPath plan, float place, Leg[] set, IReadOnlyList<IReadOnlyList<Vector2>> others)
        {
            plan.Evaluate(place, out Vector2 start, out Vector2 dir);
            foreach (var p in RoadSweeps.Points(start, dir, set))
            {
                if (Mathf.Abs(p.x) > MapEdge || Mathf.Abs(p.y) > MapEdge) return false;
                foreach (var road in others)
                    for (int i = 0; i + 1 < road.Count; i++)
                        if (DistanceToSegment(p, road[i], road[i + 1]) < Clearance) return false;
            }
            return true;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
