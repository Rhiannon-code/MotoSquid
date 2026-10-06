using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace ArcadeBP_Pro
{
    public class BikerRigReferences : MonoBehaviour
    {
        public MultiParentConstraint hipRig;
        public MultiRotationConstraint spineRootRig;
        public MultiRotationConstraint spineTipRig;
        public TwoBoneIKConstraint RightLegRig;
        public TwoBoneIKConstraint LeftLegRig;
        public TwoBoneIKConstraint RightHandRig;
        public TwoBoneIKConstraint LeftHandRig;
        public MultiAimConstraint headRig;

        [Header("Rig Targets")]
        public Transform hipTarget;
        public Transform spineRootTarget;
        public Transform spineTipTarget;
        public Transform rightLegTarget;
        public Transform rightLegHint;
        public Transform leftLegTarget;
        public Transform leftLegHint;
        public Transform rightHandTarget;
        public Transform rightHandHint;
        public Transform leftHandTarget;
        public Transform leftHandHint;
        public Transform headLookAtTarget;

    }

}
