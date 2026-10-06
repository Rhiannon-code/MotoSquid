using System;
using UnityEngine;

namespace MotoSquid.Combat
{
    [CreateAssetMenu(fileName = "CombatTuning", menuName = "Rig Lab/Combat Tuning")]
    public class CombatTuning : ScriptableObject
    {
        public enum Group { Hips, Spine, Head, LeftArm, RightArm, LeftLeg, RightLeg }

        [Serializable]
        public class Part
        {
            public float weight = 1f;
            public float amount = 1f;
        }

        [Header("Blend: riding <-> combat")]
        public float blendIn = 0.12f;
        public float blendOut = 0.18f;
        public AnimationCurve blendShape = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public float switchBlend = 0.09f;
        public float releaseAt = 1f;

        [Header("Anchors: hold idle limbs on the bike")]
        public float handHold = 1f;
        public float footHold = 1f;
        public float anchoredLimbPose;

        [Header("Pose: per body part")]
        public float amount = 1f;

        public Part hips = new Part();
        public Part spine = new Part();
        public Part head = new Part();
        public Part leftArm = new Part();
        public Part rightArm = new Part();
        public Part leftLeg = new Part();
        public Part rightLeg = new Part();

        [Header("Timing")]
        public float meleeSpeed = 1f;
        public float kickSpeed = 1f;

        [Header("Inspect")]
        public bool repeat;
        public bool freeze;
        public float freezeAt = 0.4f;

        public Part Of(Group g)
        {
            switch (g)
            {
                case Group.Hips: return hips;
                case Group.Spine: return spine;
                case Group.Head: return head;
                case Group.LeftArm: return leftArm;
                case Group.RightArm: return rightArm;
                case Group.LeftLeg: return leftLeg;
                default: return rightLeg;
            }
        }

        public float Weight(Group g) { return Of(g).weight; }
        public float Amount(Group g) { return Mathf.Clamp01(amount * Of(g).amount); }
    }
}
