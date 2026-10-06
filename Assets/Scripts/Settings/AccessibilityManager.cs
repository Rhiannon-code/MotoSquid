using MotoSquid.Combat;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MotoSquid.Settings
{
    public class AccessibilityManager : MonoBehaviour
    {
        Volume          _colorVolume;
        ColorAdjustments _colorAdjust;

        public static AccessibilityManager Instance { get; private set; }

        // Visual
        public event Action<ColourBlindFilter.ColourBlindMode> OnColourBlindModeChanged;
        public event Action<bool>  OnHighContrastChanged;
        public event Action<float> OnUIScaleChanged;
        public event Action<bool>  OnEpiSafeModeChanged;       // Disables flashes/strobes
        public event Action<float> OnScreenShakeScaleChanged;
        public event Action<bool>  OnMotionBlurChanged;
        public event Action<float> OnBrightnessChanged;
        public event Action<float> OnCameraFOVChanged;

        // Hearing
        public event Action<bool>  OnSubtitlesChanged;
        public event Action<int>   OnSubtitleSizeChanged;
        public event Action<float> OnSubtitleBgOpacityChanged;
        public event Action<bool>  OnVisualAudioCuesChanged;   // Screen flash on key sounds
        public event Action<bool>  OnMonoAudioChanged;
        public event Action<bool>  OnHelmetAudioChanged;    // In helmet ambience layer (muffled buffet)

        // Motor
        public event Action<bool>  OnRumbleChanged;
        public event Action<float> OnSteeringAssistChanged;
        public event Action<bool>  OnAutoBrakeChanged;
        public event Action<bool>  OnToggleSteerChanged;       // Tap to hold steering

        // Cognitive
        public event Action<bool>  OnSimplifiedHUDChanged;
        public event Action<bool>  OnTutorialHintsChanged;
        public event Action<bool>  OnReduceClutterChanged;

        // Visual properties
        public ColourBlindFilter.ColourBlindMode ColourBlindMode { get; private set; }
        public bool  HighContrast      { get; private set; }
        public float UIScale           { get; private set; } = 1f;
        public bool  EpiSafeMode       { get; private set; }
        public float ScreenShakeScale  { get; private set; } = 1f;  // 0=off  0.5=reduced  1=full
        public bool  MotionBlurEnabled { get; private set; } = true;
        public float Brightness        { get; private set; } = 1f;
        public float CameraFOV         { get; private set; } = 60f;

        // Hearing properties
        public bool  SubtitlesEnabled   { get; private set; } = true;
        public int   SubtitleSize       { get; private set; } = 1;  // 0=Small 1=Medium 2=Large
        public float SubtitleBgOpacity  { get; private set; } = 0.6f;
        public bool  VisualAudioCues    { get; private set; }
        public bool  MonoAudio          { get; private set; }
        public bool  HelmetAudio        { get; private set; } = true;

        // Motor properties
        public bool  RumbleEnabled    { get; private set; } = true;
        public float SteeringAssist   { get; private set; }   // 0=none  1=full
        public bool  AutoBrake        { get; private set; }
        public bool  ToggleSteer      { get; private set; }   // Hold to steer vs press to toggle

        // Cognitive properties
        public bool  SimplifiedHUD    { get; private set; }
        public bool  TutorialHints    { get; private set; } = true;
        public bool  ReduceClutter    { get; private set; }

        // Lifecycle
        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAll();
        }

        void LoadAll()
        {
            ColourBlindMode  = (ColourBlindFilter.ColourBlindMode)PlayerPrefs.GetInt("ColourBlindMode", 0);
            HighContrast    = PlayerPrefs.GetInt("HighContrast", 0) == 1;
            UIScale         = PlayerPrefs.GetFloat("UIScale", 1f);
            EpiSafeMode     = PlayerPrefs.GetInt("EpiSafeMode", 0) == 1;
            ScreenShakeScale= PlayerPrefs.GetFloat("ScreenShakeScale", 1f);
            MotionBlurEnabled = PlayerPrefs.GetInt("MotionBlur", 1) == 1;
            Brightness      = PlayerPrefs.GetFloat("Brightness", 1f);
            CameraFOV       = PlayerPrefs.GetFloat("CameraFOV", 60f);

            SubtitlesEnabled  = PlayerPrefs.GetInt("Subtitles", 1) == 1;
            SubtitleSize      = PlayerPrefs.GetInt("SubtitleSize", 1);
            SubtitleBgOpacity = PlayerPrefs.GetFloat("SubtitleBgOpacity", 0.6f);
            VisualAudioCues   = PlayerPrefs.GetInt("VisualAudioCues", 0) == 1;
            MonoAudio         = PlayerPrefs.GetInt("MonoAudio", 0) == 1;
            HelmetAudio       = PlayerPrefs.GetInt("HelmetAudio", 1) == 1;

            RumbleEnabled   = PlayerPrefs.GetInt("Rumble", 1) == 1;
            SteeringAssist  = PlayerPrefs.GetFloat("SteeringAssist", 0f);
            AutoBrake       = PlayerPrefs.GetInt("AutoBrake", 0) == 1;
            ToggleSteer     = PlayerPrefs.GetInt("ToggleSteer", 0) == 1;

            SimplifiedHUD   = PlayerPrefs.GetInt("SimplifiedHUD", 0) == 1;
            TutorialHints   = PlayerPrefs.GetInt("TutorialHints", 1) == 1;
            ReduceClutter   = PlayerPrefs.GetInt("ReduceClutter", 0) == 1;

            ApplyBrightness(Brightness);
            ApplyColourBlindPostProcess(ColourBlindMode);
            ApplyMonoAudio(MonoAudio);
        }

        // Visual setters
        public void SetColourBlindMode(int index)
        {
            ColourBlindMode = (ColourBlindFilter.ColourBlindMode)index;
            PlayerPrefs.SetInt("ColourBlindMode", index);
            ApplyColourBlindPostProcess(ColourBlindMode);
            OnColourBlindModeChanged?.Invoke(ColourBlindMode);
        }

        public void SetHighContrast(bool v)
        {
            HighContrast = v;
            PlayerPrefs.SetInt("HighContrast", v ? 1 : 0);
            OnHighContrastChanged?.Invoke(v);
        }

        public void SetUIScale(float v)
        {
            UIScale = v;
            PlayerPrefs.SetFloat("UIScale", v);
            OnUIScaleChanged?.Invoke(v);
        }

        public void SetEpiSafeMode(bool v)
        {
            EpiSafeMode = v;
            PlayerPrefs.SetInt("EpiSafeMode", v ? 1 : 0);
            OnEpiSafeModeChanged?.Invoke(v);
        }

        public void SetScreenShakeScale(float v)
        {
            ScreenShakeScale = v;
            PlayerPrefs.SetFloat("ScreenShakeScale", v);
            OnScreenShakeScaleChanged?.Invoke(v);
        }

        public void SetMotionBlur(bool v)
        {
            MotionBlurEnabled = v;
            PlayerPrefs.SetInt("MotionBlur", v ? 1 : 0);
            OnMotionBlurChanged?.Invoke(v);
        }

        public void SetBrightness(float v)
        {
            Brightness = v;
            PlayerPrefs.SetFloat("Brightness", v);
            ApplyBrightness(v);
            OnBrightnessChanged?.Invoke(v);
        }

        public void SetCameraFOV(float v)
        {
            CameraFOV = v;
            PlayerPrefs.SetFloat("CameraFOV", v);
            OnCameraFOVChanged?.Invoke(v);
        }

        // Hearing setters
        public void SetSubtitles(bool v)
        {
            SubtitlesEnabled = v;
            PlayerPrefs.SetInt("Subtitles", v ? 1 : 0);
            OnSubtitlesChanged?.Invoke(v);
        }

        public void SetSubtitleSize(int v)
        {
            SubtitleSize = v;
            PlayerPrefs.SetInt("SubtitleSize", v);
            OnSubtitleSizeChanged?.Invoke(v);
        }

        public void SetSubtitleBgOpacity(float v)
        {
            SubtitleBgOpacity = v;
            PlayerPrefs.SetFloat("SubtitleBgOpacity", v);
            OnSubtitleBgOpacityChanged?.Invoke(v);
        }

        public void SetVisualAudioCues(bool v)
        {
            VisualAudioCues = v;
            PlayerPrefs.SetInt("VisualAudioCues", v ? 1 : 0);
            OnVisualAudioCuesChanged?.Invoke(v);
        }

        public void SetMonoAudio(bool v)
        {
            MonoAudio = v;
            PlayerPrefs.SetInt("MonoAudio", v ? 1 : 0);
            ApplyMonoAudio(v);
            OnMonoAudioChanged?.Invoke(v);
        }

        // The in-helmet layer is a comfort/clarity option: it's a constant low rumble that some players
        // find masks the engine and the voice barks, so it can be switched off without losing the
        // exterior wind rush
        public void SetHelmetAudio(bool v)
        {
            HelmetAudio = v;
            PlayerPrefs.SetInt("HelmetAudio", v ? 1 : 0);
            OnHelmetAudioChanged?.Invoke(v);
        }

        // Motor setters
        public void SetRumble(bool v)
        {
            RumbleEnabled = v;
            PlayerPrefs.SetInt("Rumble", v ? 1 : 0);
            OnRumbleChanged?.Invoke(v);
        }

        public void SetSteeringAssist(float v)
        {
            SteeringAssist = v;
            PlayerPrefs.SetFloat("SteeringAssist", v);
            OnSteeringAssistChanged?.Invoke(v);
        }

        public void SetAutoBrake(bool v)
        {
            AutoBrake = v;
            PlayerPrefs.SetInt("AutoBrake", v ? 1 : 0);
            OnAutoBrakeChanged?.Invoke(v);
        }

        public void SetToggleSteer(bool v)
        {
            ToggleSteer = v;
            PlayerPrefs.SetInt("ToggleSteer", v ? 1 : 0);
            OnToggleSteerChanged?.Invoke(v);
        }

        // Cognitive setters
        public void SetSimplifiedHUD(bool v)
        {
            SimplifiedHUD = v;
            PlayerPrefs.SetInt("SimplifiedHUD", v ? 1 : 0);
            OnSimplifiedHUDChanged?.Invoke(v);
        }

        public void SetTutorialHints(bool v)
        {
            TutorialHints = v;
            PlayerPrefs.SetInt("TutorialHints", v ? 1 : 0);
            OnTutorialHintsChanged?.Invoke(v);
        }

        public void SetReduceClutter(bool v)
        {
            ReduceClutter = v;
            PlayerPrefs.SetInt("ReduceClutter", v ? 1 : 0);
            OnReduceClutterChanged?.Invoke(v);
        }

        // Private apply helpers
        void ApplyBrightness(float v)
        {
            // Brightness is a 0-1 linear multiplier (1 = neutral, lower = darker)
            v = Mathf.Clamp(v, 0.05f, 1f);

            Screen.brightness = v;

            if (!EnsureColorVolume()) return;
            // 1.0 = neutral (0 EV), 0.5 ≈ -1 EV.
            _colorAdjust.postExposure.overrideState = true;
            _colorAdjust.postExposure.value = Mathf.Log(v, 2f);
        }
        void ApplyColourBlindPostProcess(ColourBlindFilter.ColourBlindMode mode)
        {
            if (!EnsureColorVolume()) return;
            _colorAdjust.saturation.overrideState = true;
            _colorAdjust.saturation.value =
                mode == ColourBlindFilter.ColourBlindMode.Achromatopsia ? -100f : 0f;
        }

        bool EnsureColorVolume()
        {
            if (_colorAdjust != null) return true;
            if (_colorVolume == null)
            {
                var go = new GameObject("[Accessibility_ColorOverride]") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                _colorVolume          = go.AddComponent<Volume>();
                _colorVolume.isGlobal = true;
                _colorVolume.priority = 100f;
                _colorVolume.profile  = ScriptableObject.CreateInstance<VolumeProfile>();
            }
            if (!_colorVolume.profile.TryGet(out _colorAdjust))
                _colorAdjust = _colorVolume.profile.Add<ColorAdjustments>(true);
            return _colorAdjust != null;
        }

        void OnDestroy()
        {
            if (_colorVolume != null)
            {
                if (_colorVolume.profile != null) Destroy(_colorVolume.profile);
                Destroy(_colorVolume.gameObject);
            }
        }

        static void ApplyMonoAudio(bool mono)
        {
            var desired = mono ? AudioSpeakerMode.Mono : AudioSpeakerMode.Stereo;
            var config  = AudioSettings.GetConfiguration();
            if (config.speakerMode == desired) return; // Already correct skip the expensive reset
            config.speakerMode = desired;
            AudioSettings.Reset(config);
        }
    }
}
