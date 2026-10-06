#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MotoSquid.Performance
{
    public class GpuSettingsPass : EditorWindow
    {
        struct Setting
        {
            public string   Path;
            public float[]  Before;
            public float[]  After;
            public Setting(string path, float[] before, float[] after) { Path = path; Before = before; After = after; }
        }

        const string RP = "m_RenderPipelineSettings.";

        static readonly Setting[] HdrpSettings =
        {
            new Setting(RP + "gpuResidentDrawerSettings.mode",
                        new[] { 0f, 0f, 0f },          new[] { 1f, 1f, 1f }),
            new Setting(RP + "gpuResidentDrawerSettings.enableOcclusionCullingInCameras",
                        new[] { 0f, 0f, 0f },          new[] { 1f, 1f, 1f }),
            new Setting(RP + "gpuResidentDrawerSettings.smallMeshScreenPercentage",
                        new[] { 0f, 0f, 0f },          new[] { 0f, 0.1f, 0.2f }),

            new Setting(RP + "supportSSGI",
                        new[] { 0f, 0f, 1f },          new[] { 0f, 0f, 0f }),
            new Setting(RP + "supportVolumetrics",
                        new[] { 1f, 1f, 1f },          new[] { 1f, 1f, 0f }),
            new Setting(RP + "supportScreenSpaceLensFlare",
                        new[] { 1f, 1f, 1f },          new[] { 1f, 0f, 0f }),
            new Setting(RP + "supportDataDrivenLensFlare",
                        new[] { 1f, 1f, 1f },          new[] { 1f, 1f, 0f }),
            new Setting(RP + "supportTransparentDepthPostpass",
                        new[] { 1f, 1f, 1f },          new[] { 1f, 0f, 0f }),
            new Setting(RP + "supportTransparentBackface",
                        new[] { 1f, 1f, 1f },          new[] { 1f, 0f, 0f }),
            new Setting(RP + "supportWaterDecals",
                        new[] { 1f, 1f, 1f },          new[] { 0f, 0f, 0f }),

            new Setting(RP + "lightLoopSettings.maxPunctualLightsOnScreen",
                        new[] { 512f, 512f, 512f },    new[] { 256f, 128f, 64f }),
            new Setting(RP + "lightLoopSettings.maxDecalsOnScreen",
                        new[] { 512f, 512f, 512f },    new[] { 256f, 128f, 64f }),
            new Setting(RP + "decalSettings.drawDistance",
                        new[] { 1000f, 1000f, 1000f }, new[] { 1000f, 400f, 200f }),
            new Setting(RP + "decalSettings.perChannelMask",
                        new[] { 1f, 1f, 1f },          new[] { 1f, 0f, 0f }),
            new Setting(RP + "hdShadowInitParams.maxShadowRequests",
                        new[] { 128f, 128f, 32f },     new[] { 128f, 64f, 32f }),
            new Setting(RP + "hdShadowInitParams.punctualLightShadowAtlas.shadowAtlasResolution",
                        new[] { 2048f, 2048f, 2048f }, new[] { 2048f, 2048f, 1024f }),
        };

        static readonly float[] LodBiasBefore     = { 1f, 1f, 1f };
        static readonly float[] LodBiasAfter      = { 1f, 0.75f, 0.5f };
        static readonly int[]   MipmapLimitBefore = { 0, 0, 0 };
        static readonly int[]   MipmapLimitAfter  = { 0, 0, 1 };
        static readonly bool[]  StreamingBefore   = { false, true, true };
        static readonly bool[]  StreamingAfter    = { true, true, true };

        const int   CascadeBefore = 4,      CascadeAfter = 2;
        const float FogDepthBefore = 1540.9f, FogDepthAfter = 350f;
        const FogDenoisingMode FogDenoiseBefore = FogDenoisingMode.Gaussian;
        const FogDenoisingMode FogDenoiseAfter  = FogDenoisingMode.Reprojection;

        bool _revert;
        bool _dryRun = true;
        Vector2 _scroll;
        readonly List<string> _log = new List<string>();

        [MenuItem("Tools/GPU Settings Pass")]
        static void Open() => GetWindow<GpuSettingsPass>("GPU Settings Pass");

        void OnGUI()
        {
            EditorGUILayout.LabelField("GPU Settings Pass", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Applies or reverts the per tier GPU settings across the three HDRP assets, the three " +
                "quality levels, the open scene's Volume profile and both split screen cameras.\n\n" +
                "Revert restores the values the project had on 31 Aug 2026, before this pass began.\n\n" +
                "This edits shared project settings, not just your own files.",
                MessageType.Warning);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Apply optimised values", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Preview Apply")) Run(revert: false, dryRun: true);
            if (GUILayout.Button("Apply"))         Run(revert: false, dryRun: false);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Restore pre pass values", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Preview Revert")) Run(revert: true, dryRun: true);

            GUI.color = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("Revert")) Run(revert: true, dryRun: false);
            GUI.color = Color.white;

            if (_log.Count == 0) return;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"{_log.Count} line(s)", EditorStyles.miniLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (string line in _log) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        void Run(bool revert, bool dryRun)
        {
            _revert = revert;
            _dryRun = dryRun;
            EditorApplication.delayCall += () =>
            {
                _log.Clear();
                Log(_revert ? "=== REVERT to pre pass values ===" : "=== APPLY optimised values ===");
                try
                {
                    ApplyQualitySettings();
                    ApplyHdrpAssets();
                    ApplyPlayerSettings();
                    ApplyVolume();
                    ApplyCameras();
                }
                catch (System.Exception e)
                {
                    Log($"FAILED: {e.Message}");
                    Debug.LogException(e);
                }

                if (!_dryRun)
                {
                    AssetDatabase.SaveAssets();
                    Log("Assets saved. Scene changes are marked dirty, save the scene yourself.");
                }
                Repaint();
            };
        }

        void Log(string s) { _log.Add(s); Debug.Log($"[GPU Settings Pass] {s}"); }

        void ApplyQualitySettings()
        {
            float[] lod    = _revert ? LodBiasBefore     : LodBiasAfter;
            int[]   mipmap = _revert ? MipmapLimitBefore : MipmapLimitAfter;
            bool[]  stream = _revert ? StreamingBefore   : StreamingAfter;

            int restore = QualitySettings.GetQualityLevel();

            for (int i = 0; i < QualitySettings.names.Length && i < lod.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                string name = QualitySettings.names[i];

                if (!Mathf.Approximately(QualitySettings.lodBias, lod[i]))
                {
                    Log($"Quality '{name}': lodBias {QualitySettings.lodBias} -> {lod[i]}");
                    if (!_dryRun) QualitySettings.lodBias = lod[i];
                }

                if (QualitySettings.globalTextureMipmapLimit != mipmap[i])
                {
                    Log($"Quality '{name}': globalTextureMipmapLimit {QualitySettings.globalTextureMipmapLimit} -> {mipmap[i]}");
                    if (!_dryRun) QualitySettings.globalTextureMipmapLimit = mipmap[i];
                }

                if (QualitySettings.streamingMipmapsActive != stream[i])
                {
                    Log($"Quality '{name}': streamingMipmapsActive {QualitySettings.streamingMipmapsActive} -> {stream[i]}");
                    if (!_dryRun) QualitySettings.streamingMipmapsActive = stream[i];
                }
            }

            QualitySettings.SetQualityLevel(restore, applyExpensiveChanges: false);
        }

        void ApplyHdrpAssets()
        {
            for (int i = 0; i < QualitySettings.names.Length && i < 3; i++)
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(i);
                if (asset == null)
                {
                    Log($"Quality level {i} has no render pipeline asset, skipped.");
                    continue;
                }

                Log($"Level {i} '{QualitySettings.names[i]}' -> {asset.name}");

                var so = new SerializedObject(asset);
                bool changed = false;

                foreach (var s in HdrpSettings)
                    changed |= SetValue(so, s.Path, (_revert ? s.Before : s.After)[i], asset.name);

                if (changed && !_dryRun)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(asset);
                }
            }
        }

        bool SetValue(SerializedObject so, string path, float value, string assetName)
        {
            var prop = so.FindProperty(path);
            if (prop == null)
            {
                Log($"{assetName}: property '{path}' not found, skipped.");
                return false;
            }

            string field = path.Substring(path.LastIndexOf('.') + 1);

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    bool wantBool = value != 0f;
                    if (prop.boolValue == wantBool) return false;
                    Log($"{assetName}: {field} {prop.boolValue} -> {wantBool}");
                    if (_dryRun) return false;
                    prop.boolValue = wantBool;
                    return true;

                case SerializedPropertyType.Float:
                    if (Mathf.Approximately(prop.floatValue, value)) return false;
                    Log($"{assetName}: {field} {prop.floatValue} -> {value}");
                    if (_dryRun) return false;
                    prop.floatValue = value;
                    return true;

                default:
                    int wantInt = Mathf.RoundToInt(value);
                    if (prop.intValue == wantInt) return false;
                    Log($"{assetName}: {field} {prop.intValue} -> {wantInt}");
                    if (_dryRun) return false;
                    prop.intValue = wantInt;
                    return true;
            }
        }

        void ApplyPlayerSettings()
        {
            if (PlayerSettings.enableFrameTimingStats) return;

            Log("PlayerSettings: Frame Timing Stats off -> on (needed for a real GPU number)");
            if (!_dryRun) PlayerSettings.enableFrameTimingStats = true;
        }

        void ApplyVolume()
        {
            int   cascades = _revert ? CascadeBefore  : CascadeAfter;
            float fogDepth = _revert ? FogDepthBefore : FogDepthAfter;
            var   denoise  = _revert ? FogDenoiseBefore : FogDenoiseAfter;

            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                VolumeProfile profile = volume.sharedProfile;
                if (profile == null) continue;

                if (profile.TryGet(out HDShadowSettings shadows) &&
                    shadows.cascadeShadowSplitCount.value != cascades)
                {
                    Log($"{profile.name}: cascadeShadowSplitCount {shadows.cascadeShadowSplitCount.value} -> {cascades}");
                    if (!_dryRun)
                    {
                        shadows.cascadeShadowSplitCount.value = cascades;
                        EditorUtility.SetDirty(profile);
                    }
                }

                if (profile.TryGet(out Fog fog) && !Mathf.Approximately(fog.depthExtent.value, fogDepth))
                {
                    Log($"{profile.name}: fog depthExtent {fog.depthExtent.value} -> {fogDepth}, " +
                        $"denoisingMode {fog.denoisingMode.value} -> {denoise}");
                    if (!_dryRun)
                    {
                        fog.depthExtent.value   = fogDepth;
                        fog.denoisingMode.value = denoise;
                        EditorUtility.SetDirty(profile);
                    }
                }
            }
        }

        void ApplyCameras()
        {
            bool want = !_revert;

            foreach (var hd in Object.FindObjectsByType<HDAdditionalCameraData>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hd.allowDynamicResolution == want) continue;

                Log($"Camera '{hd.name}': HDRP Dynamic Resolution {hd.allowDynamicResolution} -> {want}");
                if (_dryRun) continue;

                Undo.RecordObject(hd, "Set HDRP Dynamic Resolution");
                hd.allowDynamicResolution = want;
                EditorUtility.SetDirty(hd);
            }
        }
    }
}
#endif
