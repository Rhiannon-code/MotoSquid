using MotoSquid.Core;
using MotoSquid.Race;
using MotoSquid.Roads;
using MotoSquid.Track;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.Course
{
    // Turns a RaceCourse asset into the scene objects a race reads: a WaypointPath and RouteGraph on the
    // travelled carriageway of each road, the start line and grid spawn, a finish trigger for point to
    // point, and time gates for the checkpoint mode. The course object is generated output and is
    // replaced on every build; the asset and the roads' splines are the source (rule 13).
    public static class RaceCourseBuilder
    {
        const string CourseFolder = "Assets/Scriptable Objects/Courses";
        // A neighbouring leg's end this close to the road joins it; further away the leg runs the whole road
        const float JoinReach = 40f;
        const float GateHeight = 6f;
        const float GateDepth = 2f;

        class LegRun
        {
            public RaceCourse.Leg leg;
            public RoadPath path;
            public float from, to;
            public string label;
            public List<Vector3> points = new List<Vector3>();
            public List<Vector3> forwards = new List<Vector3>();
            public float Length => Mathf.Max(0f, to - from);
        }

        [MenuItem("MotoSquid/Course/Build Selected Courses Into Open Scene")]
        static void BuildSelected()
        {
            var courses = Selection.objects.OfType<RaceCourse>().ToArray();
            if (courses.Length == 0)
            {
                Debug.LogError("[Course] Select one or more Race Course assets in the Project window.");
                return;
            }
            var network = Object.FindFirstObjectByType<RoadNetwork>();
            if (network == null)
            {
                Debug.LogError("[Course] No Road Network in the open scene.");
                return;
            }

            var paths = RoadNetworkBuild.Sample(network).ToDictionary(p => p.Name);
            foreach (var course in courses) Build(course, paths);
            EditorSceneManager.MarkSceneDirty(network.gameObject.scene);
        }

        [MenuItem("MotoSquid/Course/Build Selected Courses Into Open Scene", true)]
        static bool BuildSelectedValid() => Selection.objects.OfType<RaceCourse>().Any();

        [MenuItem("MotoSquid/Course/Create Cloverstack Example Course")]
        static void CreateExample()
        {
            if (!AssetDatabase.IsValidFolder(CourseFolder))
                AssetDatabase.CreateFolder("Assets/Scriptable Objects", "Courses");

            var course = ScriptableObject.CreateInstance<RaceCourse>();
            course.displayName = "Cloverstack Circuit";
            course.trackType = TrackType.Circuit;
            foreach (var (road, reverse) in new[]
            {
                ("Freeway A", false), ("A eastbound to B northbound", false),
                ("Freeway B", false), ("B northbound to A westbound", false),
                ("Freeway A", true),  ("A westbound to B southbound", false),
                ("Freeway B", true),  ("B southbound to A eastbound", false),
            })
                course.legs.Add(new RaceCourse.Leg { road = road, reverse = reverse });

            string path = AssetDatabase.GenerateUniqueAssetPath(CourseFolder + "/Cloverstack Circuit.asset");
            AssetDatabase.CreateAsset(course, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = course;
            Debug.Log("[Course] Created " + path + ". Open Cloverstack PoC and run Build Selected Courses.", course);
        }

        static void Build(RaceCourse course, Dictionary<string, RoadPath> paths)
        {
            var report = new List<string>();
            var unknown = course.legs.Where(l => l == null || string.IsNullOrEmpty(l.road) || !paths.ContainsKey(l.road))
                                     .Select(l => l == null ? "(empty leg)" : "'" + l.road + "'").ToList();
            if (course.legs.Count == 0 || unknown.Count > 0)
            {
                Debug.LogError($"[Course] '{course.displayName}': " +
                               (course.legs.Count == 0 ? "no legs." : "roads not in this scene: " + string.Join(", ", unknown)) +
                               "\n   Roads here: " + string.Join(", ", paths.Keys.OrderBy(k => k)), course);
                return;
            }

            var legs = course.legs.Select(l => new LegRun { leg = l, path = paths[l.road], label = l.road + (l.reverse ? " (reverse)" : "") }).ToList();
            ResolveStations(legs, course, report);
            foreach (var run in legs) Sample(run, course);
            legs.RemoveAll(run =>
            {
                if (run.points.Count >= 2) return false;
                report.Add(run.label + ": under 1 m long after joining its neighbours, dropped");
                return true;
            });
            if (legs.Count == 0)
            {
                Debug.LogError($"[Course] '{course.displayName}': no leg survived station resolution.\n   " + string.Join("\n   ", report), course);
                return;
            }

            bool circuit = course.trackType == TrackType.Circuit;
            int startIndex = StartIndex(legs[0], course.startLineStation);
            if (circuit && startIndex > 0)
            {
                // Progress 0 is the first segment's first point, and a lap counts where progress wraps, so
                // the route is rotated to begin at the start line and the run up to it becomes the last leg
                var first = legs[0];
                var tail = new LegRun { leg = first.leg, path = first.path, from = first.from, to = first.to, label = first.label + " (to start)" };
                tail.points.AddRange(first.points.Take(startIndex + 1));
                tail.forwards.AddRange(first.forwards.Take(startIndex + 1));
                first.points.RemoveRange(0, startIndex);
                first.forwards.RemoveRange(0, startIndex);
                legs.Add(tail);
                startIndex = 0;
            }

            foreach (var old in Object.FindObjectsByType<RaceCourseInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (old.course == course) Undo.DestroyObjectImmediate(old.gameObject);

            var root = new GameObject("Race Course - " + course.displayName);
            Undo.RegisterCreatedObjectUndo(root, "Build race course");
            root.AddComponent<RaceCourseInfo>().course = course;

            var waypointPath = Child(root, "Waypoints").AddComponent<WaypointPath>();
            foreach (var run in legs)
            {
                var list = new RoadWaypointList { waypoints = new RaceWaypoint[run.points.Count] };
                for (int i = 0; i < run.points.Count; i++)
                    list.waypoints[i] = new RaceWaypoint { position = run.points[i], roadT = i / (float)(run.points.Count - 1) };
                waypointPath.roadWaypoints.Add(list);
            }

            var graph = Child(root, "Route Graph").AddComponent<RouteGraph>();
            graph.waypointPath = waypointPath;
            graph.segments = legs.Select((run, i) => new RouteSegmentDef
            {
                label = run.label,
                source = RouteSegmentDef.SourceType.RoadWaypoints,
                roadIndex = i,
                exitIndices = i < legs.Count - 1 ? new[] { i + 1 } : circuit ? new[] { 0 } : new int[0],
            }).ToArray();
            graph.mainRouteIndices = Enumerable.Range(0, legs.Count).ToArray();
            graph.startSegmentIndex = 0;
            graph.Bake();

            Vector3 startPos = legs[0].points[startIndex];
            Vector3 startFwd = legs[0].forwards[startIndex];
            var startLine = Child(root, "Start Line").transform;
            startLine.SetPositionAndRotation(startPos, Quaternion.LookRotation(startFwd, Vector3.up));

            // RaceManager moves the bike to its grid slot; this only has to be on the road behind the line
            var spawn = Child(root, "Player Spawn").transform;
            spawn.SetPositionAndRotation(startPos - startFwd * 10f, startLine.rotation);

            Transform finish = null;
            if (!circuit)
            {
                var last = legs[legs.Count - 1];
                finish = Trigger(root, "Finish Line", last.points[last.points.Count - 1], last.forwards[last.forwards.Count - 1], last.path.Settings.Width);
                finish.gameObject.AddComponent<FinishLine>();
            }

            int gates = course.timeExtension ? PlaceGates(root, legs, course) : 0;

            Wire(course, graph, startLine, spawn, finish, report);

            float total = legs.Sum(run => Enumerable.Range(1, run.points.Count - 1).Sum(i => Vector3.Distance(run.points[i - 1], run.points[i])));
            var sb = new System.Text.StringBuilder();
            sb.Append($"[Course] Built '{course.displayName}': {course.trackType}, {course.gameMode}, {total:F0} m, {legs.Count} leg(s)");
            if (circuit) sb.Append($", {course.laps} lap(s)");
            if (course.timeExtension) sb.Append($", {gates} time gate(s)");
            foreach (var run in legs)
                sb.Append($"\n   {run.label}: stations {run.from:F0}-{run.to:F0} m, {run.points.Count} waypoints");
            foreach (var line in report) sb.Append("\n   ").Append(line);
            sb.Append("\n   Save the scene to keep it.");
            Debug.Log(sb.ToString(), root);
            Selection.activeGameObject = root;
        }

        // Unspecified ends join the neighbouring leg where that leg meets this road, otherwise the leg
        // runs the whole road. Neighbours are judged on their own full extents, so the order of legs
        // does not change the result
        static void ResolveStations(List<LegRun> legs, RaceCourse course, List<string> report)
        {
            foreach (var run in legs)
            {
                run.from = run.leg.fromStation >= 0f ? Mathf.Min(run.leg.fromStation, run.path.Length) : 0f;
                run.to = run.leg.toStation >= 0f ? Mathf.Min(run.leg.toStation, run.path.Length) : run.path.Length;
            }
            if (legs.Count < 2) return;

            var entry = legs.Select(run => run.path.PointAt(run.leg.reverse ? run.to : run.from, Lateral(run, course))).ToArray();
            var exit = legs.Select(run => run.path.PointAt(run.leg.reverse ? run.from : run.to, Lateral(run, course))).ToArray();
            bool closed = course.trackType == TrackType.Circuit;

            for (int i = 0; i < legs.Count; i++)
            {
                var run = legs[i];
                bool hasPrev = i > 0 || closed, hasNext = i < legs.Count - 1 || closed;
                int prev = (i - 1 + legs.Count) % legs.Count, next = (i + 1) % legs.Count;

                bool entryFree = run.leg.reverse ? run.leg.toStation < 0f : run.leg.fromStation < 0f;
                bool exitFree = run.leg.reverse ? run.leg.fromStation < 0f : run.leg.toStation < 0f;

                if (entryFree && hasPrev && Nearest(run.path, exit[prev], out float s))
                { if (run.leg.reverse) run.to = s; else run.from = s; }
                if (exitFree && hasNext && Nearest(run.path, entry[next], out s))
                { if (run.leg.reverse) run.from = s; else run.to = s; }

                if (run.to < run.from)
                {
                    report.Add($"{run.label}: joins put its end ({run.to:F0} m) before its start ({run.from:F0} m), check the leg order or set its stations");
                    run.to = run.from;
                }
            }
        }

        static bool Nearest(RoadPath path, Vector3 point, out float station)
        {
            int best = -1;
            float bestSq = JoinReach * JoinReach;
            for (int i = 0; i < path.Count; i++)
            {
                float sq = RoadPath.Flat(path.Centre[i] - point).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = i; }
            }
            station = best * path.Spacing;
            return best >= 0;
        }

        // Left-hand traffic: the carriageway travelled in the spline's direction is on its left. A reversed
        // leg rides the other one. lateralOffset is in the rider's frame, positive to their right
        static float Lateral(LegRun run, RaceCourse course)
        {
            var s = run.path.Settings;
            if (!run.leg.reverse)
                return -s.Width / 2f + s.shoulder + s.lanesForward * s.laneWidth / 2f + course.lateralOffset;
            if (s.lanesBackward == 0)
                return -s.Width / 2f + s.shoulder + s.lanesForward * s.laneWidth / 2f - course.lateralOffset;
            return s.Width / 2f - s.shoulder - s.lanesBackward * s.laneWidth / 2f - course.lateralOffset;
        }

        static void Sample(LegRun run, RaceCourse course)
        {
            if (run.Length < 1f) return;
            float lateral = Lateral(run, course);
            int steps = Mathf.Max(1, Mathf.CeilToInt(run.Length / Mathf.Max(1f, course.waypointSpacing)));
            for (int k = 0; k <= steps; k++)
            {
                float station = run.leg.reverse ? Mathf.Lerp(run.to, run.from, k / (float)steps) : Mathf.Lerp(run.from, run.to, k / (float)steps);
                run.points.Add(run.path.PointAt(station, lateral));
                int i = Mathf.Clamp(Mathf.RoundToInt(station / run.path.Spacing), 0, run.path.Count - 1);
                run.forwards.Add(run.leg.reverse ? -run.path.Forward[i] : run.path.Forward[i]);
            }
        }

        static int StartIndex(LegRun first, float startLineStation)
        {
            float cum = 0f;
            for (int i = 1; i < first.points.Count - 1; i++)
            {
                cum += Vector3.Distance(first.points[i - 1], first.points[i]);
                if (cum >= startLineStation) return i;
            }
            return Mathf.Max(0, first.points.Count - 2);
        }

        static int PlaceGates(GameObject root, List<LegRun> legs, RaceCourse course)
        {
            var holder = Child(root, "Time Gates");
            float spacing = Mathf.Max(50f, course.gateSpacing);
            float cum = 0f, nextAt = spacing;
            int number = 0;
            foreach (var run in legs)
            {
                for (int i = 1; i < run.points.Count; i++)
                {
                    cum += Vector3.Distance(run.points[i - 1], run.points[i]);
                    if (cum < nextAt) continue;
                    nextAt += spacing;
                    number++;
                    var gate = Trigger(holder, "Gate " + number, run.points[i], run.forwards[i], run.path.Settings.Width);
                    var pickup = gate.gameObject.AddComponent<TimePickup>();
                    var so = new SerializedObject(pickup);
                    so.FindProperty("timeToAdd").floatValue = course.gateTimeBonus;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    var numbered = gate.gameObject.AddComponent<NumberedGate>();
                    var nso = new SerializedObject(numbered);
                    nso.FindProperty("number").intValue = number;
                    nso.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            return number;
        }

        static Transform Trigger(GameObject parent, string name, Vector3 position, Vector3 forward, float width)
        {
            var go = Child(parent, name);
            go.transform.SetPositionAndRotation(position + Vector3.up * (GateHeight / 2f), Quaternion.LookRotation(forward, Vector3.up));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(width, GateHeight, GateDepth);
            return go.transform;
        }

        static void Wire(RaceCourse course, RouteGraph graph, Transform startLine, Transform spawn, Transform finish, List<string> report)
        {
            var race = Object.FindFirstObjectByType<RaceManager>(FindObjectsInactive.Include);
            if (race != null)
            {
                Undo.RecordObject(race, "Wire race course");
                race.routeGraph = graph;
                race.startLine = startLine;
                race.totalLaps = course.laps;
                race.trackType = course.trackType;
                race.gameMode = course.gameMode;
                EditorUtility.SetDirty(race);
                report.Add("RaceManager wired: route graph, start line, laps, type, mode");
                if (finish != null) Set(finish.GetComponent<FinishLine>(), "raceManager", race);
            }
            else report.Add("no RaceManager in the scene: add the MANAGERS, then rebuild this course to wire them");

            var setup = Object.FindFirstObjectByType<PlayerRacerSetup>(FindObjectsInactive.Include);
            if (setup != null) { Set(setup, "spawnPoint", spawn); report.Add("PlayerRacerSetup spawn point wired"); }
            else report.Add("no PlayerRacerSetup in the scene: the player bike is not spawned until it is added");

            if (course.timeExtension)
            {
                var timer = Object.FindFirstObjectByType<TimerSystem>(FindObjectsInactive.Include);
                if (timer != null)
                {
                    var so = new SerializedObject(timer);
                    so.FindProperty("timerDuration").floatValue = course.startingTimeSeconds;
                    so.ApplyModifiedProperties();
                    report.Add($"TimerSystem starts at {course.startingTimeSeconds:F0} s");
                }
                else report.Add("time extension needs a TimerSystem in the scene (with its countdown text), none found");
            }
        }

        static void Set(Component target, string field, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }
    }
}
