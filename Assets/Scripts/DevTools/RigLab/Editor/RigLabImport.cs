using MotoSquid.Combat;
using MotoSquid.Rider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.DevTools
{
    public static class RigLabImport
    {
        const string In = "Assets/MotoSquid/Data/RigLab/Exports";
        const string RigLabRoot = "Assets/MotoSquid/Data/RigLab";
        const string PoseFolder = RigLabRoot + "/FullPoses";
        const string DataFolder = RigLabRoot + "/Data";
        const string TuningAsset = RigLabRoot + "/CombatTuning.asset";
        const string GripsAsset = RigLabRoot + "/WeaponGrips.asset";

        [Serializable] class Grip { public string model; public Vector3 position, euler; public float scale; }
        [Serializable] class Grips { public List<Grip> grips = new List<Grip>(); }

        [MenuItem("Tools/Rig Lab/30. Import From Sandbox (JSON)", false, 300)]
        static void Import()
        {
            if (!Directory.Exists(In))
            {
                Debug.LogError("Rig Lab: no export folder at " + In +
                               "\nCopy the sandbox's Assets/RigLab/Export there first.");
                return;
            }

            Directory.CreateDirectory(PoseFolder);
            Directory.CreateDirectory(DataFolder);

            var report = new System.Text.StringBuilder("Rig Lab: imported from " + In + "\n");
            var failures = new List<string>();

            int poses = ImportPoses(failures);
            report.Append("   poses    ").Append(poses).Append('\n');

            bool tuning = ImportTuning(failures);
            report.Append("   tuning   ").Append(tuning ? "yes" : "MISSING").Append('\n');

            int weapons = ImportGrips(failures);
            report.Append("   weapons  ").Append(weapons).Append('\n');

            int data = ImportData(failures);
            report.Append("   data     ").Append(data).Append(" of 2 files\n");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (poses == 0)
            {
                Debug.LogError("Rig Lab: import wrote NO poses. " + In + "/Poses is empty or unreadable, " +
                               "nothing was imported and the combat driver has nothing to play.\n" +
                               string.Join("\n", failures));
                return;
            }

            if (failures.Count > 0)
            {
                Debug.LogError(report + "\nbut " + failures.Count + " item(s) failed:\n   " +
                               string.Join("\n   ", failures));
                return;
            }

            Debug.Log(report + "\nNext: 13. Build Combat Rig Layer, then 0. Check Everything.");
        }

        static int ImportPoses(List<string> failures)
        {
            var files = Directory.GetFiles(In + "/Poses", "*.json");
            int n = 0;
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var path = PoseFolder + "/" + name + ".asset";
                try
                {
                    var pose = AssetDatabase.LoadAssetAtPath<FullPose>(path);
                    bool isNew = pose == null;
                    if (isNew) pose = ScriptableObject.CreateInstance<FullPose>();

                    JsonUtility.FromJsonOverwrite(File.ReadAllText(file), pose);

                    if (pose.bonePaths.Length == 0 || pose.frameCount == 0)
                    {
                        failures.Add(name + ": parsed to 0 bones or 0 frames");
                        if (isNew) UnityEngine.Object.DestroyImmediate(pose);
                        continue;
                    }

                    int expected = pose.frameCount * pose.bonePaths.Length;
                    if (pose.rotations.Length != expected)
                    {
                        failures.Add(name + ": " + pose.rotations.Length + " rotations, expected " + expected);
                        if (isNew) UnityEngine.Object.DestroyImmediate(pose);
                        continue;
                    }

                    if (isNew) AssetDatabase.CreateAsset(pose, path);
                    else EditorUtility.SetDirty(pose);
                    n++;
                }
                catch (Exception e)
                {
                    failures.Add(name + ": " + e.Message);
                }
            }
            return n;
        }

        static bool ImportTuning(List<string> failures)
        {
            var file = In + "/CombatTuning.json";
            if (!File.Exists(file)) { failures.Add("CombatTuning.json not found"); return false; }
            try
            {
                var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningAsset);
                bool isNew = tuning == null;
                if (isNew) tuning = ScriptableObject.CreateInstance<CombatTuning>();

                JsonUtility.FromJsonOverwrite(File.ReadAllText(file), tuning);

                if (isNew) AssetDatabase.CreateAsset(tuning, TuningAsset);
                else EditorUtility.SetDirty(tuning);
                return true;
            }
            catch (Exception e) { failures.Add("CombatTuning: " + e.Message); return false; }
        }

        static int ImportGrips(List<string> failures)
        {
            var file = In + "/WeaponGrips.json";
            if (!File.Exists(file)) { failures.Add("WeaponGrips.json not found"); return 0; }

            Grips src;
            try { src = JsonUtility.FromJson<Grips>(File.ReadAllText(file)); }
            catch (Exception e) { failures.Add("WeaponGrips: " + e.Message); return 0; }
            if (src == null || src.grips == null) { failures.Add("WeaponGrips: parsed to nothing"); return 0; }

            var asset = AssetDatabase.LoadAssetAtPath<WeaponGrips>(GripsAsset);
            bool isNew = asset == null;
            if (isNew) asset = ScriptableObject.CreateInstance<WeaponGrips>();

            var resolved = new List<WeaponGrips.Grip>();
            foreach (var g in src.grips)
            {
                if (g == null || string.IsNullOrEmpty(g.model)) continue;
                var model = FindModel(g.model);
                if (model == null)
                {
                    failures.Add("weapon model '" + g.model + "' not found in this project");
                    continue;
                }
                resolved.Add(new WeaponGrips.Grip
                {
                    model = model, position = g.position, euler = g.euler, scale = g.scale
                });
            }

            asset.grips = resolved.ToArray();
            if (isNew) AssetDatabase.CreateAsset(asset, GripsAsset);
            else EditorUtility.SetDirty(asset);
            return resolved.Count;
        }

        static GameObject FindModel(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject " + name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != name) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) return go;
            }
            return null;
        }

        static int ImportData(List<string> failures)
        {
            int n = 0;
            foreach (var f in new[] { "RiderFitSettings.json", "RiderFitBaseline.json" })
            {
                var from = In + "/" + f;
                if (!File.Exists(from)) { failures.Add(f + " not found"); continue; }
                try { File.Copy(from, DataFolder + "/" + f, true); n++; }
                catch (Exception e) { failures.Add(f + ": " + e.Message); }
            }
            return n;
        }
    }
}
