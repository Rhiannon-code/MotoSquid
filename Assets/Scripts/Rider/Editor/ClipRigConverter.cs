using System.IO;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Rider
{
    public static class ClipRigConverter
    {
        const string TestRoot = "Assets/Animations/Test";

        [MenuItem("MotoSquid/Animation/Set SuperSport Clips To Humanoid")]
        public static void ConvertSuperSport() => Convert("SuperSport");

        [MenuItem("MotoSquid/Animation/Set Upright Clips To Humanoid")]
        public static void ConvertUpright() => Convert("Upright");

        static void Convert(string style)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[ClipRig] Editor is compiling/importing. Wait for it to settle, then run again.");
                return;
            }

            string styleRoot = $"{TestRoot}/{style}";
            if (!AssetDatabase.IsValidFolder(styleRoot))
            {
                Debug.LogError($"[ClipRig] Folder not found: {styleRoot}.");
                return;
            }

            int converted = 0, skipped = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { styleRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp  = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                if (Path.GetFileNameWithoutExtension(path).EndsWith("_Bike")) { skipped++; continue; }
                if (imp.animationType == ModelImporterAnimationType.Human &&
                    imp.avatarSetup   == ModelImporterAvatarSetup.CreateFromThisModel)
                { skipped++; continue; }

                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.sourceAvatar  = null;
                imp.SaveAndReimport();
                converted++;
            }

            Debug.Log($"[ClipRig] {style}: set {converted} clip(s) to Humanoid (Create From This Model), " +
                      $"skipped {skipped} (already converted or *_Bike). " +
                      "Now set Loop Time ON for the *_Loop/idle clips, OFF for one shots, then run " +
                      $"MotoSquid > Animation > Generate Rider Animator ({style}).");
        }

        const string RiderModelName = "VoodooRigged";

        static string RiderModelPath
        {
            get
            {
                foreach (var guid in AssetDatabase.FindAssets($"{RiderModelName} t:Model"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path) == RiderModelName) return path;
                }

                Debug.LogError($"[ClipRig] {RiderModelName}.fbx not found anywhere under Assets.");
                return null;
            }
        }

        [MenuItem("MotoSquid/Animation/Enable Translation DOF (SuperSport)")]
        public static void DofSuperSport() => EnableDof("SuperSport");

        [MenuItem("MotoSquid/Animation/Enable Translation DOF (Upright)")]
        public static void DofUpright() => EnableDof("Upright");

        static void EnableDof(string style)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[ClipRig] Editor is compiling/importing. Wait for it to settle, then run again.");
                return;
            }
            string styleRoot = $"{TestRoot}/{style}";
            if (!AssetDatabase.IsValidFolder(styleRoot)) { Debug.LogError($"[ClipRig] Folder not found: {styleRoot}"); return; }

            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { styleRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).EndsWith("_Bike")) continue;
                if (SetTranslationDoF(path)) n++;
            }
            var riderPath = RiderModelPath;
            if (riderPath != null && SetTranslationDoF(riderPath)) n++;

            Debug.Log($"[ClipRig] Translation DOF enabled on {n} Humanoid asset(s) ({style} rider clips + rider model). " +
                      "Re-preview a clip on the mannequin, the upper body should now match the Blender pose.");
        }

        static bool SetTranslationDoF(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null || imp.animationType != ModelImporterAnimationType.Human) return false;
            var hd = imp.humanDescription;
            if (hd.hasTranslationDoF) return false;
            hd.hasTranslationDoF = true;
            imp.humanDescription = hd;
            imp.SaveAndReimport();
            return true;
        }
    }
}
