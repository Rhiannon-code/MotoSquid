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
            if (network.surfaceStale)
                EditorGUILayout.HelpBox("The roads changed since the 3D surface was built. Check the layout in the Road Plan, then Commit to 3D.", MessageType.Warning);
            EditorGUILayout.Space();
            if (GUILayout.Button("Open Road Plan"))
                RoadPlanWindow.Open(network);
            if (GUILayout.Button("Rebuild surface (Commit to 3D)"))
                EditorApplication.delayCall += () => RoadNetworkBuild.Rebuild(network);
            if (GUILayout.Button("Run road checker"))
                EditorApplication.delayCall += () => RoadCheckerWindow.Check(network);
        }
    }
}
