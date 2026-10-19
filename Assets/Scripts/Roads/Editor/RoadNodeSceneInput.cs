using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Roads
{
    // Road nodes in the Scene view, Road Architect style: select a road (or one of its nodes) and its nodes show
    // as buttons; click one to select it and move it with the Move tool. Ctrl+click adds a node at the nearer end,
    // Shift+click inserts one on the road, Delete removes the selected nodes. Letting go updates the Road Plan;
    // the 3D surface waits for Commit to 3D.
    [InitializeOnLoad]
    static class RoadNodeSceneInput
    {
        static readonly int Hint = "RoadNodeSceneInput".GetHashCode();

        static RoadNodeSceneInput() => SceneView.duringSceneGui += OnSceneGUI;

        static void OnSceneGUI(SceneView view)
        {
            var e = Event.current;
            TrackMoves();
            if (RoadNodeEdits.Dragging && GUIUtility.hotControl == 0 && e.type != EventType.MouseDrag) RoadNodeEdits.FinishDrags();

            var road = RoadNodes.ActiveRoad();
            if (road == null) return;
            Delete(e);
            DrawNodes(road, e);
            AddOrInsert(road, e);
        }

        // Selected nodes the Move tool (or the Inspector) has moved off their knots carry the knots with them.
        static void TrackMoves()
        {
            foreach (var node in Selection.GetFiltered<RoadNode>(SelectionMode.Editable))
            {
                var road = RoadNodes.RoadOf(node.gameObject);
                if (road == null || node.index >= road.Spline.Count) continue;
                Vector3 knot = road.Spline[node.index].Position;
                if ((knot - node.transform.localPosition).sqrMagnitude > 1e-6f)
                    RoadNodeEdits.Move(road, node.index, node.transform.localPosition);
            }
        }

        static void Delete(Event e)
        {
            if (e.type != EventType.ValidateCommand && e.type != EventType.ExecuteCommand) return;
            if (e.commandName != "Delete" && e.commandName != "SoftDelete") return;
            var selected = Selection.GetFiltered<RoadNode>(SelectionMode.Editable);
            if (selected.Length == 0) return;
            if (e.type == EventType.ExecuteCommand)
                foreach (var group in selected.GroupBy(n => RoadNodes.RoadOf(n.gameObject)).Where(g => g.Key != null).ToList())
                {
                    Selection.activeGameObject = group.Key.gameObject;
                    RoadNodeEdits.Remove(group.Key, group.Select(n => n.index).ToList());
                }
            e.Use();
        }

        static void DrawNodes(Road road, Event e)
        {
            var selected = new HashSet<Object>(Selection.objects);
            foreach (var node in RoadNodes.Of(road))
            {
                Vector3 at = node.transform.position;
                float size = HandleUtility.GetHandleSize(at) * 0.08f;
                Handles.color = selected.Contains(node.gameObject) ? Color.yellow : new Color(0.3f, 0.85f, 1f);
                if (!Handles.Button(at, Quaternion.identity, size, size * 1.3f, Handles.SphereHandleCap)) continue;
                if (e.shift) Selection.objects = Selection.objects.Append(node.gameObject).ToArray();
                else Selection.activeGameObject = node.gameObject;
            }
        }

        static void AddOrInsert(Road road, Event e)
        {
            bool add = EditorGUI.actionKey, insert = e.shift;
            if (!(add || insert) || e.alt) return;
            int id = GUIUtility.GetControlID(Hint, FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);
            if (e.type != EventType.MouseDown || e.button != 0 || HandleUtility.nearestControl != id) return;
            if (!PointUnderMouse(road, e.mousePosition, out Vector3 world)) return;
            Vector3 local = road.transform.InverseTransformPoint(world);
            if (add) RoadNodeEdits.Append(road, local);
            else RoadNodeEdits.Insert(road, local);
            e.Use();
        }

        // What the mouse is over, or failing that the level of the road's nearer end.
        static bool PointUnderMouse(Road road, Vector2 mouse, out Vector3 point)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            if (Physics.Raycast(ray, out var hit, 50000f)) { point = hit.point; return true; }
            var spline = road.Spline;
            float y = road.transform.TransformPoint(spline[0].Position).y;
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            bool found = plane.Raycast(ray, out float distance);
            point = found ? ray.GetPoint(distance) : default;
            return found;
        }
    }
}
