using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Roads
{
    // One stretch of road as the plan draws it: world points, its mean height and its width.
    sealed class PlanChunk
    {
        public Road Road;
        public Vector3[] Points;
        public float Height;
        public float Width;
        public Rect Bounds;
    }

    // The road network seen from above before anything is built in 3D: every road at its real width, shaded by
    // height with higher roads drawn over lower ones (a bridge doesn't read as a crossing), and every layout
    // problem RoadPlanCheck finds marked on it. It shows whether the layout works; grades, air and clearance
    // need the built surface and stay with the Road Checker. Nodes drag here as in the Scene view (plan only:
    // heights stay put); Commit to 3D builds the surface.
    public class RoadPlanWindow : EditorWindow
    {
        const float PanelWidth = 380f;
        const float ChunkLength = 40f;
        const float PointStep = 2f;
        const float NodePick = 8f;
        const float RoadPickMin = 6f;
        const float MinDrawGap = 2f;
        // Roads within one band are outlined together and then filled together, so stretches of road don't
        // outline over each other; a higher band still outlines over the one below, which is how a bridge reads.
        public const float LayerHeight = 2f;

        static readonly Color Low = new Color(0.35f, 0.5f, 0.7f);
        static readonly Color High = new Color(1f, 0.62f, 0.2f);
        static readonly Color Background = new Color(0.13f, 0.14f, 0.15f);
        static readonly Color Outline = new Color(0.05f, 0.05f, 0.05f);

        [SerializeField] RoadNetwork network;
        [SerializeField] Vector2 centre;
        [SerializeField] float scale = 0.2f;

        sealed class Cached { public int Hash; public RoadPath Path; }
        readonly Dictionary<Road, Cached> cache = new Dictionary<Road, Cached>();
        List<PlanChunk> chunks = new List<PlanChunk>();
        List<RoadIssue> issues = new List<RoadIssue>();
        List<string> report = new List<string>();
        float minHeight, maxHeight = 1f;
        bool checkQueued, chunksQueued;
        string status = "";
        Vector2 listScroll;
        int selectedIssue = -1;
        Road dragRoad;
        int dragIndex = -1;
        bool panning;
        Rect canvas;

        [MenuItem("MotoSquid/Roads/Road Plan")]
        public static void Open() => GetWindow<RoadPlanWindow>("Road Plan");

        public static void Open(RoadNetwork network)
        {
            var window = GetWindow<RoadPlanWindow>("Road Plan");
            window.network = network;
            window.Refresh(frame: true);
        }

        void OnEnable()
        {
            RoadNodeEdits.Changed += OnRoadChanged;
            Undo.undoRedoPerformed += OnAnyChange;
            EditorApplication.hierarchyChanged += OnAnyChange;
            Selection.selectionChanged += Repaint;
            Refresh(frame: network == null);
        }

        void OnDisable()
        {
            RoadNodeEdits.Changed -= OnRoadChanged;
            Undo.undoRedoPerformed -= OnAnyChange;
            EditorApplication.hierarchyChanged -= OnAnyChange;
            Selection.selectionChanged -= Repaint;
        }

        void OnRoadChanged(Road road)
        {
            QueueChunks();
            if (!RoadNodeEdits.Dragging) QueueCheck();
        }

        void OnAnyChange()
        {
            QueueChunks();
            QueueCheck();
        }

        void Refresh(bool frame)
        {
            EditorApplication.delayCall += () =>
            {
                RebuildChunks();
                if (frame) FrameAll();
                RunCheck();
            };
        }

        void QueueChunks()
        {
            if (chunksQueued) return;
            chunksQueued = true;
            EditorApplication.delayCall += () => { chunksQueued = false; RebuildChunks(); };
        }

        void QueueCheck()
        {
            if (checkQueued) return;
            checkQueued = true;
            EditorApplication.delayCall += () => { checkQueued = false; RunCheck(); };
        }

        RoadNetwork FindNetwork()
        {
            var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<RoadNetwork>() : null;
            return selected != null ? selected : FindFirstObjectByType<RoadNetwork>();
        }

        // Every road's sampled path, re-sampled only where the road changed.
        List<RoadPath> Paths()
        {
            if (network == null) network = FindNetwork();
            var paths = new List<RoadPath>();
            if (network == null) return paths;
            var roads = network.Roads;
            foreach (var gone in cache.Keys.Where(r => r == null || !roads.Contains(r)).ToList()) cache.Remove(gone);
            foreach (var road in roads)
            {
                if (road.Spline == null || road.Spline.Count < 2) continue;
                int hash = RoadNodes.ShapeHash(road);
                if (!cache.TryGetValue(road, out var cached) || cached.Hash != hash)
                    cache[road] = cached = new Cached { Hash = hash, Path = RoadPath.Sample(road) };
                paths.Add(cached.Path);
            }
            return paths;
        }

        void RunCheck()
        {
            var paths = Paths();
            if (network == null) { issues.Clear(); status = "No Road Network in the open scene."; Repaint(); return; }
            var started = DateTime.Now;
            issues = RoadPlanCheck.Run(paths, out var zones);
            status = $"{paths.Count} roads, {zones.Count} junctions, {issues.Count} problem(s). Checked in {(DateTime.Now - started).TotalMilliseconds:0} ms.";
            selectedIssue = -1;
            Repaint();
        }

        void RebuildChunks()
        {
            var paths = Paths();
            var roads = cache.ToDictionary(kv => kv.Value.Path, kv => kv.Key);
            chunks = new List<PlanChunk>();
            foreach (var path in paths)
            {
                int perChunk = Mathf.Max(2, Mathf.RoundToInt(ChunkLength / path.Spacing));
                int stride = Mathf.Max(1, Mathf.RoundToInt(PointStep / path.Spacing));
                for (int from = 0; from < path.Count - 1; from += perChunk)
                {
                    int to = Mathf.Min(path.Count - 1, from + perChunk);
                    var points = new List<Vector3>();
                    for (int i = from; i < to; i += stride) points.Add(path.Centre[i]);
                    points.Add(path.Centre[to]);
                    float xMin = points.Min(p => p.x), xMax = points.Max(p => p.x), zMin = points.Min(p => p.z), zMax = points.Max(p => p.z);
                    chunks.Add(new PlanChunk
                    {
                        Road = roads[path], Points = points.ToArray(), Height = points.Average(p => p.y), Width = path.Settings.Width,
                        Bounds = Rect.MinMaxRect(xMin - path.HalfWidth, zMin - path.HalfWidth, xMax + path.HalfWidth, zMax + path.HalfWidth)
                    });
                }
            }
            chunks.Sort((a, b) => a.Height.CompareTo(b.Height));
            minHeight = chunks.Count > 0 ? chunks.Min(c => c.Height) : 0f;
            maxHeight = chunks.Count > 0 ? Mathf.Max(minHeight + 1f, chunks.Max(c => c.Height)) : 1f;
            Repaint();
        }

        public static Color Shade(float height, float min, float max) => Color.Lerp(Low, High, Mathf.InverseLerp(min, max, height));

        void FrameAll()
        {
            if (chunks.Count == 0) return;
            var bounds = chunks[0].Bounds;
            foreach (var c in chunks) bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, c.Bounds.xMin), Mathf.Min(bounds.yMin, c.Bounds.yMin),
                                                           Mathf.Max(bounds.xMax, c.Bounds.xMax), Mathf.Max(bounds.yMax, c.Bounds.yMax));
            centre = bounds.center;
            float width = Mathf.Max(100f, position.width - PanelWidth), height = Mathf.Max(100f, position.height - 40f);
            scale = 0.9f * Mathf.Min(width / Mathf.Max(bounds.width, 1f), height / Mathf.Max(bounds.height, 1f));
            Repaint();
        }

        Vector2 ToScreen(Vector3 world) =>
            new Vector2((world.x - centre.x) * scale + canvas.width / 2f, -(world.z - centre.y) * scale + canvas.height / 2f);

        Vector2 ToWorld(Vector2 screen) =>
            new Vector2((screen.x - canvas.width / 2f) / scale + centre.x, -(screen.y - canvas.height / 2f) / scale + centre.y);

        void OnGUI()
        {
            Toolbar();
            var area = new Rect(0f, EditorGUIUtility.singleLineHeight + 6f, position.width, position.height - EditorGUIUtility.singleLineHeight - 6f);
            canvas = new Rect(area.x, area.y, area.width - PanelWidth, area.height);
            var panel = new Rect(canvas.xMax, area.y, PanelWidth, area.height);

            GUI.BeginClip(canvas);
            var local = new Rect(0f, 0f, canvas.width, canvas.height);
            if (Event.current.type == EventType.Repaint) Draw(local);
            Input(local);
            GUI.EndClip();
            Panel(panel);
        }

        void Toolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var picked = (RoadNetwork)EditorGUILayout.ObjectField(network, typeof(RoadNetwork), true, GUILayout.Width(220f));
                if (picked != network) { network = picked; cache.Clear(); Refresh(frame: true); }
                if (GUILayout.Button("Frame", EditorStyles.toolbarButton)) FrameAll();
                if (GUILayout.Button("Check", EditorStyles.toolbarButton)) EditorApplication.delayCall += RunCheck;
                using (new EditorGUI.DisabledScope(network == null))
                {
                    if (GUILayout.Button("Connect Dead Ends", EditorStyles.toolbarButton)) EditorApplication.delayCall += ConnectDeadEnds;
                    if (GUILayout.Button("Save PNG", EditorStyles.toolbarButton)) EditorApplication.delayCall += SavePng;
                    GUILayout.FlexibleSpace();
                    bool stale = network != null && network.surfaceStale;
                    var old = GUI.backgroundColor;
                    if (stale) GUI.backgroundColor = new Color(1f, 0.6f, 0.4f);
                    if (GUILayout.Button(stale ? "Commit to 3D (3D is out of date)" : "Commit to 3D", EditorStyles.toolbarButton))
                        EditorApplication.delayCall += () => { RoadNetworkBuild.Rebuild(network); Repaint(); };
                    GUI.backgroundColor = old;
                }
            }
        }

        void Draw(Rect rect)
        {
            EditorGUI.DrawRect(rect, Background);
            DrawGrid(rect);
            var view = Rect.MinMaxRect(ToWorld(new Vector2(0f, rect.height)).x, ToWorld(new Vector2(0f, rect.height)).y,
                                       ToWorld(new Vector2(rect.width, 0f)).x, ToWorld(new Vector2(rect.width, 0f)).y);
            var active = RoadNodes.ActiveRoad();
            var visible = new List<(PlanChunk chunk, Vector3[] points)>();
            foreach (var chunk in chunks)
            {
                if (chunk.Road == null || !chunk.Bounds.Overlaps(view)) continue;
                var points = ScreenPoints(chunk.Points);
                if (points.Length >= 2) visible.Add((chunk, points));
            }
            foreach (var layer in Layers(visible, v => v.chunk.Height))
            {
                foreach (var (chunk, points) in layer)
                {
                    Handles.color = chunk.Road == active ? Color.white : Outline;
                    Handles.DrawAAPolyLine(Mathf.Max(2f, chunk.Width * scale) + (chunk.Road == active ? 4f : 2f), points);
                }
                foreach (var (chunk, points) in layer)
                {
                    Handles.color = Shade(chunk.Height, minHeight, maxHeight);
                    Handles.DrawAAPolyLine(Mathf.Max(2f, chunk.Width * scale), points);
                }
            }
            if (active != null) DrawNodes(active);
            for (int i = 0; i < issues.Count; i++)
            {
                Vector2 at = ToScreen(issues[i].Position);
                Handles.color = i == selectedIssue ? Color.yellow : Color.red;
                Handles.DrawWireDisc(at, Vector3.forward, i == selectedIssue ? 14f : 9f, 3f);
            }
            DrawLegend(rect);
        }

        // Runs of items (already sorted by height) that share one height band.
        public static IEnumerable<List<T>> Layers<T>(IReadOnlyList<T> sorted, Func<T, float> height)
        {
            for (int start = 0; start < sorted.Count;)
            {
                float band = Mathf.Floor(height(sorted[start]) / LayerHeight);
                var layer = new List<T>();
                while (start < sorted.Count && Mathf.Floor(height(sorted[start]) / LayerHeight) == band) layer.Add(sorted[start++]);
                yield return layer;
            }
        }

        Vector3[] ScreenPoints(Vector3[] world)
        {
            var points = new List<Vector3>(world.Length);
            foreach (var w in world)
            {
                Vector2 s = ToScreen(w);
                if (points.Count == 0 || ((Vector2)points[points.Count - 1] - s).sqrMagnitude >= MinDrawGap * MinDrawGap || w == world[world.Length - 1])
                    points.Add(s);
            }
            return points.ToArray();
        }

        void DrawGrid(Rect rect)
        {
            float step = 1000f;
            while (step * scale < 40f) step *= 2f;
            while (step * scale > 400f && step > 100f) step /= 2f;
            Vector2 min = ToWorld(new Vector2(0f, rect.height)), max = ToWorld(new Vector2(rect.width, 0f));
            Handles.color = new Color(1f, 1f, 1f, 0.06f);
            for (float x = Mathf.Floor(min.x / step) * step; x <= max.x; x += step)
                Handles.DrawLine(ToScreen(new Vector3(x, 0f, min.y)), ToScreen(new Vector3(x, 0f, max.y)));
            for (float z = Mathf.Floor(min.y / step) * step; z <= max.y; z += step)
                Handles.DrawLine(ToScreen(new Vector3(min.x, 0f, z)), ToScreen(new Vector3(max.x, 0f, z)));
            GUI.Label(new Rect(8f, rect.height - 22f, 300f, 18f), $"grid {step:0} m", EditorStyles.whiteMiniLabel);
        }

        void DrawLegend(Rect rect)
        {
            var bar = new Rect(rect.width - 170f, rect.height - 26f, 120f, 10f);
            for (int i = 0; i < 24; i++)
                EditorGUI.DrawRect(new Rect(bar.x + bar.width * i / 24f, bar.y, bar.width / 24f + 1f, bar.height), Color.Lerp(Low, High, i / 23f));
            GUI.Label(new Rect(bar.x - 46f, bar.y - 4f, 46f, 18f), $"{minHeight:0} m", EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(bar.xMax + 4f, bar.y - 4f, 60f, 18f), $"{maxHeight:0} m", EditorStyles.whiteMiniLabel);
        }

        void DrawNodes(Road road)
        {
            var selected = new HashSet<UnityEngine.Object>(Selection.objects);
            foreach (var node in RoadNodes.Of(road))
            {
                Vector2 at = ToScreen(node.transform.position);
                var box = new Rect(at.x - 4f, at.y - 4f, 8f, 8f);
                EditorGUI.DrawRect(new Rect(box.x - 1f, box.y - 1f, box.width + 2f, box.height + 2f), Outline);
                EditorGUI.DrawRect(box, selected.Contains(node.gameObject) ? Color.yellow : new Color(0.3f, 0.85f, 1f));
            }
        }

        void Input(Rect rect)
        {
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (!rect.Contains(e.mousePosition) && GUIUtility.hotControl != id) return;
            var road = RoadNodes.ActiveRoad();

            switch (e.type)
            {
                case EventType.ScrollWheel:
                {
                    Vector2 before = ToWorld(e.mousePosition);
                    scale = Mathf.Clamp(scale * Mathf.Exp(-e.delta.y * 0.05f), 0.005f, 20f);
                    centre += before - ToWorld(e.mousePosition);
                    e.Use();
                    break;
                }
                case EventType.MouseDown:
                    if (e.button == 1 || e.button == 2 || (e.button == 0 && e.alt))
                    {
                        panning = true;
                        GUIUtility.hotControl = id;
                        e.Use();
                    }
                    else if (e.button == 0) { Click(road, e); GUIUtility.hotControl = id; e.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    if (panning) centre += new Vector2(-e.delta.x, e.delta.y) / scale;
                    else if (dragRoad != null) DragNode(e.mousePosition);
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0;
                    panning = false;
                    if (dragRoad != null) { dragRoad = null; dragIndex = -1; RoadNodeEdits.FinishDrags(); }
                    e.Use();
                    break;
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.F) { FrameAll(); e.Use(); }
                    else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                    {
                        var nodes = Selection.GetFiltered<RoadNode>(SelectionMode.Editable);
                        foreach (var group in nodes.GroupBy(n => RoadNodes.RoadOf(n.gameObject)).Where(g => g.Key != null).ToList())
                        {
                            Selection.activeGameObject = group.Key.gameObject;
                            RoadNodeEdits.Remove(group.Key, group.Select(n => n.index).ToList());
                        }
                        e.Use();
                    }
                    break;
            }
            if (e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel) Repaint();
        }

        void Click(Road road, Event e)
        {
            Vector2 world = ToWorld(e.mousePosition);
            if (road != null && (EditorGUI.actionKey || e.shift))
            {
                var spline = road.Spline;
                var local = road.transform.InverseTransformPoint(new Vector3(world.x, road.transform.TransformPoint(spline[0].Position).y, world.y));
                if (EditorGUI.actionKey) RoadNodeEdits.Append(road, local);
                else RoadNodeEdits.Insert(road, local);
                return;
            }
            if (road != null)
                foreach (var node in RoadNodes.Of(road))
                    if ((ToScreen(node.transform.position) - e.mousePosition).magnitude <= NodePick)
                    {
                        Selection.activeGameObject = node.gameObject;
                        dragRoad = road;
                        dragIndex = node.index;
                        return;
                    }
            var picked = PickRoad(e.mousePosition);
            if (picked != null) Selection.activeGameObject = picked.gameObject;
        }

        // The topmost road under the mouse: chunks are drawn lowest first, so the last hit is the one on top.
        Road PickRoad(Vector2 mouse)
        {
            Road hit = null;
            foreach (var chunk in chunks)
            {
                if (chunk.Road == null) continue;
                float reach = Mathf.Max(RoadPickMin, chunk.Width * scale / 2f);
                for (int i = 1; i < chunk.Points.Length; i++)
                    if (DistanceToSegment(mouse, ToScreen(chunk.Points[i - 1]), ToScreen(chunk.Points[i])) <= reach) { hit = chunk.Road; break; }
            }
            return hit;
        }

        void DragNode(Vector2 mouse)
        {
            var node = RoadNodes.Of(dragRoad).FirstOrDefault(n => n.index == dragIndex);
            if (node == null) return;
            Vector2 world = ToWorld(mouse);
            var local = dragRoad.transform.InverseTransformPoint(new Vector3(world.x, node.transform.position.y, world.y));
            RoadNodeEdits.Move(dragRoad, dragIndex, local);
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        void Panel(Rect rect)
        {
            GUILayout.BeginArea(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f));
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Layout only: heights decide which roads meet, but grades, air and clearance are checked in 3D by the Road Checker after Commit to 3D.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Drag: move node · Ctrl+click: add node at end · Shift+click: insert node · Delete: remove node · Right/middle drag: pan · Wheel: zoom · F: frame", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            if (report.Count > 0)
            {
                EditorGUILayout.LabelField("Connect Dead Ends", EditorStyles.boldLabel);
                foreach (var line in report) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space();
            }
            EditorGUILayout.LabelField(issues.Count == 0 ? "No layout problems." : "Problems", EditorStyles.boldLabel);
            for (int i = 0; i < issues.Count; i++)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(issues[i].ToString(), EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("Show", GUILayout.Width(46f))) Show(i);
                }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void Show(int i)
        {
            selectedIssue = i;
            var issue = issues[i];
            centre = new Vector2(issue.Position.x, issue.Position.z);
            scale = Mathf.Max(scale, 1f);
            var road = network != null ? network.Roads.FirstOrDefault(r => r.name == issue.Road) : null;
            if (road != null) Selection.activeGameObject = road.gameObject;
            var view = SceneView.lastActiveSceneView;
            if (view != null) view.Frame(new Bounds(issue.Position, Vector3.one * 80f), false);
            Repaint();
        }

        void ConnectDeadEnds()
        {
            report = RoadNodeEdits.ConnectDeadEnds(network);
            foreach (var line in report) Debug.Log($"[Roads] {line}");
            if (report.Count == 0) report.Add("No dead ends.");
            RebuildChunks();
            RunCheck();
        }

        void SavePng()
        {
            string scene = network.gameObject.scene.name;
            string root = Path.GetDirectoryName(Application.dataPath);
            string file = EditorUtility.SaveFilePanel("Save Road Plan", root, $"{scene} Road Plan.png", "png");
            if (string.IsNullOrEmpty(file)) return;
            RoadPlanImage.Save(file, chunks, issues, minHeight, maxHeight);
            Debug.Log($"[Roads] Road Plan saved to {file}");
        }
    }
}
