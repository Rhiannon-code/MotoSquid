using System.IO;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Rider
{

    public static class BikeClipRootMotionBaker
    {
        const string TestRoot = "Assets/Animations/Test";

        [MenuItem("MotoSquid/Animation/Unbake _Bike Root ROTATION (SuperSport)")]
        public static void UnbakeRotSuperSport() => SetRootRotationBake("SuperSport", false);

        [MenuItem("MotoSquid/Animation/Unbake _Bike Root ROTATION (Upright)")]
        public static void UnbakeRotUpright() => SetRootRotationBake("Upright", false);

        [MenuItem("MotoSquid/Animation/Rebake _Bike Root ROTATION (SuperSport)")]
        public static void RebakeRotSuperSport() => SetRootRotationBake("SuperSport", true);

        [MenuItem("MotoSquid/Animation/Rebake _Bike Root ROTATION (Upright)")]
        public static void RebakeRotUpright() => SetRootRotationBake("Upright", true);

        static void SetRootRotationBake(string style, bool bake)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[BikeRootBake] Editor is compiling/importing. Wait, then run again.");
                return;
            }

            string styleRoot = $"{TestRoot}/{style}";
            if (!AssetDatabase.IsValidFolder(styleRoot))
            {
                Debug.LogError($"[BikeRootBake] Folder not found: {styleRoot}");
                return;
            }

            int done = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { styleRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(path).EndsWith("_Bike")) continue;
                if (!(AssetImporter.GetAtPath(path) is ModelImporter imp)) continue;

                var clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0) continue;

                bool changed = false;
                foreach (var c in clips)
                {
                    if (c.lockRootRotation == bake) continue;
                    c.lockRootRotation = bake;
                    changed = true;
                }

                if (!changed) continue;
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
                done++;
            }

            Debug.Log($"[BikeRootBake] {style}: root rotation Bake Into Pose = {bake} on {done} *_Bike clip(s). " +
                      (bake
                          ? "The clips pitch the model again; expect it to stack with the physics lean/wheelie."
                          : "Only Lean Transform and Wheelie Transform pitch the bike now."));
        }

        [MenuItem("MotoSquid/Animation/Bake _Bike Root Motion In Place (SuperSport)")]
        public static void BakeSuperSport() => Bake("SuperSport");

        [MenuItem("MotoSquid/Animation/Bake _Bike Root Motion In Place (Upright)")]
        public static void BakeUpright() => Bake("Upright");

        static void Bake(string style)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[BikeRootBake] Editor is compiling/importing. Wait for it to settle, then run again.");
                return;
            }
            string styleRoot = $"{TestRoot}/{style}";
            if (!AssetDatabase.IsValidFolder(styleRoot)) { Debug.LogError($"[BikeRootBake] Folder not found: {styleRoot}"); return; }

            int done = 0, skipped = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { styleRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(path).EndsWith("_Bike")) { skipped++; continue; }
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;

                var so = new SerializedObject(imp);
                var mn = so.FindProperty("m_MotionNodeName") ?? so.FindProperty("motionNodeName");
                if (mn != null) { mn.stringValue = "<Root Transform>"; so.ApplyModifiedPropertiesWithoutUndo(); }

                var clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
                foreach (var c in clips)
                {
                    c.lockRootRotation   = true;  
                    c.lockRootHeightY    = true;  
                    c.lockRootPositionXZ = true;   
                    c.keepOriginalOrientation = true;
                    c.keepOriginalPositionY   = true;  
                    c.keepOriginalPositionXZ  = false; 
                }
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
                done++;
            }
            Debug.Log($"[BikeRootBake] {style}: baked root motion in place on {done} *_Bike clip(s), " +
                      $"skipped {skipped} non-_Bike. The 'Root position controlled by curves' warning should " +
                      "clear. Re test: the bike should animate WITHOUT translating.");
        }
    }
}
