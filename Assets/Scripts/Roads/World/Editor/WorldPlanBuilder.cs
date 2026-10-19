using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Roads
{
    // Menus for the open world: import the approved freeway skeleton into the World Plan asset (the source the roads
    // are built from, so it refuses to overwrite hand edits without asking), and build the world's freeway scene.
    public static class WorldPlanBuilder
    {
        public const string Folder = "Assets/Scenes/World";
        public const string PlanPath = Folder + "/World Plan.asset";
        public const string ScenePath = Folder + "/World Freeways.unity";
        const string Source = "RoadData/world_plan.json";

        [MenuItem("MotoSquid/Roads/World/Import World Plan")]
        static void ImportMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<WorldPlan>(PlanPath) != null &&
                !EditorUtility.DisplayDialog("World Plan", $"Replace {PlanPath} with {Source}? Any edits made to the World Plan in the Inspector are lost.", "Replace", "Cancel"))
                return;
            Import();
        }

        public static WorldPlan Import()
        {
            EnsureFolder();
            var plan = AssetDatabase.LoadAssetAtPath<WorldPlan>(PlanPath);
            if (plan == null)
            {
                plan = ScriptableObject.CreateInstance<WorldPlan>();
                AssetDatabase.CreateAsset(plan, PlanPath);
            }
            JsonUtility.FromJsonOverwrite(File.ReadAllText(Source), plan);
            EditorUtility.SetDirty(plan);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Roads] World Plan imported: {plan.roads.Count} roads, {plan.sites.Count} sites.");
            Selection.activeObject = plan;
            return plan;
        }

        [MenuItem("MotoSquid/Roads/World/Build World Freeways Scene")]
        static void BuildMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<WorldPlan>(PlanPath) == null)
            {
                EditorUtility.DisplayDialog("World Plan", $"No {PlanPath} yet: run MotoSquid > Roads > World > Import World Plan first.", "OK");
                return;
            }
            RoadPocSceneBuilder.Confirm(ScenePath, Build);
        }

        public static void Build()
        {
            EnsureFolder();
            RoadPocSceneBuilder.BuildScene(ScenePath, Roads(), Vector3.zero, Vector3.zero,
                new Vector3(0f, 9000f, -9000f), Quaternion.Euler(45f, 0f, 0f), earthworks: true);
        }

        public static List<RoadPocLayout.RoadDefinition> Roads()
        {
            var report = new List<string>();
            var plan = AssetDatabase.LoadAssetAtPath<WorldPlan>(PlanPath);
            var roads = WorldPlanLayout.Roads(plan.roads, plan.sites, report);
            foreach (var line in report) Debug.LogWarning($"[Roads] World Plan: {line}");
            return roads;
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenes", "World");
        }
    }
}
