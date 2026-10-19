using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace MotoSquid.Roads
{
    // Keeps a RoadNode on every knot of every road. The spline is the road; the nodes only show its knots and
    // carry them when dragged, so whenever the spline changes some other way the nodes are put back on it.
    [InitializeOnLoad]
    public static class RoadNodes
    {
        // Set while node edits write the spline themselves, so the change isn't echoed back onto the nodes.
        internal static bool Writing;
        static bool queued;
        // The knots each road's nodes were last put on. A node missing from those same knots was deleted; knots that
        // changed since (Update Roads, a recipe edit) just need their nodes brought up to date.
        static readonly Dictionary<Road, int> syncedTo = new Dictionary<Road, int>();

        static RoadNodes()
        {
            Spline.Changed += (_, _, _) => { if (!Writing) Queue(); };
            Undo.undoRedoPerformed += Queue;
            Selection.selectionChanged += Queue;
            EditorSceneManager.sceneOpened += (_, _) => Queue();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        public static Road RoadOf(GameObject go)
        {
            if (go == null) return null;
            if (go.TryGetComponent(out Road road)) return road;
            return go.TryGetComponent(out RoadNode _) && go.transform.parent != null ? go.transform.parent.GetComponent<Road>() : null;
        }

        public static Road ActiveRoad() => RoadOf(Selection.activeGameObject);

        public static List<RoadNode> Of(Road road)
        {
            var nodes = new List<RoadNode>();
            foreach (Transform child in road.transform)
                if (child.TryGetComponent(out RoadNode node)) nodes.Add(node);
            nodes.Sort((a, b) => a.index.CompareTo(b.index));
            return nodes;
        }

        public static void Queue()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += () =>
            {
                queued = false;
                foreach (var road in Object.FindObjectsByType<Road>(FindObjectsSortMode.None)) Sync(road);
            };
        }

        // Node i sits on knot i: missing nodes are made, spare ones removed, moved ones put back.
        public static void Sync(Road road)
        {
            if (road == null || !road.TryGetComponent(out SplineContainer container) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var spline = container.Spline;
            var byIndex = new RoadNode[spline.Count];
            bool changed = false;
            foreach (var node in Of(road))
            {
                if (node.index >= 0 && node.index < byIndex.Length && byIndex[node.index] == null) byIndex[node.index] = node;
                else { Object.DestroyImmediate(node.gameObject); changed = true; }
            }
            for (int i = 0; i < byIndex.Length; i++)
            {
                if (byIndex[i] == null)
                {
                    var go = new GameObject($"Node {i}") { tag = "EditorOnly" };
                    go.transform.SetParent(road.transform, false);
                    byIndex[i] = go.AddComponent<RoadNode>();
                    byIndex[i].index = i;
                    changed = true;
                }
                if (byIndex[i].name != $"Node {i}") byIndex[i].name = $"Node {i}";
                Vector3 knot = spline[i].Position;
                var t = byIndex[i].transform;
                if ((t.localPosition - knot).sqrMagnitude > 1e-8f) { t.localPosition = knot; changed = true; }
            }
            syncedTo[road] = KnotsHash(spline);
            if (changed) EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
        }

        static int KnotsHash(Spline spline)
        {
            unchecked
            {
                int hash = spline.Count;
                foreach (var knot in spline.Knots) hash = hash * 31 + knot.Position.GetHashCode();
                return hash;
            }
        }

        // Renumbers the nodes for knots inserted (by > 0) or removed (by < 0) at `from`; removed knots' nodes go.
        public static void Shift(Road road, int from, int by)
        {
            foreach (var node in Of(road))
            {
                if (node.index < from) continue;
                if (by < 0 && node.index < from - by) { Object.DestroyImmediate(node.gameObject); continue; }
                node.index += by;
            }
        }

        public static void Remove(Road road, ICollection<int> knots)
        {
            var kept = new List<RoadNode>();
            foreach (var node in Of(road))
                if (knots.Contains(node.index)) Object.DestroyImmediate(node.gameObject);
                else kept.Add(node);
            for (int i = 0; i < kept.Count; i++) kept[i].index = i;
        }

        // A node deleted from the Hierarchy takes its knot with it. Any other mismatch (a road with no nodes yet,
        // knots changed since the nodes were placed) only brings the nodes up to date.
        internal static void OnHierarchyChanged()
        {
            if (Writing || EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var road in Object.FindObjectsByType<Road>(FindObjectsSortMode.None))
            {
                var nodes = Of(road);
                int knots = road.Spline.Count;
                if (nodes.Count == 0 || nodes.Count >= knots) continue;
                if (!syncedTo.TryGetValue(road, out int hash) || hash != KnotsHash(road.Spline)) continue;
                var present = new HashSet<int>();
                foreach (var node in nodes) present.Add(node.index);
                var missing = new List<int>();
                for (int i = 0; i < knots; i++) if (!present.Contains(i)) missing.Add(i);
                RoadNodeEdits.Remove(road, missing);
            }
            Queue();
        }

        public static int ShapeHash(Road road)
        {
            unchecked
            {
                int hash = road.transform.localToWorldMatrix.GetHashCode() * 31 + road.settings.Width.GetHashCode();
                hash = hash * 31 + road.handShaped.GetHashCode();
                foreach (var knot in road.Spline.Knots)
                    hash = hash * 31 + knot.Position.GetHashCode() * 7 + knot.TangentOut.GetHashCode() + knot.TangentIn.GetHashCode() + knot.Rotation.GetHashCode();
                foreach (var h in road.recipe.heights)
                    hash = hash * 31 + h.station.GetHashCode() * 7 + h.height.GetHashCode() + h.radius.GetHashCode();
                return hash;
            }
        }
    }
}
