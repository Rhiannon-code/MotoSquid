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

        static void Confirm(string scenePath, Action build)
        {
            if (!EditorUtility.DisplayDialog("Road proof of concept",
                    $"This creates (or replaces) {scenePath} and its road surface. Nothing else in the project is touched.",
                    "Build", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorApplication.delayCall += () => build();
        }

        // Replaces only the roads under the open scene's Road Network; the bike and everything else stay.
        [MenuItem("MotoSquid/Roads/Proof of Concept/Update Roads In Open Scene")]
        static void UpdateRoads()
        {
            var network = UnityEngine.Object.FindFirstObjectByType<RoadNetwork>();
            if (network == null)
            {
                EditorUtility.DisplayDialog("Road proof of concept", "No Road Network in the open scene. Build a test scene first.", "OK");
                return;
            }
            bool interchange = network.gameObject.scene.path == InterchangeScenePath;
            EditorApplication.delayCall += () =>
            {
                network.barrierMaterial = EnsureMaterial(BarrierMaterialPath, null, new Color(0.72f, 0.72f, 0.7f), 0.2f);
                CreateRoads(network, interchange ? RoadInterchangeLayout.Roads() : RoadPocLayout.Roads());
                RoadNetworkBuild.Rebuild(network);
                EditorSceneManager.SaveScene(network.gameObject.scene);
                Selection.activeGameObject = network.gameObject;
            };
        }

        static void BuildScene(string scenePath, List<RoadPocLayout.RoadDefinition> definitions, Vector3 groundCentre, Vector3 groundScale,
                               Vector3 cameraPosition, Quaternion cameraRotation)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenes", "Road PoC");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Assets are loaded after NewScene: loading them before it unloads them and the scene saves null references.
            Material roadMaterial = EnsureMaterial(RoadMaterialPath, EnsureGridTexture(), Color.white, 0.25f);
            Material groundMaterial = EnsureMaterial(GroundMaterialPath, null, new Color(0.22f, 0.27f, 0.2f), 0.1f);
            Material barrierMaterial = EnsureMaterial(BarrierMaterialPath, null, new Color(0.72f, 0.72f, 0.7f), 0.2f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = groundCentre;
            ground.transform.localScale = groundScale;
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

            var networkObject = new GameObject("Road Network");
            var network = networkObject.AddComponent<RoadNetwork>();
            network.surfaceMaterial = roadMaterial;
            network.barrierMaterial = barrierMaterial;
            CreateRoads(network, definitions);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.gameObject.AddComponent<Unity.Cinemachine.CinemachineBrain>();
                camera.farClipPlane = 6000f;
                camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            RoadNetworkBuild.Rebuild(network);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = networkObject;
        }

        static void CreateRoads(RoadNetwork network, List<RoadPocLayout.RoadDefinition> definitions)
        {
            var old = network.transform.Find("Roads");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var roads = new GameObject("Roads").transform;
            roads.SetParent(network.transform, false);
            foreach (var definition in definitions)
            {
                var go = new GameObject(definition.Name);
                go.transform.SetParent(roads, false);
                var container = go.AddComponent<SplineContainer>();
                container.Splines = new[] { RoadPocLayout.ToSpline(definition.Knots) };
                go.AddComponent<Road>().settings = definition.Settings;
            }
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
