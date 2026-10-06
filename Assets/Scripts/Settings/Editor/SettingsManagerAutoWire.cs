using MotoSquid.Rider;
using System.Collections.Generic;
using Michsky.UI.Heat;
using HeatDropdown = Michsky.UI.Heat.Dropdown;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MotoSquid.Settings
{
    public static class SettingsManagerAutoWire
    {
        enum CompType { Switch, Selector, Slider, Dropdown, HeatDropdown }

        static readonly Dictionary<string, (string field, CompType type)> NameMap =
            new Dictionary<string, (string, CompType)>
        {
            // Audio
            { "Master Volume",      ("masterVolumeSlider",      CompType.Slider)   },
            { "Music Volume",       ("musicVolumeSlider",       CompType.Slider)   },
            { "SFX Volume",         ("sfxVolumeSlider",         CompType.Slider)   },
            { "UI Volume",          ("uiVolumeSlider",          CompType.Slider)   },
            { "Enable Subtitles",   ("subtitlesSwitch",         CompType.Switch)   },
            { "Subtitle Scale",     ("subtitleSizeSelector",    CompType.Selector) },
            { "SubtitleOpacity",    ("subtitleBgOpacitySlider", CompType.Slider)   },

            // Display
            { "Resolution",         ("resolutionDropdown",      CompType.HeatDropdown) },
            { "Monitor",            ("monitorDropdown",         CompType.HeatDropdown) },
            { "Window Mode",        ("windowModeSelector",      CompType.Selector) },
            { "Frame Rate",         ("frameRateSelector",       CompType.Selector) },
            { "RenderScale",        ("renderScaleSelector",     CompType.Selector) },
            { "VSync",              ("vSyncSwitch",             CompType.Switch)   },
            { "Brightness",         ("brightnessSlider",        CompType.Slider)   },
            { "UI Scale",           ("uiScaleSelector",         CompType.Selector) },

            // Visuals/Graphics
            { "Quality Preset",     ("qualityPresetSelector",   CompType.Selector) },
            { "Texture Quality",    ("textureQualitySelector",  CompType.Selector) },
            { "Shadow Quality",     ("shadowQualitySelector",   CompType.Selector) },
            { "Anti Aliasing",      ("antiAliasingSelector",    CompType.Selector) },
            { "Post Processing",    ("postProcessingSelector",  CompType.Selector) },
            { "Particle Effects",   ("particleEffectsSelector", CompType.Selector) },
            { "Motion Blur",        ("motionBlurSelector",      CompType.Selector) },
            { "CameraFOV",          ("cameraFOVSelector",       CompType.Selector) },

            // Controls
            { "Camera Sensitivity", ("cameraSensitivitySlider", CompType.Slider)   },

            // Accessibility
            { "ColurBlindMode",     ("colourBlindModeSwitch",   CompType.Switch)   },
            { "HighContrast",       ("highContrastSwitch",      CompType.Switch)   },
            { "EpilepsySafeMode",   ("epiSafeModeSwitch",       CompType.Switch)   },
            { "ScreenShake",        ("screenShakeSelector",     CompType.Selector) },
            { "VisualAudioCues",    ("visualAudioCuesSwitch",   CompType.Switch)   },
            { "MonoAudio",          ("monoAudioSwitch",         CompType.Switch)   },
            { "HelmetAudio",        ("helmetAudioSwitch",       CompType.Switch)   },
            { "Rumble",             ("rumbleSwitch",            CompType.Switch)   },
            { "SteeringAssist",     ("steeringAssistSwitch",    CompType.Switch)   },
            { "AutoBrake",          ("autoBrakeSwitch",         CompType.Switch)   },
            { "ToggleSteer",        ("toggleSteerSwitch",       CompType.Switch)   },
            { "SimplifiedHUD",      ("simplifiedHUDSwitch",     CompType.Switch)   },
            { "TutorialHints",      ("tutorialHintsSwitch",     CompType.Switch)   },
            { "ReduceClutter",      ("reduceClutterSwitch",     CompType.Switch)   },

            // Gameplay/Difficulty
            { "Screen Layout",      ("splitScreenLayoutSwitch",  CompType.Switch)   },
            { "Difficulty Preset",  ("difficultyPresetSelector", CompType.Selector) },
            { "AI Difficulty",      ("aiDifficultySelector",     CompType.Selector) },
            { "Traffic Density",    ("trafficDensitySelector",   CompType.Selector) },
        };

        [MenuItem("MotoSquid/Auto Wire Settings Manager")]
        static void AutoWire()
        {
            var sm = Object.FindAnyObjectByType<SettingsManager>(FindObjectsInactive.Include);
            if (sm == null)
            {
                EditorUtility.DisplayDialog("Auto Wire", "No SettingsManager found in the open scene.", "OK");
                return;
            }

            var so      = new SerializedObject(sm);
            int wired   = 0;
            int missing = 0;

            foreach (var kv in NameMap)
            {
                string   goName    = kv.Key;
                string   fieldName = kv.Value.field;
                CompType compType  = kv.Value.type;

                var t = FindExact(goName);
                if (t == null)
                {
                    Debug.LogWarning($"[Auto Wire] No GameObject named \"{goName}\", skipping field '{fieldName}'.");
                    missing++;
                    continue;
                }

                switch (compType)
                {
                    case CompType.Switch:
                        var sw = t.GetComponentInChildren<SwitchManager>(true);
                        if (sw != null) wired += Wire(so, fieldName, sw);
                        else { Debug.LogWarning($"[AutoWire] No SwitchManager in \"{goName}\" for field '{fieldName}'."); missing++; }
                        break;

                    case CompType.Selector:
                        var sel = t.GetComponentInChildren<HorizontalSelector>(true);
                        if (sel != null) wired += Wire(so, fieldName, sel);
                        else { Debug.LogWarning($"[Auto Wire] No HorizontalSelector in \"{goName}\" for field '{fieldName}'."); missing++; }
                        break;

                    case CompType.Slider:
                        var slider = t.GetComponentInChildren<Slider>(true);
                        if (slider != null) wired += Wire(so, fieldName, slider);
                        else { Debug.LogWarning($"[Auto Wire] No Slider in \"{goName}\" for field '{fieldName}'."); missing++; }
                        break;

                    case CompType.Dropdown:
                        var dd = t.GetComponentInChildren<TMP_Dropdown>(true);
                        if (dd != null) wired += Wire(so, fieldName, dd);
                        else { Debug.LogWarning($"[Auto Wire] No TMP_Dropdown in \"{goName}\" for field '{fieldName}'."); missing++; }
                        break;

                    case CompType.HeatDropdown:
                        var hdd = t.GetComponentInChildren<HeatDropdown>(true);
                        if (hdd != null) wired += Wire(so, fieldName, hdd);
                        else { Debug.LogWarning($"[Auto Wire] No Heat Dropdown in \"{goName}\" for field '{fieldName}'."); missing++; }
                        break;
                }
            }

            var cam = Camera.main;
            if (cam != null)
            {
                wired += Wire(so, "mainCamera", cam);
                Debug.Log("[Auto Wire] Wired: mainCamera");
            }
            else { Debug.LogWarning("[Auto Wire] Could not find Main Camera."); missing++; }

            var cbf = Object.FindAnyObjectByType<ColourBlindFilter>(FindObjectsInactive.Include);
            if (cbf != null)
            {
                wired += Wire(so, "colourBlindFilter", cbf);
                Debug.Log("[Auto Wire] Wired: colourBlindFilter");
            }
            else { Debug.LogWarning("[Auto Wire] Could not find ColourBlindFilter."); missing++; }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(sm);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Auto Wire Complete",
                $"Wired {wired} fields.\n{missing} not found, check Console for details.",
                "OK");
        }

        static int WireButton(SerializedObject so, string field, string goName, ref int missing)
        {
            var t = FindExact(goName);
            if (t == null) { Warn(field, goName, ref missing); return 0; }
            var btn = t.GetComponent<Button>() ?? t.GetComponentInChildren<Button>(true);
            if (btn == null) { Warn(field, goName + " (Button)", ref missing); return 0; }
            return Wire(so, field, btn);
        }

        static int WireGameObject(SerializedObject so, string field, string goName, ref int missing)
        {
            var t = FindExact(goName);
            if (t == null) { Warn(field, goName, ref missing); return 0; }
            return Wire(so, field, t.gameObject);
        }

        static int WireTMPText(SerializedObject so, string field, string goName, ref int missing)
        {
            var t = FindExact(goName);
            if (t == null) { Warn(field, goName, ref missing); return 0; }
            var text = t.GetComponent<TMP_Text>() ?? t.GetComponentInChildren<TMP_Text>(true);
            if (text == null) { Warn(field, goName + " (TMP_Text)", ref missing); return 0; }
            return Wire(so, field, text);
        }

        static int Wire(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[Auto Wire] Field '{fieldName}' not found on SettingsManager.");
                return 0;
            }
            prop.objectReferenceValue = value;
            Debug.Log($"[Auto Wire] Wired: {fieldName} → {value.name}");
            return 1;
        }

        static Transform FindExact(string name)
        {
            string lower = name.ToLower();
            Transform exact = null;
            Transform caseInsensitive = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.IsValid()) continue;
                if (go.name == name)       { exact = go.transform; break; }
                if (caseInsensitive == null && go.name.ToLower() == lower)
                    caseInsensitive = go.transform;
            }
            return exact ?? caseInsensitive;
        }

        static void Warn(string field, string name, ref int missing)
        {
            Debug.LogWarning($"[AutoWire] Could not wire '{field}', no GameObject named \"{name}\" found.");
            missing++;
        }
    }
}
