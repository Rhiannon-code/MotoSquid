using MotoSquid.Characters;
using MotoSquid.Combat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.DevTools
{
    public static class RigLabWeaponAssets
    {
        const string GripsJson = "Assets/MotoSquid/Data/RigLab/Exports/WeaponGrips.json";
        const string OutDir = "Assets/MotoSquid/Data/Combat/Weapons";
        const string DefDir = "Assets/MotoSquid/Data/Characters";

        [Serializable] class Grip { public string model; public Vector3 position, euler; public float scale; }
        [Serializable] class Grips { public List<Grip> grips = new List<Grip>(); }

        [MenuItem("Tools/Rig Lab/32. Create Weapon Assets From Grips", false, 320)]
        static void Create()
        {
            if (!File.Exists(GripsJson))
            {
                Debug.LogError("Rig Lab: " + GripsJson + " not found. Run the import (30) first.");
                return;
            }

            var grips = JsonUtility.FromJson<Grips>(File.ReadAllText(GripsJson));
            if (grips == null || grips.grips.Count == 0)
            {
                Debug.LogError("Rig Lab: " + GripsJson + " parsed to no grips.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder(DefDir, "Weapons");

            var defs = AssetDatabase.FindAssets("t:CharacterDefinition", new[] { DefDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null)
                .ToList();

            int made = 0, wired = 0;
            var unmatched = new List<string>();

            foreach (var grip in grips.grips)
            {
                if (grip == null || string.IsNullOrEmpty(grip.model)) continue;

                var prop = FindModel(grip.model);
                if (prop == null) { unmatched.Add(grip.model + " (model not in project)"); continue; }

                var def = defs.FirstOrDefault(d => !string.IsNullOrEmpty(d.characterName)
                                                && grip.model.IndexOf(d.characterName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (def == null) { unmatched.Add(grip.model + " (no CharacterDefinition matches its name)"); continue; }

                var path = OutDir + "/MeleeWeapon_" + def.characterName + ".asset";
                var weapon = AssetDatabase.LoadAssetAtPath<MeleeWeapon>(path);
                bool isNew = weapon == null;
                if (isNew) weapon = ScriptableObject.CreateInstance<MeleeWeapon>();

                weapon.displayName = Readable(grip.model, def.characterName);
                weapon.prop = prop;
                weapon.localPosition = grip.position;
                weapon.localEuler = grip.euler;
                weapon.localScale = Vector3.one * (grip.scale <= 0f ? 1f : grip.scale);

                if (isNew) AssetDatabase.CreateAsset(weapon, path);
                else EditorUtility.SetDirty(weapon);
                made++;

                if (def.defaultWeapon != weapon)
                {
                    Undo.RecordObject(def, "Assign weapon");
                    def.defaultWeapon = weapon;
                    EditorUtility.SetDirty(def);
                    wired++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var msg = "Rig Lab: weapons - " + made + " asset(s) written to " + OutDir +
                      ", " + wired + " definition(s) wired.";
            if (unmatched.Count > 0) Debug.LogError(msg + "\n   unmatched:\n     " + string.Join("\n     ", unmatched));
            else Debug.Log(msg + "\nCheck each one in the hand and nudge localPosition/localEuler if it sits wrong.");
        }

        static string Readable(string model, string character)
        {
            var s = model;
            if (s.StartsWith("Weapon", StringComparison.OrdinalIgnoreCase)) s = s.Substring(6);
            var i = s.IndexOf(character, StringComparison.OrdinalIgnoreCase);
            if (i >= 0) s = s.Remove(i, character.Length);
            s = s.Trim('_', ' ');
            return string.IsNullOrEmpty(s) ? model : s;
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
    }
}
