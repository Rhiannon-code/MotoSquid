using System.Text;
using UnityEditor;
using UnityEngine;
using NGS.MagicLOD.Runtime.Components;

namespace NGS.MagicLOD.Editors.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MagicLODRuntimeComponent))]
    public sealed class MagicLODRuntimeComponentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length > 1)
                return;

            EditorGUILayout.Space();

            MagicLODRuntimeComponent component = (MagicLODRuntimeComponent)target;

            if (component.HasErrors)
            {
                EditorGUILayout.HelpBox(component.ErrorsText, MessageType.Error);
                return;
            }

            if (component.IsDecimationInProgress)
            {
                EditorGUILayout.HelpBox("In Progress...", MessageType.Info);
                return;
            }

            if (!component.IsDecimated)
            {
                EditorGUILayout.HelpBox("Not Decimated", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(CreateStatisticsMessage(component), MessageType.Info);
        }

        public override bool RequiresConstantRepaint()
        {
            return targets.Length == 1 &&
                Application.isPlaying &&
                target is MagicLODRuntimeComponent component &&
                component.IsDecimationInProgress;
        }


        private static string CreateStatisticsMessage(MagicLODRuntimeComponent component)
        {
            int[] decimatedTriangles = component.DecimatedTrianglesCount;
            StringBuilder message = new StringBuilder();

            message.Append("Original: ");
            message.Append(component.OriginalTrianglesCount);
            message.Append(" triangles");

            for (int i = 0; i < decimatedTriangles.Length; i++)
            {
                message.Append('\n');

                if (decimatedTriangles.Length == 1)
                {
                    message.Append("Simplified");
                }
                else
                {
                    message.Append("LOD ");
                    message.Append(i);
                }

                message.Append(": ");
                message.Append(decimatedTriangles[i]);
                message.Append(" triangles");
            }

            return message.ToString();
        }
    }
}
