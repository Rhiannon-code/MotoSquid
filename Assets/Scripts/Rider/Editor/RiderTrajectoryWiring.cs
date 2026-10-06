using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MotoSquid.Rider
{
    public static class RiderTrajectoryWiring
    {
        const string Tag = "[TrajectoryWiring]";

        static readonly (string constraint, string anchor, HumanBodyBones tip, HumanBodyBones mid, bool isArm)[] Limbs =
        {
            ("Left Hand IK",  "GripAnchor_L",   HumanBodyBones.LeftHand,  HumanBodyBones.LeftLowerArm,  true),
            ("Right Hand IK", "GripAnchor_R",   HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, true),
            ("Left Foot IK",  "FootPegGrip_L",  HumanBodyBones.LeftFoot,  HumanBodyBones.LeftLowerLeg,  false),
            ("Right Foot IK", "FootPegGrip_R",  HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg, false),
        };

        [MenuItem("MotoSquid/Animation/Trajectory/Wire Player (Selected)")]
        public static void WireSelected()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogError($"{Tag} Select a built character (the prefab root in the scene) first.");
                return;
            }

            var animator = root.GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(a => a.avatar != null && a.avatar.isHuman);
            if (animator == null)
            {
                Debug.LogError($"{Tag} {root.name} has no Humanoid Animator under it, that is the rider.");
                return;
            }

            var constraints = root.GetComponentsInChildren<TwoBoneIKConstraint>(true);
            if (constraints.Length == 0)
            {
                Debug.LogError($"{Tag} {root.name} has no Two Bone IK constraints. Build the Limb IK Rig first.");
                return;
            }

            var player = root.GetComponent<RiderTrajectoryPlayer>()
                         ?? Undo.AddComponent<RiderTrajectoryPlayer>(root);
            player.animator = animator;

            var wired = new System.Collections.Generic.List<RiderTrajectoryPlayer.Contact>();

            foreach (var (constraintName, anchorName, tip, mid, isArm) in Limbs)
            {
                var constraint = constraints.FirstOrDefault(c => c.name == constraintName);
                if (constraint == null)
                {
                    Debug.LogWarning($"{Tag} {root.name}: no constraint named '{constraintName}', skipped.");
                    continue;
                }

                var anchor = FindDeep(root.transform, anchorName);
                if (anchor == null)
                {
                    Debug.LogWarning($"{Tag} {root.name}: no '{anchorName}' on the bike art, skipped.");
                    continue;
                }

                var data = constraint.data;

                if (data.target == null || data.target == anchor)
                {
                    var owned = FindDeep(constraint.transform, constraintName + " Target");
                    if (owned == null)
                    {
                        var go = new GameObject(constraintName + " Target");
                        Undo.RegisterCreatedObjectUndo(go, "Create IK target");
                        go.transform.SetParent(constraint.transform, false);
                        owned = go.transform;
                    }

                    owned.position = anchor.position;
                    owned.rotation = anchor.rotation;
                    data.target = owned;
                    constraint.data = data;
                    EditorUtility.SetDirty(constraint);
                }

                wired.Add(new RiderTrajectoryPlayer.Contact
                {
                    tip = tip, mid = mid, anchor = anchor, constraint = constraint, isArm = isArm,
                });
            }

            player.contacts = wired.ToArray();
            EditorUtility.SetDirty(player);

            Debug.Log($"{Tag} {root.name}: wired {wired.Count}/{Limbs.Length} contact(s). " +
                      "Assign a trajectory on the player and enter Play Mode. " +
                      "Constraints now drive targets the player owns, so the bike anchors stay put.");
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var found = FindDeep(c, name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
