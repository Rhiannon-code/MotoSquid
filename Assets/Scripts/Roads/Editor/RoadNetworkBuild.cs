using System.Collections.Generic;
using System.IO;
using NGS.MagicLOD.Runtime;
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
        public const string GroundName = "Ground (generated)";

        public static List<RoadPath> Sample(RoadNetwork network)
        {
            var paths = new List<RoadPath>();
            foreach (var road in network.Roads)
                paths.Add(RoadPath.Sample(road));
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

            var surface = RoadSurfaceBuilder.Build(Sample(network), network.uvTileSize, network.earthworks);
            string folder = Path.GetDirectoryName(scenePath).Replace('\\', '/');
            string prefix = $"{folder}/{Path.GetFileNameWithoutExtension(scenePath)} {network.name}";
            // Only walls and decks cast shadows: they are what stands over other roads and the ground.
            int tiles = PlaceTiles(network, SurfaceName, $"{prefix} Surface.asset",
                surface.Vertices, surface.Triangles, surface.Uvs, network.surfaceMaterial, ShadowCastingMode.Off, ColliderOverlap, lods: true);
            PlaceTiles(network, BarriersName, $"{prefix} Barriers.asset",
                surface.BarrierVertices, surface.BarrierTriangles, surface.BarrierUvs, network.barrierMaterial, ShadowCastingMode.On, 0f, lods: true);
            if (network.earthworks.enabled)
                PlaceTiles(network, GroundName, $"{prefix} Ground.asset",
                    surface.GroundVertices, surface.GroundTriangles, surface.GroundUvs, network.groundMaterial, ShadowCastingMode.Off, 0f, lods: false);
            else if (network.transform.Find(GroundName) is Transform ground)
                Object.DestroyImmediate(ground.gameObject);
            network.surfaceStale = false;
            EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(network.gameObject.scene);

            Debug.Log($"[Roads] Built {network.name}: {surface.Vertices.Count} vertices, {surface.Triangles.Count / 3} triangles in {tiles} tiles, " +
                      $"{surface.Zones.Count} junctions, {surface.BarrierTriangles.Count / 3} barrier triangles.");
            foreach (var zone in surface.Zones) Debug.Log($"[Roads] Junction: {RoadSurfaceBuilder.Describe(zone)}");
            foreach (var warning in surface.Warnings) Debug.LogWarning($"[Roads] {warning}");
            return surface;
        }

        // The road surface's collider tiles overlap their neighbours by this much, so a wheel crossing a tile
        // edge always has whole surface under it.
        const float ColliderOverlap = 10f;

        // Distant tiles draw coarser copies made by Magic LOD with their borders held in place, so neighbouring
        // tiles at different levels still meet. Kept fractions of the triangles, and the screen heights (of the
        // tile's bounds) below which each level takes over; colliders always keep the full mesh.
        static readonly float[] LodQuality = { 0.3f, 0.1f };
        static readonly float[] LodScreenHeights = { 0.25f, 0.08f, 0.002f };

        // One child per tile under the named object, each with its own mesh and collider. Every tile mesh lives
        // in one asset file, replaced whole on each rebuild because tiles come and go.
        static int PlaceTiles(RoadNetwork network, string name, string assetPath, List<Vector3> vertices, List<int> triangles,
                              List<Vector2> uvs, Material material, ShadowCastingMode shadows, float colliderOverlap, bool lods)
        {
            var whole = new Mesh { indexFormat = IndexFormat.UInt32 };
            whole.SetVertices(vertices);
            whole.SetTriangles(triangles, 0);
            whole.RecalculateNormals();
            var normals = new List<Vector3>();
            whole.GetNormals(normals);
            Object.DestroyImmediate(whole);

            var render = RoadTiles.Split(vertices, triangles, uvs, normals, network.tileSize);
            var colliders = colliderOverlap > 0f ? RoadTiles.Split(vertices, triangles, uvs, normals, network.tileSize, colliderOverlap) : render;

            AssetDatabase.DeleteAsset(assetPath);
            Mesh container = null;
            Mesh Save(string meshName, TileMesh tile)
            {
                var mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(tile.Vertices);
                mesh.SetTriangles(tile.Triangles, 0);
                mesh.SetUVs(0, tile.Uvs);
                mesh.SetNormals(tile.Normals);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                if (container == null)
                {
                    AssetDatabase.CreateAsset(mesh, assetPath);
                    container = mesh;
                }
                else AssetDatabase.AddObjectToAsset(mesh, container);
                return mesh;
            }

            var parent = EmptyParent(network, name);
            var keys = new SortedSet<(int, int)>(render.Keys);
            keys.UnionWith(colliders.Keys);
            foreach (var key in keys)
            {
                string tileName = $"{name.Replace(" (generated)", "")} {key.Item1},{key.Item2}";
                var go = new GameObject($"Tile {key.Item1},{key.Item2}");
                go.transform.SetParent(parent, false);
                go.layer = LayerFor(name);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                                                           StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                Mesh renderMesh = null;
                if (render.TryGetValue(key, out var renderTile))
                {
                    renderMesh = Save(tileName, renderTile);
                    go.AddComponent<MeshFilter>().sharedMesh = renderMesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = shadows;
                }
                if (colliders.TryGetValue(key, out var colliderTile))
                    go.AddComponent<MeshCollider>().sharedMesh = colliders == render ? renderMesh : Save($"{tileName} collider", colliderTile);
                if (lods && renderMesh != null) AddLods(go, renderMesh, tileName, material, shadows, mesh => Keep(mesh));
            }
            return render.Count;

            Mesh Keep(Mesh mesh)
            {
                AssetDatabase.AddObjectToAsset(mesh, container);
                return mesh;
            }
        }

        static void AddLods(GameObject tile, Mesh full, string tileName, Material material, ShadowCastingMode shadows, System.Func<Mesh, Mesh> keep)
        {
            var result = MeshDecimator.Decimate(full, DecimationSettings.Default, LodQuality);
            if (result.taskStatus != DecimationStatus.Success || result.decimatedMeshes == null)
            {
                Debug.LogWarning($"[Roads] No LODs for {tileName}: {result.errorText}");
                return;
            }
            var levels = new List<LOD> { new LOD(LodScreenHeights[0], new Renderer[] { tile.GetComponent<MeshRenderer>() }) };
            for (int i = 0; i < result.decimatedMeshes.Length; i++)
            {
                var mesh = result.decimatedMeshes[i];
                mesh.name = $"{tileName} LOD{i + 1}";
                var level = new GameObject($"LOD{i + 1}");
                level.transform.SetParent(tile.transform, false);
                level.layer = tile.layer;
                level.AddComponent<MeshFilter>().sharedMesh = keep(mesh);
                var renderer = level.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = shadows;
                GameObjectUtility.SetStaticEditorFlags(level, GameObjectUtility.GetStaticEditorFlags(tile));
                levels.Add(new LOD(LodScreenHeights[Mathf.Min(i + 1, LodScreenHeights.Length - 1)], new Renderer[] { renderer }));
            }
            var group = tile.AddComponent<LODGroup>();
            group.SetLODs(levels.ToArray());
            group.RecalculateBounds();
        }

        // The bikes' drivable mask, the AI's barrier mask and the grid snap all read these layers; a missing
        // layer name falls back to Default rather than failing the build
        static int LayerFor(string generated)
        {
            string layer = generated == SurfaceName ? "Road" : generated == BarriersName ? "Barriers" : null;
            return layer == null ? 0 : Mathf.Max(0, LayerMask.NameToLayer(layer));
        }

        // The named generated object, emptied: its old tiles go, and so do the single mesh and collider it
        // carried before tiles existed.
        static Transform EmptyParent(RoadNetwork network, string name)
        {
            var parent = network.transform.Find(name);
            if (parent == null)
            {
                parent = new GameObject(name).transform;
                parent.SetParent(network.transform, false);
            }
            foreach (var old in parent.GetComponents<Component>())
                if (old is MeshCollider || old is MeshRenderer || old is MeshFilter) Object.DestroyImmediate(old);
            for (int i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
            parent.localPosition = Vector3.zero;
            parent.localRotation = Quaternion.identity;
            parent.localScale = Vector3.one;
            return parent;
        }
    }
}
