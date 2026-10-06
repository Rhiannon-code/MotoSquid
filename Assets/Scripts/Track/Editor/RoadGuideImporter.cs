using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotoSquid.Track
{
    // Builds RoadGuides for the real Melbourne freeway/highway network from RoadData/major_roads.json
    // (written by tools/roads/prepare_roads.py): flat lines as wide as each road's lanes, to build
    // EasyRoads3D roads over by hand. The guides are generated output: rebuilding replaces them.
    public class RoadGuideImporter : EditorWindow
    {
        const string DataFile = "RoadData/major_roads.json";
        const string RootName = "Road Guides (Vicmap + OSM)";
        const string MaterialFolder = "Assets/Materials/RoadGuides";
        const float LaneWidth = 3.5f;
        const float HoverAboveRoad = 0.1f;
        const float SatelliteHover = 0.05f;

        // Fitted against Vicmap by tools/roads/georeference.py: rerun it if the image changes.
        const string SatelliteTexture = "Assets/Textures/screenshot_2026-03-12_15-43-01.png";
        const float SatelliteMetresPerPixel = 5.6716f;
        static readonly Vector2 SatelliteCbdPixel = new Vector2(1170.01f, 160.97f);
        const float SatelliteYaw = 0.0715f;

        [Serializable] class RoadFile { public RoadRecord[] roads; }
        [Serializable]
        class RoadRecord
        {
            public string id, name, direction, divided, type;
            public int roadClass, lanes;
            public bool lanesFromOsm;
            public float[] xz, y;
        }

        Vector3 cbdPosition;
        Terrain terrain;
        float scale = 0.15f;
        bool satellite = true;
        string status;

        [MenuItem("MotoSquid/Roads/Road Guides")]
        static void Open() => GetWindow<RoadGuideImporter>("Road Guides");

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Builds reference lines for Melbourne's freeways and highways in the open scene. " +
                                    "They are EditorOnly and never ship. Rebuilding replaces them.", MessageType.Info);
            terrain = (Terrain)EditorGUILayout.ObjectField(new GUIContent("Fit inside terrain",
                "Centres the network on this terrain and refuses a scale that would spill past its edges."), terrain, typeof(Terrain), true);
            using (new EditorGUI.DisabledScope(terrain != null))
                cbdPosition = EditorGUILayout.Vector3Field("CBD position in this scene", cbdPosition);
            scale = EditorGUILayout.Slider(new GUIContent("Scale", "1 = real size. Below 1 shrinks the layout, heights and widths alike, as a map preview."), scale, 0.05f, 1f);
            satellite = EditorGUILayout.Toggle(new GUIContent("Satellite image underneath",
                "Lays the georeferenced satellite screenshot under the guides, at the same scale."), satellite);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build guides")) EditorApplication.delayCall += () => Run(Build);
                if (GUILayout.Button("Remove guides")) EditorApplication.delayCall += () => Run(Remove);
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
        }

        void Run(Func<string> action)
        {
            try { status = action(); }
            catch (Exception e) { status = "Failed: " + e.Message; Debug.LogException(e); }
            finally { EditorUtility.ClearProgressBar(); Repaint(); }
        }

        string Remove()
        {
            var root = GameObject.Find(RootName);
            if (root == null) return "No guides in this scene.";
            Undo.DestroyObjectImmediate(root);
            return "Guides removed.";
        }

        string Build()
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), DataFile);
            if (!File.Exists(path)) return $"Missing {DataFile}. Run tools/roads/prepare_roads.py first.";
            var roads = JsonUtility.FromJson<RoadFile>(File.ReadAllText(path)).roads;
            if (terrain != null && !FitToTerrain(roads, out string tooBig)) return tooBig;

            Remove();
            var root = new GameObject(RootName) { tag = "EditorOnly" };
            Undo.RegisterCreatedObjectUndo(root, "Build road guides");
            var groups = new System.Collections.Generic.Dictionary<string, Transform>();

            for (int i = 0; i < roads.Length; i++)
            {
                var r = roads[i];
                if (i % 50 == 0)
                    EditorUtility.DisplayProgressBar("Building road guides", r.name, (float)i / roads.Length);

                bool ramp = r.name.Contains("Ramp");
                string groupName = ramp ? "Ramps" : string.IsNullOrEmpty(r.name) ? "Unnamed" : r.name;
                if (!groups.TryGetValue(groupName, out var group))
                {
                    group = new GameObject(groupName).transform;
                    group.SetParent(root.transform, false);
                    groups[groupName] = group;
                }

                var points = new Vector3[r.xz.Length / 2];
                for (int p = 0; p < points.Length; p++)
                    points[p] = cbdPosition + scale * new Vector3(r.xz[p * 2], r.y[p], r.xz[p * 2 + 1]) + Vector3.up * HoverAboveRoad;

                var go = new GameObject($"{r.name} [{r.lanes} lanes] {r.id}");
                go.transform.SetParent(group, false);
                // Lie flat: TransformZ alignment faces the line along the transform's forward, here straight up.
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.alignment = LineAlignment.TransformZ;
                line.positionCount = points.Length;
                line.SetPositions(points);
                line.widthMultiplier = Mathf.Max(1, r.lanes) * LaneWidth * scale;
                line.numCornerVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = MaterialFor(r, ramp);

                var guide = go.AddComponent<RoadGuide>();
                guide.vicmapId = r.id;
                guide.roadName = r.name;
                guide.roadClass = r.roadClass;
                guide.lanes = r.lanes;
                guide.lanesFromOsm = r.lanesFromOsm;
                guide.oneWay = r.direction != "B";
                guide.divided = r.divided;
                guide.structure = r.type;
                guide.points = points;
                guide.scale = scale;
            }

            if (satellite) BuildSatellite(root.transform);
            int guessed = roads.Count(r => !r.lanesFromOsm);
            return $"Built {roads.Length} guides at {scale:0.##}x scale in {groups.Count} groups ({guessed} with guessed lane counts). " +
                   "Select one to see its name, lanes and direction.";
        }

        bool FitToTerrain(RoadRecord[] roads, out string tooBig)
        {
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            int widestLanes = 1;
            foreach (var r in roads)
            {
                widestLanes = Mathf.Max(widestLanes, r.lanes);
                for (int p = 0; p < r.xz.Length; p += 2)
                {
                    var point = new Vector2(r.xz[p], r.xz[p + 1]);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }

            Vector3 size = terrain.terrainData.size;
            float widest = widestLanes * LaneWidth;
            float largestScale = Mathf.Min(size.x / (max.x - min.x + widest), size.z / (max.y - min.y + widest));
            tooBig = $"At {scale:0.###}x the network is {(max.x - min.x) * scale:0} x {(max.y - min.y) * scale:0} m, bigger than the " +
                     $"{size.x:0} x {size.z:0} m terrain. Largest scale that fits: {Mathf.Floor(largestScale * 1000f) / 1000f:0.###}x.";
            if (scale > largestScale) return false;

            Vector3 corner = terrain.GetPosition();
            Vector2 middle = (min + max) * 0.5f * scale;
            cbdPosition = new Vector3(corner.x + size.x * 0.5f - middle.x, 0f, corner.z + size.z * 0.5f - middle.y);
            cbdPosition.y = corner.y + terrain.SampleHeight(cbdPosition);
            return true;
        }

        void BuildSatellite(Transform root)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SatelliteTexture);
            if (texture == null) throw new FileNotFoundException("Satellite image missing", SatelliteTexture);
            ((TextureImporter)AssetImporter.GetAtPath(SatelliteTexture)).GetSourceTextureWidthAndHeight(out int width, out int height);

            float yaw = SatelliteYaw * Mathf.Deg2Rad, cos = Mathf.Cos(yaw), sin = Mathf.Sin(yaw);
            float right = (width * 0.5f - SatelliteCbdPixel.x) * SatelliteMetresPerPixel;
            float north = (SatelliteCbdPixel.y - height * 0.5f) * SatelliteMetresPerPixel;
            var centre = new Vector3(right * cos + north * sin, 0f, north * cos - right * sin);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Satellite reference";
            DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            quad.transform.SetPositionAndRotation(cbdPosition + scale * centre + Vector3.up * SatelliteHover, Quaternion.Euler(90f, SatelliteYaw, 0f));
            quad.transform.localScale = new Vector3(width, height, 1f) * (SatelliteMetresPerPixel * scale);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var material = GuideMaterial("Satellite", Color.white);
            material.SetTexture("_UnlitColorMap", texture);
            renderer.sharedMaterial = material;
        }

        static Material MaterialFor(RoadRecord r, bool ramp)
        {
            if (r.type == "tunnel") return GuideMaterial("Tunnel", new Color(0.6f, 0.3f, 0.9f));
            if (r.type == "bridge") return GuideMaterial("Bridge", new Color(0.2f, 0.8f, 0.9f));
            if (ramp) return GuideMaterial("Ramp", new Color(0.3f, 0.85f, 0.3f));
            return r.roadClass == 0 ? GuideMaterial("Freeway", new Color(1f, 0.55f, 0.1f))
                                    : GuideMaterial("Highway", new Color(1f, 0.9f, 0.2f));
        }

        static Material GuideMaterial(string kind, Color colour)
        {
            string path = $"{MaterialFolder}/RoadGuide_{kind}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Materials", "RoadGuides");
            material = new Material(Shader.Find("HDRP/Unlit"));
            material.SetColor("_UnlitColor", colour);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
