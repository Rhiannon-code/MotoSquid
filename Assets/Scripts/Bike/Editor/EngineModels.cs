using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Bike
{
    public class EngineModels : EditorWindow
    {
        const string AudioRoot  = "Assets/Audio";
        const string EngineDir  = "Assets/Prefabs/Engines";
        const string OnClip        = "low_on.wav";
        const string OffClip       = "low_off.wav";
        const string HelmetOnClip  = "Helmet/int_low_on.wav";
        const string HelmetOffClip = "Helmet/int_low_off.wav";

        List<GameObject> _models;
        string[]         _modelNames;
        int[]            _choice;    
        Vector2          _scroll;

        [MenuItem("MotoSquid/Audio/Engine Models")]
        static void Open()
        {
            var w = GetWindow<EngineModels>("Engine Models");
            w.minSize = new Vector2(430, 320);
            w.Refresh();
        }

        // Generate
        [MenuItem("MotoSquid/Audio/Generate Engine Model Prefabs")]
        static void GenerateAll()
        {
            if (!AssetDatabase.IsValidFolder(EngineDir))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(EngineDir).Replace('\\', '/'), Path.GetFileName(EngineDir));

            int made = 0, refreshed = 0;
            var log = new StringBuilder();

            foreach (var folder in ModelFolders())
            {
                string model = Path.GetFileName(folder);
                string path  = $"{EngineDir}/Engine_{model}.prefab";

                var on     = Clip(folder, OnClip);
                var off    = Clip(folder, OffClip);
                var onInt  = Clip(folder, HelmetOnClip);
                var offInt = Clip(folder, HelmetOffClip);

                if (on == null || off == null)
                {
                    log.AppendLine($"x {model}: no {OnClip}/{OffClip}");
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                {
                    var contents = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var eng = contents.GetComponent<EngineAudio>() ?? contents.AddComponent<EngineAudio>();
                        eng.onClip = on; eng.offClip = off; eng.onHelmetClip = onInt; eng.offHelmetClip = offInt;
                        EditorUtility.SetDirty(eng);
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                    refreshed++;
                    log.AppendLine($"~ {model} (clips refreshed, tuning kept)");
                }
                else
                {
                    var go  = new GameObject($"Engine_{model}");
                    var eng = go.AddComponent<EngineAudio>();
                    eng.onClip = on; eng.offClip = off; eng.onHelmetClip = onInt; eng.offHelmetClip = offInt;
                    PrefabUtility.SaveAsPrefabAsset(go, path);
                    Object.DestroyImmediate(go);
                    made++;
                    log.AppendLine($"+ {model}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Engine model prefabs",
                $"Created {made}, refreshed {refreshed}.\n\nIn {EngineDir}\n\n{log}", "OK");
        }

        static IEnumerable<string> ModelFolders()
        {
            if (!Directory.Exists(AudioRoot)) yield break;
            foreach (var d in Directory.GetDirectories(AudioRoot).OrderBy(d => d))
                if (File.Exists(Path.Combine(d, OnClip)))
                    yield return d.Replace('\\', '/');
        }

        static AudioClip Clip(string folder, string rel) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/{rel}");

        static bool Apply(string bikeRel, GameObject modelPrefab)
        {
            var model = modelPrefab != null ? modelPrefab.GetComponent<EngineAudio>() : null;
            if (model == null) return false;

            string path = BikeRoster.PathOf(bikeRel);
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return false;

            try
            {
                var eng = root.GetComponentInChildren<EngineAudio>(true);
                if (eng == null) return false; 

                eng.onClip = model.onClip; eng.offClip = model.offClip;
                eng.onHelmetClip = model.onHelmetClip; eng.offHelmetClip = model.offHelmetClip;

                eng.idleRpm01 = model.idleRpm01; eng.revCeiling01 = model.revCeiling01;
                eng.burnoutRpm01 = model.burnoutRpm01;
                eng.rpmRise = model.rpmRise; eng.rpmFall = model.rpmFall;
                eng.loadSmooth = model.loadSmooth; eng.engineBrakeRpm = model.engineBrakeRpm;
                eng.minPitch = model.minPitch; eng.maxPitch = model.maxPitch;
                eng.masterVolume = model.masterVolume; eng.spatialBlend = model.spatialBlend;
                eng.minDistance = model.minDistance; eng.maxDistance = model.maxDistance;

                EditorUtility.SetDirty(eng);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        int CurrentModelIndex(string bikeRel)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(BikeRoster.PathOf(bikeRel));
            var eng  = root != null ? root.GetComponentInChildren<EngineAudio>(true) : null;
            if (eng == null || eng.onClip == null) return -1;
            for (int i = 0; i < _models.Count; i++)
            {
                var m = _models[i].GetComponent<EngineAudio>();
                if (m != null && m.onClip == eng.onClip) return i;
            }
            return -1;
        }

        void Refresh()
        {
            _models = AssetDatabase.FindAssets("t:Prefab", new[] { EngineDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(g => g != null && g.GetComponent<EngineAudio>() != null)
                .OrderBy(g => g.name)
                .ToList();

            _modelNames = _models.Select(m => m.name.StartsWith("Engine_") ? m.name.Substring(7) : m.name).ToArray();

            _choice = new int[BikeRoster.All.Length];
            for (int i = 0; i < _choice.Length; i++)
            {
                int cur = CurrentModelIndex(BikeRoster.All[i].rel);
                _choice[i] = cur >= 0 ? cur : 0;
            }
            Repaint();
        }

        void OnGUI()
        {
            if (_models == null) Refresh();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64))) Refresh();
                if (GUILayout.Button("Generate model prefabs", EditorStyles.toolbarButton, GUILayout.Width(160))) { GenerateAll(); Refresh(); }
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_models.Count} model(s)", EditorStyles.miniLabel);
            }

            if (_models.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No engine model prefabs in {EngineDir}.\n\n" +
                    "Run 'Generate model prefabs' to create one per audio model folder.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Assigning copies the model's clips and tuning onto the bike's EngineAudio. The mixer " +
                "group and local player flag are left alone, those belong to Wire All Bike Audio.\n\n" +
                "Built prefabs (Voodoo_R_*) lose this on the next character rebuild, re-apply after.",
                MessageType.None);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < BikeRoster.All.Length; i++)
            {
                var (rel, isPlayer) = BikeRoster.All[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    int cur = CurrentModelIndex(rel);
                    GUI.color = cur >= 0 ? Color.white : new Color(1f, 0.85f, 0.4f);
                    EditorGUILayout.LabelField($"{BikeRoster.NameOf(rel)}{(isPlayer ? "  (player)" : "")}",
                                               GUILayout.Width(190));
                    GUI.color = Color.white;

                    _choice[i] = EditorGUILayout.Popup(_choice[i], _modelNames);

                    if (GUILayout.Button("Apply", GUILayout.Width(56)))
                    {
                        if (!Apply(rel, _models[_choice[i]]))
                            EditorUtility.DisplayDialog("Engine Models",
                                $"{BikeRoster.NameOf(rel)} has no EngineAudio component.\n\n" +
                                "Run MotoSquid > Wire All Bike Audio first, then assign a model.", "OK");
                        AssetDatabase.SaveAssets();
                        Refresh();
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Apply all"))
            {
                var failed = new List<string>();
                for (int i = 0; i < BikeRoster.All.Length; i++)
                    if (!Apply(BikeRoster.All[i].rel, _models[_choice[i]]))
                        failed.Add(BikeRoster.NameOf(BikeRoster.All[i].rel));

                AssetDatabase.SaveAssets();
                Refresh();
                EditorUtility.DisplayDialog("Engine Models",
                    failed.Count == 0
                        ? "Applied to every bike in the roster."
                        : "Applied, except these (no EngineAudio, run Wire All Bike Audio first):\n\n" +
                          string.Join("\n", failed), "OK");
            }
        }
    }
}
