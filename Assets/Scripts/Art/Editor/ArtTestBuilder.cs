using MotoSquid.Bike;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MotoSquid.Art
{
    // Builds the Art Test scene: a Cyberpunk Megapolis street on Toon copies of the pack's materials, the
    // player Senshi, two switchable mood rigs and a full-screen ink outline. The scene is generated output
    // and is replaced on every build. The Toon materials, volume profiles and ink material are only created
    // when missing, so tuning them by hand survives a rebuild; delete one to get the default back (rule 13).
    public static class ArtTestBuilder
    {
        const string ScenePath = "Assets/Scenes/Art Test/Art Test.unity";
        const string Root = "Assets/Art Test";
        const string WorldMaterials = Root + "/Materials/World";
        const string WorldLitMaterials = Root + "/Materials/World Lit";
        const string Profiles = Root + "/Profiles";
        const string InkMaterialPath = Root + "/Materials/Ink Outline.mat";
        const string Pack = "Assets/Cyberpunk_Megapolis/Prefabs/";
        const string BikePrefab = "Assets/Prefabs/Characters/Player/Senshi Renegade 900 RR.prefab";
        const string ToonShader = "Toon/Toon";

        const float StreetLength = 1200f;
        const int RoadTilesAcross = 3;
        const int RoadLayer = 8;
        const int BuildingsLayer = 13;

        static readonly string[] Buildings = Enumerable.Range(1, 12)
            .Select(i => $"{Pack}Combined_Building/CP_Combined_Building_{i:00}.prefab").ToArray();

        class MoodSpec
        {
            public string name;
            public float exposureEV, sunLux, sunPitch, sunYaw, lampLumen, neonLumen;
            public Color sunColour, lampColour, skyTop, skyMiddle, skyBottom;
            public Color[] neon;
            public float fogDistance, fogHeight;
            public float bloom, bloomScatter, flare, grain, chromatic, vignette, contrast, saturation;
            public Color splitShadows, splitHighlights;
        }

        static readonly MoodSpec NightNeon = new MoodSpec
        {
            name = "Night Neon",
            exposureEV = 0f, sunLux = 0.8f, sunPitch = 35f, sunYaw = 150f,
            sunColour = new Color(0.55f, 0.65f, 1f),
            lampLumen = 5000f, lampColour = new Color(1f, 0.55f, 0.85f),
            neonLumen = 1500f, neon = new[] { new Color(1f, 0.1f, 0.6f), new Color(0.1f, 0.85f, 1f) },
            skyTop = new Color(0.004f, 0.005f, 0.02f), skyMiddle = new Color(0.16f, 0.03f, 0.12f),
            skyBottom = new Color(0.05f, 0.02f, 0.06f),
            fogDistance = 220f, fogHeight = 60f,
            bloom = 0.35f, bloomScatter = 0.75f, flare = 0.4f, grain = 0.2f, chromatic = 0.15f, vignette = 0.3f,
            contrast = 15f, saturation = 20f,
            splitShadows = new Color(0.35f, 0.45f, 0.75f), splitHighlights = new Color(0.9f, 0.55f, 0.75f),
        };

        static readonly MoodSpec DuskSynthwave = new MoodSpec
        {
            name = "Dusk Synthwave",
            exposureEV = 1f, sunLux = 10f, sunPitch = 7f, sunYaw = 180f,
            sunColour = new Color(1f, 0.55f, 0.35f),
            lampLumen = 2000f, lampColour = new Color(1f, 0.8f, 0.6f),
            neonLumen = 800f, neon = new[] { new Color(1f, 0.45f, 0.75f), new Color(0.4f, 0.95f, 0.9f) },
            skyTop = new Color(0.25f, 0.08f, 0.35f), skyMiddle = new Color(1.2f, 0.5f, 0.3f),
            skyBottom = new Color(0.4f, 0.15f, 0.3f),
            fogDistance = 400f, fogHeight = 120f,
            bloom = 0.25f, bloomScatter = 0.8f, flare = 0.3f, grain = 0.15f, chromatic = 0.1f, vignette = 0.2f,
            contrast = 10f, saturation = 10f,
            splitShadows = new Color(0.45f, 0.35f, 0.75f), splitHighlights = new Color(1f, 0.7f, 0.5f),
        };

        [MenuItem("MotoSquid/Art Test/Build Art Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(WorldMaterials);
            Directory.CreateDirectory(WorldLitMaterials);
            Directory.CreateDirectory(Profiles);

            // Load nothing before this: NewScene unloads assets no open scene holds
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var report = new List<string>();

            var street = new GameObject("Street").transform;
            var lampHeads = new List<Vector3>();
            float roadTop = BuildStreet(street, lampHeads, out float buildingInner, report);

            var moodsRoot = new GameObject("Moods").transform;
            var moods = new[] { NightNeon, DuskSynthwave }
                .Select(m => BuildMood(m, moodsRoot, lampHeads, buildingInner, roadTop)).ToArray();
            for (int i = 0; i < moods.Length; i++) moods[i].SetActive(i == 0);

            var ink = BuildInkOutline();
            var bike = SpawnBike(roadTop, report);
            BuildCamera(bike);

            var controls = new GameObject("Art Test Controls").AddComponent<ArtTestControls>();
            controls.moods = moods;
            controls.inkOutline = ink;
            controls.boost = bike != null ? bike.GetComponentInChildren<BoostSystem>(true) : null;
            if (controls.boost == null) report.Add("ERROR: no BoostSystem on the bike, F3 infinite boost does nothing");

            var probe = controls.gameObject.AddComponent<ArtTestPerfProbe>();
            probe.street = street;
            probe.toon = street.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                               .Where(m => m != null).Distinct().ToArray();
            probe.lit = probe.toon.Select(LitCopy).ToArray();
            probe.gpuDrivenOptOut = (Behaviour)street.GetComponent(GpuDrivenOptOut);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Verify(controls, report);
            Debug.Log("[ArtTest] Built " + ScenePath + "\n  " + string.Join("\n  ", report));
        }

        // Returns the road surface height; lamp light positions and the building line are passed out for the moods
        static float BuildStreet(Transform street, List<Vector3> lampHeads, out float buildingInner, List<string> report)
        {
            var converted = new Dictionary<Material, Material>();
            var road = Load($"{Pack}Street/CP_Road_8x8m.prefab");
            var kerb = Load($"{Pack}Street/CP_Curbstone_8m_01.prefab");
            var walk = Load($"{Pack}Street/CP_Sidewalk_A_8x4m.prefab");
            var lamp = Load($"{Pack}Environment/CP_Street_Lamp_01.prefab");

            var probe = Spawn(road, street, 0f, out var tile);
            Object.DestroyImmediate(probe);
            float tileW = tile.size.x, tileL = tile.size.z;
            float halfRoad = RoadTilesAcross * tileW * 0.5f;
            float roadTop = tile.size.y;

            for (float z = -40f; z < StreetLength + 40f; z += tileL)
                for (int i = 0; i < RoadTilesAcross; i++)
                    Dress(road, street, 0f, new Vector3(-halfRoad + tileW * (i + 0.5f), 0f, z), RoadLayer, converted);

            float walkOuter = halfRoad;
            foreach (int side in new[] { -1, 1 })
            {
                float kerbW = 0f, walkW = 0f;
                for (float z = -40f; z < StreetLength + 40f; )
                {
                    var k = Dress(kerb, street, AlongZ(kerb), new Vector3(0f, 0f, z), RoadLayer, converted, out var kb);
                    kerbW = kb.size.x;
                    Shift(k, side * (halfRoad + kerbW * 0.5f) - kb.center.x);
                    z += kb.size.z;
                }
                for (float z = -40f; z < StreetLength + 40f; )
                {
                    var w = Dress(walk, street, AlongZ(walk), new Vector3(0f, 0f, z), RoadLayer, converted, out var wb);
                    walkW = wb.size.x;
                    Shift(w, side * (halfRoad + kerbW + walkW * 0.5f) - wb.center.x);
                    z += wb.size.z;
                }
                walkOuter = halfRoad + kerbW + walkW;

                for (float z = 16f; z < StreetLength; z += 32f)
                {
                    var l = Dress(lamp, street, side > 0 ? -90f : 90f, new Vector3(side * (halfRoad + kerbW + 0.6f), roadTop, z), RoadLayer, converted);
                    foreach (var light in l.GetComponentsInChildren<Light>(true))
                    {
                        lampHeads.Add(light.transform.position);
                        Object.DestroyImmediate(light.gameObject.GetComponent<HDAdditionalLightData>());
                        Object.DestroyImmediate(light);
                    }
                }

                int n = side > 0 ? 0 : 6;
                for (float z = -20f; z < StreetLength + 20f; n++)
                {
                    var prefab = Load(Buildings[n % Buildings.Length]);
                    var b = Dress(prefab, street, side > 0 ? -90f : 90f, Vector3.zero, BuildingsLayer, converted, out var bb);
                    var at = new Vector3(side * (walkOuter + 1f + bb.size.x * 0.5f), 0f, z + bb.size.z * 0.5f);
                    b.transform.position += at - new Vector3(bb.center.x, 0f, bb.center.z);
                    z += bb.size.z + 3f;
                }
            }

            buildingInner = walkOuter + 1f;

            // The pack's road and sidewalk colliders are zero-thickness boxes the bike drops through
            var ground = new GameObject("Ground Slab") { layer = RoadLayer };
            ground.transform.SetParent(street);
            var slab = ground.AddComponent<BoxCollider>();
            slab.center = new Vector3(0f, roadTop - 1f, StreetLength * 0.5f);
            slab.size = new Vector3(walkOuter * 2f, 2f, StreetLength + 80f);

            DisallowGpuDriven(street.gameObject);
            report.Add($"street: {StreetLength} m, road {halfRoad * 2f:0.#} m wide, surface y {roadTop:0.###}, {converted.Count} pack materials on Toon, {lampHeads.Count} lamp lights moved to the moods");
            return roadTop;
        }

        static GameObject BuildMood(MoodSpec m, Transform parent, List<Vector3> lampHeads, float buildingInner, float roadTop)
        {
            var root = new GameObject(m.name);
            root.transform.SetParent(parent);

            var volume = new GameObject("Volume").AddComponent<Volume>();
            volume.transform.SetParent(root.transform);
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = Profile(m);

            var sun = AddLight(root.transform, m.name == NightNeon.name ? "Moon" : "Sun", LightType.Directional,
                            m.sunColour, m.sunLux, LightUnit.Lux, 0f, true);
            sun.transform.rotation = Quaternion.Euler(m.sunPitch, m.sunYaw, 0f);

            var lamps = new GameObject("Street Lights").transform;
            lamps.SetParent(root.transform);
            foreach (var p in lampHeads)
            {
                var l = AddLight(lamps, "Lamp", LightType.Spot, m.lampColour, m.lampLumen, LightUnit.Lumen, 28f, false);
                l.transform.SetPositionAndRotation(p, Quaternion.Euler(90f, 0f, 0f));
                l.spotAngle = 110f;
            }

            var neon = new GameObject("Neon Accents").transform;
            neon.SetParent(root.transform);
            int i = 0;
            for (float z = 8f; z < StreetLength; z += 40f)
                foreach (int side in new[] { -1, 1 })
                {
                    var l = AddLight(neon, "Neon", LightType.Point, m.neon[i++ % m.neon.Length], m.neonLumen, LightUnit.Lumen, 14f, false);
                    l.transform.position = new Vector3(side * (buildingInner - 1.5f), roadTop + 5f, z + (side > 0 ? 20f : 0f));
                }
            return root;
        }

        static Light AddLight(Transform parent, string name, LightType type, Color colour, float intensity, LightUnit unit, float range, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var hd = go.AddHDLight(type);
            var light = go.GetComponent<Light>();
            light.lightUnit = unit;
            light.intensity = intensity;
            light.color = colour;
            if (range > 0f) light.range = range;
            hd.EnableShadows(shadows);
            return light;
        }

        static VolumeProfile Profile(MoodSpec m)
        {
            string path = $"{Profiles}/{m.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;

            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);

            var env = Add<VisualEnvironment>(p);
            env.skyType.Override((int)SkyType.Gradient);
            env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

            var sky = Add<GradientSky>(p);
            sky.top.Override(m.skyTop);
            sky.middle.Override(m.skyMiddle);
            sky.bottom.Override(m.skyBottom);
            sky.gradientDiffusion.Override(1.5f);

            var exposure = Add<Exposure>(p);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(m.exposureEV);

            var fog = Add<Fog>(p);
            fog.enabled.Override(true);
            fog.colorMode.Override(FogColorMode.SkyColor);
            fog.meanFreePath.Override(m.fogDistance);
            fog.baseHeight.Override(0f);
            fog.maximumHeight.Override(m.fogHeight);
            fog.enableVolumetricFog.Override(false);

            Add<Tonemapping>(p).mode.Override(TonemappingMode.Neutral);

            var bloom = Add<Bloom>(p);
            bloom.intensity.Override(m.bloom);
            bloom.scatter.Override(m.bloomScatter);
            bloom.threshold.Override(0.8f);

            var flare = Add<ScreenSpaceLensFlare>(p);
            flare.intensity.Override(m.flare);
            flare.firstFlareIntensity.Override(0.15f);
            flare.secondaryFlareIntensity.Override(0f);
            flare.warpedFlareIntensity.Override(0f);
            flare.streaksIntensity.Override(1f);
            flare.streaksLength.Override(0.6f);
            flare.streaksThreshold.Override(0.3f);

            var grain = Add<FilmGrain>(p);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(m.grain);
            grain.response.Override(0.8f);

            Add<ChromaticAberration>(p).intensity.Override(m.chromatic);
            Add<Vignette>(p).intensity.Override(m.vignette);

            var grade = Add<ColorAdjustments>(p);
            grade.contrast.Override(m.contrast);
            grade.saturation.Override(m.saturation);

            var split = Add<SplitToning>(p);
            split.shadows.Override(m.splitShadows);
            split.highlights.Override(m.splitHighlights);

            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        static T Add<T>(VolumeProfile p) where T : VolumeComponent
        {
            var c = p.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        static CustomPassVolume BuildInkOutline()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(InkMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("FullScreen/InkOutline"));
                AssetDatabase.CreateAsset(mat, InkMaterialPath);
            }
            var volume = new GameObject("Ink Outline").AddComponent<CustomPassVolume>();
            volume.isGlobal = true;
            // Before the opaque fog, so distance haze fades the lines and trails draw over them
            volume.injectionPoint = CustomPassInjectionPoint.AfterOpaqueAndSky;
            var pass = volume.AddPassOfType<FullScreenCustomPass>();
            pass.name = "Ink Outline";
            pass.fullscreenPassMaterial = mat;
            pass.materialPassName = "Custom Pass 0";
            return volume;
        }

        static GameObject SpawnBike(float roadTop, List<string> report)
        {
            var prefab = Load(BikePrefab);
            if (prefab == null) { report.Add("ERROR: bike prefab missing at " + BikePrefab); return null; }
            var bike = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            bike.transform.SetPositionAndRotation(new Vector3(-4f, roadTop, 30f), Quaternion.identity);
            DisallowGpuDriven(bike);
            return bike;
        }

        static void BuildCamera(GameObject bike)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.farClipPlane = 2000f;
            go.AddComponent<HDAdditionalCameraData>();
            go.AddComponent<AudioListener>();
            go.AddComponent<CinemachineBrain>();
            var at = bike != null ? bike.transform.position : Vector3.zero;
            go.transform.SetPositionAndRotation(at + new Vector3(0f, 2f, -6f), Quaternion.Euler(10f, 0f, 0f));
        }

        static GameObject Dress(GameObject prefab, Transform parent, float yaw, Vector3 at, int layer,
                                Dictionary<Material, Material> converted) =>
            Dress(prefab, parent, yaw, at, layer, converted, out _);

        // Places the instance's bounds centre on at.xz with its base on at.y, unpacked, on Toon, on one layer
        static GameObject Dress(GameObject prefab, Transform parent, float yaw, Vector3 at, int layer,
                                Dictionary<Material, Material> converted, out Bounds placed)
        {
            var go = Spawn(prefab, parent, yaw, out var b);
            go.transform.position = new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var c in go.GetComponentsInChildren<BoxCollider>(true))
                if (Mathf.Min(c.size.x, c.size.y, c.size.z) < 0.01f) Object.DestroyImmediate(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = r.sharedMaterials.Select(m => Toon(m, converted)).ToArray();
            placed = WorldBounds(go);
            return go;
        }

        static GameObject Spawn(GameObject prefab, Transform parent, float yaw, out Bounds bounds)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            bounds = WorldBounds(go);
            return go;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void Shift(GameObject go, float x) => go.transform.position += new Vector3(x, 0f, 0f);

        // Yaw that lays a strip prefab's long side along the street
        static float AlongZ(GameObject prefab)
        {
            var go = Spawn(prefab, null, 0f, out var b);
            Object.DestroyImmediate(go);
            return b.size.x > b.size.z ? 90f : 0f;
        }

        static Material Toon(Material src, Dictionary<Material, Material> converted)
        {
            if (src == null) return null;
            if (converted.TryGetValue(src, out var done)) return done;

            string path = $"{WorldMaterials}/{src.name}_Toon.mat";
            var toon = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (toon == null)
            {
                toon = new Material(Shader.Find(ToonShader));
                bool grunge = src.shader.name == "AE/Grunge";
                var tex = TextureOf(src, grunge ? "_Base_Color" : "_MainTex");
                var colour = src.HasProperty(grunge ? "_Tint" : "_Color") ? src.GetColor(grunge ? "_Tint" : "_Color") : Color.white;
                Vector2 tiling = grunge ? (Vector2)src.GetVector("_Tiling_Main_Texture")
                               : src.HasProperty("_MainTex") ? src.mainTextureScale : Vector2.one;

                toon.SetTexture("_MainTex", tex);
                toon.SetTexture("_BaseMap", tex);
                toon.SetTextureScale("_MainTex", tiling);
                toon.SetTextureScale("_BaseMap", tiling);
                toon.SetColor("_BaseColor", colour);
                toon.SetColor("_Color", colour);
                toon.SetTexture("_NormalMap", TextureOf(src, grunge ? "_Normal" : "_BumpMap"));

                // Cool, hard-edged two-step shade: anime backgrounds, not light falloff
                toon.SetFloat("_Use_BaseAs1st", 1f);
                toon.SetFloat("_Use_1stAs2nd", 1f);
                toon.SetColor("_1st_ShadeColor", new Color(0.62f, 0.58f, 0.78f));
                toon.SetColor("_2nd_ShadeColor", new Color(0.38f, 0.34f, 0.55f));
                toon.SetFloat("_BaseColor_Step", 0.5f);
                toon.SetFloat("_BaseShade_Feather", 0.02f);
                toon.SetFloat("_ShadeColor_Step", 0.25f);
                toon.SetFloat("_1st2nd_Shades_Feather", 0.02f);
                toon.SetFloat("_GI_Intensity", 0.35f);

                bool emissive = !grunge && src.IsKeywordEnabled("_EMISSION");
                var emissionMap = emissive ? TextureOf(src, "_EmissionMap") : null;
                toon.SetTexture("_Emissive_Tex", emissionMap != null ? emissionMap : Texture2D.whiteTexture);
                toon.SetColor("_Emissive_Color", emissive ? src.GetColor("_EmissionColor") : Color.black);

                // The world gets its lines from the full-screen ink pass, so the per-material hull is off
                toon.SetFloat("_Outline_Width", 0f);
                toon.SetShaderPassEnabled("SRPDefaultUnlit", false);

                // Standard Fade and Transparent are drawn as cutout: hard edges suit the look and keep it opaque
                bool cutout = !grunge && src.HasProperty("_Mode") && src.GetFloat("_Mode") >= 1f;
                toon.SetFloat("_ClippingMode", cutout ? 1f : 0f);
                toon.SetFloat("_IsBaseMapAlphaAsClippingMask", cutout ? 1f : 0f);
                toon.SetFloat("_Clipping_Level", cutout && src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
                toon.shaderKeywords = new[]
                {
                    "_EMISSIVE_SIMPLE", "_IS_OUTLINE_CLIPPING_NO", "_OUTLINE_NML",
                    cutout ? "_IS_CLIPPING_MODE" : "_IS_CLIPPING_OFF",
                };
                if (cutout)
                {
                    toon.SetOverrideTag("RenderType", "TransparentCutout");
                    toon.renderQueue = (int)RenderQueue.AlphaTest;
                }
                AssetDatabase.CreateAsset(toon, path);
            }
            converted[src] = toon;
            return toon;
        }

        // The Toon shader is not SRP Batcher compatible, so the GPU Resident Drawer rejects every Toon mesh.
        // The opt-out component is internal to SRP Core, hence the lookup by name
        static readonly System.Type GpuDrivenOptOut = System.Type.GetType(
            "UnityEngine.Rendering.DisallowGPUDrivenRendering, Unity.RenderPipelines.GPUDriven.Runtime", true);

        static void DisallowGpuDriven(GameObject go)
        {
            var c = go.AddComponent(GpuDrivenOptOut);
            var so = new SerializedObject(c);
            so.FindProperty("m_applyToChildrenRecursively").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // HDRP Lit twin of a world Toon material, for the Play-mode GPU Resident Drawer comparison
        static Material LitCopy(Material toon)
        {
            string path = $"{WorldLitMaterials}/{toon.name.Replace("_Toon", "")}_Lit.mat";
            var lit = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (lit != null) return lit;

            lit = new Material(Shader.Find("HDRP/Lit"));
            lit.SetTexture("_BaseColorMap", toon.GetTexture("_MainTex"));
            lit.SetTextureScale("_BaseColorMap", toon.GetTextureScale("_MainTex"));
            lit.SetColor("_BaseColor", toon.GetColor("_BaseColor"));
            lit.SetTexture("_NormalMap", toon.GetTexture("_NormalMap"));
            lit.SetTexture("_EmissiveColorMap", toon.GetTexture("_Emissive_Tex"));
            lit.SetColor("_EmissiveColor", toon.GetColor("_Emissive_Color"));
            if (toon.GetFloat("_ClippingMode") > 0f)
            {
                lit.SetFloat("_AlphaCutoffEnable", 1f);
                lit.SetFloat("_AlphaCutoff", toon.GetFloat("_Clipping_Level"));
            }
            UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(lit);
            AssetDatabase.CreateAsset(lit, path);
            return lit;
        }

        static Texture TextureOf(Material m, string property) => m.HasProperty(property) ? m.GetTexture(property) : null;

        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        // Rule 15: read the saved scene's references back rather than trusting the build
        static void Verify(ArtTestControls controls, List<string> report)
        {
            var so = new SerializedObject(controls);
            if (so.FindProperty("moods").arraySize != 2) report.Add("ERROR: moods did not save");
            if (so.FindProperty("inkOutline").objectReferenceValue == null) report.Add("ERROR: ink outline reference did not save");
            foreach (var mood in controls.moods)
                if (mood.GetComponentInChildren<Volume>(true).sharedProfile == null) report.Add($"ERROR: {mood.name} volume has no profile");
            var pass = controls.inkOutline.customPasses.OfType<FullScreenCustomPass>().FirstOrDefault();
            if (pass == null || pass.fullscreenPassMaterial == null) report.Add("ERROR: ink pass has no material");
            var probe = controls.GetComponent<ArtTestPerfProbe>();
            if (probe.gpuDrivenOptOut == null) report.Add("ERROR: perf probe has no Resident Drawer opt-out to toggle");
            if (probe.lit.Any(m => m == null)) report.Add("ERROR: perf probe is missing Lit materials");
            if (Shader.Find(ToonShader) == null) report.Add("ERROR: shader '" + ToonShader + "' not found, the Unity Toon Shader package is missing");
        }
    }
}
