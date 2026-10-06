using MotoSquid.Bike;
using UnityEngine;

namespace MotoSquid.Rider
{
public class BikeAnimationController : MonoBehaviour
{
    public BikeController bikeControllerRhiannon;
    public BikeAIController bikeAIRhiannon;

    // One of the two is set, never both. The AI controller exposes the same six values under the same
    // names, so the rider is posed identically whoever is riding - without this an AI racer has no
    // procedural rider at all
    bool OnAI => bikeControllerRhiannon == null && bikeAIRhiannon != null;
    Vector3 BikeVelocity      => OnAI ? bikeAIRhiannon.localBikeVelocity          : bikeControllerRhiannon.localBikeVelocity;
    float   BikeLeanAngle     => OnAI ? bikeAIRhiannon.currentLeanAngle           : bikeControllerRhiannon.currentLeanAngle;
    bool    BikeIsBurningOut  => OnAI ? bikeAIRhiannon.isDoingBurnout             : bikeControllerRhiannon.isDoingBurnout;
    int     BikeSteerInput    => OnAI ? bikeAIRhiannon.CurrentSteerInput          : bikeControllerRhiannon.CurrentSteerInput;
    bool    BikeIsGrounded    => OnAI ? bikeAIRhiannon.bikeIsGrounded             : bikeControllerRhiannon.bikeIsGrounded;
    float   BikeMaxLeanAngle  => OnAI ? bikeAIRhiannon.bikeSettings.maxLeanAngle  : bikeControllerRhiannon.bikeSettings.maxLeanAngle;

    public float maxHipPosOffset = 0.2f;

    // How far the hip drops as it slides across on lean, as a fraction of that slide. The rig has no
    // collision, so too much of this puts the pelvis inside the seat and the thigh through the fairing
    [Range(0f, 1f)] public float leanHipSinkRatio = 0.5f;
    public float maxHipRotOffset = 20.0f;
    public float maxSpineRotOffset = 10.0f;

    [Header("Burnout pose")]
    // Pitch added to the rider while holding a burnout, so they sit up instead of staying tucked.
    // Flip the sign if the rider leans further forward instead of standing up
    public float burnoutSpineUprightAngle = -8f;
    public float burnoutHipUprightAngle   = -4f;
    public float burnoutPoseBlendSpeed    = 6f;
    float burnoutPoseBlend;
    public float maxKneeOffset = 0.2f;
    public float spineTipOffset = 10f;
    public float normalSpeedThreshold = 1.0f;
    public float highSpeedThreshold = 10.0f;
    public float leanSpeed = 20.0f;
    public float transitionSpeed = 5.0f;

    [Header("Reverse Leg Animation")]
    public bool useReverseLegAnimation = true;
    public float reverseStepHeight = 0.25f;
    public float reverseStepDistance = 0.5f;
    public float reverseStepSpeed = 5;

    [Header("Rig References")]
    public Transform hipTargetRig;
    public Transform spineRootTargetRig;
    public Transform spineTipTargetRig;
    public Transform rightLegTargetRig;
    public Transform rightLegHintRig;
    public Transform leftLegTargetRig;
    public Transform leftLegHintRig;
    public Transform rightHandTargetRig;
    public Transform leftHandTargetRig;
    public Transform headLookAtTargetRig;

    [Header("Hip Targets")]
    public Transform hipIdleTarget;
    public Transform hipNormalSpeedTarget;
    public Transform hipHighSpeedTarget;
    public Transform hipInAirTarget;

    // New reverse target
    public Transform hipReverseTarget;

    [Header("Spine Targets")]
    public Transform spineIdleTarget;
    public Transform spineNormalSpeedTarget;
    public Transform spineHighSpeedTarget;

    // New reverse target
    public Transform spineReverseTarget; 

    [Header("Leg Targets")]
    public Transform leftlegIdleTarget;
    public Transform leftlegInMotionTarget;

    // New reverse target
    public Transform leftlegReverseTarget; 
    public Transform rightlegIdleTarget;
    public Transform rightlegInMotionTarget;

    // New reverse target
    public Transform rightlegReverseTarget; 

    [Header("Hand Targets")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    // New reverse target
    public Transform leftHandReverseTarget; 

    // New reverse target
    public Transform rightHandReverseTarget; 


    [Header("Bike Data")] [HideInInspector]
    public float currentSpeed; // Public variable for current speed

    [HideInInspector] public float currentLeanAngle; // Public variable for current lean angle

    Vector3 leanPosOffsetForHip;

    // The hip slides sideways with lean. Anything aiming a knee has to slide with it or its pole
    // vector swings the knee inboard, through the bike
    public Vector3 LeanHipOffset { get { return leanPosOffsetForHip; } }

    Vector3 leanRotOffsetForHip;

    Vector3 leanRotOffsetForSpine;

    Vector3 leftLegHint_InitalPos;
    Vector3 rightLegHint_InitalPos;

    Vector3 leftlegReverseTargetInitialPos;
    Vector3 rightlegReverseTargetInitialPos;

    void Start()
    {
        // Naming the empty fields rather than saying "one or more": the rig is wired by a build step,
        // and knowing WHICH reference is missing says which step did not finish
        var missing = new System.Collections.Generic.List<string>();
        if (bikeControllerRhiannon == null && bikeAIRhiannon == null) missing.Add("a bike controller (player or AI)");
        if (leftLegHintRig == null) missing.Add("leftLegHintRig");
        if (rightLegHintRig == null) missing.Add("rightLegHintRig");
        if (leftlegReverseTarget == null) missing.Add("leftlegReverseTarget");
        if (rightlegReverseTarget == null) missing.Add("rightlegReverseTarget");
        if (hipTargetRig == null) missing.Add("hipTargetRig");
        if (spineRootTargetRig == null) missing.Add("spineRootTargetRig");
        if (spineTipTargetRig == null) missing.Add("spineTipTargetRig");
        if (rightLegTargetRig == null) missing.Add("rightLegTargetRig");
        if (leftLegTargetRig == null) missing.Add("leftLegTargetRig");

        if (missing.Count > 0)
        {
            Debug.LogError("BikeAnimationController on '" + name + "': " + missing.Count +
                           " rig reference(s) not assigned - " + string.Join(", ", missing), this);
            enabled = false;
            return;
        }

        leftLegHint_InitalPos = leftLegHintRig.localPosition;
        rightLegHint_InitalPos = rightLegHintRig.localPosition;

        InitializeRigTargets();

        leftlegReverseTargetInitialPos = leftlegReverseTarget.localPosition;
        rightlegReverseTargetInitialPos = rightlegReverseTarget.localPosition;
    }

    void InitializeRigTargets()
    {
        hipTargetRig.localPosition = hipIdleTarget.localPosition;
        hipTargetRig.localRotation = hipIdleTarget.localRotation;

        spineRootTargetRig.localRotation = spineIdleTarget.localRotation;
        spineTipTargetRig.localRotation = spineIdleTarget.localRotation;

        rightLegTargetRig.localPosition = rightlegIdleTarget.localPosition;
        leftLegTargetRig.localPosition = leftlegIdleTarget.localPosition;
    }

    void Update()
    {
        currentSpeed = BikeVelocity.magnitude * Mathf.Sign(BikeVelocity.z);
        currentLeanAngle = BikeLeanAngle;

        PushIdleLeanWeight();
        HandleLeaning();
        HandleHipTargets();
        HandleSpineTargets();
        HandleLegTargets();
        HandleHeadLookAt();
        KeepHandsOnHandlebars();
        if (!externalKneeControl) HandleKneesOffset();
        if (currentSpeed < -0.5f && !BikeIsBurningOut && useReverseLegAnimation)
            ReverseLegAnimation();
        else
            RestReverseLegTargets();
    }

    // ReverseLegAnimation writes these shared transforms every frame. Left wherever the stride
    // stopped, a save bakes that frame in, and the next Start reads it as the new baseline, so the
    // targets walk further from the bike every session until the leg cannot reach them
    void RestReverseLegTargets()
    {
        if (leftlegReverseTarget != null) leftlegReverseTarget.localPosition = leftlegReverseTargetInitialPos;
        if (rightlegReverseTarget != null) rightlegReverseTarget.localPosition = rightlegReverseTargetInitialPos;
    }

    // Claimed by RiderLeanSplay when it drives the knees, so the two do not write the same
    // hints in one frame with the later execution order silently winning
    [System.NonSerialized] public bool externalKneeControl;

    // A forced riding pose does not move the bike, so its standstill tilt has to be suppressed
    // from here or it stays leaned over while the rider is posed for speed
    void PushIdleLeanWeight()
    {
        float weight = forcedSpeedBlend >= 0f ? 0f : 1f;
        if (bikeControllerRhiannon != null) bikeControllerRhiannon.idleLeanWeight = weight;
        if (bikeAIRhiannon != null) bikeAIRhiannon.idleLeanWeight = weight;
    }

    float leanLerp = 0.0f;

    void HandleLeaning()
    {
        if (Mathf.Abs(BikeSteerInput) > 0)
        {
            leanLerp = Mathf.Lerp(leanLerp, 1, Time.deltaTime * leanSpeed);
        }
        else
        {
            leanLerp = Mathf.Lerp(leanLerp, 0, Time.deltaTime * leanSpeed);
        }

        float leanAngle = currentLeanAngle;
        float posX = (-leanAngle / BikeMaxLeanAngle) * maxHipPosOffset;

        leanPosOffsetForHip = new Vector3(posX, -Mathf.Abs(posX * leanHipSinkRatio), 0);

        float RotZ_Hip = (leanAngle / BikeMaxLeanAngle) * maxHipRotOffset * leanLerp;

        burnoutPoseBlend = Mathf.MoveTowards(burnoutPoseBlend, BikeIsBurningOut ? 1f : 0f,
            Time.deltaTime * burnoutPoseBlendSpeed);

        leanRotOffsetForHip = new Vector3(burnoutHipUprightAngle * burnoutPoseBlend, 0, RotZ_Hip);

        float RotZ_spine = (leanAngle / BikeMaxLeanAngle) * maxSpineRotOffset;

        leanRotOffsetForSpine = new Vector3(burnoutSpineUprightAngle * burnoutPoseBlend, 0, RotZ_spine);
    }

    void HandleHipTargets()
    {
        if (!BikeIsGrounded)
        {
            TransitionToTarget(hipTargetRig, hipInAirTarget, leanPosOffsetForHip, leanRotOffsetForHip);
            return;
        }

        if (currentSpeed < normalSpeedThreshold)
        {
            if (currentSpeed < -0.5f && !BikeIsBurningOut && useReverseLegAnimation)
            {
                TransitionToTarget(hipTargetRig, hipReverseTarget, leanPosOffsetForHip, leanRotOffsetForHip);
            }
            else
            {
                TransitionToTarget(hipTargetRig, hipIdleTarget, leanPosOffsetForHip, leanRotOffsetForHip);
            }
        }
        else
        {
            TransitionToBlend(hipTargetRig, hipNormalSpeedTarget, hipHighSpeedTarget, SpeedBlend01,
                leanPosOffsetForHip, leanRotOffsetForHip);
        }
    }

    void HandleSpineTargets()
    {
        if (currentSpeed < normalSpeedThreshold)
        {
            if (currentSpeed < -0.5f && !BikeIsBurningOut && useReverseLegAnimation)
            {
                TransitionToTarget(spineRootTargetRig, spineReverseTarget, Vector3.zero, leanRotOffsetForSpine);
                TransitionToTarget(spineTipTargetRig, spineReverseTarget, Vector3.zero,
                    new Vector3(spineTipOffset, 0, 0) + leanRotOffsetForSpine);
            }
            else
            {
                TransitionToTarget(spineRootTargetRig, spineIdleTarget, Vector3.zero, leanRotOffsetForSpine);
                TransitionToTarget(spineTipTargetRig, spineIdleTarget, Vector3.zero,
                    new Vector3(spineTipOffset, 0, 0) + leanRotOffsetForSpine);
            }
        }
        else
        {
            TransitionToBlend(spineRootTargetRig, spineNormalSpeedTarget, spineHighSpeedTarget, SpeedBlend01,
                Vector3.zero, leanRotOffsetForSpine);
            TransitionToBlend(spineTipTargetRig, spineNormalSpeedTarget, spineHighSpeedTarget, SpeedBlend01,
                Vector3.zero, new Vector3(spineTipOffset, 0, 0) + leanRotOffsetForSpine);
        }
    }

    void HandleLegTargets()
    {
        if (currentSpeed < normalSpeedThreshold && BikeIsGrounded)
        {
            if (currentSpeed < -0.5f && !BikeIsBurningOut && useReverseLegAnimation)
            {
                TransitionToTarget(rightLegTargetRig, rightlegReverseTarget, Vector3.zero, Vector3.zero);
                TransitionToTarget(leftLegTargetRig, leftlegReverseTarget, Vector3.zero, Vector3.zero);
            }
            else
            {
                TransitionToTarget(rightLegTargetRig, rightlegIdleTarget, Vector3.zero, Vector3.zero);
                TransitionToTarget(leftLegTargetRig, leftlegIdleTarget, Vector3.zero, Vector3.zero);
            }
        }
        else
        {
            TransitionToTarget(rightLegTargetRig, rightlegInMotionTarget, Vector3.zero, Vector3.zero);
            TransitionToTarget(leftLegTargetRig, leftlegInMotionTarget, Vector3.zero, Vector3.zero);
        }
    }

    void ReverseLegAnimation()
    {
        float time = Time.time * reverseStepSpeed;

        float leftLegY = leftlegReverseTargetInitialPos.y + Mathf.Clamp01(Mathf.Sin(time)) * reverseStepHeight;
        float rightLegY = rightlegReverseTargetInitialPos.y +
                          Mathf.Clamp01(Mathf.Sin(time + Mathf.PI)) * reverseStepHeight; // Opposite phase

        float leftLegZ = leftlegReverseTargetInitialPos.z + Mathf.Cos(time) * reverseStepDistance;
        float rightLegZ =
            rightlegReverseTargetInitialPos.z + Mathf.Cos(time + Mathf.PI) * reverseStepDistance; // Opposite phase

        leftlegReverseTarget.localPosition = new Vector3(leftlegReverseTargetInitialPos.x, leftLegY, leftLegZ);
        rightlegReverseTarget.localPosition = new Vector3(rightlegReverseTargetInitialPos.x, rightLegY, rightLegZ);
    }


    void HandleHeadLookAt()
    {
        // Implement head look-at logic here if needed
    }

    void HandleKneesOffset()
    {
        float leanFactor = Mathf.Abs(currentLeanAngle / BikeMaxLeanAngle);
        float posX = leanFactor * maxKneeOffset;

        Vector3 LeftLegHintoffset = leftLegHint_InitalPos + new Vector3(-posX, 0, 0);
        Vector3 RightLegHintoffset = rightLegHint_InitalPos + new Vector3(posX, 0, 0);

        Vector3 leftLegHintPos = Vector3.Lerp(leftLegHint_InitalPos, LeftLegHintoffset, leanFactor);
        Vector3 rightLegHintPos = Vector3.Lerp(rightLegHint_InitalPos, RightLegHintoffset, leanFactor);

        leftLegHintRig.localPosition =
            Vector3.Lerp(leftLegHintRig.localPosition, leftLegHintPos, Time.deltaTime * leanSpeed);
        rightLegHintRig.localPosition =
            Vector3.Lerp(rightLegHintRig.localPosition, rightLegHintPos, Time.deltaTime * leanSpeed);
    }

    // Set by the Live Pose Tuner to pin one end of the blend; thresholds alone cannot express
    // "fully tucked" once the pose is continuous rather than branched. Negative means off.
    [System.NonSerialized] public float forcedSpeedBlend = -1f;

    float SpeedBlend01
    {
        get
        {
            if (forcedSpeedBlend >= 0f) return Mathf.Clamp01(forcedSpeedBlend);
            // A collapsed range saturates rather than reading as 0, which would show the normal pose
            if (highSpeedThreshold <= normalSpeedThreshold) return currentSpeed >= highSpeedThreshold ? 1f : 0f;
            return Mathf.Clamp01(Mathf.InverseLerp(normalSpeedThreshold, highSpeedThreshold, currentSpeed));
        }
    }

    void TransitionToBlend(Transform target, Transform from, Transform to, float blend,
        Vector3 positionOffset, Vector3 rotationOffset)
    {
        Vector3 desiredPosition = Vector3.Lerp(from.TransformPoint(positionOffset),
            to.TransformPoint(positionOffset), blend);
        Quaternion desiredRotation = Quaternion.Slerp(from.rotation, to.rotation, blend)
                                     * Quaternion.Euler(rotationOffset);

        target.position = Vector3.Lerp(target.position, desiredPosition, Time.deltaTime * transitionSpeed);
        target.rotation = Quaternion.Slerp(target.rotation, desiredRotation, Time.deltaTime * transitionSpeed);
    }

    void TransitionToTarget(Transform target, Transform desiredTransform, Vector3 PositionOffset,
        Vector3 RotationOffset)
    {
        // Calculate the target position with the offset in local space
        Vector3 targetPosition = desiredTransform.TransformPoint(PositionOffset);

        // Smoothly interpolate the target position to the desired position with the offset
        target.position = Vector3.Lerp(target.position, targetPosition, Time.deltaTime * transitionSpeed);

        // Calculate the target rotation with the rotation offset in local space
        Quaternion desiredRotationWithOffset = desiredTransform.rotation * Quaternion.Euler(RotationOffset);

        // Smoothly interpolate the target rotation to the desired rotation with the offset
        target.rotation =
            Quaternion.Slerp(target.rotation, desiredRotationWithOffset, Time.deltaTime * transitionSpeed);
    }

    // Hands stay on the grips while reversing too: only the legs paddle
    void KeepHandsOnHandlebars()
    {
        rightHandTargetRig.position = rightHandTarget.position;
        leftHandTargetRig.position = leftHandTarget.position;
        rightHandTargetRig.rotation = rightHandTarget.rotation;
        leftHandTargetRig.rotation = leftHandTarget.rotation;
    }
}
}

