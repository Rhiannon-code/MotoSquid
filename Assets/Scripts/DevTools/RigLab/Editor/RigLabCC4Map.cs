using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.DevTools
{
    public static class RigLabCC4Map
    {
        static readonly Dictionary<HumanBodyBones, string> CC4 = new Dictionary<HumanBodyBones, string>
        {
            { HumanBodyBones.Hips, "CC_Base_Hip" },
            { HumanBodyBones.Spine, "CC_Base_Waist" },
            { HumanBodyBones.Chest, "CC_Base_Spine01" },
            { HumanBodyBones.UpperChest, "CC_Base_Spine02" },
            { HumanBodyBones.Neck, "CC_Base_NeckTwist01" },
            { HumanBodyBones.Head, "CC_Base_Head" },

            { HumanBodyBones.LeftShoulder, "CC_Base_L_Clavicle" },
            { HumanBodyBones.LeftUpperArm, "CC_Base_L_Upperarm" },
            { HumanBodyBones.LeftLowerArm, "CC_Base_L_Forearm" },
            { HumanBodyBones.LeftHand, "CC_Base_L_Hand" },
            { HumanBodyBones.RightShoulder, "CC_Base_R_Clavicle" },
            { HumanBodyBones.RightUpperArm, "CC_Base_R_Upperarm" },
            { HumanBodyBones.RightLowerArm, "CC_Base_R_Forearm" },
            { HumanBodyBones.RightHand, "CC_Base_R_Hand" },

            { HumanBodyBones.LeftUpperLeg, "CC_Base_L_Thigh" },
            { HumanBodyBones.LeftLowerLeg, "CC_Base_L_Calf" },
            { HumanBodyBones.LeftFoot, "CC_Base_L_Foot" },
            { HumanBodyBones.LeftToes, "CC_Base_L_ToeBase" },
            { HumanBodyBones.RightUpperLeg, "CC_Base_R_Thigh" },
            { HumanBodyBones.RightLowerLeg, "CC_Base_R_Calf" },
            { HumanBodyBones.RightFoot, "CC_Base_R_Foot" },
            { HumanBodyBones.RightToes, "CC_Base_R_ToeBase" },
        };

        [MenuItem("Tools/Rig Lab/2b. Repair CC4 Humanoid Mapping (selected models)", false, 2)]
        static void Repair()
        {
            var models = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .Select(p => new { path = p, importer = AssetImporter.GetAtPath(p) as ModelImporter })
                .Where(m => m.importer != null)
                .ToList();

            if (models.Count == 0)
            {
                EditorUtility.DisplayDialog("Rig Lab",
                    "Select one or more character model files in the Project window first.", "OK");
                return;
            }

            foreach (var m in models) RepairOne(m.path, m.importer);
            AssetDatabase.Refresh();
        }

        static void RepairOne(string path, ModelImporter importer)
        {
            var names = new HashSet<string>(BoneNames(path));
            var missing = CC4.Values.Where(b => !names.Contains(b)).ToList();
            if (missing.Count > 0)
            {
                Debug.LogError("Rig Lab: " + path + " has no bone named " + string.Join(", ", missing) +
                               ", this does not look like a CC4 skeleton, nothing changed.");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            var desc = importer.humanDescription;
            var human = desc.human != null ? desc.human.ToList() : new List<HumanBone>();
            var changed = new List<string>();

            foreach (var kv in CC4)
            {
                string slot = HumanTrait.BoneName[(int)kv.Key];
                int at = human.FindIndex(h => h.humanName == slot);

                if (at >= 0 && human[at].boneName == kv.Value) continue;

                var bone = at >= 0 ? human[at] : new HumanBone();
                if (at >= 0) changed.Add(slot + ": " + human[at].boneName + " -> " + kv.Value);
                else changed.Add(slot + ": (unset) -> " + kv.Value);

                bone.humanName = slot;
                bone.boneName = kv.Value;
                bone.limit.useDefaultValues = true;

                if (at >= 0) human[at] = bone; else human.Add(bone);
            }

            // Unity rejects the whole avatar if two human bones claim one transform, and a stale
            // auto guess can sit on a bone we have just taken, Bliss had RightEye on CC_Base_Head
            var owner = CC4.ToDictionary(kv => kv.Value, kv => HumanTrait.BoneName[(int)kv.Key]);
            for (int i = human.Count - 1; i >= 0; i--)
            {
                string slot;
                if (!owner.TryGetValue(human[i].boneName, out slot) || human[i].humanName == slot) continue;
                changed.Add(human[i].humanName + ": dropped, " + human[i].boneName + " belongs to " + slot);
                human.RemoveAt(i);
            }

            if (changed.Count == 0)
            {
                // Still reimport, the rig type set above is the whole point on this path, and it is
                // only the reimport that rebuilds the avatar
                importer.SaveAndReimport();
                Debug.Log("Rig Lab: " + path + " already mapped correctly, rig set to Humanoid.");
                return;
            }

            desc.human = human.ToArray();
            importer.humanDescription = desc;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log("Rig Lab: remapped " + path + "\n  " + string.Join("\n  ", changed));
        }

        static IEnumerable<string> BoneNames(string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) return Enumerable.Empty<string>();
            return root.GetComponentsInChildren<Transform>(true).Select(t => t.name);
        }
    }
}
