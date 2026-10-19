using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    // Whether the layout works, from the roads alone, before any surface is built: roads that should meet do,
    // nothing overlaps without joining, nothing is cut off, and no corner is too tight. Heights only decide which
    // roads meet (a road passing over another is not a junction); grades, air and clearance need the built
    // surface and stay with the road checker.
    public static class RoadPlanCheck
    {
        const float IssueGap = 50f;
        // The same 10% the road checker allows over its limits: bent features squeeze their loops a little.
        const float RadiusTolerance = 1.1f;

        public static List<RoadIssue> Run(IReadOnlyList<RoadPath> paths, out List<JunctionZone> zones)
        {
            var issues = new List<RoadIssue>();
            var conflicts = new List<string>();
            var conflictsAt = new List<(RoadPath road, float station, Vector3 at)>();
            zones = RoadSurfaceBuilder.FindZones(paths, conflicts, conflictsAt);
            for (int i = 0; i < conflicts.Count; i++)
                issues.Add(new RoadIssue
                {
                    Kind = IssueKind.Conflict, Road = conflictsAt[i].road.Name, Lane = -1, Station = conflictsAt[i].station,
                    Position = conflictsAt[i].at, Value = 1f, Limit = 0f, Detail = conflicts[i]
                });
            if (paths.Count > 0) issues.AddRange(RoadConnectivity.Check(paths, zones));
            foreach (var path in paths) TightCorners(path, issues);
            return issues;
        }

        static void TightCorners(RoadPath path, List<RoadIssue> issues)
        {
            float rule = path.Settings.minRadius, limit = rule / RadiusTolerance;
            float worst = 0f, worstAt = 0f, last = float.NegativeInfinity;
            bool open = false;
            void Flush()
            {
                if (!open) return;
                issues.Add(new RoadIssue
                {
                    Kind = IssueKind.TightCorner, Road = path.Name, Lane = -1, Station = worstAt, Position = path.PointAt(worstAt, 0f),
                    Value = 1f / worst, Limit = limit,
                    Detail = $"corner radius {1f / worst:0} m, tighter than {rule:0} m (allowed to {limit:0} m)"
                });
                open = false;
                worst = 0f;
            }
            for (int i = 0; i < path.Count; i++)
            {
                float k = Mathf.Abs(path.Curvature[i]);
                if (k * limit <= 1f) continue;
                float s = i * path.Spacing;
                if (open && s - last > IssueGap) Flush();
                if (k > worst) { worst = k; worstAt = s; }
                open = true;
                last = s;
            }
            Flush();
        }
    }
}
