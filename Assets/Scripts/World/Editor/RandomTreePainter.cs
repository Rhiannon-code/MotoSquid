#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

namespace MotoSquid.World
{
    public class RandomTreePainter : EditorWindow
    {
        Terrain terrain;
        float   brushRadius    = 10f;
        float   spacing        = 3f;
        float   heightScale    = 1f;
        float   heightVariance = 0.3f;
        float   rotationOffset   = 0f;
        float   rotationRangeMin = 0f;
        float   rotationRangeMax = 360f;
        bool    randomRotation   = true;

        bool[]  prototypeSelected = new bool[0];
        Vector2 protoScroll;
        bool    painting;

        int      targetSceneIndex = 0;
        string[] sceneNames       = new string[0];
        Scene[]  loadedScenes     = new Scene[0];
        GameObject treeContainer;

        static RandomTreePainter instance;

        [MenuItem("Tools/Random Tree Painter")]
        static void Open()
        {
            instance = GetWindow<RandomTreePainter>("Random Tree Painter");
            instance.RefreshScenes();
            instance.RefreshTerrain();
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            instance = this;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            painting = false;
        }

        void OnSceneOpened(Scene scene, OpenSceneMode mode) => RefreshScenes();
        void OnSceneClosed(Scene scene) => RefreshScenes();

        void RefreshScenes()
        {
            var scenes = new List<Scene>();
            var names  = new List<string> { "Terrain (paint into TerrainData)" };
            scenes.Add(default);

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.isLoaded)
                {
                    scenes.Add(s);
                    names.Add(s.name);
                }
            }

            loadedScenes     = scenes.ToArray();
            sceneNames       = names.ToArray();
            targetSceneIndex = Mathf.Clamp(targetSceneIndex, 0, loadedScenes.Length - 1);
            Repaint();
        }

        bool UseSceneSpawn =>
            targetSceneIndex > 0 &&
            targetSceneIndex < loadedScenes.Length &&
            loadedScenes[targetSceneIndex].IsValid() &&
            loadedScenes[targetSceneIndex].isLoaded;

        Scene TargetScene => loadedScenes[targetSceneIndex];

        void OnGUI()
        {
            EditorGUILayout.LabelField("Random Tree Painter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Hold LEFT MOUSE in the Scene view to paint.\n" +
                "Hold SHIFT + LEFT MOUSE to erase.",
                MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Target Scene", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (sceneNames.Length == 0) RefreshScenes();
            int newIdx = EditorGUILayout.Popup(targetSceneIndex, sceneNames);
            if (newIdx != targetSceneIndex)
            {
                targetSceneIndex = newIdx;
                treeContainer    = null;
            }
            if (GUILayout.Button("↺", GUILayout.Width(26))) RefreshScenes();
            EditorGUILayout.EndHorizontal();

            if (UseSceneSpawn)
            {
                EditorGUILayout.HelpBox(
                    $"Trees will be spawned as GameObjects into: {TargetScene.name}",
                    MessageType.Info);

                treeContainer = (GameObject)EditorGUILayout.ObjectField(
                    "Trees Container", treeContainer, typeof(GameObject), true);

                if (treeContainer != null && treeContainer.scene != TargetScene)
                {
                    EditorGUILayout.HelpBox(
                        $"Container is in '{treeContainer.scene.name}', not '{TargetScene.name}'. Please reassign or auto create.",
                        MessageType.Error);
                    treeContainer = null;
                }

                if (treeContainer == null)
                {
                    if (GUILayout.Button("Auto create Trees Container in " + TargetScene.name))
                        CreateTreeContainer();
                    EditorGUILayout.HelpBox(
                        "A container GameObject in the target scene is required to parent spawned trees.",
                        MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Trees will be painted into TerrainData (modifies the terrain in the main scene).",
                    MessageType.None);
            }

            EditorGUILayout.Space(4);
            Terrain prev = terrain;
            terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
            if (terrain != prev) RefreshPrototypes();

            if (terrain == null)
            {
                EditorGUILayout.HelpBox("Assign a terrain above.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            brushRadius    = EditorGUILayout.Slider("Radius",          brushRadius,    1f,   200f);
            spacing        = EditorGUILayout.Slider("Min Spacing",     spacing,        0.5f,  50f);
            heightScale    = EditorGUILayout.Slider("Base Height",     heightScale,    0.1f,   5f);
            heightVariance = EditorGUILayout.Slider("Height Variance", heightVariance, 0f,     2f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Rotation (Y-axis)", EditorStyles.boldLabel);
            rotationOffset = EditorGUILayout.Slider("Offset (degrees)", rotationOffset, -180f, 180f);
            randomRotation = EditorGUILayout.Toggle("Random Rotation", randomRotation);
            using (new EditorGUI.DisabledScope(!randomRotation))
            {
                EditorGUILayout.MinMaxSlider("  Random Range", ref rotationRangeMin, ref rotationRangeMax, 0f, 360f);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  Min: {rotationRangeMin:F0}°", GUILayout.Width(100));
                EditorGUILayout.LabelField($"Max: {rotationRangeMax:F0}°");
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Tree Prototypes  (checked = included in random pick)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All"))     SetAll(true);
            if (GUILayout.Button("None"))    SetAll(false);
            if (GUILayout.Button("Refresh")) RefreshPrototypes();
            EditorGUILayout.EndHorizontal();

            TreePrototype[] protos = terrain.terrainData.treePrototypes;
            if (prototypeSelected.Length != protos.Length)
                RefreshPrototypes();

            protoScroll = EditorGUILayout.BeginScrollView(protoScroll, GUILayout.MaxHeight(250));
            for (int i = 0; i < protos.Length; i++)
            {
                string label = protos[i].prefab != null ? protos[i].prefab.name : $"Prototype {i}";
                prototypeSelected[i] = EditorGUILayout.ToggleLeft(label, prototypeSelected[i]);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            Color prevColor = GUI.color;
            GUI.color = painting ? Color.red : Color.green;
            if (GUILayout.Button(painting ? "STOP PAINTING" : "START PAINTING", GUILayout.Height(32)))
                painting = !painting;
            GUI.color = prevColor;

            if (painting)
                EditorGUILayout.HelpBox("Click/drag in Scene view. Shift+click to erase.", MessageType.None);
        }

        void OnSceneGUI(SceneView sv)
        {
            if (!painting || terrain == null) return;

            Event e = Event.current;

            if (Physics.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out RaycastHit hit, Mathf.Infinity))
            {
                Handles.color = e.shift ? new Color(1, 0.3f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.3f, 0.3f);
                Handles.DrawSolidDisc(hit.point, hit.normal, brushRadius);
                Handles.color = e.shift ? Color.red : Color.green;
                Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);
                sv.Repaint();
            }

            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                if (Physics.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out RaycastHit h, Mathf.Infinity))
                {
                    if (e.shift) EraseTreesAt(h.point);
                    else         PlantTreesAt(h.point);
                    e.Use();
                }
            }
        }

        void PlantTreesAt(Vector3 worldCenter)
        {
            int[] selected = GetSelectedIndices();
            if (selected.Length == 0)
            {
                Debug.LogWarning("[RandomTreePainter] No prototypes selected.");
                return;
            }

            if (UseSceneSpawn)
                PlantAsGameObjects(worldCenter, selected);
            else
                PlantInTerrainData(worldCenter, selected);
        }

        void PlantAsGameObjects(Vector3 worldCenter, int[] selected)
        {
            if (treeContainer == null)
            {
                Debug.LogWarning("[RandomTreePainter] Assign a Trees Container or click auto create.");
                return;
            }

            TreePrototype[] protos = terrain.terrainData.treePrototypes;

            List<Vector2> existing = treeContainer.transform
                .Cast<Transform>()
                .Select(t => new Vector2(t.position.x, t.position.z))
                .ToList();

            int attempts = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.PI * brushRadius * brushRadius / (spacing * spacing) * 4),
                4, 200);

            bool anyAdded = false;

            for (int i = 0; i < attempts; i++)
            {
                Vector2 offset = Random.insideUnitCircle * brushRadius;
                Vector3 world  = worldCenter + new Vector3(offset.x, 0, offset.y);
                Vector2 flat   = new Vector2(world.x, world.z);

                if (existing.Any(e => Vector2.Distance(e, flat) < spacing)) continue;

                int protoIdx = selected[Random.Range(0, selected.Length)];
                if (protos[protoIdx].prefab == null) continue;

                world.y = terrain.SampleHeight(world) + terrain.transform.position.y;

                float scale = Mathf.Max(0.05f, heightScale + Random.Range(-heightVariance, heightVariance) * 0.5f);

                float yDeg = rotationOffset;
                if (randomRotation) yDeg += Random.Range(rotationRangeMin, rotationRangeMax);

                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(
                    protos[protoIdx].prefab, treeContainer.transform);
                go.transform.SetPositionAndRotation(world, Quaternion.Euler(0, yDeg, 0));
                go.transform.localScale = go.transform.localScale * scale;

                Undo.RegisterCreatedObjectUndo(go, "Paint Tree (Scene)");
                existing.Add(flat);
                anyAdded = true;
            }

            if (anyAdded)
                EditorSceneManager.MarkSceneDirty(TargetScene);
        }

        void PlantInTerrainData(Vector3 worldCenter, int[] selected)
        {
            TerrainData td    = terrain.terrainData;
            Vector3     tPos  = terrain.transform.position;
            Vector3     tSize = td.size;

            List<Vector2> existing = td.treeInstances
                .Select(t => new Vector2(t.position.x * tSize.x, t.position.z * tSize.z))
                .ToList();

            List<TreeInstance> instances = td.treeInstances.ToList();
            bool anyAdded = false;

            int attempts = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.PI * brushRadius * brushRadius / (spacing * spacing) * 4),
                4, 200);

            for (int i = 0; i < attempts; i++)
            {
                Vector2 offset = Random.insideUnitCircle * brushRadius;
                Vector3 world  = worldCenter + new Vector3(offset.x, 0, offset.y);

                float nx = Mathf.Clamp01((world.x - tPos.x) / tSize.x);
                float nz = Mathf.Clamp01((world.z - tPos.z) / tSize.z);

                Vector2 pos2d = new Vector2(nx * tSize.x, nz * tSize.z);

                if (existing.Any(e => Vector2.Distance(e, pos2d) < spacing)) continue;

                float height = Mathf.Max(0.05f, heightScale + Random.Range(-heightVariance, heightVariance) * 0.5f);

                float yDeg = rotationOffset;
                if (randomRotation) yDeg += Random.Range(rotationRangeMin, rotationRangeMax);

                instances.Add(new TreeInstance
                {
                    prototypeIndex = selected[Random.Range(0, selected.Length)],
                    position       = new Vector3(nx, 0, nz),
                    widthScale     = height,
                    heightScale    = height,
                    rotation       = yDeg * Mathf.Deg2Rad,
                    color          = Color.white,
                    lightmapColor  = Color.white
                });

                existing.Add(pos2d);
                anyAdded = true;
            }

            if (anyAdded)
            {
                Undo.RecordObject(td, "Paint Random Trees");
                td.SetTreeInstances(instances.ToArray(), true);
                EditorUtility.SetDirty(td);
            }
        }

        void EraseTreesAt(Vector3 worldCenter)
        {
            if (UseSceneSpawn)
                EraseSceneTreesAt(worldCenter);
            else
                EraseTerrainTreesAt(worldCenter);
        }

        void EraseSceneTreesAt(Vector3 worldCenter)
        {
            if (treeContainer == null) return;

            var toDelete = treeContainer.transform
                .Cast<Transform>()
                .Where(t =>
                {
                    float dx = t.position.x - worldCenter.x;
                    float dz = t.position.z - worldCenter.z;
                    return Mathf.Sqrt(dx * dx + dz * dz) <= brushRadius;
                })
                .Select(t => t.gameObject)
                .ToList();

            foreach (var go in toDelete)
                Undo.DestroyObjectImmediate(go);

            if (toDelete.Count > 0)
                EditorSceneManager.MarkSceneDirty(TargetScene);
        }

        void EraseTerrainTreesAt(Vector3 worldCenter)
        {
            TerrainData td    = terrain.terrainData;
            Vector3     tPos  = terrain.transform.position;
            Vector3     tSize = td.size;

            float ncx = (worldCenter.x - tPos.x) / tSize.x;
            float ncz = (worldCenter.z - tPos.z) / tSize.z;

            TreeInstance[] remaining = td.treeInstances
                .Where(t =>
                {
                    float dx = (t.position.x - ncx) * tSize.x;
                    float dz = (t.position.z - ncz) * tSize.z;
                    return Mathf.Sqrt(dx * dx + dz * dz) > brushRadius;
                })
                .ToArray();

            Undo.RecordObject(td, "Erase Trees");
            td.SetTreeInstances(remaining, true);
            EditorUtility.SetDirty(td);
        }

        void CreateTreeContainer()
        {
            if (!UseSceneSpawn) return;

            GameObject container = new GameObject("Trees_" + TargetScene.name);
            SceneManager.MoveGameObjectToScene(container, TargetScene);
            Undo.RegisterCreatedObjectUndo(container, "Create Trees Container");
            treeContainer = container;
            EditorSceneManager.MarkSceneDirty(TargetScene);
            Repaint();
        }

        int[] GetSelectedIndices()
        {
            List<int> result = new List<int>();
            for (int i = 0; i < prototypeSelected.Length; i++)
                if (prototypeSelected[i]) result.Add(i);
            return result.ToArray();
        }

        void SetAll(bool value)
        {
            for (int i = 0; i < prototypeSelected.Length; i++)
                prototypeSelected[i] = value;
        }

        void RefreshTerrain()
        {
            if (terrain == null) terrain = Terrain.activeTerrain;
            RefreshPrototypes();
        }

        void RefreshPrototypes()
        {
            if (terrain == null) { prototypeSelected = new bool[0]; return; }
            int count  = terrain.terrainData.treePrototypes.Length;
            bool[] next = new bool[count];
            for (int i = 0; i < Mathf.Min(count, prototypeSelected.Length); i++)
                next[i] = prototypeSelected[i];
            for (int i = prototypeSelected.Length; i < count; i++)
                next[i] = true;
            prototypeSelected = next;
            Repaint();
        }
    }
}
#endif
