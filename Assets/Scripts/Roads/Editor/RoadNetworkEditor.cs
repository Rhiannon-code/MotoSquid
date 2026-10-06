using UnityEditor;
using UnityEngine;

namespace MotoSquid.Roads
{
    [CustomEditor(typeof(RoadNetwork))]
    public class RoadNetworkEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var network = (RoadNetwork)target;
            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild surface"))
                EditorApplication.delayCall += () => RoadNetworkBuild.Rebuild(network);
            if (GUILayout.Button("Run road checker"))
                EditorApplication.delayCall += () => RoadCheckerWindow.Check(network);
        }
    }
}
