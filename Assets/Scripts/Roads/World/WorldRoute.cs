using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MotoSquid.Roads
{
    // One World Plan road while interchanges are placed on it: its route as corner points, the straights the
    // interchanges claim (each replacing the stretch of route it sits on), and the height changes they need.
    public sealed class WorldRoute
    {
        const float MaxRadius = 1200f;
        const float StreetMaxRadius = 300f;

        sealed class Straight { public float From, To; public Vector2[] Points; public float Keep; public string Site; }
        sealed class Rise { public Vector2 Centre; public float Offset; public float Height; public float Grade = RoadDesign.FreewayGrade; public bool Own; }

        public readonly string Name;
        public readonly IReadOnlyList<Vector2> Points;
        readonly List<Vector2> points;
        readonly bool sweeps;
        readonly List<Vector2> technical;
        readonly float[] stations;
        readonly float grade;
        readonly WorldRoadKind kind;
        readonly List<Straight> straights = new List<Straight>();
        readonly List<Rise> rises = new List<Rise>();
        float startHeight;

        public WorldRoute(WorldRoad road)
        {
            Name = road.name;
            points = new List<Vector2>(road.points);
            Points = points;
            sweeps = road.sweeps;
            technical = new List<Vector2>(road.technical ?? new List<Vector2>());
            grade = road.grade > 0f ? road.grade : RoadDesign.FreewayGrade;
            kind = road.kind;
            foreach (var level in road.levels ?? new List<WorldLevel>())
            {
                float change = VerticalProfile.ChangeLength(level.height, grade, RoadDesign.FreewayVerticalRadius);
                rises.Add(new Rise { Centre = level.from, Offset = -change, Height = level.height, Grade = grade, Own = true });
                rises.Add(new Rise { Centre = level.to, Offset = 0f, Height = 0f, Grade = grade, Own = true });
            }
            stations = new float[points.Count];
            for (int i = 1; i < points.Count; i++) stations[i] = stations[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }

        public WorldRoadKind Kind => kind;
        public float Length => stations[stations.Length - 1];
        // The stretches interchanges have claimed so far (metres along the route, in its travel order).
        public IEnumerable<(string site, float from, float to)> Claimed => straights.Select(s => (s.Site, s.From, s.To));
        public Vector2 Start => points[0];
        public Vector2 End => points[points.Count - 1];

        // Where on the route a point falls (metres along it) and which way the route runs there.
        public float StationOf(Vector2 p, out Vector2 direction)
        {
            float best = float.MaxValue, station = 0f;
            direction = Vector2.right;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i], ab = points[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best) { best = d; station = stations[i] + t * ab.magnitude; direction = ab.normalized; }
            }
            return station;
        }

        public Vector2 PointAt(float station)
        {
            for (int i = 0; i < points.Count - 1; i++)
                if (station <= stations[i + 1] || i == points.Count - 2)
                    return Vector2.Lerp(points[i], points[i + 1], Mathf.Clamp01((station - stations[i]) / Mathf.Max(stations[i + 1] - stations[i], 1e-6f)));
            return End;
        }

        // A feature that the road runs straight through: the route between `from` and `to` (in its travel order)
        // becomes one straight, of which the middle `keep` metres must stay straight.
        public void Through(string site, Vector2 from, Vector2 to, float keep)
        {
            float a = StationOf(from, out _), b = StationOf(to, out _);
            if (a > b) (from, to, a, b) = (to, from, b, a);
            straights.Add(new Straight { Site = site, From = a, To = b, Points = new[] { from, to }, Keep = keep });
        }

        // A feature the road ends at: from `far` (on its approach) straight to `end`, the last `keep` metres kept straight.
        public void EndsAt(string site, bool atStart, Vector2 far, Vector2 end, float keep)
        {
            float s = StationOf(far, out _);
            straights.Add(atStart
                ? new Straight { Site = site, From = 0f, To = s, Points = new[] { end, far }, Keep = keep }
                : new Straight { Site = site, From = s, To = Length, Points = new[] { far, end }, Keep = keep });
        }

        // The road reaches `height` (from street level, or back down to it) starting `offset` metres from where it
        // passes `centre`; a road that starts on a bridge gives StartHeight instead.
        public void Rises(Vector2 centre, float offset, float height) => rises.Add(new Rise { Centre = centre, Offset = offset, Height = height });
        public void StartsAt(float height) => startHeight = height;

        public RoadPocLayout.RoadDefinition Build(List<string> report, IReadOnlyList<IReadOnlyList<Vector2>> others)
        {
            var ordered = straights.OrderBy(s => s.From).ToList();
            for (int i = 1; i < ordered.Count; i++)
                if (ordered[i].From < ordered[i - 1].To - 1f)
                    report?.Add($"{Name}: the {ordered[i - 1].Site} and {ordered[i].Site} interchanges overlap by {ordered[i - 1].To - ordered[i].From:0} m along it.");

            var route = new List<Vector2>();
            var keep = new List<float>();
            void Add(Vector2 p, float keptBefore)
            {
                if (route.Count > 0) keep.Add(keptBefore);
                route.Add(p);
            }
            int next = 0;
            for (int i = 0; i < points.Count; i++)
            {
                while (next < ordered.Count && ordered[next].From <= stations[i])
                {
                    var s = ordered[next++];
                    Add(s.Points[0], 0f);
                    Add(s.Points[1], s.Keep);
                }
                if (ordered.Any(s => stations[i] >= s.From - 1f && stations[i] <= s.To + 1f)) continue;
                Add(points[i], 0f);
            }
            while (next < ordered.Count) { var s = ordered[next++]; Add(s.Points[0], 0f); Add(s.Points[1], s.Keep); }
            JoinCloseStraights(route, keep);

            var (start, direction, legs) = RoadFillet.Fillet(route, 0f, kind == WorldRoadKind.Freeway ? MaxRadius : StreetMaxRadius, keep);
            // Each kept straight sits in the middle of its leg, between the two free margins.
            List<(float from, float to)> Kept(PlanPath on)
            {
                var spans = new List<(float from, float to)>();
                for (int i = 0; i + 1 < route.Count; i++)
                    if (keep[i] > 0f)
                    {
                        float centre = StationOn(on, (route[i] + route[i + 1]) / 2f);
                        spans.Add((centre - keep[i] / 2f - WorldInterchanges.Margin, centre + keep[i] / 2f + WorldInterchanges.Margin));
                    }
                return spans;
            }
            legs = WorldSweeps.Add(Name, start, direction, legs, Kept(LayoutLegs.Build(start, direction, legs)), sweeps, technical, others, report);
            var plan = LayoutLegs.Build(start, direction, legs);
            return RoadFeatures.Definition(Name, start, direction, legs, Profile(plan, Kept(plan), report), Settings(kind), grade);
        }

        static RoadSettings Settings(WorldRoadKind kind)
        {
            if (kind == WorldRoadKind.Arterial) return RoadSettings.Arterial();
            if (kind == WorldRoadKind.Street) return RoadSettings.Street();
            // Level, as in the corridor: rolling a 42 m road into a lean between two interchanges lifts its outer lanes.
            var freeway = RoadSettings.FreewayTwoWay();
            freeway.maxBank = 0f;
            return freeway;
        }

        // Two straights with only a short leg between them would squeeze their corner into that leg. Their inner ends
        // become the one point where their lines meet, so the corner can use both straights' free margins.
        static void JoinCloseStraights(List<Vector2> route, List<float> keep)
        {
            const float Close = 400f;
            for (int i = 1; i + 2 < route.Count; i++)
            {
                if (keep[i - 1] <= 0f || keep[i + 1] <= 0f || keep[i] > 0f) continue;
                if (Vector2.Distance(route[i], route[i + 1]) > Close) continue;
                Vector2 a = route[i - 1], b = route[i], c = route[i + 1], d = route[i + 2];
                Vector2 r = b - a, q = d - c;
                float denominator = r.x * q.y - r.y * q.x;
                if (Mathf.Abs(denominator) < 1e-6f) { route.RemoveAt(i + 1); keep.RemoveAt(i); continue; }
                float t = ((c.x - a.x) * q.y - (c.y - a.y) * q.x) / denominator;
                Vector2 meet = a + r * t;
                float u = Vector2.Dot(meet - c, q) / q.sqrMagnitude;
                // Only where the lines meet inside both free margins: anywhere else the corner would bend a footprint.
                float margin = WorldInterchanges.Margin;
                if (t < 1f - margin / r.magnitude || u > margin / q.magnitude || Vector2.Distance(meet, b) > Close) continue;
                route[i] = meet;
                route.RemoveAt(i + 1);
                keep.RemoveAt(i);
            }
        }

        // `kept` holds the interchange footprints along the road: its own climbs must stay out of them.
        VerticalProfile Profile(PlanPath plan, List<(float from, float to)> kept, List<string> report)
        {
            var steps = rises.Select(r => (from: StationOn(plan, r.Centre) + r.Offset, r.Height, r.Grade, r.Own)).OrderBy(s => s.from).ToList();
            var profile = new VerticalProfile().Point(0f, startHeight);
            float height = startHeight, clearFrom = 0f;
            foreach (var (from, to, g, own) in steps)
            {
                if (from < clearFrom) report?.Add($"{Name}: height changes at {from:0} m start before the previous one has finished ({clearFrom:0} m).");
                float change = VerticalProfile.ChangeLength(to - height, g, RoadDesign.FreewayVerticalRadius);
                if (own)
                    foreach (var (a, b) in kept)
                        if (from < b && from + change > a) report?.Add($"{Name}: its climb to {to:0} m ({from:0}-{from + change:0} m) runs into an interchange's straight ({a:0}-{b:0} m).");
                profile.Step(Mathf.Max(from, 0f), to, g, RoadDesign.FreewayVerticalRadius);
                clearFrom = from + change;
                height = to;
            }
            return profile.Level();
        }

        static float StationOn(PlanPath plan, Vector2 p)
        {
            float best = float.MaxValue, station = 0f;
            for (float s = 0f; s <= plan.Length; s += 2f)
            {
                plan.Evaluate(s, out Vector2 q, out _);
                float d = (q - p).sqrMagnitude;
                if (d < best) { best = d; station = s; }
            }
            return station;
        }
    }
}
