using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.DevTools
{
    public static class RigLabVerifyAvatars
    {
        const string ClipFolder = "Assets/Animations";

        static readonly (HumanBodyBones bone, string expect)[] Expected =
        {
            (HumanBodyBones.Hips, "Root_M"),
            (HumanBodyBones.Spine, "Spine1_M"),
            (HumanBodyBones.Chest, "Spine2_M"),
            (HumanBodyBones.UpperChest, "Chest_M"),
            (HumanBodyBones.LeftShoulder, "Scapula_L"),
            (HumanBodyBones.LeftUpperArm, "Shoulder_L"),
            (HumanBodyBones.LeftLowerArm, "Elbow_L"),
            (HumanBodyBones.LeftHand, "Wrist_L"),
            (HumanBodyBones.LeftUpperLeg, "Hip_L"),
            (HumanBodyBones.LeftLowerLeg, "Knee_L"),
            (HumanBodyBones.LeftFoot, "Ankle_L"),
            (HumanBodyBones.RightUpperArm, "Shoulder_R"),
            (HumanBodyBones.RightHand, "Wrist_R"),
            (HumanBodyBones.RightUpperLeg, "Hip_R"),
            (HumanBodyBones.RightFoot, "Ankle_R"),
        };

        [MenuItem("Tools/Rig Lab/21. Verify Clip Avatars", false, 216)]
        static void Verify()
        {
            var paths = AssetDatabase.FindAssets("t:Model", new[] { ClipFolder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            if (paths.Count == 0) { Debug.LogWarning("Rig Lab: no models in " + ClipFolder); return; }

            var bad = new List<string>();
            var noAvatar = new List<string>();
            int ok = 0;

            foreach (var path in paths)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();

                if (importer == null || importer.animationType != ModelImporterAnimationType.Human)
                { noAvatar.Add(name + "  (not Humanoid)"); continue; }

                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                { noAvatar.Add(name + "  (no valid avatar)"); continue; }

                var map = importer.humanDescription.human
                    .ToDictionary(h => h.humanName, h => h.boneName);

                var wrong = new List<string>();
                foreach (var e in Expected)
                {
                    string humanName = HumanName(e.bone);
                    string actual;
                    if (!map.TryGetValue(humanName, out actual)) { wrong.Add(humanName + "=<unmapped>"); continue; }
                    if (actual != e.expect) wrong.Add(humanName + "=" + actual + " (want " + e.expect + ")");
                }

                if (wrong.Count > 0) bad.Add(name + "\n      " + string.Join("\n      ", wrong));
                else ok++;
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("Rig Lab: avatar check over ").Append(paths.Count).Append(" clip(s)\n");
            sb.Append("   correct : ").Append(ok).Append('\n');
            sb.Append("   missing : ").Append(noAvatar.Count).Append('\n');
            sb.Append("   mismapped: ").Append(bad.Count).Append('\n');
            foreach (var n in noAvatar) sb.Append("\n   NO AVATAR  ").Append(n);
            foreach (var b in bad) sb.Append("\n   MISMAPPED  ").Append(b);

            if (bad.Count > 0 || noAvatar.Count > 0) Debug.LogError(sb.ToString());
            else Debug.Log(sb + "\n   All clips ready to sample.");
        }

        static string HumanName(HumanBodyBones b)
        {
            switch (b)
            {
                case HumanBodyBones.LeftUpperArm: return "LeftUpperArm";
                case HumanBodyBones.LeftLowerArm: return "LeftLowerArm";
                case HumanBodyBones.LeftUpperLeg: return "LeftUpperLeg";
                case HumanBodyBones.LeftLowerLeg: return "LeftLowerLeg";
                case HumanBodyBones.RightUpperArm: return "RightUpperArm";
                case HumanBodyBones.RightUpperLeg: return "RightUpperLeg";
                default: return b.ToString();
            }
        }
    }
}
