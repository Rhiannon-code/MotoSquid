using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MotoSquid.Roads.RoadDesign;

namespace MotoSquid.Roads
{
    // Puts each World Plan site's interchange in place: the feature is drawn in its own frame (its through road along
    // x, the other road along z), turned and moved onto the site, and each road is told what it needs there: a
    // straight through the footprint, or a straight approach to where it ends, and any change of level.
    public static class WorldInterchanges
    {
        const float RampGrade = 0.07f;
        // Free straight beyond a footprint on each side, for the route's own rounding to land on.
        public const float Margin = 150f;
        // A knot this close to a road's line belongs to the footprint along it.
        const float Band = 60f;
        // The compact T's ramps join its through road right at the ends of its footprint, so the through road stays
        // straight a little further: a bend starting at the junction's edge kinks the ramp's join.
        const float TBuffer = 80f;
        // Between a half diamond's street end and the freeway's own width: room for the street's end not to join it.
        const float HalfDiamondGap = 1f;

        public static void Place(WorldSite site, IReadOnlyDictionary<string, WorldRoute> routes, List<RoadPocLayout.RoadDefinition> features, List<string> report)
        {
            if (site.roads.Count < 2 || !routes.TryGetValue(site.roads[0], out var first) || !routes.TryGetValue(site.roads[1], out var second))
            {
                report?.Add($"Site '{site.name}' names a road that isn't in the plan; skipped.");
                return;
            }
            switch (site.kind)
            {
                case WorldSiteKind.Cloverstack: Cloverstack(site, street: first, cutting: second, features, report); break;
                case WorldSiteKind.T: Ending(site, first, second, features, compactT: true, report); break;
                case WorldSiteKind.MergeSplit: Ending(site, first, second, features, compactT: false, report); break;
                case WorldSiteKind.Continuation: Continuation(site, first, second); break;
                case WorldSiteKind.Diamond: Diamond(site, freeway: first, street: second, features, report); break;
                case WorldSiteKind.HalfDiamond: HalfDiamond(site, freeway: first, street: second, features, report); break;
            }
        }

        // The two roads are turned the same amount each way until they cross square.
        static void Cloverstack(WorldSite site, WorldRoute street, WorldRoute cutting, List<RoadPocLayout.RoadDefinition> features, List<string> report)
        {
            cutting.StationOf(site.at, out Vector2 tA);
            street.StationOf(site.at, out Vector2 tB);
            if (Cross(tA, tB) < 0f) tB = -tB;
            float apart = Vector2.Angle(tA, tB);
            Vector2 x = Rotate(tA, -(90f - apart) / 2f), z = Left(x);

            float depth = -LevelSeparation;
            var ramps = RoadFeatures.Cloverstack(depth, LevelSeparation, RampGrade, out float core);
            var (xMin, xMax) = Reach(ramps, alongX: true);
            var (zMin, zMax) = Reach(ramps, alongX: false);
            cutting.Through(site.name, site.at + x * (xMin - Margin), site.at + x * (xMax + Margin), xMax - xMin);
            street.Through(site.name, site.at + z * (zMin - Margin), site.at + z * (zMax + Margin), zMax - zMin);
            float change = VerticalProfile.ChangeLength(depth, FreewayGrade, FreewayVerticalRadius);
            report?.Add($"footprint {site.name}: {cutting.Name} {xMin:0}..{xMax:0} m, {street.Name} {zMin:0}..{zMax:0} m, turned {(90f - apart) / 2f:0.0}° each");
            cutting.Rises(site.at, -core - change, depth);
            cutting.Rises(site.at, core, 0f);
            features.AddRange(ramps.Select(r => Placed(r, site, x, ("A", Short(cutting.Name, site)), ("B", Short(street.Name, site)))));
        }

        // A zone's way on and off a freeway: the freeway keeps its line and drops into a cutting, the cross street
        // crosses it square at street level on a bridge, and the four ramps climb out of the cutting to T-junctions on it.
        static void Diamond(WorldSite site, WorldRoute freeway, WorldRoute street, List<RoadPocLayout.RoadDefinition> features, List<string> report)
        {
            freeway.StationOf(site.at, out Vector2 x);
            Vector2 z = Left(x);
            var grades = new DiamondSettings
            {
                freewayHeight = -LevelSeparation,
                eastboundOffRamp = RampGrade, eastboundOnRamp = RampGrade, westboundOffRamp = RampGrade, westboundOnRamp = RampGrade,
            };
            var ramps = RoadFeatures.DiamondRamps(Short(freeway.Name, site), 0f, grades, RoadSettings.Arterial().Width / 2f).ToList();
            var (xMin, xMax) = Reach(ramps, alongX: true);
            var (zMin, zMax) = Reach(ramps, alongX: false);
            float bridge = RoadSettings.FreewayTwoWay().Width / 2f + RoadFeatures.BridgeClear;
            zMin = Mathf.Min(zMin, -bridge);
            zMax = Mathf.Max(zMax, bridge);
            freeway.Through(site.name, site.at + x * (xMin - Margin), site.at + x * (xMax + Margin), xMax - xMin);
            street.Through(site.name, site.at + z * (zMin - Margin), site.at + z * (zMax + Margin), zMax - zMin);
            // The climb out starts clear of the ramp junctions: inside one the surface is held at the junction's level.
            float core = Mathf.Max(-xMin, xMax) + RollClear + RoadFeatures.MergeLevel;
            float change = VerticalProfile.ChangeLength(LevelSeparation, FreewayGrade, FreewayVerticalRadius);
            freeway.Rises(site.at, -core - change, -LevelSeparation);
            freeway.Rises(site.at, core, 0f);
            report?.Add($"footprint {site.name}: {freeway.Name} {xMin:0}..{xMax:0} m (cutting {core + change:0} m either side), {street.Name} {zMin:0}..{zMax:0} m");
            features.AddRange(ramps.Select(r => Placed(r, site, x)));
        }

        // A zone street ending beside a freeway that has nothing on its far side (or no room for a full diamond):
        // just the off and on ramps on the street's side, level with the freeway, meeting the street's end.
        static void HalfDiamond(WorldSite site, WorldRoute freeway, WorldRoute street, List<RoadPocLayout.RoadDefinition> features, List<string> report)
        {
            freeway.StationOf(site.at, out Vector2 x);
            bool atStart = (street.Start - site.at).sqrMagnitude < (street.End - site.at).sqrMagnitude;
            Vector2 inward = street.PointAt(atStart ? Mathf.Min(400f, street.Length) : Mathf.Max(0f, street.Length - 400f));
            if (Cross(x, inward - site.at) < 0f) x = -x;
            Vector2 z = Left(x);
            // The street ends across the ramps' ends (their centre line is the knots' reach), and so must stay clear of the
            // freeway: nearer than half of each road's width apart and the two would join.
            float streetHalf = RoadSettings.Arterial().Width / 2f, rampHalf = RoadSettings.Ramp().Width / 2f;
            float rampLine = RoadFeatures.LeftLane(RoadSettings.FreewayTwoWay()) + RoadFeatures.Clear(RoadSettings.FreewayTwoWay(), RoadSettings.Ramp());
            float wider = Mathf.Max(0f, RoadSettings.FreewayTwoWay().Width / 2f + streetHalf + HalfDiamondGap + rampHalf - rampLine);
            var ramps = RoadFeatures.HalfDiamondRamps(Short(freeway.Name, site), streetHalf, RampGrade, wider).ToList();
            var (xMin, xMax) = Reach(ramps, alongX: true);
            var (_, zMax) = Reach(ramps, alongX: false);
            float end = zMax - rampHalf;
            freeway.Through(site.name, site.at + x * (xMin - Margin), site.at + x * (xMax + Margin), xMax - xMin);
            street.EndsAt(site.name, atStart, site.at + z * (end + RollClear + Margin), site.at + z * end, RollClear);
            report?.Add($"footprint {site.name}: {freeway.Name} {xMin:0}..{xMax:0} m, {street.Name} ends {end:0} m out");
            features.AddRange(ramps.Select(r => Placed(r, site, x)));
        }

        // A road ending on another, with the compact T or a merge / split. The feature is drawn with the ending road
        // arriving from +z, so the through road's direction is chosen to put the ending road on its left.
        static void Ending(WorldSite site, WorldRoute ending, WorldRoute through, List<RoadPocLayout.RoadDefinition> features, bool compactT, List<string> report)
        {
            through.StationOf(site.at, out Vector2 x);
            bool atStart = (ending.Start - site.at).sqrMagnitude < (ending.End - site.at).sqrMagnitude;
            Vector2 inward = ending.PointAt(atStart ? Mathf.Min(400f, ending.Length) : Mathf.Max(0f, ending.Length - 400f));
            if (Cross(x, inward - site.at) < 0f) x = -x;
            Vector2 z = Left(x);

            List<RoadPocLayout.RoadDefinition> ramps;
            float end, straightFrom;
            if (compactT)
            {
                ramps = RoadFeatures.CompactT(LevelSeparation, RampGrade, out float stemLevel);
                end = RoadFeatures.StemEnd;
                float change = VerticalProfile.ChangeLength(LevelSeparation, FreewayGrade, FreewayVerticalRadius);
                straightFrom = end + stemLevel + change + RollClear;
                Vector2 endPoint = site.at + z * end;
                if (atStart)
                {
                    ending.StartsAt(LevelSeparation);
                    ending.Rises(endPoint, stemLevel, 0f);
                }
                else ending.Rises(endPoint, -stemLevel - change, LevelSeparation);
            }
            else
            {
                ramps = RoadMergeSplit.Roads(Short(ending.Name, site), LevelSeparation, RampGrade, out end);
                straightFrom = end + RollClear;
            }
            var (xMin, xMax) = Reach(ramps, alongX: true);
            if (compactT) { xMin -= TBuffer; xMax += TBuffer; }
            report?.Add($"footprint {site.name}: {through.Name} {xMin:0}..{xMax:0} m, {ending.Name} last {straightFrom:0} m");
            through.Through(site.name, site.at + x * (xMin - Margin), site.at + x * (xMax + Margin), xMax - xMin);
            ending.EndsAt(site.name, atStart, site.at + z * (straightFrom + Margin), site.at + z * end, straightFrom - end);
            features.AddRange(ramps.Select(r => Placed(r, site, x, ("CityLink", Short(ending.Name, site)), ("West Gate", Short(through.Name, site)))));
        }

        // Two roads carrying on into each other end on one shared straight line, so their ends meet exactly: a few
        // degrees apart, the joined outline gets a spike, and a wall follows it into the road.
        const float ContinuationStraight = 200f;

        static void Continuation(WorldSite site, WorldRoute a, WorldRoute b)
        {
            Vector2 Inward(WorldRoute r, out bool atStart)
            {
                atStart = (r.Start - site.at).sqrMagnitude < (r.End - site.at).sqrMagnitude;
                Vector2 inside = r.PointAt(atStart ? Mathf.Min(ContinuationStraight, r.Length) : Mathf.Max(0f, r.Length - ContinuationStraight));
                return (inside - site.at).normalized;
            }
            Vector2 intoA = Inward(a, out bool aAtStart), intoB = Inward(b, out bool bAtStart);
            Vector2 line = (intoA - intoB).normalized;
            a.EndsAt(site.name, aAtStart, site.at + line * (ContinuationStraight + Margin), site.at, ContinuationStraight);
            b.EndsAt(site.name, bAtStart, site.at - line * (ContinuationStraight + Margin), site.at, ContinuationStraight);
        }

        // How far a feature's roads reach along its x road (knots near z = 0) or its z road (knots near x = 0).
        static (float min, float max) Reach(IEnumerable<RoadPocLayout.RoadDefinition> roads, bool alongX)
        {
            float min = 0f, max = 0f;
            foreach (var road in roads)
                foreach (var knot in road.Knots)
                {
                    float along = alongX ? knot.Position.x : knot.Position.z, across = alongX ? knot.Position.z : knot.Position.x;
                    if (Mathf.Abs(across) > Band) continue;
                    min = Mathf.Min(min, along);
                    max = Mathf.Max(max, along);
                }
            return (min, max);
        }

        static RoadPocLayout.RoadDefinition Placed(RoadPocLayout.RoadDefinition road, WorldSite site, Vector2 x, params (string drawn, string real)[] names)
        {
            var recipe = road.Recipe;
            Vector2 local = recipe.start;
            recipe.start = site.at + x * local.x + Left(x) * local.y;
            float turn = Mathf.Atan2(x.y, x.x) * Mathf.Rad2Deg;
            recipe.heading += turn;
            road.Name = $"{site.name}: {Renamed(road.Name, turn, names)}";
            road.Knots = recipe.Knots();
            return road;
        }

        // A feature's ramps are named for the roads and compass directions it was drawn with (x east, z north); placed,
        // they take the real roads' names and the directions the feature now faces.
        static string Renamed(string name, float turn, (string drawn, string real)[] names)
        {
            var words = new List<(string from, string to)>();
            string[] compass = { "eastbound", "northbound", "westbound", "southbound" };
            for (int i = 0; i < 4; i++)
                words.Add((compass[i], compass[((i + Mathf.RoundToInt(turn / 90f)) % 4 + 4) % 4]));
            words.AddRange(names.Select(n => (n.drawn, n.real)));
            var tokens = words.Select((w, i) => (w, token: $"\u0001{i}\u0001")).ToList();
            foreach (var (w, token) in tokens) name = System.Text.RegularExpressions.Regex.Replace(name, $@"\b{w.from}\b", token);
            foreach (var (w, token) in tokens) name = name.Replace(token, w.to);
            return name;
        }

        // A combined road ("West Gate / Monash") goes by the part of its name the site uses ("Monash x EastLink").
        static string Short(string road, WorldSite site)
        {
            var parts = road.Split('/').Select(p => p.Trim()).ToList();
            return parts.FirstOrDefault(p => site.name.Contains(p.Replace(" Freeway", ""))) ?? parts[0];
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static Vector2 Left(Vector2 v) => new Vector2(-v.y, v.x);
        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
