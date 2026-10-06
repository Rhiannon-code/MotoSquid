using MotoSquid.Rider;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.DevTools
{
    public static class RigLabTargetGizmos
    {
        public static HashSet<string> Highlight = new HashSet<string>();
        public static bool Enabled = true;

        static readonly Color Hip = new Color(1f, 0.82f, 0.2f);
        static readonly Color Spine = new Color(0.3f, 0.85f, 1f);
        static readonly Color Leg = new Color(0.45f, 1f, 0.45f);
        static readonly Color Hand = new Color(1f, 0.45f, 0.9f);

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        static void Draw(BikeAnimationTargets targets, GizmoType gizmoType)
        {
            if (!Enabled) return;

            foreach (var f in typeof(BikeAnimationTargets).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.FieldType != typeof(Transform)) continue;
                var tr = f.GetValue(targets) as Transform;
                if (tr == null) continue;

                bool lit = Highlight.Count == 0 || Highlight.Contains(f.Name);
                var colour = ColourFor(f.Name);
                if (!lit) colour.a = 0.18f;

                float size = lit ? 0.055f : 0.03f;

                Gizmos.color = colour;
                Gizmos.matrix = Matrix4x4.TRS(tr.position, tr.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one * size);

                if (IsRotational(f.Name))
                {
                    float len = lit ? 0.22f : 0.12f;
                    Gizmos.DrawLine(Vector3.zero, Vector3.forward * len);
                    Gizmos.DrawLine(Vector3.forward * len, new Vector3(0f, 0.035f, len - 0.05f));
                    Gizmos.DrawLine(Vector3.forward * len, new Vector3(0f, -0.035f, len - 0.05f));
                }
                Gizmos.matrix = Matrix4x4.identity;

                if (lit)
                {
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    style.normal.textColor = colour;
                    Handles.Label(tr.position + Vector3.up * 0.07f,
                        Pretty(f.Name) + (RotationOnly(f.Name) ? "  (rotation only)" : ""), style);
                }
            }
        }

        static bool IsRotational(string field)
        {
            return field.StartsWith("spine") || field.StartsWith("hip");
        }

        // The spine rigs are MultiRotationConstraints: they read the target's rotation and ignore
        // its position, so dragging one does nothing. The hip is a MultiParent and uses both
        static bool RotationOnly(string field)
        {
            return field.StartsWith("spine");
        }

        static Color ColourFor(string field)
        {
            if (field.StartsWith("hip")) return Hip;
            if (field.StartsWith("spine")) return Spine;
            if (field.Contains("leg") || field.Contains("Leg")) return Leg;
            return Hand;
        }

        static string Pretty(string field)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < field.Length; i++)
            {
                char c = field[i];
                if (i > 0 && char.IsUpper(c)) sb.Append(' ');
                sb.Append(i == 0 ? char.ToUpper(c) : c);
            }
            return sb.ToString().Replace(" Target", "");
        }
    }
}
