using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Rider
{

    public static class LeanClipGenerator
    {
        const string TestRoot = "Assets/Animations/Test";
        const string ClipName = "AS_Lean_Additive";

        const float SpineFrontBack      = -0.28f;
        const float ChestFrontBack      = -0.40f;
        const float UpperChestFrontBack = -0.40f;

        [MenuItem("MotoSquid/Animation/Generate Lean Clip (SuperSport)")]
        public static void GenerateSuperSport() => Generate("SuperSport");

        [MenuItem("MotoSquid/Animation/Generate Lean Clip (Upright)")]
        public static void GenerateUpright() => Generate("Upright");

        public static void Generate(string style)
        {
            string dir = $"{TestRoot}/{style}";
            if (!AssetDatabase.IsValidFolder(dir)) { Debug.LogError($"[LeanClip] Style folder not found: {dir}"); return; }
            string path = $"{dir}/{ClipName}.anim";

            var clip = new AnimationClip { name = ClipName };

            int applied = 0;
            applied += AddMuscle(clip, "Spine Front Back", SpineFrontBack);
            applied += AddMuscle(clip, "Chest Front Back", ChestFrontBack);
            applied += AddMuscle(clip, "UpperChest Front Back", UpperChestFrontBack);
            if (applied == 0)
            {
                Debug.LogError("[LeanClip] No spine muscles were applied, muscle names didn't match this Unity's " +
                               "humanoid muscle set. Aborted (no clip written).");
                return;
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) EditorUtility.CopySerialized(clip, existing);
            else AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LeanClip] Wrote {path} ({applied} spine muscle curves). Now run " +
                      $"MotoSquid/Animation/Generate Rider Animator ({style}), rebuild, then tune the " +
                      "'Upper Lean (Additive)' layer weight. If she tucks the WRONG way, flip the sign of the " +
                      "*FrontBack consts in LeanClipGenerator and re-run.");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        static int AddMuscle(AnimationClip clip, string muscle, float value)
        {
            if (!HumanTrait.MuscleName.Contains(muscle))
            {
                Debug.LogWarning($"[LeanClip] Unknown humanoid muscle '{muscle}', skipped.");
                return 0;
            }
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), muscle);
            var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, value));
            AnimationUtility.SetEditorCurve(clip, binding, curve);
            return 1;
        }
    }
}
