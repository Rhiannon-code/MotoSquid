using UnityEngine;


namespace ArcadeBP_Pro
{
    public class BikerAnimationController : MonoBehaviour
    {
        [Tooltip("Reference to the bike controller script.")]
        public ArcadeBikeControllerPro bikeController;

        [Tooltip("Maximum positional offset for the hip during leaning. Example: Setting this to 0.2 means the hip can offset by 0.2 units sideways.")]
        public float maxHipPosOffset = 0.2f;

        [Tooltip("Maximum rotational offset for the hip during leaning. Example: Setting this to 20 means the hip can rotate by 20 degrees.")]
        public float maxHipRotOffset = 20.0f;

        [Tooltip("Maximum rotational offset for the spine during leaning. Example: Setting this to 10 means the spine can rotate by 10 degrees.")]
        public float maxSpineRotOffset = 10.0f;

        [Tooltip("Maximum offset for the knee hints during leaning. Example: Setting this to 0.2 means the knee hints can offset by 0.2 units.")]
        public float maxKneeOffset = 0.2f;

        [Tooltip("Offset for the spine tip during high-speed motion. Example: Setting this to 10 means the spine tip will offset by 10 units during high-speed motion.")]
        public float spineTipOffset = 10f;

        [Space]

        [Tooltip("Speed threshold for normal speed animations. Example: Setting this to 1 means speeds below 1 will trigger normal speed animations.")]
        public float normalSpeedThreshold = 1.0f;

        [Tooltip("Speed threshold for high-speed animations. Example: Setting this to 10 means speeds above 10 will trigger high-speed animations.")]
        public float highSpeedThreshold = 10.0f;

        [Tooltip("Speed of the lean animation transition. Example: Setting this to 2 means the lean animation will transition at a rate of 2 units/second.")]
        public float leanSpeed = 20.0f;

        [Tooltip("Speed of transitioning between different animation states. Example: Setting this to 2 means transitions will occur at a rate of 2 units/second.")]
        public float transitionSpeed = 5.0f;

        [Header("Reverse Leg Animation")]

        [Tooltip("Enabling this will use the reverse leg animation.")]
        public bool useReverseLegAnimation = true;

        [Tooltip("Height of the reverse leg step animation. Example: Setting this to 0.25 means the legs will lift by 0.25 units during a reverse step.")]
        public float reverseStepHeight = 0.25f;

        [Tooltip("Distance of the reverse leg step animation. Example: Setting this to 0.5 means the legs will move forward by 0.5 units during a reverse step.")]
        public float reverseStepDistance = 0.5f;

        [Tooltip("Speed of the reverse leg step animation. Example: Setting this to 1 means the reverse step animation will occur at a rate of 1 unit/second.")]
        public float reverseStepSpeed = 5;

        [Header("Rig References")]
        [Tooltip("Reference to the hip target rig transform.")]
        public Transform hipTarget_rig;

        [Tooltip("Reference to the spine root target rig transform.")]
        public Transform spineRootTarget_rig;

        [Tooltip("Reference to the spine tip target rig transform.")]
        public Transform spineTipTarget_rig;

        [Tooltip("Reference to the right leg target rig transform.")]
        public Transform rightLegTarget_rig;

        [Tooltip("Reference to the right leg hint rig transform.")]
        public Transform rightLegHint_rig;

        [Tooltip("Reference to the left leg target rig transform.")]
        public Transform leftLegTarget_rig;

        [Tooltip("Reference to the left leg hint rig transform.")]
        public Transform leftLegHint_rig;

        [Tooltip("Reference to the right hand target rig transform.")]
        public Transform rightHandTarget_rig;

        [Tooltip("Reference to the left hand target rig transform.")]
        public Transform leftHandTarget_rig;

        [Tooltip("Reference to the head look-at target rig transform.")]
        public Transform headLookAtTarget_rig;

        [Header("Hip Targets")]
        [Tooltip("Hip will move and rotate to this transform when bike is idle.")]
        public Transform hipIdleTarget;

        [Tooltip("Hip will move and rotate to this transform when bike is at normal speed.")]
        public Transform hipNormalSpeedTarget;

        [Tooltip("Hip will move and rotate to this transform when bike is at high speed.")]
        public Transform hipHighSpeedTarget;

        [Tooltip("Hip will move and rotate to this transform when bike is in the air.")]
        public Transform hipInAirTarget;

        [Tooltip("Hip will move and rotate to this transform during reverse animation.")]
        public Transform hipReverseTarget; // New reverse target

        [Header("Spine Targets")]
        [Tooltip("Spine will move and rotate to this transform when bike is idle.")]
        public Transform spineIdleTarget;

        [Tooltip("Spine will move and rotate to this transform when bike is at normal speed.")]
        public Transform spineNormalSpeedTarget;

        [Tooltip("Spine will move and rotate to this transform when bike is at high speed.")]
        public Transform spineHighSpeedTarget;

        [Tooltip("Spine will move and rotate to this transform during reverse animation.")]
        public Transform spineReverseTarget; // New reverse target

        [Header("Leg Targets")]
        [Tooltip("Left leg will move and rotate to this transform when bike is idle.")]
        public Transform leftlegIdleTarget;

        [Tooltip("Left leg will move and rotate to this transform when bike is in motion.")]
        public Transform leftlegInMotionTarget;

        [Tooltip("Left leg will move and rotate to this transform during reverse animation.")]
        public Transform leftlegReverseTarget; // New reverse target

        [Tooltip("Right leg will move and rotate to this transform when bike is idle.")]
        public Transform rightlegIdleTarget;

        [Tooltip("Right leg will move and rotate to this transform when bike is in motion.")]
        public Transform rightlegInMotionTarget;

        [Tooltip("Right leg will move and rotate to this transform during reverse animation.")]
        public Transform rightlegReverseTarget; // New reverse target

        [Header("Hand Targets")]
        [Tooltip("Left hand will move and rotate to this transform when holding the handlebar.")]
        public Transform leftHandTarget;

        [Tooltip("Right hand will move and rotate to this transform when holding the handlebar.")]
        public Transform rightHandTarget;

        [Tooltip("Left hand will move and rotate to this transform during reverse animation.")]
        public Transform leftHandReverseTarget; // New reverse target

        [Tooltip("Right hand will move and rotate to this transform during reverse animation.")]
        public Transform rightHandReverseTarget; // New reverse target


        [Header("Bike Data")]
        [HideInInspector] public float currentSpeed; // Public variable for current speed
        [HideInInspector] public float currentLeanAngle; // Public variable for current lean angle

        Vector3 leanPosOffsetForHip;
        Vector3 leanRotOffsetForHip;

        Vector3 leanRotOffsetForSpine;

        Vector3 leftLegHint_InitalPos;
        Vector3 rightLegHint_InitalPos;

        Vector3 leftlegReverseTargetInitialPos;
        Vector3 rightlegReverseTargetInitialPos;

        void Start()
        {
            leftLegHint_InitalPos = leftLegHint_rig.localPosition;
            rightLegHint_InitalPos = rightLegHint_rig.localPosition;

            InitializeRigTargets();

            leftlegReverseTargetInitialPos = leftlegReverseTarget.localPosition;
            rightlegReverseTargetInitialPos = rightlegReverseTarget.localPosition;
        }

        void InitializeRigTargets()
        {
            hipTarget_rig.localPosition = hipIdleTarget.localPosition;
            hipTarget_rig.localRotation = hipIdleTarget.localRotation;

            spineRootTarget_rig.localRotation = spineIdleTarget.localRotation;
            spineTipTarget_rig.localRotation = spineIdleTarget.localRotation;

            rightLegTarget_rig.localPosition = rightlegIdleTarget.localPosition;
            leftLegTarget_rig.localPosition = leftlegIdleTarget.localPosition;
        }

        void Update()
        {
            currentSpeed = bikeController.localBikeVelocity.magnitude * Mathf.Sign(bikeController.localBikeVelocity.z);
            currentLeanAngle = bikeController.currentLeanAngle;

            HandleLeaning();
            HandleHipTargets();
            HandleSpineTargets();
            HandleLegTargets();
            HandleHeadLookAt();
            KeepHandsOnHandlebars();
            HandleKneesOffset();
            ReverseLegAnimation();
        }

        float leanLerp = 0.0f;

        void HandleLeaning()
        {
            if (Mathf.Abs(bikeController.CurrentSteerInput) > 0)
            {
                leanLerp = Mathf.Lerp(leanLerp, 1, Time.deltaTime * leanSpeed);
            }
            else
            {
                leanLerp = Mathf.Lerp(leanLerp, 0, Time.deltaTime * leanSpeed);
            }

            float leanAngle = currentLeanAngle;
            float posX = (-leanAngle / bikeController.bikeSettings.maxLeanAngle) * maxHipPosOffset;

            leanPosOffsetForHip = new Vector3(posX, -Mathf.Abs(posX / 2), 0);

            float RotZ_Hip = (leanAngle / bikeController.bikeSettings.maxLeanAngle) * maxHipRotOffset * leanLerp;

            leanRotOffsetForHip = new Vector3(0, 0, RotZ_Hip);

            float RotZ_spine = (leanAngle / bikeController.bikeSettings.maxLeanAngle) * maxSpineRotOffset;

            leanRotOffsetForSpine = new Vector3(0, 0, RotZ_spine);
        }

        void HandleHipTargets()
        {
            if (!bikeController.bikeIsGrounded)
            {
                TransitionToTarget(hipTarget_rig, hipInAirTarget, leanPosOffsetForHip, leanRotOffsetForHip);
                return;
            }

            if (currentSpeed < normalSpeedThreshold)
            {
                if (currentSpeed < -0.5f && !bikeController.isDoingBurnout && useReverseLegAnimation)
                {
                    TransitionToTarget(hipTarget_rig, hipReverseTarget, leanPosOffsetForHip, leanRotOffsetForHip);
                }
                else
                {
                    TransitionToTarget(hipTarget_rig, hipIdleTarget, leanPosOffsetForHip, leanRotOffsetForHip);
                }
            }
            else if (currentSpeed < highSpeedThreshold)
            {
                TransitionToTarget(hipTarget_rig, hipNormalSpeedTarget, leanPosOffsetForHip, leanRotOffsetForHip);
            }
            else
            {
                TransitionToTarget(hipTarget_rig, hipHighSpeedTarget, leanPosOffsetForHip, leanRotOffsetForHip);
            }
        }

        void HandleSpineTargets()
        {
            if (currentSpeed < normalSpeedThreshold)
            {
                if (currentSpeed < -0.5f && !bikeController.isDoingBurnout && useReverseLegAnimation)
                {
                    TransitionToTarget(spineRootTarget_rig, spineReverseTarget, Vector3.zero, leanRotOffsetForSpine);
                    TransitionToTarget(spineTipTarget_rig, spineReverseTarget, Vector3.zero, new Vector3(spineTipOffset, 0, 0) + leanRotOffsetForSpine);
                }
                else
                {
                    TransitionToTarget(spineRootTarget_rig, spineIdleTarget, Vector3.zero, leanRotOffsetForSpine);
                    TransitionToTarget(spineTipTarget_rig, spineIdleTarget, Vector3.zero, new Vector3(spineTipOffset, 0, 0) + leanRotOffsetForSpine);
                }
            }
            else if (currentSpeed < highSpeedThreshold)
            {
                TransitionToTarget(spineRootTarget_rig, spineNormalSpeedTarget, Vector3.zero, leanRotOffsetForSpine);
                TransitionToTarget(spineTipTarget_rig, spineNormalSpeedTarget, Vector3.zero, new Vector3(spineTipOffset, 0, 0) + leanRotOffsetForSpine);
            }
            else
            {
                TransitionToTarget(spineRootTarget_rig, spineHighSpeedTarget, Vector3.zero, leanRotOffsetForSpine);
                TransitionToTarget(spineTipTarget_rig, spineHighSpeedTarget, Vector3.zero, new Vector3(spineTipOffset, 0, 0) + leanRotOffsetForSpine);
            }
        }

        void HandleLegTargets()
        {
            if (currentSpeed < normalSpeedThreshold && bikeController.bikeIsGrounded)
            {
                if (currentSpeed < -0.5f && !bikeController.isDoingBurnout && useReverseLegAnimation)
                {
                    TransitionToTarget(rightLegTarget_rig, rightlegReverseTarget, Vector3.zero, Vector3.zero);
                    TransitionToTarget(leftLegTarget_rig, leftlegReverseTarget, Vector3.zero, Vector3.zero);
                }
                else
                {
                    TransitionToTarget(rightLegTarget_rig, rightlegIdleTarget, Vector3.zero, Vector3.zero);
                    TransitionToTarget(leftLegTarget_rig, leftlegIdleTarget, Vector3.zero, Vector3.zero);
                }
            }
            else
            {
                TransitionToTarget(rightLegTarget_rig, rightlegInMotionTarget, Vector3.zero, Vector3.zero);
                TransitionToTarget(leftLegTarget_rig, leftlegInMotionTarget, Vector3.zero, Vector3.zero);
            }
        }

        void ReverseLegAnimation()
        {
            float time = Time.time * reverseStepSpeed;

            float leftLegY = leftlegReverseTargetInitialPos.y + Mathf.Clamp01(Mathf.Sin(time)) * reverseStepHeight;
            float rightLegY = rightlegReverseTargetInitialPos.y + Mathf.Clamp01(Mathf.Sin(time + Mathf.PI)) * reverseStepHeight; // Opposite phase

            float leftLegZ = leftlegReverseTargetInitialPos.z + Mathf.Cos(time) * reverseStepDistance;
            float rightLegZ = rightlegReverseTargetInitialPos.z + Mathf.Cos(time + Mathf.PI) * reverseStepDistance; // Opposite phase

            leftlegReverseTarget.localPosition = new Vector3(leftlegReverseTargetInitialPos.x, leftLegY, leftLegZ);
            rightlegReverseTarget.localPosition = new Vector3(rightlegReverseTargetInitialPos.x, rightLegY, rightLegZ);
        }


        void HandleHeadLookAt()
        {
            // Implement head look-at logic here if needed
        }

        void HandleKneesOffset()
        {
            float leanFactor = Mathf.Abs(currentLeanAngle / bikeController.bikeSettings.maxLeanAngle);
            float posX = leanFactor * maxKneeOffset;

            Vector3 LeftLegHintoffset = leftLegHint_InitalPos + new Vector3(-posX, 0, 0);
            Vector3 RightLegHintoffset = rightLegHint_InitalPos + new Vector3(posX, 0, 0);

            Vector3 leftLegHintPos = Vector3.Lerp(leftLegHint_InitalPos, LeftLegHintoffset, leanFactor);
            Vector3 rightLegHintPos = Vector3.Lerp(rightLegHint_InitalPos, RightLegHintoffset, leanFactor);

            leftLegHint_rig.localPosition = Vector3.Lerp(leftLegHint_rig.localPosition, leftLegHintPos, Time.deltaTime * leanSpeed);
            rightLegHint_rig.localPosition = Vector3.Lerp(rightLegHint_rig.localPosition, rightLegHintPos, Time.deltaTime * leanSpeed);
        }

        void TransitionToTarget(Transform target, Transform desiredTransform, Vector3 PositionOffset, Vector3 RotationOffset)
        {
            // Calculate the target position with the offset in local space
            Vector3 targetPosition = desiredTransform.TransformPoint(PositionOffset);

            // Smoothly interpolate the target position to the desired position with the offset
            target.position = Vector3.Lerp(target.position, targetPosition, Time.deltaTime * transitionSpeed);

            // Calculate the target rotation with the rotation offset in local space
            Quaternion desiredRotationWithOffset = desiredTransform.rotation * Quaternion.Euler(RotationOffset);

            // Smoothly interpolate the target rotation to the desired rotation with the offset
            target.rotation = Quaternion.Slerp(target.rotation, desiredRotationWithOffset, Time.deltaTime * transitionSpeed);
        }

        void KeepHandsOnHandlebars()
        {
            if (currentSpeed < -0.5f && !bikeController.isDoingBurnout)
            {
                rightHandTarget_rig.position = rightHandReverseTarget.position;
                leftHandTarget_rig.position = leftHandReverseTarget.position;
                rightHandTarget_rig.rotation = rightHandReverseTarget.rotation;
                leftHandTarget_rig.rotation = leftHandReverseTarget.rotation;
            }
            else
            {
                rightHandTarget_rig.position = rightHandTarget.position;
                leftHandTarget_rig.position = leftHandTarget.position;
                rightHandTarget_rig.rotation = rightHandTarget.rotation;
                leftHandTarget_rig.rotation = leftHandTarget.rotation;
            }
        }
    }

}
