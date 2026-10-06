using MotoSquid.Rider;
using UnityEngine;
using System.Collections.Generic;

namespace MotoSquid.Track
{
    [System.Serializable]
    public class RouteSegmentDef
    {
        public string label = "Segment";

        public enum SourceType { RoadWaypoints, ManualWaypoints, IntersectionCurve }
        public SourceType source = SourceType.RoadWaypoints;
        public int roadIndex = 0;
        public Transform[] manualWaypoints;
        public int   exitRoadIndex  = 0;
        public int   entryRoadIndex = 1;
        [Range(0.1f, 1f)]
        public float curveTension   = 0.5f;
        public int   curveSteps     = 8;
        public int[] exitIndices = new int[0];
        public bool isShortcut      = false;
        public bool requiresTrigger = false;
        public bool isOffRoad       = false;
    }

    public class BakedSegment
    {
        public Vector3[] positions;   
        public float[]   legLengths; 
        public float     totalLength;
        public float     progressStart; 
        public float     progressEnd;  
    }

    // Inspector authored sharp corner
    [System.Serializable]
    public class CornerHint
    {
        public string label            = "90° Corner";
        public int    segmentIndex     = 0;
        public int    waypointIndex    = 0;
        public float  approachSpeedKmh = 40f;
    }

    public struct BakedCornerHint
    {
        public int   segmentIndex;
        public int   waypointIndex;
        public float distanceFromSegStart;
        public float approachSpeedMs;     
    }

    [DefaultExecutionOrder(-5)] 
    public class RouteGraph : MonoBehaviour
    {
        [Header("References")]
        public WaypointPath waypointPath;

        [Header("Segments define roads, shortcuts and off road paths here")]
        public RouteSegmentDef[] segments;

        [Header("Main Route")]
        public int[] mainRouteIndices;

        [Header("Start")]
        public int startSegmentIndex = 0;

        [Header("Debug")]
        public bool aiShortcutsEnabled = true;

        [Header("Corner Hints")]

        public CornerHint[] cornerHints;

        // Runtime
        public BakedSegment[]     BakedSegments     { get; private set; }
        public BakedCornerHint[]  BakedCornerHints  { get; private set; }
        public float MainRouteLength { get; private set; }
        public bool  IsBaked => BakedSegments != null;

        private void Awake() => Bake();

        // Baking
        public void Bake()
        {
            if (segments == null || segments.Length == 0) return;

            int n = segments.Length;
            BakedSegments = new BakedSegment[n];

            for (int s = 0; s < n; s++)
                BakedSegments[s] = BakeOne(segments[s]);

            TrimOverlaps();
            AssignProgress();
            BakeCornerHints();
        }

        private void BakeCornerHints()
        {
            if (cornerHints == null || cornerHints.Length == 0)
            {
                BakedCornerHints = System.Array.Empty<BakedCornerHint>();
                return;
            }

            var list = new List<BakedCornerHint>(cornerHints.Length);
            foreach (var hint in cornerHints)
            {
                if (hint == null) continue;
                if (hint.segmentIndex < 0 || hint.segmentIndex >= BakedSegments.Length) continue;

                var seg = BakedSegments[hint.segmentIndex];
                if (seg.positions == null || seg.positions.Length < 2) continue;

                int clampedWp = Mathf.Clamp(hint.waypointIndex, 0, seg.positions.Length - 1);

                // Sum leg lengths up to (but not including) the apex waypoint
                float dist = 0f;
                for (int i = 0; i < clampedWp && i < seg.legLengths.Length; i++)
                    dist += seg.legLengths[i];

                list.Add(new BakedCornerHint
                {
                    segmentIndex       = hint.segmentIndex,
                    waypointIndex      = clampedWp,
                    distanceFromSegStart = dist,
                    approachSpeedMs    = Mathf.Max(hint.approachSpeedKmh, 5f) / 3.6f,
                });
            }

            BakedCornerHints = list.ToArray();
        }

        private void TrimOverlaps()
        {
            // exitIdx to deepest head cut index seen across all predecessors
            var maxHeadCuts = new Dictionary<int, int>();

            for (int s = 0; s < segments.Length; s++)
            {
                int[] exits = segments[s].exitIndices;
                if (exits == null || exits.Length == 0) continue;

                var b = BakedSegments[s];
                if (b.positions == null || b.positions.Length < 2) continue;

                foreach (int exitIdx in exits)
                {
                    if (exitIdx < 0 || exitIdx >= BakedSegments.Length) continue;
                    var eb = BakedSegments[exitIdx];
                    if (eb.positions == null || eb.positions.Length < 2) continue;

                    // Trim this segment's tail to end near the exit segment's first point
                    Vector3 exitStart = eb.positions[0];
                    int tailCut = FindNearestIndex(b.positions, exitStart);
                    if (tailCut > 0 && tailCut < b.positions.Length - 1)
                        b = BakedSegments[s] = RebakeFromPositions(Trim(b.positions, 0, tailCut));

                    Vector3 segEnd = b.positions[b.positions.Length - 1];
                    int headCut = FindNearestIndex(eb.positions, segEnd);
                    if (headCut > 0 && headCut < eb.positions.Length - 1)
                    {
                        if (!maxHeadCuts.TryGetValue(exitIdx, out int prev) || headCut > prev)
                            maxHeadCuts[exitIdx] = headCut;
                    }
                }
            }

            // Apply the deepest head cut collected for each exit segment
            foreach (var kvp in maxHeadCuts)
            {
                var eb = BakedSegments[kvp.Key];
                if (eb.positions == null || kvp.Value >= eb.positions.Length - 1) continue;
                BakedSegments[kvp.Key] = RebakeFromPositions(Trim(eb.positions, kvp.Value, eb.positions.Length - 1));
            }
        }

        private static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return u*u*u * p0 + 3f*u*u*t * p1 + 3f*u*t*t * p2 + t*t*t * p3;
        }

        private static int FindNearestIndex(Vector3[] pts, Vector3 target)
        {
            int best = 0;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                float d = (pts[i] - target).sqrMagnitude;
                if (d < bestDistSq) { bestDistSq = d; best = i; }
            }
            return best;
        }

        private static Vector3[] Trim(Vector3[] pts, int from, int to)
        {
            int len = to - from + 1;
            var result = new Vector3[len];
            System.Array.Copy(pts, from, result, 0, len);
            return result;
        }

        private static BakedSegment RebakeFromPositions(Vector3[] pts)
        {
            var b = new BakedSegment();
            if (pts == null || pts.Length < 2)
            {
                b.positions  = System.Array.Empty<Vector3>();
                b.legLengths = System.Array.Empty<float>();
                return b;
            }

            b.positions  = pts;
            int legCount = pts.Length - 1;
            b.legLengths = new float[legCount];
            b.totalLength = 0f;

            for (int i = 0; i < legCount; i++)
            {
                float len       = Vector3.Distance(pts[i], pts[i + 1]);
                b.legLengths[i] = Mathf.Max(len, 0.01f);
                b.totalLength  += b.legLengths[i];
            }

            return b;
        }

        private BakedSegment BakeOne(RouteSegmentDef def)
        {
            var b   = new BakedSegment();
            Vector3[] pts = null;

            if (def.source == RouteSegmentDef.SourceType.RoadWaypoints && waypointPath != null)
            {
                var wps = waypointPath.GetWaypoints(def.roadIndex);
                if (wps != null && wps.Length >= 2)
                {
                    pts = new Vector3[wps.Length];
                    for (int i = 0; i < wps.Length; i++)
                        pts[i] = wps[i].position;
                }
            }
            else if (def.source == RouteSegmentDef.SourceType.ManualWaypoints && def.manualWaypoints != null)
            {
                var valid = new List<Vector3>();
                foreach (var t in def.manualWaypoints)
                    if (t != null) valid.Add(t.position);
                if (valid.Count >= 2)
                    pts = valid.ToArray();
            }
            else if (def.source == RouteSegmentDef.SourceType.IntersectionCurve && waypointPath != null)
            {
                var exitWps  = waypointPath.GetWaypoints(def.exitRoadIndex);
                var entryWps = waypointPath.GetWaypoints(def.entryRoadIndex);

                if (exitWps  != null && exitWps.Length  >= 2 &&
                    entryWps != null && entryWps.Length >= 2)
                {
                    // Curve start, last point of the exit road
                    Vector3 p0 = exitWps[exitWps.Length - 1].position;
                    // Use a few points back for a stable exit tangent (avoids jitter from the last leg)
                    Vector3 exitTangent = (p0 - exitWps[Mathf.Max(0, exitWps.Length - 3)].position).normalized;

                    // Curve end, first point of the entry road
                    Vector3 p3 = entryWps[0].position;
                    // Use a few points forward for a stable entry tangent
                    Vector3 entryTangent = (entryWps[Mathf.Min(entryWps.Length - 1, 2)].position - p3).normalized;

                    // Control points, push along each tangent by (tension × chord length)
                    float    chord = Vector3.Distance(p0, p3);
                    float    k     = chord * Mathf.Clamp(def.curveTension, 0.1f, 1f);
                    Vector3  p1    = p0 + exitTangent  * k;
                    Vector3  p2    = p3 - entryTangent * k;

                    int steps = Mathf.Max(2, def.curveSteps);
                    pts = new Vector3[steps + 1];
                    for (int i = 0; i <= steps; i++)
                        pts[i] = CubicBezier(p0, p1, p2, p3, (float)i / steps);
                }
            }

            if (pts == null || pts.Length < 2)
            {
                b.positions  = System.Array.Empty<Vector3>();
                b.legLengths = System.Array.Empty<float>();
                return b;
            }

            b.positions  = pts;
            int legCount = pts.Length - 1;
            b.legLengths = new float[legCount];
            b.totalLength = 0f;

            for (int i = 0; i < legCount; i++)
            {
                float len      = Vector3.Distance(pts[i], pts[i + 1]);
                b.legLengths[i] = Mathf.Max(len, 0.01f);
                b.totalLength  += b.legLengths[i];
            }

            return b;
        }

        private void AssignProgress()
        {
            int[] route = (mainRouteIndices != null && mainRouteIndices.Length > 0)
                ? mainRouteIndices
                : BuildDefaultRoute();

            // Total main route length
            float mainLen = 0f;
            foreach (int si in route)
                if (si >= 0 && si < BakedSegments.Length)
                    mainLen += BakedSegments[si].totalLength;
            MainRouteLength = Mathf.Max(mainLen, 0.001f);

            // Assign progress to main route segments sequentially
            float cum = 0f;
            foreach (int si in route)
            {
                if (si < 0 || si >= BakedSegments.Length) continue;
                BakedSegments[si].progressStart = cum / MainRouteLength;
                cum += BakedSegments[si].totalLength;
                BakedSegments[si].progressEnd   = cum / MainRouteLength;
            }

            // For branch segments, estimate by matching their endpoints to the main route
            var mainSet = new HashSet<int>(route);
            for (int s = 0; s < BakedSegments.Length; s++)
            {
                if (mainSet.Contains(s)) continue;
                var b = BakedSegments[s];
                if (b.positions == null || b.positions.Length < 2) continue;
                b.progressStart = NearestMainProgress(b.positions[0], route);
                b.progressEnd   = NearestMainProgress(b.positions[b.positions.Length - 1], route);
            }
        }

        private int[] BuildDefaultRoute()
        {
            var r = new int[segments.Length];
            for (int i = 0; i < segments.Length; i++) r[i] = i;
            return r;
        }

        private float NearestMainProgress(Vector3 worldPos, int[] route)
        {
            float bestDistSq = float.MaxValue;
            float bestProg   = 0f;

            foreach (int si in route)
            {
                if (si < 0 || si >= BakedSegments.Length) continue;
                var b = BakedSegments[si];
                if (b.positions == null || b.positions.Length < 2) continue;

                float cumDist = b.progressStart * MainRouteLength;
                for (int i = 0; i < b.legLengths.Length; i++)
                {
                    Vector3 leg    = b.positions[i + 1] - b.positions[i];
                    float   legLen = b.legLengths[i];
                    float   t      = legLen > 0.001f
                        ? Mathf.Clamp01(Vector3.Dot(worldPos - b.positions[i], leg) / (legLen * legLen))
                        : 0f;
                    Vector3 closest = Vector3.Lerp(b.positions[i], b.positions[i + 1], t);
                    float   distSq  = (worldPos - closest).sqrMagnitude;

                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestProg   = (cumDist + t * legLen) / MainRouteLength;
                    }
                    cumDist += legLen;
                }
            }

            return Mathf.Clamp01(bestProg);
        }

        // Public API
        public float GetProgress(int segIdx, int wpIdx, float frac)
        {
            if (!IsBaked || segIdx < 0 || segIdx >= BakedSegments.Length) return 0f;
            var b = BakedSegments[segIdx];
            if (b.positions == null || b.positions.Length < 2) return b.progressStart;

            wpIdx = Mathf.Clamp(wpIdx, 0, b.legLengths.Length - 1);

            float distIntoSeg = 0f;
            for (int i = 0; i < wpIdx; i++)
                distIntoSeg += b.legLengths[i];
            distIntoSeg += frac * b.legLengths[wpIdx];

            float segSpan = b.progressEnd - b.progressStart;
            float prog    = b.totalLength > 0.001f
                ? b.progressStart + segSpan * (distIntoSeg / b.totalLength)
                : b.progressStart;

            return Mathf.Clamp01(prog);
        }

        public float FindNearestProgress(Vector3 worldPos, out int bestSeg, out int bestWp, out float bestFrac,
                                         bool avoidTriggerOnly = false)
        {
            bestSeg = 0; bestWp = 0; bestFrac = 0f;
            float bestDistSq = float.MaxValue;
            float bestProg   = 0f;

            if (!IsBaked) return 0f;

            for (int s = 0; s < BakedSegments.Length; s++)
            {
                // Skip trigger-only (shortcut) segments if the bike wasn't on one at crash time
                if (avoidTriggerOnly && s < segments.Length && segments[s].requiresTrigger) continue;

                var b = BakedSegments[s];
                if (b.positions == null || b.positions.Length < 2) continue;

                for (int i = 0; i < b.legLengths.Length; i++)
                {
                    Vector3 leg    = b.positions[i + 1] - b.positions[i];
                    float   legLen = b.legLengths[i];
                    float   t      = legLen > 0.001f
                        ? Mathf.Clamp01(Vector3.Dot(worldPos - b.positions[i], leg) / (legLen * legLen))
                        : 0f;
                    Vector3 closest = Vector3.Lerp(b.positions[i], b.positions[i + 1], t);
                    float   distSq  = (worldPos - closest).sqrMagnitude;

                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestSeg    = s;
                        bestWp     = i;
                        bestFrac   = t;
                        bestProg   = GetProgress(s, i, t);
                    }
                }
            }

            return bestProg;
        }

        public int[] GetExits(int segIdx)
        {
            if (segments == null || segIdx < 0 || segIdx >= segments.Length)
                return System.Array.Empty<int>();
            return segments[segIdx].exitIndices ?? System.Array.Empty<int>();
        }

        // Debug Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Auto bake so gizmos survive script recompiles without needing a manual bake step
            if (!IsBaked) Bake();
            if (!IsBaked) return;

            for (int s = 0; s < BakedSegments.Length; s++)
            {
                var b = BakedSegments[s];
                if (b.positions == null || b.positions.Length < 2) continue;

                bool isOff    = s < segments.Length && segments[s].isOffRoad;
                bool isShort  = s < segments.Length && segments[s].isShortcut;
                bool isCurve  = s < segments.Length &&
                                segments[s].source == RouteSegmentDef.SourceType.IntersectionCurve;

                Gizmos.color = isOff   ? Color.yellow
                             : isShort ? new Color(1f, 0f, 1f)
                             : isCurve ? Color.green
                             :           Color.cyan;

                for (int i = 0; i < b.positions.Length - 1; i++)
                {
                    Gizmos.DrawLine(b.positions[i], b.positions[i + 1]);
                    Gizmos.DrawSphere(b.positions[i], isCurve ? 0.6f : 0.4f);
                }
                Gizmos.DrawSphere(b.positions[b.positions.Length - 1], 0.7f);

                // Draw exit arrows
                if (s < segments.Length && segments[s].exitIndices != null)
                {
                    Vector3 segEnd = b.positions[b.positions.Length - 1];
                    foreach (int e in segments[s].exitIndices)
                    {
                        if (e < 0 || e >= BakedSegments.Length) continue;
                        var eb = BakedSegments[e];
                        if (eb.positions == null || eb.positions.Length < 1) continue;
                        Gizmos.color = Color.white;
                        Gizmos.DrawLine(segEnd, eb.positions[0]);
                    }
                }
            }

            // Corner hints: draw an orange sphere at the apex waypoint and a label
            if (cornerHints != null)
            {
                foreach (var hint in cornerHints)
                {
                    if (hint == null) continue;
                    if (hint.segmentIndex < 0 || hint.segmentIndex >= BakedSegments.Length) continue;
                    var seg = BakedSegments[hint.segmentIndex];
                    if (seg.positions == null || seg.positions.Length < 2) continue;
                    int wp = Mathf.Clamp(hint.waypointIndex, 0, seg.positions.Length - 1);

                    Gizmos.color = new Color(1f, 0.5f, 0f); // orange
                    Gizmos.DrawSphere(seg.positions[wp] + Vector3.up * 1.5f, 1.2f);
                    Gizmos.DrawLine(seg.positions[wp], seg.positions[wp] + Vector3.up * 1.5f);
                    UnityEditor.Handles.Label(
                        seg.positions[wp] + Vector3.up * 3f,
                        $"{hint.label}\n{hint.approachSpeedKmh:F0} km/h");
                }
            }
        }

        // Custom editor with per segment bake status
        [UnityEditor.CustomEditor(typeof(RouteGraph))]
        private class RouteGraphEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                var graph = (RouteGraph)target;

                UnityEditor.EditorGUILayout.Space(8);
                GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
                if (GUILayout.Button("Bake", GUILayout.Height(32)))
                {
                    graph.Bake();
                    UnityEditor.SceneView.RepaintAll();
                }
                GUI.backgroundColor = Color.white;

                if (!graph.IsBaked)
                {
                    UnityEditor.EditorGUILayout.HelpBox("Not baked yet.", UnityEditor.MessageType.Warning);
                    return;
                }

                UnityEditor.EditorGUILayout.Space(4);
                UnityEditor.EditorGUILayout.LabelField("Bake Results", UnityEditor.EditorStyles.boldLabel);
                for (int s = 0; s < graph.BakedSegments.Length; s++)
                {
                    var b    = graph.BakedSegments[s];
                    string label = s < graph.segments.Length ? graph.segments[s].label : $"Seg {s}";
                    int    pts   = b.positions?.Length ?? 0;
                    string src   = s < graph.segments.Length ? graph.segments[s].source.ToString() : "";
                    string info  = pts < 2
                        ? $"  [{s}] {label} ({src}) ⚠ {pts} pts (empty!)"
                        : $"  [{s}] {label} ({src}) {pts} pts, {b.totalLength:F0} m";
                    var style = pts < 2
                        ? UnityEditor.EditorStyles.boldLabel
                        : UnityEditor.EditorStyles.miniLabel;
                    GUI.color = pts < 2 ? Color.red : Color.white;
                    UnityEditor.EditorGUILayout.LabelField(info, style);
                    GUI.color = Color.white;
                }
            }
        }
#endif
    }
}
