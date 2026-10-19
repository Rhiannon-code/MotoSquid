using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

namespace MotoSquid.Roads
{
    // Node edits shared by the Scene view and the Road Plan window. A drag reshapes the road near the node as it
    // goes (the moved knot and its neighbours get automatic tangents); letting go moves the height points with
    // their stretch of road, applies the grade rules to any height the drag changed, and marks the 3D surface
    // out of date. Nothing is built in 3D here: that waits for Commit to 3D.
    public static class RoadNodeEdits
    {
        // Below this a drag's height change is taken as the hand wobbling, not a height edit (m).
        const float HeightThreshold = 0.05f;

        public static event Action<Road> Changed;

        sealed class Drag
        {
            public float[] Stations;
            public int Group;
            public readonly HashSet<int> Moved = new HashSet<int>();
        }

        static readonly Dictionary<Road, Drag> drags = new Dictionary<Road, Drag>();

        public static bool Dragging => drags.Count > 0;

        public static void Move(Road road, int index, Vector3 local)
        {
            if (!drags.TryGetValue(road, out var drag))
                drags[road] = drag = new Drag { Stations = RoadPath.KnotStations(road.Spline), Group = Undo.GetCurrentGroup() };
            var container = Record(road, "Move road node");
            var spline = container.Spline;
            var knot = spline[index];
            knot.Position = local;
            Write(() =>
            {
                spline.SetKnot(index, knot);
                for (int j = Mathf.Max(0, index - 1); j <= Mathf.Min(spline.Count - 1, index + 1); j++)
                    if (spline.GetTangentMode(j) != TangentMode.AutoSmooth) spline.SetTangentMode(j, TangentMode.AutoSmooth);
            });
            road.handShaped = road.edited = true;
            drag.Moved.Add(index);
            RoadNodes.Sync(road);
            Changed?.Invoke(road);
        }

        public static void FinishDrags()
        {
            foreach (var (road, drag) in drags.ToList())
            {
                if (road == null) continue;
                var after = RoadPath.KnotStations(road.Spline);
                ApplyHeights(road, drag.Stations, after, after, drag.Moved);
                Undo.CollapseUndoOperations(drag.Group);
                Done(road);
            }
            drags.Clear();
        }

        // A node on the road under `local`, splitting the curve there so the road keeps its exact shape.
        public static void Insert(Road road, Vector3 local)
        {
            var container = Record(road, "Insert road node");
            var spline = container.Spline;
            var before = RoadPath.KnotStations(spline);
            SplineUtility.GetNearestPoint(spline, (float3)local, out float3 _, out float t);
            int curve = spline.SplineToCurveT(t, out float curveT);
            if (curve >= spline.Count - 1 || curveT <= 0.001f || curveT >= 0.999f) return;
            CurveUtility.Split(spline.GetCurve(curve), curveT, out var left, out var right);
            Write(() =>
            {
                spline.SetTangentMode(curve, TangentMode.Broken);
                spline.SetTangentMode(curve + 1, TangentMode.Broken);
                spline.SetKnot(curve, WithTangents(spline[curve], null, left.P1 - left.P0));
                spline.SetKnot(curve + 1, WithTangents(spline[curve + 1], right.P2 - right.P3, null));
                spline.Insert(curve + 1, new BezierKnot(left.P3, left.P2 - left.P3, right.P1 - right.P0, quaternion.identity), TangentMode.Broken);
            });
            RoadNodes.Shift(road, curve + 1, 1);
            var after = RoadPath.KnotStations(spline);
            ApplyHeights(road, before, Without(after, curve + 1), after, new int[0]);
            road.handShaped = road.edited = true;
            Done(road);
        }

        // A node added at whichever end of the road is nearer `local`, level with that end.
        public static void Append(Road road, Vector3 local)
        {
            var container = Record(road, "Add road node");
            var spline = container.Spline;
            var before = RoadPath.KnotStations(spline);
            bool atStart = Vector3.Distance(local, spline[0].Position) < Vector3.Distance(local, spline[spline.Count - 1].Position);
            int end = atStart ? 0 : spline.Count - 1;
            local.y = spline[end].Position.y;
            int index = atStart ? 0 : spline.Count;
            Write(() =>
            {
                spline.Insert(index, new BezierKnot(local), TangentMode.AutoSmooth);
                spline.SetTangentMode(atStart ? 1 : spline.Count - 2, TangentMode.AutoSmooth);
            });
            RoadNodes.Shift(road, index, 1);
            var after = RoadPath.KnotStations(spline);
            var heights = road.recipe.heights;
            if (heights.Count > 1)
            {
                HeightEdit.Remap(heights, before, Without(after, index));
                float endHeight = road.recipe.Profile().Height(atStart ? after[1] : after[after.Length - 2], out _);
                HeightEdit.SetHeight(heights, after[index], endHeight, DesignRadius(road), road.settings.maxGrade, out _);
            }
            Settle(road, after);
            road.handShaped = road.edited = true;
            Done(road);
        }

        public static void Remove(Road road, IList<int> knots)
        {
            var container = road.GetComponent<SplineContainer>();
            var spline = container.Spline;
            var doomed = new HashSet<int>(knots.Where(k => k >= 0 && k < spline.Count));
            if (doomed.Count == 0) return;
            if (spline.Count - doomed.Count < 2)
            {
                Debug.LogWarning($"[Roads] {road.name} needs at least two nodes; delete the road itself instead.");
                RoadNodes.Queue();
                return;
            }
            Record(road, "Delete road node");
            var before = RoadPath.KnotStations(spline);
            Write(() =>
            {
                foreach (int k in doomed.OrderByDescending(k => k)) spline.RemoveAt(k);
                foreach (int k in doomed)
                {
                    int left = k - doomed.Count(d => d < k);
                    for (int j = Mathf.Max(0, left - 1); j <= Mathf.Min(spline.Count - 1, left); j++)
                        spline.SetTangentMode(j, TangentMode.AutoSmooth);
                }
            });
            RoadNodes.Remove(road, doomed);
            var after = RoadPath.KnotStations(spline);
            ApplyHeights(road, before.Where((_, i) => !doomed.Contains(i)).ToArray(), after, after, new int[0]);
            road.handShaped = road.edited = true;
            Done(road);
        }

        // Carries the road on along a dead-end route: knots at the route's leg ends and at most 100 m apart, with
        // tangents that follow its arc and straight exactly, and a height point where it meets the other road.
        public static bool Extend(Road road, bool atStart, DeadEndRouter.Route route)
        {
            int group = Undo.GetCurrentGroup();
            var container = Record(road, "Connect dead end");
            var spline = container.Spline;
            var before = RoadPath.KnotStations(spline);
            var plan = route.Plan();
            var stations = new List<float> { 0f };
            float at = 0f;
            foreach (var leg in route.Legs)
            {
                float length = leg.radius > 0f ? leg.radius * Mathf.Abs(leg.degrees) * Mathf.Deg2Rad : leg.straight;
                int pieces = Mathf.Max(1, Mathf.CeilToInt(length / RoadRecipe.NodeSpacing));
                for (int k = 1; k <= pieces; k++) stations.Add(at + length * k / pieces);
                at += length;
            }

            var heightsOff = road.recipe.heights.Count < 2
                ? VerticalProfile.Stepped(route.StartHeight, DesignRadius(road), (0f, route.EndHeight, road.settings.maxGrade)) : null;
            Vector3 Point(float s)
            {
                plan.Evaluate(s, out Vector2 xz, out _);
                float y = heightsOff != null ? heightsOff.Height(s, out _) : route.StartHeight;
                return road.transform.InverseTransformPoint(new Vector3(xz.x, y, xz.y));
            }
            Vector3 Direction(float s)
            {
                plan.Evaluate(s, out _, out Vector2 dir);
                return road.transform.InverseTransformDirection(new Vector3(dir.x, 0f, dir.y));
            }

            Write(() =>
            {
                // Tangents run the way the route travels (out of the dead end); at the start that is against the road.
                float sign = atStart ? -1f : 1f;
                int endKnot = atStart ? 0 : spline.Count - 1;
                float first = RoadRecipe.Handle(plan, stations[0], stations[1]);
                spline.SetTangentMode(endKnot, TangentMode.Broken);
                spline.SetKnot(endKnot, atStart
                    ? WithTangents(spline[endKnot], Direction(0f) * first, null)
                    : WithTangents(spline[endKnot], null, Direction(0f) * first));
                for (int i = 1; i < stations.Count; i++)
                {
                    float back = RoadRecipe.Handle(plan, stations[i - 1], stations[i]);
                    float ahead = i < stations.Count - 1 ? RoadRecipe.Handle(plan, stations[i], stations[i + 1]) : back;
                    Vector3 dir = Direction(stations[i]) * sign;
                    var knot = new BezierKnot(Point(stations[i]), -dir * (atStart ? ahead : back), dir * (atStart ? back : ahead), quaternion.identity);
                    if (atStart) spline.Insert(0, knot, TangentMode.Broken);
                    else spline.Add(knot, TangentMode.Broken);
                }
            });
            int added = stations.Count - 1;
            if (atStart) RoadNodes.Shift(road, 0, added);
            var after = RoadPath.KnotStations(spline);
            road.handShaped = road.edited = true;

            var heights = road.recipe.heights;
            if (heights.Count > 1)
            {
                var aligned = atStart ? after.Skip(added).ToArray() : after.Take(before.Length).ToArray();
                HeightEdit.Remap(heights, before, aligned);
                float joinStation = atStart ? 0f : after[after.Length - 1];
                var result = HeightEdit.SetHeight(heights, joinStation, route.EndHeight, DesignRadius(road), road.settings.maxGrade, out _);
                if (result != HeightEdit.Result.Applied)
                {
                    Undo.RevertAllDownToGroup(group);
                    RoadNodes.Queue();
                    return false;
                }
                Settle(road, after);
            }
            Done(road);
            return true;
        }

        // Every dead end in the network carried on to the nearest road it can join. Returns what it did, one line
        // per dead end.
        public static List<string> ConnectDeadEnds(RoadNetwork network)
        {
            var report = new List<string>();
            int group = Undo.GetCurrentGroup();
            var roads = network.Roads.ToDictionary(r => r.name);
            var paths = roads.Values.Select(RoadPath.Sample).ToList();
            var deadEnds = RoadPlanCheck.Run(paths, out _).Where(i => i.Kind == IssueKind.DeadEnd).ToList();
            foreach (var dead in deadEnds)
            {
                var road = roads[dead.Road];
                int index = paths.FindIndex(p => p.Name == dead.Road);
                bool atStart = dead.Station < 1f;
                string end = atStart ? "start" : "end";
                var route = DeadEndRouter.Find(paths[index], atStart, paths, road.settings.maxGrade, DesignRadius(road));
                if (route == null) { report.Add($"{road.name} {end}: no road to join within reach - join it by hand."); continue; }
                if (!Extend(road, atStart, route)) { report.Add($"{road.name} {end}: the height to {route.Target.Name} breaks the grade rules - join it by hand."); continue; }
                paths[index] = RoadPath.Sample(road);
                report.Add($"{road.name} {end}: carried {route.Length:0} m on to {route.Target.Name} at {route.TargetStation:0} m.");
            }
            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Connect dead ends");
            return report;
        }

        public static void MarkStale(Road road)
        {
            var network = road != null ? road.GetComponentInParent<RoadNetwork>() : null;
            if (network == null || network.surfaceStale) return;
            Undo.RecordObject(network, "Roads changed");
            network.surfaceStale = true;
            EditorUtility.SetDirty(network);
        }

        // `before` and `after` are the stations of the same knots before and after the edit; `stations` are all
        // the knots' stations now.
        static void ApplyHeights(Road road, IReadOnlyList<float> before, IReadOnlyList<float> after, float[] stations, IEnumerable<int> moved)
        {
            var heights = road.recipe.heights;
            if (heights.Count < 2) return;
            HeightEdit.Remap(heights, before, after);
            var spline = road.Spline;
            foreach (int i in moved)
            {
                float wanted = spline[i].Position.y;
                float now = road.recipe.Profile().Height(stations[i], out _);
                if (Mathf.Abs(wanted - now) < HeightThreshold) continue;
                var result = HeightEdit.SetHeight(heights, stations[i], wanted, DesignRadius(road), road.settings.maxGrade, out float applied);
                if (result == HeightEdit.Result.Limited)
                    Debug.Log($"[Roads] {road.name} node {i}: height {wanted:0.0} m is steeper than the grade rules allow; set to {applied:0.0} m.");
                else if (result == HeightEdit.Result.Refused)
                    Debug.LogWarning($"[Roads] {road.name} node {i} sits inside a height rounding, so its height was left alone. Edit the height points in the road's Recipe instead.");
            }
            Settle(road, stations);
        }

        // Knots, and so nodes, sit at the road's real height, so a node shows where the road is.
        static void Settle(Road road, float[] stations)
        {
            if (road.recipe.heights.Count < 2) return;
            var profile = road.recipe.Profile();
            var spline = road.Spline;
            Write(() =>
            {
                for (int i = 0; i < spline.Count && i < stations.Length; i++)
                {
                    var knot = spline[i];
                    float y = profile.Height(stations[i], out _);
                    if (Mathf.Abs(knot.Position.y - y) < 1e-4f) continue;
                    knot.Position = new float3(knot.Position.x, y, knot.Position.z);
                    spline.SetKnot(i, knot);
                }
            });
        }

        // The radius a new height point gets: the road's own design rounding, or the ramp rule.
        static float DesignRadius(Road road)
        {
            float radius = road.recipe.heights.Count > 0 ? road.recipe.heights.Max(h => h.radius) : 0f;
            return radius > 0f ? radius : RoadDesign.RampVerticalRadius;
        }

        static SplineContainer Record(Road road, string name)
        {
            var container = road.GetComponent<SplineContainer>();
            Undo.RecordObjects(new Object[] { road, container }, name);
            return container;
        }

        static void Done(Road road)
        {
            EditorUtility.SetDirty(road);
            EditorUtility.SetDirty(road.GetComponent<SplineContainer>());
            EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            MarkStale(road);
            RoadNodes.Sync(road);
            Changed?.Invoke(road);
        }

        static void Write(Action edit)
        {
            RoadNodes.Writing = true;
            try { edit(); }
            finally { RoadNodes.Writing = false; }
        }

        // Tangents given in the container's space, whatever the knot's own rotation; null keeps that side.
        static BezierKnot WithTangents(BezierKnot knot, float3? tangentIn, float3? tangentOut)
        {
            var inverse = math.inverse(knot.Rotation);
            if (tangentIn.HasValue) knot.TangentIn = math.rotate(inverse, tangentIn.Value);
            if (tangentOut.HasValue) knot.TangentOut = math.rotate(inverse, tangentOut.Value);
            return knot;
        }

        static float[] Without(float[] stations, int index) => stations.Where((_, i) => i != index).ToArray();
    }
}
