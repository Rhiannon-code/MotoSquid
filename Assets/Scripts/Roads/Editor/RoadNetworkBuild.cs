using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotoSquid.Roads
{
    public static class RoadNetworkBuild
    {
        public const string SurfaceName = "Road Surface (generated)";
        public const string BarriersName = "Road Barriers (generated)";

        public static List<RoadPath> Sample(RoadNetwork network)
        {
            var paths = new List<RoadPath>();
            foreach (var road in network.Roads)
                paths.Add(RoadPath.Sample(road.name, road.Spline, road.transform.localToWorldMatrix, road.settings));
            return paths;
        }

        // The surface is generated output: rebuilding replaces it. Edit the roads' splines and settings.
        public static RoadSurface Rebuild(RoadNetwork network)
        {
            string scenePath = network.gameObject.scene.path;
            if (string.IsNullOrEmpty(scenePath))
            {
                EditorUtility.DisplayDialog("Road surface", "Save the scene first: the surface mesh is saved next to it.", "OK");
                return null;
            }

            var surface = RoadSurfaceBuilder.Build(Sample(network), network.uvTileSize);
            string folder = Path.GetDirectoryName(scenePath).Replace('\\', '/');
            var surfaceMesh = SaveMesh($"{folder}/{network.name} Surface.asset", surface.Vertices, surface.Triangles, surface.Uvs);
            var barrierMesh = SaveMesh($"{folder}/{network.name} Barriers.asset", surface.BarrierVertices, surface.BarrierTriangles, surface.BarrierUvs);
            AssetDatabase.SaveAssets();

            Place(network, SurfaceName, surfaceMesh, network.surfaceMaterial);
            Place(network, BarriersName, barrierMesh, network.barrierMaterial);
            EditorSceneManager.MarkSceneDirty(network.gameObject.scene);

            Debug.Log($"[Roads] Built {network.name}: {surface.Vertices.Count} vertices, {surface.Triangles.Count / 3} triangles, " +
                      $"{surface.Zones.Count} junctions, {surface.BarrierTriangles.Count / 3} barrier triangles.");
            foreach (var zone in surface.Zones) Debug.Log($"[Roads] Junction: {RoadSurfaceBuilder.Describe(zone)}");
            foreach (var warning in surface.Warnings) Debug.LogWarning($"[Roads] {warning}");
            return surface;
        }

        static Mesh SaveMesh(string path, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Generated children: rebuilding replaces their meshes, so nothing on them should be hand-edited.
        static void Place(RoadNetwork network, string name, Mesh mesh, Material material)
        {
            var child = network.transform.Find(name);
            var go = child != null ? child.gameObject : new GameObject(name);
            go.transform.SetParent(network.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Ensure<MeshFilter>(go).sharedMesh = mesh;
            var renderer = Ensure<MeshRenderer>(go);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            var collider = Ensure<MeshCollider>(go);
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                                                       StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        }

        static T Ensure<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
    }
}
