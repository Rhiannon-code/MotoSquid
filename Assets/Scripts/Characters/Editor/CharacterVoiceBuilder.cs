using MotoSquid.Audio;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Characters
{
    public static class CharacterVoiceBuilder
    {
        const string VoicesRoot      = "Assets/Audio/Voices";
        const string DefinitionsRoot = "Assets/MotoSquid/Data/Characters";

        static readonly (string folder, string field)[] Mapping =
        {
            ("Start of Race Taunts",   "startTaunt"),
            ("Attacking",              "attacking"),
            ("Attacking - Foley",      "swingGrunt"),
            ("Being Attacked",         "beingAttacked"),
            ("Being Attacked - Foley", "beingAttacked"),
            ("Overtaking",             "overtaking"),
            ("Being Overtaken",        "beingOvertaken"),
            ("Crash Foley",            "crash"),
            ("Loses Race",             "loseRace"),
            ("Wins Race",              "winRace"),
        };

        static readonly string[] Fields = Mapping.Select(m => m.field).Distinct().ToArray();

        [MenuItem("MotoSquid/Audio/Build Character Voices")]
        static void Build()
        {
            var defs = LoadDefinitions();
            if (defs.Count == 0)
            {
                Debug.LogError($"[Voices] No CharacterDefinition assets under {DefinitionsRoot}.");
                return;
            }

            var buckets = defs.ToDictionary(d => d, _ => Fields.ToDictionary(f => f, _ => new List<AudioClip>()));
            var unattributed = new List<string>();
            int seen = 0, madeReadable = 0;

            foreach (var (folder, field) in Mapping)
            {
                string path = $"{VoicesRoot}/{folder}";
                if (!AssetDatabase.IsValidFolder(path))
                {
                    Debug.LogWarning($"[Voices] Folder missing: {path}");
                    continue;
                }

                var clipPaths = AssetDatabase.FindAssets("t:AudioClip", new[] { path })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == path)
                    .OrderBy(p => p, System.StringComparer.Ordinal)
                    .ToList();

                // Freshly imported takes come in as Compressed In Memory, which the game cannot read its
                // lead-in from, so they would play their head silence. Set here rather than by hand per file
                foreach (var clipPath in clipPaths)
                    if (AssetImporter.GetAtPath(clipPath) is AudioImporter importer &&
                        importer.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad)
                    {
                        var settings = importer.defaultSampleSettings;
                        settings.loadType = AudioClipLoadType.DecompressOnLoad;
                        importer.defaultSampleSettings = settings;
                        importer.SaveAndReimport();
                        madeReadable++;
                    }

                var clips = clipPaths.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null);

                foreach (var clip in clips)
                {
                    seen++;
                    var owner = defs.FirstOrDefault(d => clip.name.StartsWith(d.characterName, System.StringComparison.OrdinalIgnoreCase));
                    if (owner == null) unattributed.Add($"{folder}/{clip.name}");
                    else buckets[owner][field].Add(clip);
                }
            }

            if (seen == 0)
            {
                Debug.LogError($"[Voices] No AudioClips found under {VoicesRoot}. The .wav files are on disk but not imported yet, focus the editor and let it import, then re-run.");
                return;
            }

            var report = new StringBuilder($"[Voices] {seen} clips across {defs.Count} characters\n");
            var gaps = new List<string>();
            var unreadable = new List<string>();

            foreach (var def in defs)
            {
                var voice = LoadOrCreate(def.characterName);
                var so = new SerializedObject(voice);
                so.FindProperty("characterName").stringValue = def.characterName;

                foreach (var field in Fields)
                {
                    var clips = buckets[def][field];
                    var prop = so.FindProperty(field);
                    prop.arraySize = clips.Count;
                    for (int i = 0; i < clips.Count; i++)
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                    if (clips.Count == 0) gaps.Add($"{def.characterName}.{field}");
                }
                // The game measures lead-in itself; this only flags clips it will not be able to read
                foreach (var clip in Fields.SelectMany(f => buckets[def][f]).Distinct())
                    CharacterVoice.MeasureLeadIn(clip, unreadable);

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(voice);

                var dso = new SerializedObject(def);
                dso.FindProperty("voice").objectReferenceValue = voice;
                dso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(def);

                report.AppendLine($"  {def.characterName,-8} {string.Join("  ", Fields.Select(f => $"{f}:{buckets[def][f].Count}"))}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (madeReadable > 0) report.AppendLine($"  Set {madeReadable} clip(s) to Decompress On Load.");
            Debug.Log(report.ToString());
            if (gaps.Count > 0)
                Debug.LogWarning($"[Voices] {gaps.Count} empty categories, these stay silent:\n  {string.Join("\n  ", gaps)}");
            if (unattributed.Count > 0)
                Debug.LogWarning($"[Voices] {unattributed.Count} clips matched no character and were skipped:\n  {string.Join("\n  ", unattributed)}");
            if (unreadable.Count > 0)
                Debug.LogWarning($"[Voices] {unreadable.Count} clips are not Decompress On Load, so their lead-in " +
                                 $"could not be measured and they will play their head silence:\n  {string.Join("\n  ", unreadable)}");
        }

        // Longest name first so one character's name being a prefix of another's cannot steal its clips
        static List<CharacterDefinition> LoadDefinitions() =>
            AssetDatabase.FindAssets("t:CharacterDefinition", new[] { DefinitionsRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterDefinition>)
                .Where(d => d != null && !string.IsNullOrWhiteSpace(d.characterName))
                .OrderByDescending(d => d.characterName.Length)
                .ToList();

        static CharacterVoice LoadOrCreate(string characterName)
        {
            string path = $"{DefinitionsRoot}/CharacterVoice_{characterName.Replace(' ', '_')}.asset";
            var voice = AssetDatabase.LoadAssetAtPath<CharacterVoice>(path);
            if (voice == null)
            {
                voice = ScriptableObject.CreateInstance<CharacterVoice>();
                AssetDatabase.CreateAsset(voice, path);
            }
            return voice;
        }
    }
}
