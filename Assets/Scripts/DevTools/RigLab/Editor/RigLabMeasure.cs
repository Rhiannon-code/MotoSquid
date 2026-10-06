using MotoSquid.Bike;
using MotoSquid.Rider;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabMeasure
    {
        internal const float RefHipY = 1.0003f;
        internal const float RefSpineTipY = 1.1806f;
        internal const float RefHandY = 1.3765f;
        internal const float RefHandX = 0.6614f;
        internal const float RefFootY = 0.1039f;
        internal const float RefFootX = 0.1017f;

        [MenuItem("Tools/Rig Lab/6. Measure Riders", false, 40)]
        static void Measure()
        {
            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }

            var sb = new StringBuilder("Rig Lab measurements   (reference = Dummy_Mannequin bind pose)\n");
            sb.Append("Play mode: ").Append(Application.isPlaying ? "YES" : "NO: rig targets are still at build positions").Append("\n");

            foreach (var bike in bikes)
            {
                var anim = bike.GetComponentInChildren<Animator>(true);
                if (anim == null) { sb.Append("\n").Append(bike.name).Append(": no rider Animator\n"); continue; }

                sb.Append("\n").Append(bike.name).Append("  rider=").Append(anim.gameObject.name).Append("\n");
                sb.Append("   humanoid   : ").Append(anim.isHuman).Append("\n");

                var root = anim.transform;
                sb.Append("   root local : pos ").Append(V(root.localPosition))
                  .Append("  rot ").Append(V(root.localEulerAngles))
                  .Append("  scale ").Append(V(root.localScale)).Append("\n");

                if (!anim.isHuman) { sb.Append("   (not humanoid, no bone measurements)\n"); continue; }

                var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                var spine = anim.GetBoneTransform(HumanBodyBones.Spine);
                var chest = anim.GetBoneTransform(HumanBodyBones.Chest);
                var upperChest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
                var lArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                var lFore = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                var rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                var lUpLeg = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var lLowLeg = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                var lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                var head = anim.GetBoneTransform(HumanBodyBones.Head);

                Bone(sb, "hips", root, hips, RefHipY);
                Bone(sb, "spine", root, spine, 0f);
                Bone(sb, "chest", root, chest, RefSpineTipY);
                Bone(sb, "upperChest", root, upperChest, 0f);
                Bone(sb, "leftUpperArm", root, lArm, 0f);
                Bone(sb, "leftLowerArm", root, lFore, 0f);
                Bone(sb, "leftHand", root, lHand, RefHandY);
                Bone(sb, "rightHand", root, rHand, RefHandY);
                Bone(sb, "leftUpperLeg", root, lUpLeg, 0f);
                Bone(sb, "leftFoot", root, lFoot, RefFootY);
                Bone(sb, "head", root, head, 0f);

                sb.Append("   arm length : ").Append(Chain(root, lArm, lFore, lHand).ToString("0.0000"))
                  .Append("   leg length : ").Append(Chain(root, lUpLeg, lLowLeg, lFoot).ToString("0.0000")).Append("\n");

                if (hips != null)
                {
                    float ratio = Local(root, hips).y / RefHipY;
                    sb.Append("   IMPLIED UNIFORM SCALE vs mannequin: ").Append(ratio.ToString("0.000"))
                      .Append(Application.isPlaying ? "   (meaningless in Play - hips are rig-driven)"
                                                    : (ratio < 0.9f || ratio > 1.1f ? "   <-- MISMATCH" : "   ok")).Append("\n");

                    float lateral = Local(root, hips).x;
                    sb.Append("   hips lateral offset from centre: ").Append(lateral.ToString("0.0000"))
                      .Append(Mathf.Abs(lateral) > 0.02f ? "   <-- OFF CENTRE" : "   ok").Append("\n");
                }

                if (lHand != null && rHand != null)
                {
                    sb.Append("   hand span  : ").Append((Local(root, lHand) - Local(root, rHand)).magnitude.ToString("0.000"))
                      .Append("   (mannequin ").Append((RefHandX * 2f).ToString("0.000")).Append(")\n");
                    sb.Append("   L hand local euler: ").Append(V(lHand.localEulerAngles))
                      .Append("   world euler: ").Append(V(lHand.eulerAngles)).Append("\n");
                }

                var refs = bike.GetComponentInChildren<BikerRigReferences>(true);
                if (refs != null && refs.LeftHandRig != null)
                    sb.Append("   L hand IK maintainRot=").Append(refs.LeftHandRig.data.maintainTargetRotationOffset ? "1" : "0")
                      .Append("   weight=").Append(refs.LeftHandRig.weight.ToString("0.00")).Append("\n");

                var rig = bike.GetComponentInChildren<UnityEngine.Animations.Rigging.Rig>(true);
                if (rig != null) sb.Append("   Rig weight : ").Append(rig.weight.ToString("0.00")).Append("\n");
            }
            Debug.Log(sb.ToString());
        }

        internal static float Chain(Transform root, Transform a, Transform b, Transform c)
        {
            if (a == null || b == null || c == null) return 0f;
            return Vector3.Distance(Local(root, a), Local(root, b)) + Vector3.Distance(Local(root, b), Local(root, c));
        }

        static Vector3 Local(Transform root, Transform bone)
        {
            return bone == null ? Vector3.zero : root.InverseTransformPoint(bone.position);
        }

        static void Bone(StringBuilder sb, string label, Transform root, Transform bone, float refY)
        {
            sb.Append("   ").Append(label.PadRight(11)).Append(": ");
            if (bone == null) { sb.Append("MISSING\n"); return; }
            var l = Local(root, bone);
            sb.Append(bone.name.PadRight(24)).Append(" local ").Append(V(l));
            if (refY > 0f) sb.Append("   mannequin y ").Append(refY.ToString("0.000"));
            sb.Append("\n");
        }

        static string V(Vector3 v)
        {
            return string.Format("({0,7:0.0000}, {1,7:0.0000}, {2,7:0.0000})", v.x, v.y, v.z);
        }
    }
}
