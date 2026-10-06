using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MotoSquid.Combat
{
    public class CombatRigReferences : MonoBehaviour
    {
        [Serializable]
        public class BoneOverride
        {
            public string bonePath;
            public OverrideTransform constraint;
            public CombatTuning.Group group;
        }

        public Rig rig;

        [Header("Pose overrides: written every frame while an action plays")]
        public BoneOverride[] overrides = new BoneOverride[0];

        [Header("Anchors: hold the idle limbs on the bike")]
        public TwoBoneIKConstraint leftHand;
        public TwoBoneIKConstraint rightHand;
        public TwoBoneIKConstraint leftFoot;
        public TwoBoneIKConstraint rightFoot;

        public bool IsComplete
        {
            get
            {
                if (rig == null || overrides.Length == 0) return false;
                if (leftHand == null || rightHand == null || leftFoot == null || rightFoot == null) return false;
                foreach (var o in overrides)
                    if (o.constraint == null || string.IsNullOrEmpty(o.bonePath)) return false;
                return true;
            }
        }

    }
}
