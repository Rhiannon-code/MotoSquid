using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace MotoSquid.Roads
{
    // Builds the proof-of-concept scenes from their layouts. Rebuilding replaces the scene and its surface.
    public static class RoadPocSceneBuilder
    {
        const string Folder = "Assets/Scenes/Road PoC";
        const string ScenePath = Folder + "/Road PoC.unity";
        const string InterchangeScenePath = Folder + "/Interchange PoC.unity";
        const string CorridorScenePath = Folder + "/Corridor PoC.unity";
        const string CloverstackScenePath = Folder + "/Cloverstack PoC.unity";
        const string WorldScenePath = Folder + "/Open World Roads.unity";
        const string CorridorLayoutPath = Folder + "/Corridor Layout.asset";
        const string GridTexturePath = Folder + "/Road Grid.png";
        const string RoadMaterialPath = Folder + "/Road Grid.mat";
        const string GroundMaterialPath = Folder + "/Ground.mat";
        const string BarrierMaterialPath = Folder + "/Barrier.mat";

        [MenuItem("MotoSquid/Roads/Proof of Concept/Build Test Scene")]
        static void Build() => Confirm(ScenePath, () => BuildScene(ScenePath, RoadPocLayout.Roads(),
            new Vector3(0f, -0.05f, 300f), new Vector3(400f, 1f, 200f),
            new Vector3(-1150f, 25f, -60f), Quaternion.Euler(15f, 60f, 0f)));

        [MenuItem("MotoSquid/Roads/Proof of Concept/Build Interchange Scene")]
        static void BuildInterchange() => Confirm(InterchangeScenePath, () => BuildScene(InterchangeScenePath, RoadInterchangeLayout.Roads(),
            new Vector3(0f, -0.05f, 0f), new Vector3(420f, 1f, 420f),
            new Vector3(-600f, 120f, -600f), Quaternion.Euler(20f, 45f, 0f)));

        // The corridor stands on generated ground with cuttings and walls, so it has no ground plane.
        [MenuItem("MotoSquid/Roads/Proof of Concept/Build Corridor Scene")]
        static void BuildCorridor() => Confirm(CorridorScenePath, () => BuildScene(CorridorScenePath, RoadCorridorLayout.Roads(EnsureCorridorLayout().settings),
            Vector3.zero, Vector3.zero, new Vector3(1300f, 260f, -700f), Quaternion.Euler(10f, -54f, 0f), earthworks: true));

        [MenuItem("MotoSquid/Roads/Proof of Concept/Build Cloverstack Scene")]
        static void BuildCloverstack() => Confirm(CloverstackScenePath, () => BuildScene(CloverstackScenePath, RoadCloverstackLayout.Roads(),
            Vector3.zero, Vector3.zero, new Vector3(900f, 220f, -900f), Quaternion.Euler(12f, -45f, 0f), earthworks: true));

        // The whole open world from RoadData/world_roads.txt (tools/roads/build_world.py), on generated ground.
        [MenuItem("MotoSquid/Roads/Proof of Concept/Build Open World Scene")]
        static void BuildWorld() => Confirm(WorldScenePath, () => BuildScene(WorldScenePath, WorldRoads(),
            Vector3.zero, Vector3.zero, new Vector3(0f, 6000f, -6000f), Quaternion.Euler(45f, 0f, 0f), earthworks: true));

        static List<RoadPocLayout.RoadDefinition> WorldRoads()
        {
            var report = new List<string>();
            var roads = RoadWorldLayout.Roads(RoadWorldData.Load(RoadWorldData.DefaultFile), new WorldSettings(), report);
            Debug.Log($"[Roads] Open world: {roads.Count} roads\n" + string.Join("\n", report));
            return roads;
        }

        internal static void Confirm(string scenePath, Action build)
        {
            if (!EditorUtility.DisplayDialog("Road proof of concept",
                    $"This creates (or replaces) {scenePath} and the generated meshes saved next to it. Nothing else in the project is touched.",
                    "Build", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorApplication.delayCall += () => build();
        }

        // Replaces the layout's own roads under the open scene's Road Network and adds any it is missing. Roads
        // edited by hand, and roads the layout never made, are left alone; the bike and everything else stay.
        [MenuItem("MotoSquid/Roads/Proof of Concept/Update Roads In Open Scene")]
        static void UpdateRoads()
        {
            var network = UnityEngine.Object.FindFirstObjectByType<RoadNetwork>();
            if (network == null)
            {
                EditorUtility.DisplayDialog("Road proof of concept", "No Road Network in the open scene. Build a test scene first.", "OK");
                return;
            }
            string scenePath = network.gameObject.scene.path;
            EditorApplication.delayCall += () =>
            {
                network.barrierMaterial = EnsureMaterial(BarrierMaterialPath, null, new Color(0.72f, 0.72f, 0.7f), 0.2f);
                PlaceRoads(network, scenePath == InterchangeScenePath ? RoadInterchangeLayout.Roads()
                                   : scenePath == WorldScenePath ? WorldRoads()
                                   : scenePath == WorldPlanBuilder.ScenePath ? WorldPlanBuilder.Roads()
                                   : scenePath == CorridorScenePath ? RoadCorridorLayout.Roads(EnsureCorridorLayout().settings)
                                   : scenePath == CloverstackScenePath ? RoadCloverstackLayout.Roads()
                                   : RoadPocLayout.Roads());
                RoadNetworkBuild.Rebuild(network);
                EditorSceneManager.SaveScene(network.gameObject.scene);
                Selection.activeGameObject = network.gameObject;
            };
        }

        internal static void BuildScene(string scenePath, List<RoadPocLayout.RoadDefinition> definitions, Vector3 groundCentre, Vector3 groundScale,
                               Vector3 cameraPosition, Quaternion cameraRotation, bool earthworks = false)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenes", "Road PoC");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Assets are loaded after NewScene: loading them before it unloads them and the scene saves null references.
            Material roadMaterial = EnsureMaterial(RoadMaterialPath, EnsureGridTexture(), Color.white, 0.25f);
            Material groundMaterial = EnsureMaterial(GroundMaterialPath, null, new Color(0.22f, 0.27f, 0.2f), 0.1f);
            Material barrierMaterial = EnsureMaterial(BarrierMaterialPath, null, new Color(0.72f, 0.72f, 0.7f), 0.2f);

            if (!earthworks)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = groundCentre;
                ground.transform.localScale = groundScale;
                ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            }

            var networkObject = new GameObject("Road Network");
            var network = networkObject.AddComponent<RoadNetwork>();
            network.surfaceMaterial = roadMaterial;
            network.barrierMaterial = barrierMaterial;
            network.groundMaterial = groundMaterial;
            network.earthworks.enabled = earthworks;
            PlaceRoads(network, definitions);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.gameObject.AddComponent<Unity.Cinemachine.CinemachineBrain>();
                camera.farClipPlane = 20000f;
                camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            RoadNetworkBuild.Rebuild(network);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = networkObject;
        }

        static void PlaceRoads(RoadNetwork network, List<RoadPocLayout.RoadDefinition> definitions)
        {
            var root = network.transform.Find("Roads");
            if (root == null)
            {
                root = new GameObject("Roads").transform;
                root.SetParent(network.transform, false);
            }
            var existing = new Dictionary<string, Road>();
            foreach (var road in network.Roads) existing[road.name] = road;

            int added = 0, replaced = 0, kept = 0, removed = 0;
            var wanted = new HashSet<string>();
            foreach (var definition in definitions)
            {
                wanted.Add(definition.Name);
                if (existing.TryGetValue(definition.Name, out var road))
                {
                    if (road.edited) { kept++; continue; }
                    replaced++;
                }
                else
                {
                    var go = new GameObject(definition.Name, typeof(SplineContainer));
                    go.transform.SetParent(root, false);
                    road = go.AddComponent<Road>();
                    added++;
                }
                // The layout's knots are in the network's space, so a road moved as a whole goes back with them.
                road.transform.localPosition = Vector3.zero;
                road.transform.localRotation = Quaternion.identity;
                road.transform.localScale = Vector3.one;
                road.settings = definition.Settings;
                road.recipe = definition.Recipe ?? new RoadRecipe();
                road.generated = true;
                road.edited = false;
                road.handShaped = false;
                road.SetKnots(definition.Knots);
                EditorUtility.SetDirty(road);
            }
            foreach (var road in existing.Values)
                if (!wanted.Contains(road.name) && road.generated && !road.edited)
                {
                    UnityEngine.Object.DestroyImmediate(road.gameObject);
                    removed++;
                }
            Debug.Log($"[Roads] Update: {added} added, {replaced} replaced from the layout, {kept} kept (edited by hand), {removed} removed.");
        }

        // The asset is the source of the corridor's tuning; rebuilding the roads reads it. It is only written
        // when its numbers predate the current defaults (CorridorSettings.CurrentVersion), which resets them.
        static CorridorLayoutAsset EnsureCorridorLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<CorridorLayoutAsset>(CorridorLayoutPath);
            if (layout == null)
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenes", "Road PoC");
                layout = ScriptableObject.CreateInstance<CorridorLayoutAsset>();
                AssetDatabase.CreateAsset(layout, CorridorLayoutPath);
            }
            if (layout.settings.version != CorridorSettings.CurrentVersion)
            {
                if (layout.settings.version != 0) Debug.Log($"[Roads] {CorridorLayoutPath} held version {layout.settings.version} numbers; reset to the current defaults.");
                else Debug.Log($"[Roads] {CorridorLayoutPath} reset to the current (compact) defaults.");
                layout.settings = new CorridorSettings { version = CorridorSettings.CurrentVersion };
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
            }
            return layout;
        }

        // A 4 m tile with a line every metre, so bumps and kinks show in the Scene view.
        static Texture2D EnsureGridTexture()
        {
            if (!File.Exists(GridTexturePath))
            {
                const int size = 512;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var asphalt = new Color(0.32f, 0.32f, 0.34f);
                var line = new Color(0.45f, 0.45f, 0.48f);
                var edge = new Color(0.62f, 0.62f, 0.66f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool tileEdge = x < 3 || y < 3;
                    bool metre = x % (size / 4) < 2 || y % (size / 4) < 2;
                    texture.SetPixel(x, y, tileEdge ? edge : metre ? line : asphalt);
                }
                File.WriteAllBytes(GridTexturePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(GridTexturePath);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
        }

        static Material EnsureMaterial(string path, Texture2D baseMap, Color colour, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.SetTexture("_BaseColorMap", baseMap);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
