using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Roads
{
    public class RoadCheckerWindow : EditorWindow
    {
        const int ListedIssues = 300;

        [SerializeField] RoadCheckSettings settings = new RoadCheckSettings();
        RoadNetwork network;
        RoadCheckResult result;
        string error;
        Vector2 scroll;

        [MenuItem("MotoSquid/Roads/Road Checker")]
        static void Open() => GetWindow<RoadCheckerWindow>("Road Checker");

        public static void Check(RoadNetwork network)
        {
            var window = GetWindow<RoadCheckerWindow>("Road Checker");
            window.network = network;
            window.Run();
        }

        void OnGUI()
        {
            network = (RoadNetwork)EditorGUILayout.ObjectField("Road network", network, typeof(RoadNetwork), true);
            var serialized = new SerializedObject(this);
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(settings)), true);
            serialized.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(network == null))
                if (GUILayout.Button("Run checker")) EditorApplication.delayCall += Run;

            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (result == null) return;

            EditorGUILayout.HelpBox(result.Passed
                ? $"PASSED: {result.Samples:N0} samples, no breaches."
                : $"FAILED: {result.Issues.Count} breaches over {result.Samples:N0} samples.",
                result.Passed ? MessageType.Info : MessageType.Warning);

            foreach (var kv in result.Summary().OrderBy(kv => kv.Key))
                EditorGUILayout.LabelField(kv.Key.ToString(), $"{kv.Value.count} places, worst {kv.Value.worst:0.###}");

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Air time", EditorStyles.boldLabel);
            foreach (var air in result.Air.Where(a => a.Lane == 0))
                EditorGUILayout.LabelField(air.ToString());

            EditorGUILayout.LabelField("Breaches", EditorStyles.boldLabel);
            foreach (var issue in result.Issues.Take(ListedIssues))
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(issue.ToString());
                    if (GUILayout.Button("Show", GUILayout.Width(50))) Frame(issue.Position);
                }
            if (result.Issues.Count > ListedIssues)
                EditorGUILayout.LabelField($"... and {result.Issues.Count - ListedIssues} more (see the Console).");
            EditorGUILayout.EndScrollView();
        }

        void Run()
        {
            error = null;
            result = null;
            try
            {
                var surface = network.transform.Find(RoadNetworkBuild.SurfaceName);
                if (surface == null || !surface.TryGetComponent(out MeshCollider collider) || collider.sharedMesh == null)
                {
                    error = "No generated surface yet: press Rebuild surface on the Road Network first.";
                    return;
                }

                var paths = RoadNetworkBuild.Sample(network);
                var zones = RoadSurfaceBuilder.Prepare(paths);
                bool backfaces = Physics.queriesHitBackfaces;
                Physics.queriesHitBackfaces = true;
                try { result = RoadChecker.Run(paths, zones, new ColliderProbe(collider), settings); }
                finally { Physics.queriesHitBackfaces = backfaces; }

                Debug.Log($"[Roads] Checker: {(result.Passed ? "PASSED" : $"{result.Issues.Count} breaches")} over {result.Samples:N0} samples.");
                foreach (var air in result.Air) Debug.Log($"[Roads] {air}");
                foreach (var issue in result.Issues) Debug.LogWarning($"[Roads] {issue}");
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogException(e);
            }
            finally { Repaint(); }
        }

        static void Frame(Vector3 position)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.Frame(new Bounds(position, Vector3.one * 25f), false);
        }
    }
}
