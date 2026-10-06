using MotoSquid.AI;
using MotoSquid.Core;
using MotoSquid.Traffic;
using System.Collections;
using System.Collections.Generic;
using Michsky.UI.Heat;
using HeatDropdown = Michsky.UI.Heat.Dropdown;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace MotoSquid.Settings
{
    public class SettingsManager : MonoBehaviour
    {

        [Header("Audio Mixer (optional)")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string masterVolumeParam = "MasterVolume";
        [SerializeField] private string musicVolumeParam  = "MusicVolume";
        [SerializeField] private string sfxVolumeParam    = "SFXVolume";
        [SerializeField] private string uiVolumeParam     = "UIVolume";

        // The designed mix balance. Sliders ride on top of these, so 100% means "as mixed", not 0 dB,
        // without them a slider at full wipes the balance off every bus it controls
        [Header("Mix balance (dB, sliders ride on top)")]
        [SerializeField] private float masterTrimDb = 0f;
        [SerializeField] private float musicTrimDb  = -10.5f;
        [SerializeField] private float sfxTrimDb    = 0f;
        [SerializeField] private float uiTrimDb     = -9f;

        [Header("Audio Sliders")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider uiVolumeSlider;

        [Header("Audio: Music Source")]
        [SerializeField] private SwitchManager userMusicSwitch;

        [Header("Audio: Subtitles")]
        [SerializeField] private SwitchManager      subtitlesSwitch;
        [SerializeField] private HorizontalSelector subtitleSizeSelector;
        [SerializeField] private Slider             subtitleBgOpacitySlider;

        [Header("Display: Monitor & Window")]
        [SerializeField] private HeatDropdown       monitorDropdown;
        [SerializeField] private HeatDropdown       resolutionDropdown;
        [SerializeField] private HorizontalSelector windowModeSelector;
        [SerializeField] private HorizontalSelector frameRateSelector;
        [SerializeField] private SwitchManager      vSyncSwitch;
        [SerializeField] private HorizontalSelector renderScaleSelector;

        [Header("Display: UI")]
        [SerializeField] private HorizontalSelector uiScaleSelector;
        [SerializeField] private Slider             brightnessSlider;

        [Header("Graphics: Preset")]
        [SerializeField] private HorizontalSelector qualityPresetSelector;
        [SerializeField] private Camera             mainCamera;

        [Header("Graphics: Post Processing Volume")]
        [SerializeField] private Volume postProcessingVolume;

        [Header("Graphics: Per Effect")]
        [SerializeField] private HorizontalSelector antiAliasingSelector;
        [SerializeField] private HorizontalSelector shadowQualitySelector;
        [SerializeField] private HorizontalSelector textureQualitySelector;
        [SerializeField] private HorizontalSelector postProcessingSelector;
        [SerializeField] private HorizontalSelector particleEffectsSelector;
        [SerializeField] private HorizontalSelector motionBlurSelector;
        [SerializeField] private HorizontalSelector cameraFOVSelector;

        [Header("Controls")]
        [SerializeField] private Slider cameraSensitivitySlider;

        [Header("Accessibility: Visual")]
        [SerializeField] private SwitchManager             colourBlindModeSwitch;
        [SerializeField] private SwitchManager             highContrastSwitch;
        [SerializeField] private SwitchManager             epiSafeModeSwitch;
        [SerializeField] private HorizontalSelector        screenShakeSelector;
        [SerializeField] private ColourBlindFilter colourBlindFilter;

        [Header("Accessibility: Hearing")]
        [SerializeField] private SwitchManager visualAudioCuesSwitch;
        [SerializeField] private SwitchManager monoAudioSwitch;
        [SerializeField] private SwitchManager helmetAudioSwitch;  

        [Header("Accessibility: Motor")]
        [SerializeField] private SwitchManager rumbleSwitch;
        [SerializeField] private SwitchManager steeringAssistSwitch;
        [SerializeField] private SwitchManager autoBrakeSwitch;
        [SerializeField] private SwitchManager toggleSteerSwitch;

        [Header("Accessibility: Cognitive")]
        [SerializeField] private SwitchManager simplifiedHUDSwitch;
        [SerializeField] private SwitchManager tutorialHintsSwitch;
        [SerializeField] private SwitchManager reduceClutterSwitch;

        [Header("Gameplay: Split Screen")]
        [SerializeField] private SwitchManager splitScreenLayoutSwitch;

        [Header("Difficulty")]
        [SerializeField] private HorizontalSelector difficultyPresetSelector;
        [SerializeField] private HorizontalSelector aiDifficultySelector;
        [SerializeField] private HorizontalSelector trafficDensitySelector;

        static readonly float[] RenderScalePercents = { 50f, 75f, 100f, 125f, 150f, 200f };
        static readonly int[]   FrameRates          = { 30, 60, 120, 144, 0 };      
        static readonly float[] CameraFOVValues     = { 60f, 75f, 90f, 100f, 110f };

        const int DefaultFrameRateIndex = 4;    
                                            
        const int SettingsVersion       = 3;
        static readonly string[] ResetOnVersionBump = { "FrameRate", "QualityPreset", "VSync" };
        static readonly float[] AIDifficultyValues  = { 0.3f, 0.6f, 1.0f };
        static readonly float[] ShadowDistances     = { 0f, 50f, 150f, 300f, 500f };

        private List<Resolution> _resolutionEntries;
        private ParticleSystem[] _cachedParticles;
        private Volume           _shadowVolume;
        private HDShadowSettings _shadowSettings;
        private float            _renderScalePercent = 100f;

        void Start()
        {
            PopulateMonitorDropdown();
            PopulateResolutionDropdown();
            _cachedParticles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            EnsureShadowVolume();
            LoadSettings();
        }

        void SaveAllToPrefs()
        {
            PlayerPrefs.SetFloat("MasterVolume",  masterVolumeSlider?.value ?? 100f);
            PlayerPrefs.SetFloat("MusicVolume",   musicVolumeSlider?.value  ?? 100f);
            PlayerPrefs.SetInt("UseUserMusic",    (userMusicSwitch?.isOn ?? false) ? 1 : 0);
            PlayerPrefs.SetFloat("SFXVolume",     sfxVolumeSlider?.value    ?? 100f);
            PlayerPrefs.SetFloat("UIVolume",      uiVolumeSlider?.value     ?? 100f);

            PlayerPrefs.SetInt("MonitorIndex",    monitorDropdown?.selectedItemIndex    ?? 0);
            PlayerPrefs.SetInt("ResolutionIndex", resolutionDropdown?.selectedItemIndex ?? 0);
            PlayerPrefs.SetInt("WindowMode",      windowModeSelector?.index        ?? 0);
            PlayerPrefs.SetInt("FrameRate",       frameRateSelector?.index         ?? DefaultFrameRateIndex);
            PlayerPrefs.SetInt("VSync",           (vSyncSwitch?.isOn ?? true)   ? 1 : 0);
            PlayerPrefs.SetInt("RenderScale",     renderScaleSelector?.index       ?? 2);
            PlayerPrefs.SetInt("UIScale",         uiScaleSelector?.index           ?? 1);
            PlayerPrefs.SetFloat("BrightnessPct", brightnessSlider?.value          ?? 100f);

            PlayerPrefs.SetInt("QualityPreset",   qualityPresetSelector?.index     ?? 0);
            PlayerPrefs.SetInt("AntiAliasing",    antiAliasingSelector?.index      ?? 2);
            PlayerPrefs.SetInt("ShadowQuality",   shadowQualitySelector?.index     ?? 3);
            PlayerPrefs.SetInt("TextureQuality",  textureQualitySelector?.index    ?? 0);
            PlayerPrefs.SetInt("PostProcessing",  postProcessingSelector?.index    ?? 2);
            PlayerPrefs.SetInt("ParticleEffects", particleEffectsSelector?.index ?? 0);
            PlayerPrefs.SetInt("MotionBlur",      motionBlurSelector?.index        ?? 0);
            PlayerPrefs.SetInt("CameraFOVIdx",    cameraFOVSelector?.index         ?? 0);

            PlayerPrefs.SetFloat("CameraSensitivity", cameraSensitivitySlider?.value ?? 50f);

            PlayerPrefs.SetInt("HighContrast",     (highContrastSwitch?.isOn    ?? false) ? 1 : 0);
            PlayerPrefs.SetInt("EpiSafeMode",      (epiSafeModeSwitch?.isOn     ?? false) ? 1 : 0);
            PlayerPrefs.SetInt("VisualAudioCues",  (visualAudioCuesSwitch?.isOn ?? false) ? 1 : 0);
            PlayerPrefs.SetInt("MonoAudio",        (monoAudioSwitch?.isOn       ?? false) ? 1 : 0);
            PlayerPrefs.SetInt("HelmetAudio",      (helmetAudioSwitch?.isOn     ?? true)  ? 1 : 0);
            PlayerPrefs.SetInt("Rumble",           (rumbleSwitch?.isOn          ?? true)  ? 1 : 0);

            PlayerPrefs.SetInt("AutoBrake",        (autoBrakeSwitch?.isOn       ?? false) ? 1 : 0);
            PlayerPrefs.SetInt("ToggleSteer",      (toggleSteerSwitch?.isOn     ?? false) ? 1 : 0);
            PlayerPrefs.SetInt("SimplifiedHUD",    (simplifiedHUDSwitch?.isOn   ?? false) ? 1 : 0);
            PlayerPrefs.SetInt("TutorialHints",    (tutorialHintsSwitch?.isOn   ?? true)  ? 1 : 0);
            PlayerPrefs.SetInt("ReduceClutter",    (reduceClutterSwitch?.isOn   ?? false) ? 1 : 0);

            PlayerPrefs.SetInt("SplitScreenTopBottom", (splitScreenLayoutSwitch?.isOn ?? false) ? 1 : 0);

            PlayerPrefs.SetInt("DifficultyPreset", difficultyPresetSelector?.index ?? 1);
            PlayerPrefs.SetInt("AIDifficulty",     aiDifficultySelector?.index     ?? 1);
            PlayerPrefs.SetInt("TrafficDensity",   trafficDensitySelector?.index   ?? 1);

            PlayerPrefs.Save();
        }

        static void ApplyChangedDefaults()
        {
            if (PlayerPrefs.GetInt("SettingsVersion", 1) >= SettingsVersion) return;

            foreach (string key in ResetOnVersionBump) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetInt("SettingsVersion", SettingsVersion);
            PlayerPrefs.Save();
        }

        void LoadSettings()
        {
            ApplyChangedDefaults();

            // Audio
            float master = PlayerPrefs.GetFloat("MasterVolume", 100f);
            float music  = PlayerPrefs.GetFloat("MusicVolume",  100f);
            float sfx    = PlayerPrefs.GetFloat("SFXVolume",    100f);
            masterVolumeSlider?.SetValueWithoutNotify(master);
            musicVolumeSlider?.SetValueWithoutNotify(music);
            sfxVolumeSlider?.SetValueWithoutNotify(sfx);
            float ui = PlayerPrefs.GetFloat("UIVolume", 100f);
            uiVolumeSlider?.SetValueWithoutNotify(ui);
            OnMasterVolumeChanged(master); OnMusicVolumeChanged(music); OnSFXVolumeChanged(sfx); OnUIVolumeChanged(ui);
            SetSwitchWithoutNotify(userMusicSwitch, PlayerPrefs.GetInt("UseUserMusic", 0) == 1);

            // Display
            PopulateMonitorDropdown();
            int mon = monitorDropdown?.selectedItemIndex ?? 0;
            OnMonitorChanged(mon);
            OnResolutionChanged(resolutionDropdown?.selectedItemIndex ?? 0);
            int wm = PlayerPrefs.GetInt("WindowMode", 0);
            SetSelectorWithoutNotify(windowModeSelector, wm); OnWindowModeChanged(wm);
            int fr = PlayerPrefs.GetInt("FrameRate", DefaultFrameRateIndex);
            SetSelectorWithoutNotify(frameRateSelector, fr); OnFrameRateChanged(fr);
            bool vs = PlayerPrefs.GetInt("VSync", 1) == 1;
            SetSwitchWithoutNotify(vSyncSwitch, vs); OnVSyncChanged(vs);
            int rs = PlayerPrefs.GetInt("RenderScale", 2);
            SetSelectorWithoutNotify(renderScaleSelector, rs); OnRenderScaleChanged(rs);
            int uiScale = PlayerPrefs.GetInt("UIScale", 1);
            SetSelectorWithoutNotify(uiScaleSelector, uiScale); OnUIScaleChanged(uiScale);
            float brightness = PlayerPrefs.GetFloat("BrightnessPct", 100f);
            brightnessSlider?.SetValueWithoutNotify(brightness); OnBrightnessChanged(brightness);

            // Graphics
            int qp = PlayerPrefs.GetInt("QualityPreset", QualitySettings.GetQualityLevel());
            SetSelectorWithoutNotify(qualityPresetSelector, qp);
            QualitySettings.SetQualityLevel(qp, true);
            int aa = PlayerPrefs.GetInt("AntiAliasing", 2);
            SetSelectorWithoutNotify(antiAliasingSelector, aa); OnAntiAliasingChanged(aa);
            int sq = PlayerPrefs.GetInt("ShadowQuality", 3);
            SetSelectorWithoutNotify(shadowQualitySelector, sq); OnShadowQualityChanged(sq);
            if (PlayerPrefs.HasKey("TextureQuality"))
            {
                int tq = PlayerPrefs.GetInt("TextureQuality");
                SetSelectorWithoutNotify(textureQualitySelector, tq); OnTextureQualityChanged(tq);
            }
            int pp = PlayerPrefs.GetInt("PostProcessing", 2);
            SetSelectorWithoutNotify(postProcessingSelector, pp); OnPostProcessingChanged(pp);
            int pe = PlayerPrefs.GetInt("ParticleEffects", 0);
            SetSelectorWithoutNotify(particleEffectsSelector, pe); OnParticleEffectsChanged(pe);
            int mb = PlayerPrefs.GetInt("MotionBlur", 0);
            SetSelectorWithoutNotify(motionBlurSelector, mb); OnMotionBlurChanged(mb);
            int fov = PlayerPrefs.GetInt("CameraFOVIdx", 0);   // 0 = 60 FOV = neutral (no bias)
            SetSelectorWithoutNotify(cameraFOVSelector, fov); OnCameraFOVChanged(fov);
            SyncGraphicsToCurrentSettings();

            // Controls
            float sens = PlayerPrefs.GetFloat("CameraSensitivity", 50f);
            cameraSensitivitySlider?.SetValueWithoutNotify(sens); OnCameraSensitivityChanged(sens);

            // Accessibility (read live values from the manager)
            if (Acc != null)
            {
                SetSwitchWithoutNotify(colourBlindModeSwitch, Acc.ColourBlindMode != 0);
                SetSwitchWithoutNotify(highContrastSwitch, Acc.HighContrast);
                SetSwitchWithoutNotify(epiSafeModeSwitch, Acc.EpiSafeMode);
                int shakeIndex = Acc.ScreenShakeScale <= 0f ? 0 : Acc.ScreenShakeScale < 1f ? 1 : 2;
                SetSelectorWithoutNotify(screenShakeSelector, shakeIndex);
                SetSwitchWithoutNotify(visualAudioCuesSwitch, Acc.VisualAudioCues);
                SetSwitchWithoutNotify(monoAudioSwitch, Acc.MonoAudio);
                SetSwitchWithoutNotify(helmetAudioSwitch, Acc.HelmetAudio);
                SetSwitchWithoutNotify(subtitlesSwitch, Acc.SubtitlesEnabled);
                SetSelectorWithoutNotify(subtitleSizeSelector, Acc.SubtitleSize);
                subtitleBgOpacitySlider?.SetValueWithoutNotify(Acc.SubtitleBgOpacity * 100f);
                SetSwitchWithoutNotify(rumbleSwitch, Acc.RumbleEnabled);
                SetSwitchWithoutNotify(steeringAssistSwitch, Acc.SteeringAssist > 0f);
                SetSwitchWithoutNotify(autoBrakeSwitch, Acc.AutoBrake);
                SetSwitchWithoutNotify(toggleSteerSwitch, Acc.ToggleSteer);
                SetSwitchWithoutNotify(simplifiedHUDSwitch, Acc.SimplifiedHUD);
                SetSwitchWithoutNotify(tutorialHintsSwitch, Acc.TutorialHints);
                SetSwitchWithoutNotify(reduceClutterSwitch, Acc.ReduceClutter);
                colourBlindFilter?.ApplyMode(Acc.ColourBlindMode);
                colourBlindFilter?.SetHighContrast(Acc.HighContrast);
            }

            // Gameplay
            bool splitTop = PlayerPrefs.GetInt("SplitScreenTopBottom", 0) == 1;
            SetSwitchWithoutNotify(splitScreenLayoutSwitch, splitTop);

            // Difficulty
            int diff    = PlayerPrefs.GetInt("DifficultyPreset", 1);
            int aiDiff  = PlayerPrefs.GetInt("AIDifficulty",     1);
            int traffic = PlayerPrefs.GetInt("TrafficDensity",   1);
            SetSelectorWithoutNotify(difficultyPresetSelector, diff);
            SetSelectorWithoutNotify(aiDifficultySelector, aiDiff);
            SetSelectorWithoutNotify(trafficDensitySelector, traffic);
            if (GameSession.Instance != null)
            {
                GameSession.Instance.DifficultyPreset = diff;
                GameSession.Instance.AIDifficulty     = AIDifficultyValues[Mathf.Clamp(aiDiff, 0, AIDifficultyValues.Length - 1)];
                GameSession.Instance.TrafficDensity   = traffic;
            }
        }

        // Audio callbacks

        static float Norm(float v) => v / 100f;

        public void OnMasterVolumeChanged(float value)
        {
            SetMixerVolume(masterVolumeParam, Norm(value), masterTrimDb);
        }

        public void OnMusicVolumeChanged(float value)
        {
            SetMixerVolume(musicVolumeParam, Norm(value), musicTrimDb);
        }

        public void OnSFXVolumeChanged(float value)
        {
            SetMixerVolume(sfxVolumeParam, Norm(value), sfxTrimDb);
        }

        public void OnUIVolumeChanged(float value)
        {
            SetMixerVolume(uiVolumeParam, Norm(value), uiTrimDb);
        }

        void SetMixerVolume(string param, float linear, float trimDb)
        {
            if (audioMixer == null) return;
            audioMixer.SetFloat(param, trimDb + Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f);
        }

        public void OnSubtitlesChanged(bool v)         { Acc?.SetSubtitles(v); }
        public void OnSubtitleSizeChanged(int v)        { Acc?.SetSubtitleSize(v); }
        public void OnSubtitleBgOpacityChanged(float v) { Acc?.SetSubtitleBgOpacity(Norm(v)); }

        // Display callbacks

        public void OnMonitorChanged(int index)
        {
            var layout = new List<DisplayInfo>();
            Screen.GetDisplayLayout(layout);
            if (index < 0 || index >= layout.Count) return;
            var info = layout[index];
            Screen.MoveMainWindowTo(info, new Vector2Int(info.workArea.x, info.workArea.y));
        }

        public void OnResolutionChanged(int index)
        {
            if (_resolutionEntries == null || index < 0 || index >= _resolutionEntries.Count) return;
            var r = _resolutionEntries[index];
            Screen.SetResolution(r.width, r.height, Screen.fullScreenMode, r.refreshRateRatio);
        }

        public void OnWindowModeChanged(int index)
        {
            Screen.fullScreenMode = index switch
            {
                0 => FullScreenMode.Windowed,
                1 => FullScreenMode.FullScreenWindow,
                _ => FullScreenMode.ExclusiveFullScreen
            };
        }

        public void OnFrameRateChanged(int index)
        {
            Application.targetFrameRate = FrameRates[Mathf.Clamp(index, 0, FrameRates.Length - 1)];
        }

        public void OnVSyncChanged(bool enabled)
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
        }

        public void OnRenderScaleChanged(int index)
        {
            _renderScalePercent = RenderScalePercents[Mathf.Clamp(index, 0, RenderScalePercents.Length - 1)];
            SetHDRPRenderScale(_renderScalePercent);
        }

        public void OnUIScaleChanged(int index)
        {
            float[] scales = { 0.75f, 1f, 1.25f, 1.5f, 2f };
            Acc?.SetUIScale(scales[Mathf.Clamp(index, 0, scales.Length - 1)]);
        }

        public void OnBrightnessChanged(float v)
        {
            Acc?.SetBrightness(Norm(v));
        }

        // Graphics callbacks

        public void OnQualityPresetChanged(int index)
        {
            QualitySettings.SetQualityLevel(index, true);
            SyncGraphicsToCurrentSettings();
            SetHDRPRenderScale(_renderScalePercent);
        }

        public void OnAntiAliasingChanged(int index)
        {
            var hd = GetHDCamera();
            if (hd != null)
            {
                hd.antialiasing = index switch
                {
                    1 => HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing,
                    2 => HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                    3 => HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing,
                    _ => HDAdditionalCameraData.AntialiasingMode.None
                };
            }
        }

        public void OnShadowQualityChanged(int index)
        {
            index = Mathf.Clamp(index, 0, ShadowDistances.Length - 1);

            EnsureShadowVolume();
            _shadowSettings.maxShadowDistance.value = ShadowDistances[index];
        }

        public void OnTextureQualityChanged(int index)
        {
            QualitySettings.globalTextureMipmapLimit = index;
        }

        public void OnPostProcessingChanged(int index)
        {
            if (postProcessingVolume != null) postProcessingVolume.enabled = index > 0;
        }

        public static bool ParticleEffectsEnabled { get; private set; } = true;

        public void OnParticleEffectsChanged(int index)
        {
            bool enabled = index == 0;
            ParticleEffectsEnabled = enabled;
            var systems = _cachedParticles ?? FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            foreach (var ps in systems)
            {
                if (ps == null) continue;
                if (enabled) { if (!ps.isPlaying) ps.Play(); }
                else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void OnMotionBlurChanged(int index)
        {
            Acc?.SetMotionBlur(index > 0);
        }

        public void OnCameraFOVChanged(int index)
        {
            Acc?.SetCameraFOV(CameraFOVValues[Mathf.Clamp(index, 0, CameraFOVValues.Length - 1)]);
        }

        // Controls callbacks

        public void OnCameraSensitivityChanged(float v)
        {
            PlayerPrefs.SetFloat("CameraSensitivity", v);
        }

        // Accessibility callbacks

        public void OnColourBlindModeChanged(bool v)
        {
            int mode = v ? 1 : 0;
            Acc?.SetColourBlindMode(mode);
            colourBlindFilter?.ApplyMode((ColourBlindFilter.ColourBlindMode)mode);
        }

        public void OnHighContrastChanged(bool v)
        {
            Acc?.SetHighContrast(v);
            colourBlindFilter?.SetHighContrast(v);
        }

        public void OnEpiSafeModeChanged(bool v) { Acc?.SetEpiSafeMode(v); }

        public void OnScreenShakeChanged(int index)
        {
            float[] scales = { 0f, 0.5f, 1f };
            Acc?.SetScreenShakeScale(scales[Mathf.Clamp(index, 0, 2)]);
        }

        public void OnVisualAudioCuesChanged(bool v) { Acc?.SetVisualAudioCues(v); }
        public void OnMonoAudioChanged(bool v)        { Acc?.SetMonoAudio(v); }
        public void OnHelmetAudioChanged(bool v)      { Acc?.SetHelmetAudio(v); }
        public void OnRumbleChanged(bool v)           { Acc?.SetRumble(v); }

        public void OnSteeringAssistChanged(bool v)
        {
            Acc?.SetSteeringAssist(v ? 1f : 0f);
        }

        public void OnAutoBrakeChanged(bool v)    { Acc?.SetAutoBrake(v); }
        public void OnToggleSteerChanged(bool v)  { Acc?.SetToggleSteer(v); }
        public void OnSimplifiedHUDChanged(bool v){ Acc?.SetSimplifiedHUD(v); }
        public void OnTutorialHintsChanged(bool v){ Acc?.SetTutorialHints(v); }
        public void OnReduceClutterChanged(bool v){ Acc?.SetReduceClutter(v); }

        // Gameplay callbacks

        public void OnSplitScreenLayoutChanged(bool topBottom) { }

        // Difficulty callbacks

        public void OnDifficultyPresetChanged(int index)
        {
            int[]   trafficValues = { 0, 1, 2 };
            int clamped = Mathf.Clamp(index, 0, 2);
            SetSelectorWithoutNotify(aiDifficultySelector, clamped);
            SetSelectorWithoutNotify(trafficDensitySelector, trafficValues[clamped]);
            GameSession.Instance?.ApplyDifficultyPreset(index);
        }

        public void OnAIDifficultyChanged(int index)
        {
            if (GameSession.Instance != null)
                GameSession.Instance.AIDifficulty = AIDifficultyValues[Mathf.Clamp(index, 0, AIDifficultyValues.Length - 1)];
        }

        public void OnTrafficDensityChanged(int index)
        {
            if (GameSession.Instance != null)
                GameSession.Instance.TrafficDensity = index;
        }

        // Monitor dropdown population

        void PopulateMonitorDropdown()
        {
            if (monitorDropdown == null) return;
            var layout = new List<DisplayInfo>();
            Screen.GetDisplayLayout(layout);
            monitorDropdown.items.Clear();
            for (int i = 0; i < layout.Count; i++)
                monitorDropdown.items.Add(new HeatDropdown.Item { itemName = $"Display {i + 1}  ({layout[i].width}×{layout[i].height})" });
            if (monitorDropdown.items.Count == 0) return;
            int saved = Mathf.Clamp(PlayerPrefs.GetInt("MonitorIndex", 0), 0, monitorDropdown.items.Count - 1);
            monitorDropdown.selectedItemIndex = saved;
            monitorDropdown.Initialize();
        }

        // Resolution dropdown population

        void PopulateResolutionDropdown()
        {
            if (resolutionDropdown == null) return;

            _resolutionEntries = new List<Resolution>();
            resolutionDropdown.items.Clear();

            Resolution[] nativeResolutions = Screen.resolutions;
            Resolution nativeMax = nativeResolutions.Length > 0
                ? nativeResolutions[nativeResolutions.Length - 1]
                : Screen.currentResolution;

            int savedIndex = 0;
            for (int i = 0; i < nativeResolutions.Length; i++)
            {
                var r = nativeResolutions[i];
                _resolutionEntries.Add(r);
                resolutionDropdown.items.Add(new HeatDropdown.Item { itemName = $"{r.width} × {r.height}  @{(int)r.refreshRateRatio.value}Hz" });
                if (r.width == Screen.width && r.height == Screen.height)
                    savedIndex = i;
            }

            (int w, int h)[] ultrawidePresets = { (2560, 1080), (3440, 1440), (3840, 1600), (5120, 2160) };
            foreach (var (w, h) in ultrawidePresets)
            {
                if (w > nativeMax.width || h > nativeMax.height) continue;
                if (_resolutionEntries.Exists(r => r.width == w && r.height == h)) continue;
                Resolution fake = new Resolution();
                _resolutionEntries.Add(fake);
                resolutionDropdown.items.Add(new HeatDropdown.Item { itemName = $"{w} × {h}  @{(int)nativeMax.refreshRateRatio.value}Hz  (21:9 UW)" });
            }

            if (resolutionDropdown.items.Count == 0) return;
            int idx = Mathf.Clamp(PlayerPrefs.GetInt("ResolutionIndex", savedIndex), 0, resolutionDropdown.items.Count - 1);
            resolutionDropdown.selectedItemIndex = idx;
            resolutionDropdown.Initialize();
        }

        // Graphics sync

        void SyncGraphicsToCurrentSettings()
        {
            var hd = GetHDCamera();
            if (hd != null)
            {
                int aaIndex = hd.antialiasing switch
                {
                    HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing       => 1,
                    HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing => 2,
                    HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing              => 3,
                    _                                                                          => 0
                };
                SetSelectorWithoutNotify(antiAliasingSelector, aaIndex);
            }
            SetSelectorWithoutNotify(textureQualitySelector,
                Mathf.Clamp(QualitySettings.globalTextureMipmapLimit, 0, 3));
            SetSwitchWithoutNotify(vSyncSwitch, QualitySettings.vSyncCount > 0);
        }

        // Heat UI helpers

        void SetDropdownWithoutNotify(HeatDropdown dd, int index)
        {
            if (dd == null || dd.items.Count == 0) return;
            index = Mathf.Clamp(index, 0, dd.items.Count - 1);
            dd.selectedItemIndex = index;
            if (dd.headerText != null) dd.headerText.text = dd.items[index].itemName;
        }

        void SetSwitchWithoutNotify(SwitchManager sw, bool value)
        {
            if (sw == null) return;
            sw.isOn = value;
            var animator = sw.GetComponent<Animator>();
            if (animator == null) return;
            animator.enabled = true;
            animator.Play(value ? "On Instant" : "Off Instant");
        }

        void SetSelectorWithoutNotify(HorizontalSelector sel, int index)
        {
            if (sel == null || sel.items.Count == 0) return;
            index = Mathf.Clamp(index, 0, sel.items.Count - 1);
            sel.index        = index;
            sel.defaultIndex = index;
            if (sel.label != null) sel.label.text = sel.items[index].itemTitle;
            if (sel.indicatorParent == null) return;
            for (int i = 0; i < sel.indicatorParent.childCount; i++)
            {
                var child  = sel.indicatorParent.GetChild(i);
                var onObj  = child.Find("On");
                var offObj = child.Find("Off");
                if (onObj  != null) onObj.gameObject.SetActive(i == index);
                if (offObj != null) offObj.gameObject.SetActive(i != index);
            }
        }

        // Persistence

        void OnApplicationQuit()             { SaveAllToPrefs(); PlayerPrefs.Save(); }
        void OnApplicationPause(bool paused) { if (paused) { SaveAllToPrefs(); PlayerPrefs.Save(); } }

        void OnDestroy()
        {
            if (_shadowVolume != null)
            {
                if (_shadowVolume.profile != null) Destroy(_shadowVolume.profile);
                Destroy(_shadowVolume.gameObject);
            }
        }

        // HDRP helpers

        AccessibilityManager Acc => AccessibilityManager.Instance;

        HDAdditionalCameraData GetHDCamera()
        {
            var cam = mainCamera != null ? mainCamera : Camera.main;
            return cam != null ? cam.GetComponent<HDAdditionalCameraData>() : null;
        }

        void EnsureShadowVolume()
        {
            if (_shadowSettings != null) return;
            if (_shadowVolume == null)
            {
                var go = new GameObject("[Settings_ShadowOverride]") { hideFlags = HideFlags.HideAndDontSave };
                _shadowVolume          = go.AddComponent<Volume>();
                _shadowVolume.isGlobal = true;
                _shadowVolume.priority = 100f;
                _shadowVolume.profile  = ScriptableObject.CreateInstance<VolumeProfile>();
            }
            if (!_shadowVolume.profile.TryGet(out _shadowSettings))
                _shadowSettings = _shadowVolume.profile.Add<HDShadowSettings>(true);
            _shadowSettings.maxShadowDistance.overrideState = true;
        }

        void SetHDRPRenderScale(float percentage)
        {
            DynamicResolutionHandler.SetDynamicResScaler(
                () => percentage,
                DynamicResScalePolicyType.ReturnsPercentage);
        }
    }
}
