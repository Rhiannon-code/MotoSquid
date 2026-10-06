using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MotoSquid.Performance
{
    // Applies the PerformanceProfile to the HDRP assets, Quality and Player settings, after listing every
    // change it would make. Re-planning after an apply should list nothing: that is how it proves each
    // setting actually stuck
    public class PerformanceBaselineWindow : EditorWindow
    {
        const string ProfilePath = "Assets/Scriptable Objects/Performance/PerformanceProfile.asset";
        static readonly string[] ModelFolders = { "Assets/Models" };
        static readonly string[] TextureFolders = { "Assets/Textures", "Assets/Materials", "Assets/Models" };

        delegate void EditHdrp(ref RenderPipelineSettings s);

        class Change
        {
            public string label, from, to;
            public Action apply;
        }

        PerformanceProfile profile;
        List<Change> plan;
        string status;
        Vector2 scroll;

        [MenuItem("MotoSquid/Performance/Apply Baseline")]
        static void Open() => GetWindow<PerformanceBaselineWindow>("Performance Baseline");

        void OnEnable() => profile = AssetDatabase.LoadAssetAtPath<PerformanceProfile>(ProfilePath);

        void OnGUI()
        {
            profile = (PerformanceProfile)EditorGUILayout.ObjectField("Profile", profile, typeof(PerformanceProfile), false);
            if (profile == null)
            {
                if (GUILayout.Button("Create profile with starting values")) EditorApplication.delayCall += () => Run(CreateProfile);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview changes")) EditorApplication.delayCall += () => Run(Preview);
                using (new EditorGUI.DisabledScope(plan == null || plan.Count == 0))
                    if (GUILayout.Button("Apply")) EditorApplication.delayCall += () => Run(Apply);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reimport steps (each asks first)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mesh LOD on models")) EditorApplication.delayCall += () => Run(EnableMeshLods);
                if (GUILayout.Button("Mipmap streaming on textures")) EditorApplication.delayCall += () => Run(EnableTextureStreaming);
            }

            EditorGUILayout.HelpBox(
                "By hand, not settable from code: Project Settings > Graphics > HDRP > Frame Settings (Default Values) > Camera > " +
                "Asynchronous Execution, and Physics > Settings > Broad phase Type (measure before changing it: the floating " +
                "origin moves every collider each recentre).", MessageType.None);

            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            if (plan == null) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var c in plan)
                EditorGUILayout.LabelField(c.label, c.apply == null ? c.to : $"{c.from}  →  {c.to}");
            EditorGUILayout.EndScrollView();
        }

        void Run(Func<string> action)
        {
            try { status = action(); }
            catch (Exception e) { status = "Failed: " + e.Message; Debug.LogException(e); }
            finally { EditorUtility.ClearProgressBar(); Repaint(); }
        }

        string Preview()
        {
            plan = Plan(profile);
            int problems = plan.Count(c => c.apply == null);
            return plan.Count == 0 ? "Everything already matches the profile."
                : $"{plan.Count - problems} change(s) to apply{(problems > 0 ? $", {problems} problem(s) listed" : "")}.";
        }

        string Apply()
        {
            if (!EditorUtility.DisplayDialog("Apply performance baseline",
                    "This rewrites the HDRP quality assets, Quality settings and Player settings. Commit first.", "Apply", "Cancel"))
                return "Cancelled.";

            int original = QualitySettings.GetQualityLevel();
            try
            {
                foreach (var c in Plan(profile).Where(c => c.apply != null))
                    c.apply();
            }
            finally
            {
                QualitySettings.SetQualityLevel(original, false);
            }
            AssetDatabase.SaveAssets();

            plan = Plan(profile);
            int left = plan.Count(c => c.apply != null);
            return left == 0 ? "Applied. Re-checked: every setting now matches the profile."
                : $"Applied, but {left} setting(s) still differ on re-check; they are listed below.";
        }

        static List<Change> Plan(PerformanceProfile p)
        {
            var plan = new List<Change>();
            void Add(string label, object from, object to, Action apply)
            {
                bool same = from is float a && to is float b ? Mathf.Approximately(a, b) : Equals(from, to);
                if (!same) plan.Add(new Change { label = label, from = from?.ToString(), to = to?.ToString(), apply = apply });
            }

            Add("Player: frame timing stats", PlayerSettings.enableFrameTimingStats, p.frameTimingStats,
                () => PlayerSettings.enableFrameTimingStats = p.frameTimingStats);

            string[] names = QualitySettings.names;
            int original = QualitySettings.GetQualityLevel();
            try
            {
                foreach (var tier in p.tiers)
                {
                    string q = tier.qualityLevel + ": ";
                    int level = Array.IndexOf(names, tier.qualityLevel);
                    if (level < 0)
                    {
                        plan.Add(new Change { label = q, to = "no quality level with this name" });
                        continue;
                    }
                    if (!(QualitySettings.GetRenderPipelineAssetAt(level) is HDRenderPipelineAsset hd))
                    {
                        plan.Add(new Change { label = q, to = "this quality level has no HDRP asset" });
                        continue;
                    }

                    var s = hd.currentPlatformRenderPipelineSettings;
                    void Hd(string label, object from, object to, EditHdrp edit) => Add(q + label, from, to, () =>
                    {
                        var settings = hd.currentPlatformRenderPipelineSettings;
                        edit(ref settings);
                        hd.currentPlatformRenderPipelineSettings = settings;
                        EditorUtility.SetDirty(hd);
                    });

                    var drawer = tier.gpuResidentDrawer ? GPUResidentDrawerMode.InstancedDrawing : GPUResidentDrawerMode.Disabled;
                    bool occlusion = tier.gpuResidentDrawer && tier.gpuOcclusionCulling;
                    Hd("GPU Resident Drawer", s.gpuResidentDrawerSettings.mode, drawer, (ref RenderPipelineSettings x) => x.gpuResidentDrawerSettings.mode = drawer);
                    Hd("GPU occlusion culling", s.gpuResidentDrawerSettings.enableOcclusionCullingInCameras, occlusion,
                        (ref RenderPipelineSettings x) => x.gpuResidentDrawerSettings.enableOcclusionCullingInCameras = occlusion);
                    if (p.deferredOnly)
                        Hd("lit shader mode", s.supportedLitShaderMode, RenderPipelineSettings.SupportedLitShaderMode.DeferredOnly,
                            (ref RenderPipelineSettings x) => x.supportedLitShaderMode = RenderPipelineSettings.SupportedLitShaderMode.DeferredOnly);

                    Hd("max shadows on screen", s.hdShadowInitParams.maxShadowRequests, tier.maxShadowsOnScreen,
                        (ref RenderPipelineSettings x) => x.hdShadowInitParams.maxShadowRequests = tier.maxShadowsOnScreen);
                    Hd("shadow dynamic rescale", s.hdShadowInitParams.punctualLightShadowAtlas.useDynamicViewportRescale, tier.dynamicShadowRescale,
                        (ref RenderPipelineSettings x) => x.hdShadowInitParams.punctualLightShadowAtlas.useDynamicViewportRescale = tier.dynamicShadowRescale);
                    Hd("sun shadow filtering", s.hdShadowInitParams.directionalShadowFilteringQuality, tier.directionalShadowFiltering,
                        (ref RenderPipelineSettings x) => x.hdShadowInitParams.directionalShadowFilteringQuality = tier.directionalShadowFiltering);
                    Hd("light shadow filtering", s.hdShadowInitParams.punctualShadowFilteringQuality, tier.punctualShadowFiltering,
                        (ref RenderPipelineSettings x) => x.hdShadowInitParams.punctualShadowFilteringQuality = tier.punctualShadowFiltering);
                    Hd("max lights on screen", s.lightLoopSettings.maxPunctualLightsOnScreen, tier.maxPunctualLightsOnScreen,
                        (ref RenderPipelineSettings x) => x.lightLoopSettings.maxPunctualLightsOnScreen = tier.maxPunctualLightsOnScreen);

                    Hd("screen-space reflections", s.supportSSR, tier.screenSpaceReflections, (ref RenderPipelineSettings x) => x.supportSSR = tier.screenSpaceReflections);
                    Hd("screen-space AO", s.supportSSAO, tier.screenSpaceAmbientOcclusion, (ref RenderPipelineSettings x) => x.supportSSAO = tier.screenSpaceAmbientOcclusion);
                    Hd("screen-space GI", s.supportSSGI, tier.screenSpaceGlobalIllumination, (ref RenderPipelineSettings x) => x.supportSSGI = tier.screenSpaceGlobalIllumination);
                    Hd("volumetric fog", s.supportVolumetrics, tier.volumetricFog, (ref RenderPipelineSettings x) => x.supportVolumetrics = tier.volumetricFog);
                    Hd("low-res transparency", s.lowresTransparentSettings.enabled, tier.lowResolutionTransparency,
                        (ref RenderPipelineSettings x) => x.lowresTransparentSettings.enabled = tier.lowResolutionTransparency);
                    Hd("decal draw distance", s.decalSettings.drawDistance, tier.decalDrawDistance,
                        (ref RenderPipelineSettings x) => x.decalSettings.drawDistance = tier.decalDrawDistance);

                    var probes = tier.adaptiveProbeVolumes ? RenderPipelineSettings.LightProbeSystem.AdaptiveProbeVolumes
                                                           : RenderPipelineSettings.LightProbeSystem.LegacyLightProbes;
                    bool gpuStream = tier.adaptiveProbeVolumes && tier.probeVolumeGpuStreaming;
                    bool diskStream = gpuStream && tier.probeVolumeDiskStreaming;
                    Hd("light probe system", s.lightProbeSystem, probes, (ref RenderPipelineSettings x) => x.lightProbeSystem = probes);
                    Hd("probe volume GPU streaming", s.supportProbeVolumeGPUStreaming, gpuStream, (ref RenderPipelineSettings x) => x.supportProbeVolumeGPUStreaming = gpuStream);
                    Hd("probe volume disk streaming", s.supportProbeVolumeDiskStreaming, diskStream, (ref RenderPipelineSettings x) => x.supportProbeVolumeDiskStreaming = diskStream);

                    // Quality settings only read and write through the active level, so visit each in turn.
                    QualitySettings.SetQualityLevel(level, false);
                    void Quality(string label, object from, object to, Action set) => Add(q + label, from, to, () =>
                    {
                        QualitySettings.SetQualityLevel(level, false);
                        set();
                    });
                    Quality("LOD bias", QualitySettings.lodBias, tier.lodBias, () => QualitySettings.lodBias = tier.lodBias);
                    Quality("maximum LOD level", QualitySettings.maximumLODLevel, tier.maximumLodLevel, () => QualitySettings.maximumLODLevel = tier.maximumLodLevel);
                    Quality("Mesh LOD threshold", QualitySettings.meshLodThreshold, tier.meshLodThreshold, () => QualitySettings.meshLodThreshold = tier.meshLodThreshold);
                    Quality("texture streaming", QualitySettings.streamingMipmapsActive, tier.textureStreaming, () => QualitySettings.streamingMipmapsActive = tier.textureStreaming);
                    Quality("texture streaming budget (MB)", QualitySettings.streamingMipmapsMemoryBudget, (float)tier.textureStreamingBudgetMB,
                        () => QualitySettings.streamingMipmapsMemoryBudget = tier.textureStreamingBudgetMB);
                    Quality("async upload time slice (ms)", QualitySettings.asyncUploadTimeSlice, tier.asyncUploadTimeSliceMs,
                        () => QualitySettings.asyncUploadTimeSlice = tier.asyncUploadTimeSliceMs);
                    Quality("async upload buffer (MB)", QualitySettings.asyncUploadBufferSize, tier.asyncUploadBufferMB,
                        () => QualitySettings.asyncUploadBufferSize = tier.asyncUploadBufferMB);
                    Quality("async upload persistent buffer", QualitySettings.asyncUploadPersistentBuffer, true,
                        () => QualitySettings.asyncUploadPersistentBuffer = true);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(original, false);
            }
            return plan;
        }

        string CreateProfile()
        {
            var created = CreateInstance<PerformanceProfile>();
            string[] names = QualitySettings.names;
            int original = QualitySettings.GetQualityLevel();
            try
            {
                created.tiers = names.Select((name, level) =>
                {
                    QualitySettings.SetQualityLevel(level, false);
                    var t = StartingValues(name);
                    t.meshLodThreshold = QualitySettings.meshLodThreshold;
                    return t;
                }).ToArray();
            }
            finally
            {
                QualitySettings.SetQualityLevel(original, false);
            }

            string folder = System.IO.Path.GetDirectoryName(ProfilePath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Scriptable Objects", "Performance");
            AssetDatabase.CreateAsset(created, ProfilePath);
            profile = created;
            return $"Created {ProfilePath}. Review it, then Preview changes.";
        }

        // Starting points, not measured answers: the stress test only measured High Fidelity at 4K.
        static PerformanceProfile.Tier StartingValues(string level)
        {
            var t = new PerformanceProfile.Tier { qualityLevel = level };
            switch (level)
            {
                case "High Fidelity":
                    t.maxShadowsOnScreen = 48;
                    t.directionalShadowFiltering = HDShadowFilteringQuality.High;
                    t.decalDrawDistance = 150;
                    t.textureStreamingBudgetMB = 4096;
                    break;
                case "Performant":
                    t.lodBias = 0.6f;
                    t.maxShadowsOnScreen = 16;
                    t.directionalShadowFiltering = HDShadowFilteringQuality.Low;
                    t.punctualShadowFiltering = HDShadowFilteringQuality.Low;
                    t.maxPunctualLightsOnScreen = 64;
                    t.screenSpaceReflections = false;
                    t.volumetricFog = false;
                    t.decalDrawDistance = 60;
                    t.textureStreamingBudgetMB = 1536;
                    break;
                default:
                    t.lodBias = 0.8f;
                    t.maxPunctualLightsOnScreen = 96;
                    break;
            }
            return t;
        }

        static string EnableMeshLods()
        {
            var importers = Importers<ModelImporter>(ModelFolders, "t:Model").Where(m => !m.generateMeshLods).ToList();
            if (importers.Count == 0) return "Every model already generates Mesh LODs.";
            if (!EditorUtility.DisplayDialog("Mesh LOD on models",
                    $"Turn on Mesh LOD generation for {importers.Count} models in {string.Join(", ", ModelFolders)}? " +
                    "Each reimports, which takes a while. Commit first.", "Reimport", "Cancel"))
                return "Cancelled.";
            Reimport(importers, m => m.generateMeshLods = true);
            return $"Mesh LOD generation on for {importers.Count} models.";
        }

        static string EnableTextureStreaming()
        {
            var importers = Importers<TextureImporter>(TextureFolders, "t:Texture")
                .Where(t => t.mipmapEnabled && !t.streamingMipmaps
                            && t.textureType != TextureImporterType.Sprite && t.textureType != TextureImporterType.GUI)
                .ToList();
            if (importers.Count == 0) return "Every mipmapped texture already streams.";
            if (!EditorUtility.DisplayDialog("Mipmap streaming on textures",
                    $"Turn on mipmap streaming for {importers.Count} textures in {string.Join(", ", TextureFolders)}? " +
                    "Each reimports, which takes a while. Commit first.", "Reimport", "Cancel"))
                return "Cancelled.";
            Reimport(importers, t => t.streamingMipmaps = true);
            return $"Mipmap streaming on for {importers.Count} textures.";
        }

        static IEnumerable<T> Importers<T>(string[] folders, string filter) where T : AssetImporter =>
            AssetDatabase.FindAssets(filter, folders.Where(AssetDatabase.IsValidFolder).ToArray())
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Select(AssetImporter.GetAtPath).OfType<T>();

        static void Reimport<T>(List<T> importers, Action<T> change) where T : AssetImporter
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < importers.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Reimporting", importers[i].assetPath, (float)i / importers.Count)) break;
                    change(importers[i]);
                    importers[i].SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
    }
}
