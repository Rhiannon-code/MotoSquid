using UnityEngine;


namespace ArcadeBP_Pro
{
    public class BikerAnimationTargets : MonoBehaviour
    {
        [Header("Hip Targets")]
        public Transform hipIdleTarget;
        public Transform hipNormalSpeedTarget;
        public Transform hipHighSpeedTarget;
        public Transform hipInAirTarget;
        public Transform hipReverseTarget; // New reverse target

        [Header("Spine Targets")]
        public Transform spineIdleTarget;
        public Transform spineNormalSpeedTarget;
        public Transform spineHighSpeedTarget;
        public Transform spineReverseTarget; // New reverse target

        [Header("Leg Targets")]
        public Transform leftlegIdleTarget;
        public Transform leftlegInMotionTarget;
        public Transform leftlegReverseTarget; // New reverse target
        public Transform rightlegIdleTarget;
        public Transform rightlegInMotionTarget;
        public Transform rightlegReverseTarget; // New reverse target

        [Header("Hand Targets")]
        public Transform leftHandTarget;
        public Transform rightHandTarget;
        public Transform leftHandReverseTarget; // New reverse target
        public Transform rightHandReverseTarget; // New reverse target
    }

}
