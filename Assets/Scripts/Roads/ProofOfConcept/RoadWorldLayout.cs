using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MotoSquid.Roads.LayoutLegs;
using static MotoSquid.Roads.RoadDesign;

namespace MotoSquid.Roads
{
    [Serializable]
    public class WorldSettings
    {
        [Tooltip("Smallest corner radius a road of each class is drawn with where its real route turns (m): freeway, highway, arterial.")]
        public float freewayMinRadius = 250f;
        public float highwayMinRadius = 150f;
        public float arterialMinRadius = 80f;
        [Tooltip("Largest corner radius (m): wide real curves are drawn no gentler than this.")]
        public float maxRadius = 1200f;
    }

    // The open world: every real road (RoadWorldData) redrawn in the game's shapes. Each corner of a real
    // route becomes a straight-arc-straight fillet, as round as its neighbours allow. Freeways meet nothing at
    // grade: wherever another road crosses one, the freeway drops into a cutting (or, where the real freeway
    // is a bridge, rises onto walls) and stays level across, so the crossing road passes over or under it.
    // Everything else meets at grade. Both directions share one surface on every road (no divider).
    public static class RoadWorldLayout
    {
        const float SampleStep = 5f;
        const float CrossingMargin = 15f;
        const float SpanMargin = 60f;
        // The freeway's level changes run a little under its grade limit: the outer lane of a bend is longer
        // or shorter than the centreline, so its grade differs.
        const float ChangeGrade = FreewayGrade - 0.005f;

        sealed class Planned
        {
            public RoadWorldData.Road Source;
            public Vector2 Start, Direction;
            public Leg[] Legs;
            public PlanPath Plan;
            public RoadSettings Settings;
            public List<Vector2> Samples;
            public readonly List<(float from, float to, float height)> Levels = new List<(float, float, float)>();
            public readonly List<float> Meetings = new List<float>();
        }

        public static List<RoadPocLayout.RoadDefinition> Roads(RoadWorldData data, WorldSettings settings, List<string> report = null)
        {
            report ??= new List<string>();
            var planned = new List<Planned>();
            foreach (var road in data.Roads)
            {
                if (road.Points.Count < 2) continue;
                var (start, direction, legs) = RoadFillet.Fillet(road.Points, MinRadius(road.Class, settings), settings.maxRadius);
                var p = new Planned { Source = road, Start = start, Direction = direction, Legs = legs, Settings = SettingsFor(road) };
                p.Plan = Build(start, direction, legs);
                p.Samples = Sample(p.Plan);
                planned.Add(p);
            }

            SeparateFreeways(planned, report);
            FindMeetings(planned);

            var roads = new List<RoadPocLayout.RoadDefinition>();
            foreach (var p in planned) roads.Add(Definition(p));
            return roads;
        }

        static float MinRadius(int roadClass, WorldSettings s) =>
            roadClass == 0 ? s.freewayMinRadius : roadClass == 1 ? s.highwayMinRadius : s.arterialMinRadius;

        static RoadSettings SettingsFor(RoadWorldData.Road road)
        {
            if (road.Class == 0) return RoadSettings.FreewayTwoWay();
            if (road.Lanes >= 2) return RoadSettings.Highway();
            return new RoadSettings { lanesForward = 1, lanesBackward = 1 };
        }

        // A real route's corners as fillets: at each vertex an arc tangent to both neighbouring segments, its
        // radius the largest that fits in half of each (so neighbouring fillets never overlap), capped at
        // maxRadius. Corners too sharp for minRadius in the room available keep the tighter radius.
        static List<Vector2> Sample(PlanPath plan)
        {
            var samples = new List<Vector2>();
            int count = Mathf.Max(2, Mathf.CeilToInt(plan.Length / SampleStep) + 1);
            for (int k = 0; k < count; k++)
            {
                plan.Evaluate(plan.Length * k / (count - 1), out Vector2 p, out _);
                samples.Add(p);
            }
            return samples;
        }

        // Every crossing of a freeway by another road becomes a level stretch of the freeway below (or above)
        // the crossing road, joined by the freeway's own grade changes; stretches whose changes would touch
        // merge into one. Where two freeways cross, only the first one listed changes level.
        static void SeparateFreeways(List<Planned> planned, List<string> report)
        {
            float change = VerticalProfile.ChangeLength(LevelSeparation, ChangeGrade, FreewayVerticalRadius);
            for (int f = 0; f < planned.Count; f++)
            {
                var freeway = planned[f];
                if (freeway.Source.Class != 0) continue;
                var spans = new List<(float from, float to, float vote)>();
                int atGrade = 0;
                var joins = Joins(freeway, planned);
                for (int o = 0; o < planned.Count; o++)
                {
                    if (o == f) continue;
                    var other = planned[o];
                    if (other.Source.Class == 0 && o < f) continue;
                    foreach (var (station, angle) in Crossings(freeway, other))
                    {
                        // Too near the freeway's end to climb or drop in time: where a freeway ends it is at
                        // street level anyway, so the two meet there.
                        if (station < change + SpanMargin || station > freeway.Plan.Length - change - SpanMargin)
                        {
                            atGrade++;
                            continue;
                        }
                        float half = other.Settings.Width / 2f / Mathf.Max(0.3f, Mathf.Sin(angle * Mathf.Deg2Rad)) + CrossingMargin;
                        // Another road joins the freeway at grade within the climb or drop: the freeway has
                        // to be at street level there, so this crossing meets at grade as well.
                        if (joins.Any(j => j > station - half - change - SpanMargin && j < station + half + change + SpanMargin))
                        {
                            atGrade++;
                            continue;
                        }
                        spans.Add((station - half, station + half, ElevatedShare(freeway, station - half, station + half)));
                    }
                }
                spans.Sort((a, b) => a.from.CompareTo(b.from));
                var merged = new List<(float from, float to, float raised, float weight)>();
                foreach (var s in spans)
                {
                    if (merged.Count > 0 && s.from - merged[merged.Count - 1].to < 2f * change + SpanMargin)
                    {
                        var m = merged[merged.Count - 1];
                        float w = s.to - s.from;
                        merged[merged.Count - 1] = (m.from, Mathf.Max(m.to, s.to), m.raised + s.vote * w, m.weight + w);
                    }
                    else merged.Add((s.from, s.to, s.vote * (s.to - s.from), s.to - s.from));
                }
                foreach (var m in merged)
                {
                    float height = m.raised / Mathf.Max(m.weight, 1e-3f) > 0.5f ? LevelSeparation : -LevelSeparation;
                    freeway.Levels.Add((Mathf.Max(0f, m.from), Mathf.Min(freeway.Plan.Length, m.to), height));
                }
                report.Add($"{freeway.Source.Name} ({freeway.Source.Id}): {merged.Count} level stretches for {spans.Count} crossings, {atGrade} met at grade near its ends");
            }
        }

        // Every station where a road meets another at grade: its own ends where they touch a road, other
        // roads' ends on it, and crossings neither side is separated at. Junctions run level through these.
        static void FindMeetings(List<Planned> planned)
        {
            foreach (var road in planned)
            {
                road.Meetings.AddRange(Joins(road, planned));
                foreach (var (station, _) in planned.SelectMany(other => other == road ? Enumerable.Empty<(float, float)>() : Crossings(road, other)))
                    if (!road.Levels.Any(l => station >= l.from && station <= l.to))
                        road.Meetings.Add(station);
            }
            foreach (var road in planned)
                foreach (var (end, station) in new[] { (road.Samples[0], 0f), (road.Samples[road.Samples.Count - 1], road.Plan.Length) })
                    if (planned.Any(other => other != road && other.Samples.Any(q => Vector2.Distance(q, end) < (road.Settings.Width + other.Settings.Width) / 2f)))
                        road.Meetings.Add(station);
        }

        // Stations along a road where another road starts or ends on it (an at-grade join).
        static List<float> Joins(Planned road, List<Planned> planned)
        {
            var stations = new List<float>();
            float step = road.Plan.Length / (road.Samples.Count - 1);
            foreach (var other in planned)
            {
                if (other == road) continue;
                float reach = road.Settings.Width / 2f + other.Settings.Width / 2f;
                foreach (var end in new[] { other.Samples[0], other.Samples[other.Samples.Count - 1] })
                {
                    int best = -1;
                    float bestDistance = reach;
                    for (int i = 0; i < road.Samples.Count; i++)
                    {
                        float d = Vector2.Distance(road.Samples[i], end);
                        if (d < bestDistance) { bestDistance = d; best = i; }
                    }
                    if (best >= 0) stations.Add(best * step);
                }
            }
            return stations;
        }

        static float ElevatedShare(Planned road, float from, float to)
        {
            // Fraction of the real route's points near this stretch that were real bridges.
            var src = road.Source;
            road.Plan.Evaluate(Mathf.Clamp((from + to) / 2f, 0f, road.Plan.Length), out Vector2 centre, out _);
            int near = 0, raised = 0;
            float reach = Mathf.Max(60f, (to - from) / 2f);
            for (int i = 0; i < src.Points.Count; i++)
                if (Vector2.Distance(src.Points[i], centre) < reach)
                {
                    near++;
                    if (src.Elevated[i]) raised++;
                }
            return near == 0 ? 0f : raised / (float)near;
        }

        // Where `other` crosses `road`'s centreline: the station along `road` and the crossing angle (degrees).
        static IEnumerable<(float station, float angle)> Crossings(Planned road, Planned other)
        {
            var a = road.Samples;
            var b = other.Samples;
            if (!BoundsOverlap(a, b)) yield break;
            float stepA = road.Plan.Length / (a.Count - 1);
            for (int i = 0; i < a.Count - 1; i++)
                for (int j = 0; j < b.Count - 1; j++)
                    if (Intersect(a[i], a[i + 1], b[j], b[j + 1], out float t))
                    {
                        float angle = Vector2.Angle(a[i + 1] - a[i], b[j + 1] - b[j]);
                        yield return ((i + t) * stepA, Mathf.Min(angle, 180f - angle));
                    }
        }

        static bool BoundsOverlap(List<Vector2> a, List<Vector2> b)
        {
            float ax0 = a.Min(p => p.x), ax1 = a.Max(p => p.x), az0 = a.Min(p => p.y), az1 = a.Max(p => p.y);
            float bx0 = b.Min(p => p.x), bx1 = b.Max(p => p.x), bz0 = b.Min(p => p.y), bz1 = b.Max(p => p.y);
            return ax0 <= bx1 && bx0 <= ax1 && az0 <= bz1 && bz0 <= az1;
        }

        static bool Intersect(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, out float t)
        {
            t = 0f;
            Vector2 r = p2 - p, s = q2 - q;
            float denom = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denom) < 1e-9f) return false;
            Vector2 qp = q - p;
            t = (qp.x * s.y - qp.y * s.x) / denom;
            float u = (qp.x * r.y - qp.y * r.x) / denom;
            return t >= 0f && t < 1f && u >= 0f && u < 1f;
        }

        static RoadPocLayout.RoadDefinition Definition(Planned p)
        {
            var settings = p.Settings.Clone();
            settings.maxRollRate = Mathf.Min(settings.maxRollRate, RollRateFor(settings.Width));
            var profile = new VerticalProfile().Point(0f, 0f);
            float change = VerticalProfile.ChangeLength(LevelSeparation, ChangeGrade, FreewayVerticalRadius);
            foreach (var (from, to, height) in p.Levels)
            {
                profile.Step(Mathf.Max(0f, from - change), height, ChangeGrade, FreewayVerticalRadius).Step(to, 0f, ChangeGrade, FreewayVerticalRadius);
                settings.bankZones.Add(new BankZone { from = from - change - RollClear, to = to + change + RollClear, degrees = 0f });
            }
            float clear = settings.Width / 2f + RollClear;
            foreach (float station in p.Meetings)
                settings.bankZones.Add(new BankZone { from = station - clear, to = station + clear, degrees = 0f });
            profile.Level();
            var recipe = RoadRecipe.From(p.Start, p.Direction, p.Legs, profile);
            return new RoadPocLayout.RoadDefinition { Name = $"{p.Source.Name} ({p.Source.Id})", Knots = recipe.Knots(), Settings = settings, Recipe = recipe };
        }
    }
}
