using MotoSquid.Bike;
using MotoSquid.Combat;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabRacerMatrix
    {
        const string OutDir = "Assets/MotoSquid/Resources/Racers";

        [MenuItem("Tools/Rig Lab/50. Build Racer Matrix (from roster)", false, 500)]
        static void Build()
        {
            var roster = AssetDatabase.FindAssets("t:RacerRoster")
                .Select(g => AssetDatabase.LoadAssetAtPath<RacerRoster>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(r => r != null);
            if (roster == null)
            {
                Debug.LogError("Rig Lab: no RacerRoster asset. Create > Rig Lab > Racer Roster, " +
                               "then fill in the characters and bikes you have.");
                return;
            }

            EnsureFolder(OutDir);

            var made = new List<string>();
            var kept = new List<string>();
            var waiting = new List<string>();

            foreach (var c in roster.characters)
            {
                foreach (var b in roster.bikes)
                {
                    if (c == null || b == null) continue;
                    if (!c.Ready || !b.Ready)
                    {
                        waiting.Add(Pair(c, b) + " , " + (!c.Ready ? "no rider model" : "no bike prefab"));
                        continue;
                    }

                    foreach (var ai in new[] { false, true })
                    {
                        var name = RacerRoster.PrefabName(c, b, ai);
                        var path = OutDir + "/" + name + ".prefab";

                        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) { kept.Add(name); continue; }
                        if (BuildOne(c, b, ai, path)) made.Add(name);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var sb = new System.Text.StringBuilder("Rig Lab: racer matrix in " + OutDir + "\n");
            sb.Append("   built    ").Append(made.Count).Append('\n');
            sb.Append("   kept     ").Append(kept.Count).Append("  (already existed, tuning left alone)\n");
            sb.Append("   waiting  ").Append(waiting.Count).Append("  (art not in yet)\n");
            if (waiting.Count > 0) sb.Append("\n   ").Append(string.Join("\n   ", waiting));
            Debug.Log(sb.ToString());
        }

        static void EnsureFolder(string dir)
        {
            var parts = dir.Split('/');
            var path = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = path + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
        }

        static string Pair(RacerRoster.Character c, RacerRoster.Bike b) =>
            (c != null ? c.displayName : "?") + " on " + (b != null ? b.displayName : "?");

        /// Assembled in the open scene: the rig builder works on live objects, not on prefab contents.
        static bool BuildOne(RacerRoster.Character c, RacerRoster.Bike b, bool ai, string path)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(b.bikePrefab);
            if (root == null) { Debug.LogError("Rig Lab: could not instantiate " + b.displayName); return false; }
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            try
            {
                var bike = root.GetComponent<BikeController>();
                if (bike == null) { Debug.LogError("Rig Lab: " + b.displayName + " has no BikeController."); return false; }

                if (!RigLab.SwapRiderOn(bike, c.riderModel, false))
                {
                    Debug.LogError("Rig Lab: rider swap failed for " + Pair(c, b));
                    return false;
                }

                Tint(root, c.tint);

                var holder = root.GetComponentInChildren<MeleeWeaponHolder>(true);
                if (holder != null && c.weapon != null)
                {
                    var so = new SerializedObject(holder);
                    var p = so.FindProperty("startingWeapon");
                    if (p != null) { p.objectReferenceValue = c.weapon; so.ApplyModifiedPropertiesWithoutUndo(); }
                }

                if (ai)
                {
                    var gaps = new List<string>();
                    if (!RigLabMakeAI.Convert(root, gaps))
                    {
                        Debug.LogError("Rig Lab: AI conversion failed for " + Pair(c, b) + "\n   " +
                                       string.Join("\n   ", gaps));
                        return false;
                    }
                    if (gaps.Count > 0)
                        Debug.LogWarning("Rig Lab: " + Pair(c, b) + " (AI) needs attention\n   " +
                                         string.Join("\n   ", gaps.Distinct()));
                }

                root.name = System.IO.Path.GetFileNameWithoutExtension(path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("Rig Lab: " + Pair(c, b) + (ai ? " (AI)" : " (Player)") + " -> " + path +
                          "\n   the combat rig still needs 13, and the rig wants tuning by eye.");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        static void Tint(GameObject root, Color tint)
        {
            if (tint == Color.white) return;
            var rider = root.GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(a => a.avatar != null && a.avatar.isHuman);
            if (rider == null) return;

            foreach (var r in rider.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var copy = new Material(mats[i]) { name = mats[i].name + "_" + ColorUtility.ToHtmlStringRGB(tint) };
                    if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", copy.GetColor("_BaseColor") * tint);
                    else if (copy.HasProperty("_Color")) copy.SetColor("_Color", copy.GetColor("_Color") * tint);
                    mats[i] = copy;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}
