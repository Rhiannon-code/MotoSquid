using MotoSquid.DevTools;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.Bike
{
    public static class BikeAnchorFitter
    {
        const string Tag = "[AnchorFit]";
        const string SourceModel = "Assets/Animations/Test/Characters/SM_Mannequin/SKM_Mannequin.fbx";
        const string ClipRoot    = "Assets/Animations/Test";
        const string ReferenceClip = "AS_Idle_Riding";

        static readonly (string style, string bike)[] Bikes =
        {
            ("SuperSport", "Assets/Prefabs/Characters/VoodooBike_R.prefab"),
            ("Upright",    "Assets/Prefabs/Characters/Futuristic_Bike.prefab"),
        };

        [MenuItem("MotoSquid/Animation/Trajectory/Fit Bike Anchors To Clips")]
        public static void Fit()
        {
            if (!EditorUtility.DisplayDialog("Fit bike anchors",
                    "This moves GripAnchor_L/R and FootPegGrip_L/R on VoodooBike_R and Futuristic_Bike " +
                    "to match the authored reference pose.\n\n" +
                    "Every character built from these bikes inherits the change. Commit first. Continue?",
                    "Fit", "Cancel"))
            {
                return;
            }

            foreach (var (style, bikePath) in Bikes) FitOne(style, bikePath);
            AssetDatabase.SaveAssets();
        }

        static void FitOne(string style, string bikePath)
        {
            if (!TryMeasure(style, out var grip, out var peg)) return;

            var root = PrefabUtility.LoadPrefabContents(bikePath);
            try
            {
                var seat = Find(root.transform, "SeatAnchor");
                if (seat == null)
                {
                    Debug.LogError($"{Tag} {bikePath} has no SeatAnchor, cannot place anything relative to it.");
                    return;
                }

                Place(root, seat, "GripAnchor_L",  new Vector3(-grip.x, grip.y, grip.z), style);
                Place(root, seat, "GripAnchor_R",  new Vector3( grip.x, grip.y, grip.z), style);
                Place(root, seat, "FootPegGrip_L", new Vector3(-peg.x,  peg.y,  peg.z),  style);
                Place(root, seat, "FootPegGrip_R", new Vector3( peg.x,  peg.y,  peg.z),  style);

                PrefabUtility.SaveAsPrefabAsset(root, bikePath);
                Debug.Log($"{Tag} {style}: fitted {System.IO.Path.GetFileName(bikePath)}. " +
                          "Rebuild the characters on this bike, or the built prefabs keep the old anchors.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void Place(GameObject root, Transform seat, string name, Vector3 offset, string style)
        {
            var anchor = Find(root.transform, name);
            if (anchor == null)
            {
                Debug.LogWarning($"{Tag} {style}: no '{name}' on the bike, skipped.");
                return;
            }

            var before = root.transform.InverseTransformVector(anchor.position - seat.position);
            anchor.position = seat.position + root.transform.TransformVector(offset);

            Debug.Log($"{Tag} {style}: {name} " +
                      $"({before.x * 100f:0.0}, {before.y * 100f:0.0}, {before.z * 100f:0.0}) -> " +
                      $"({offset.x * 100f:0.0}, {offset.y * 100f:0.0}, {offset.z * 100f:0.0}) cm from SeatAnchor, " +
                      $"moved {Vector3.Distance(before, offset) * 100f:0.0} cm.");
        }

        static bool TryMeasure(string style, out Vector3 grip, out Vector3 peg)
        {
            grip = peg = Vector3.zero;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel);
            var clipPath = AssetDatabase.FindAssets("t:Model", new[] { $"{ClipRoot}/{style}" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == ReferenceClip);

            var clip = clipPath == null ? null : AssetDatabase.LoadAllAssetRepresentationsAtPath(clipPath)
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

            if (model == null || clip == null)
            {
                Debug.LogError($"{Tag} {style}: source model or {ReferenceClip} missing.");
                return false;
            }

            var instance = Object.Instantiate(model);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var animator = instance.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    Debug.LogError($"{Tag} {SourceModel} is not Humanoid.");
                    return false;
                }

                clip.SampleAnimation(instance, 0f);
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);

                grip = Mean(instance, hips, animator, HumanBodyBones.LeftHand, HumanBodyBones.RightHand);
                peg  = Mean(instance, hips, animator, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static Vector3 Mean(GameObject instance, Transform hips, Animator animator,
            HumanBodyBones left, HumanBodyBones right)
        {
            var l = instance.transform.InverseTransformVector(animator.GetBoneTransform(left).position - hips.position);
            var r = instance.transform.InverseTransformVector(animator.GetBoneTransform(right).position - hips.position);
            return new Vector3((Mathf.Abs(l.x) + Mathf.Abs(r.x)) * 0.5f, (l.y + r.y) * 0.5f, (l.z + r.z) * 0.5f);
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = Find(c, name);
                if (f != null) return f;
            }

            return null;
        }
    }
}
