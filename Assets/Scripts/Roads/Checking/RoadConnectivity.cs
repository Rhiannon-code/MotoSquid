using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MotoSquid.Roads
{
    // The network as a rider sees it: every road end joins another road, every road belongs to one network, and
    // from any junction you can ride to any other without going the wrong way down a one-way road.
    public static class RoadConnectivity
    {
        public static List<RoadIssue> Check(IReadOnlyList<RoadPath> paths, IReadOnlyList<JunctionZone> zones)
        {
            var issues = new List<RoadIssue>();
            foreach (var path in paths.Where(p => !p.Closed))
            {
                if (!EndJoined(path, zones, true)) issues.Add(Issue(IssueKind.DeadEnd, path, 0f, "stops without joining another road"));
                if (!EndJoined(path, zones, false)) issues.Add(Issue(IssueKind.DeadEnd, path, path.Length, "stops without joining another road"));
            }

            var pieces = Pieces(paths, zones);
            var main = pieces.OrderByDescending(piece => piece.Sum(p => p.Length)).First();
            foreach (var piece in pieces.Where(piece => piece != main))
                issues.Add(Issue(IssueKind.Disconnected, piece[0], 0f,
                    $"cut off from the main network along with {piece.Count - 1} other road(s): {string.Join(", ", piece.Select(p => p.Name))}"));

            var mainZones = zones.Where(z => main.Contains(z.Parts[0].Path)).ToList();
            issues.AddRange(OneWayTraps(main, mainZones));
            return issues;
        }

        static bool EndJoined(RoadPath path, IReadOnlyList<JunctionZone> zones, bool start) =>
            zones.Any(z => z.Parts.Any(part => part.Path == path &&
                (start ? part.From <= RoadSurfaceBuilder.EndReach : part.To >= path.Count - 1 - RoadSurfaceBuilder.EndReach)));

        static List<List<RoadPath>> Pieces(IReadOnlyList<RoadPath> paths, IReadOnlyList<JunctionZone> zones)
        {
            var piece = paths.ToDictionary(p => p, p => p);
            RoadPath Root(RoadPath p) => piece[p] == p ? p : piece[p] = Root(piece[p]);
            foreach (var zone in zones)
                foreach (var part in zone.Parts)
                    piece[Root(part.Path)] = Root(zone.Parts[0].Path);
            return paths.GroupBy(Root).Select(g => g.ToList()).ToList();
        }

        // Junctions are nodes and the road between two neighbouring junctions is a link, one way or both by its
        // lanes. Inside a junction every road is assumed to reach every other, so only the links can trap a rider.
        static IEnumerable<RoadIssue> OneWayTraps(List<RoadPath> paths, List<JunctionZone> zones)
        {
            if (zones.Count < 2) yield break;
            var ahead = zones.Select(_ => new List<int>()).ToArray();
            var behind = zones.Select(_ => new List<int>()).ToArray();
            foreach (var path in paths)
            {
                var along = zones.Select((zone, index) => (index, part: zone.Parts.FirstOrDefault(p => p.Path == path)))
                                 .Where(z => z.part.Path != null).OrderBy(z => z.part.From).Select(z => z.index).ToList();
                int links = path.Closed ? along.Count : along.Count - 1;
                for (int i = 0; i < links && along.Count > 1; i++)
                {
                    int a = along[i], b = along[(i + 1) % along.Count];
                    if (path.Settings.lanesForward > 0) { ahead[a].Add(b); behind[b].Add(a); }
                    if (path.Settings.lanesBackward > 0) { ahead[b].Add(a); behind[a].Add(b); }
                }
            }

            var core = LargestLoop(zones.Count, ahead, behind);
            var reached = Reach(core.First(), ahead);
            for (int z = 0; z < zones.Count; z++)
            {
                if (core.Contains(z)) continue;
                var part = zones[z].Parts[0];
                string detail = !reached[z] ? "can't be reached from the rest of the network"
                                            : "has no way back out to the rest of the network";
                yield return Issue(IssueKind.OneWayTrap, part.Path, (part.From + part.To) / 2 * part.Path.Spacing,
                                   $"junction ({RoadSurfaceBuilder.Describe(zones[z])}) {detail}");
            }
        }

        static HashSet<int> LargestLoop(int count, List<int>[] ahead, List<int>[] behind)
        {
            var best = new HashSet<int>();
            var placed = new bool[count];
            for (int seed = 0; seed < count; seed++)
            {
                if (placed[seed]) continue;
                var there = Reach(seed, ahead);
                var back = Reach(seed, behind);
                var loop = new HashSet<int>(Enumerable.Range(0, count).Where(z => there[z] && back[z]));
                foreach (int z in loop) placed[z] = true;
                if (loop.Count > best.Count) best = loop;
            }
            return best;
        }

        static bool[] Reach(int from, List<int>[] links)
        {
            var seen = new bool[links.Length];
            var open = new Stack<int>();
            open.Push(from);
            seen[from] = true;
            while (open.Count > 0)
                foreach (int next in links[open.Pop()])
                    if (!seen[next]) { seen[next] = true; open.Push(next); }
            return seen;
        }

        static RoadIssue Issue(IssueKind kind, RoadPath path, float station, string detail) => new RoadIssue
        {
            Kind = kind, Road = path.Name, Lane = -1, Station = station,
            Position = path.PointAt(station, 0f), Value = 1f, Limit = 0f, Detail = detail
        };
    }
}
