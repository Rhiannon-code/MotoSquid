using UnityEngine;
using UnityEditor;
using UnityEngine.Animations.Rigging;


namespace ArcadeBP_Pro
{
    public class BikerAnimationCreatorPro : EditorWindow
    {
        private Animator targetAnimator;

        private GameObject BikerRig;
        private ArcadeBikeControllerPro bikeController;

        private Transform hip;
        private Transform spineRoot;
        private Transform spineTip;
        private Transform rightFoot;
        private Transform rightLowerLeg;
        private Transform rightUpperLeg;
        private Transform leftFoot;
        private Transform leftLowerLeg;
        private Transform leftUpperLeg;
        private Transform rightHand;
        private Transform rightLowerArm;
        private Transform rightUpperArm;
        private Transform leftHand;
        private Transform leftLowerArm;
        private Transform leftUpperArm;
        private Transform head;

        [MenuItem("Tools/Ash Tools/Arcade Bike Physics Pro/Biker Animation Creator")]
        public static void ShowWindow()
        {
            GetWindow(typeof(BikerAnimationCreatorPro));
        }

        private void OnGUI()
        {
            GUILayout.Label("Biker Animation Creator", EditorStyles.boldLabel);
            targetAnimator = (Animator)EditorGUILayout.ObjectField("Target Animator", targetAnimator, typeof(Animator), true);

            bikeController = (ArcadeBikeControllerPro)EditorGUILayout.ObjectField("Arcade Bike Controller", bikeController, typeof(ArcadeBikeControllerPro), true);


            if (GUILayout.Button("Build Rig"))
            {
                BuildRig();
            }
        }

        private void BuildRig()
        {
            GetReferences();
            ApplyReferences();
        }

        private void ApplyReferences()
        {
            RigBuilder rigBuilder;
            if (targetAnimator.gameObject.GetComponent<RigBuilder>() == null)
            {
                rigBuilder = targetAnimator.gameObject.AddComponent<RigBuilder>();
            }
            else
            {
                rigBuilder = targetAnimator.gameObject.GetComponent<RigBuilder>();
            }

            BikerRig = (GameObject)Resources.Load("Biker Rig");

            GameObject tempControlRig = Instantiate(BikerRig, targetAnimator.transform);

            Rig rig = tempControlRig.GetComponent<Rig>();
            rigBuilder.layers.Clear();
            rigBuilder.layers.Add(new RigLayer(rig, true));


            //references
            BikerRigReferences biker_rigs = tempControlRig.GetComponent<BikerRigReferences>();

            //hip Rig
            biker_rigs.hipRig.data.constrainedObject = hip;

            //Spine Rig
            biker_rigs.spineRootRig.data.constrainedObject = spineRoot;
            biker_rigs.spineTipRig.data.constrainedObject = spineTip;

            //leg rig
            //Right Leg Rig
            biker_rigs.RightLegRig.data.tip = rightFoot;
            biker_rigs.RightLegRig.data.mid = rightLowerLeg;
            biker_rigs.RightLegRig.data.root = rightUpperLeg;
            //left Leg Rig
            biker_rigs.LeftLegRig.data.tip = leftFoot;
            biker_rigs.LeftLegRig.data.mid = leftLowerLeg;
            biker_rigs.LeftLegRig.data.root = leftUpperLeg;

            //Hand Rig
            //Right Hand Rig
            biker_rigs.RightHandRig.data.tip = rightHand;
            biker_rigs.RightHandRig.data.mid = rightLowerArm;
            biker_rigs.RightHandRig.data.root = rightUpperArm;
            //left Hand Rig
            biker_rigs.LeftHandRig.data.tip = leftHand;
            biker_rigs.LeftHandRig.data.mid = leftLowerArm;
            biker_rigs.LeftHandRig.data.root = leftUpperArm;

            //Head Aim Rig
            biker_rigs.headRig.data.constrainedObject = head;



            setRigTargetTransforms(biker_rigs);

            AddBikerAnimationControllerSetup(biker_rigs);

            targetAnimator.transform.parent = bikeController.bikeReferences.BikeModel;
            targetAnimator.transform.localPosition = Vector3.zero;
            targetAnimator.transform.localRotation = Quaternion.identity;

            targetAnimator.runtimeAnimatorController = (RuntimeAnimatorController)Resources.Load("Biker Animation Controller");
            targetAnimator.applyRootMotion = false;

        }

        private void setRigTargetTransforms(BikerRigReferences rig_References)
        {
            // hip rig
            rig_References.hipRig.data.sourceObjects[0].transform.position = rig_References.hipRig.data.constrainedObject.position;

            //spine rig
            rig_References.spineRootRig.data.sourceObjects[0].transform.position = rig_References.spineRootRig.data.constrainedObject.position;
            rig_References.spineTipRig.data.sourceObjects[0].transform.position = rig_References.spineTipRig.data.constrainedObject.position;
            rig_References.spineRootRig.data.sourceObjects[0].transform.localRotation = Quaternion.identity;
            rig_References.spineTipRig.data.sourceObjects[0].transform.localRotation = Quaternion.identity;
            //rig_References.spineRig.data.rootTarget.rotation = rig_References.spineRig.data.root.rotation;
            //rig_References.spineRig.data.tipTarget.rotation = rig_References.spineRig.data.tip.rotation;

            //leg rig
            //Right Leg Rig
            rig_References.RightLegRig.data.target.position = rig_References.RightLegRig.data.tip.position;
            //left Leg Rig
            rig_References.LeftLegRig.data.target.position = rig_References.LeftLegRig.data.tip.position;

            //Hand Rig
            //Right Hand Rig
            rig_References.RightHandRig.data.target.position = rig_References.RightHandRig.data.tip.position;
            //left Hand Rig
            rig_References.LeftHandRig.data.target.position = rig_References.LeftHandRig.data.tip.position;

            //Head Aim Rig
            rig_References.headRig.data.sourceObjects[0].transform.transform.position = bikeController.bikeReferences.FrontWheelParent.position + 2*bikeController.transform.forward;

        }

        private void GetReferences()
        {
            hip = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            spineRoot = targetAnimator.GetBoneTransform(HumanBodyBones.Spine);
            spineTip = targetAnimator.GetBoneTransform(HumanBodyBones.Chest);
            rightFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            rightLowerLeg = targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightUpperLeg = targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            leftLowerLeg = targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftUpperLeg = targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightHand = targetAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            rightLowerArm = targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightUpperArm = targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftHand = targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            leftLowerArm = targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftUpperArm = targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            head = targetAnimator.GetBoneTransform(HumanBodyBones.Head);

        }


        private void AddBikerAnimationControllerSetup(BikerRigReferences biker_rigs)
        {
            BikerAnimationController bikerAnimationController = targetAnimator.gameObject.AddComponent<BikerAnimationController>();
            bikerAnimationController.bikeController = bikeController;


            //set up the rig references

            bikerAnimationController.hipTarget_rig = biker_rigs.hipTarget;

            bikerAnimationController.spineRootTarget_rig = biker_rigs.spineRootTarget;
            bikerAnimationController.spineTipTarget_rig = biker_rigs.spineTipTarget;

            bikerAnimationController.rightLegTarget_rig = biker_rigs.rightLegTarget;
            bikerAnimationController.rightLegHint_rig = biker_rigs.rightLegHint;

            bikerAnimationController.leftLegTarget_rig = biker_rigs.leftLegTarget;
            bikerAnimationController.leftLegHint_rig = biker_rigs.leftLegHint;

            bikerAnimationController.rightHandTarget_rig = biker_rigs.rightHandTarget;
            bikerAnimationController.leftHandTarget_rig = biker_rigs.leftHandTarget;

            bikerAnimationController.headLookAtTarget_rig = biker_rigs.headLookAtTarget;


            //set up the animation targets

            var bikerAnimationTargets = bikeController.bikeReferences.BikerAnimationTargets;

            bikerAnimationController.hipIdleTarget = bikerAnimationTargets.hipIdleTarget;
            bikerAnimationController.hipNormalSpeedTarget = bikerAnimationTargets.hipNormalSpeedTarget;
            bikerAnimationController.hipHighSpeedTarget = bikerAnimationTargets.hipHighSpeedTarget;
            bikerAnimationController.hipInAirTarget = bikerAnimationTargets.hipInAirTarget;
            bikerAnimationController.hipReverseTarget = bikerAnimationTargets.hipReverseTarget;

            bikerAnimationController.spineIdleTarget = bikerAnimationTargets.spineIdleTarget;
            bikerAnimationController.spineNormalSpeedTarget = bikerAnimationTargets.spineNormalSpeedTarget;
            bikerAnimationController.spineHighSpeedTarget = bikerAnimationTargets.spineHighSpeedTarget;
            bikerAnimationController.spineReverseTarget = bikerAnimationTargets.spineReverseTarget;

            bikerAnimationController.leftlegIdleTarget = bikerAnimationTargets.leftlegIdleTarget;
            bikerAnimationController.leftlegInMotionTarget = bikerAnimationTargets.leftlegInMotionTarget;
            bikerAnimationController.leftlegReverseTarget = bikerAnimationTargets.leftlegReverseTarget;

            bikerAnimationController.rightlegIdleTarget = bikerAnimationTargets.rightlegIdleTarget;
            bikerAnimationController.rightlegInMotionTarget = bikerAnimationTargets.rightlegInMotionTarget;
            bikerAnimationController.rightlegReverseTarget = bikerAnimationTargets.rightlegReverseTarget;

            bikerAnimationController.leftHandTarget = bikerAnimationTargets.leftHandTarget;
            bikerAnimationController.rightHandTarget = bikerAnimationTargets.rightHandTarget;
            bikerAnimationController.rightHandReverseTarget = bikerAnimationTargets.rightHandReverseTarget;
            bikerAnimationController.leftHandReverseTarget = bikerAnimationTargets.leftHandReverseTarget;


            configureBikerAnimationController(bikerAnimationController, biker_rigs);
        }


        void configureBikerAnimationController(BikerAnimationController bikerAnimationController, BikerRigReferences biker_rigs)
        {
            if (bikeController.gameObject.GetComponentInChildren<BikerAnimationController>() != null)
            {
                BikerAnimationController present_bac = bikeController.gameObject.GetComponentInChildren<BikerAnimationController>();

                //match variables
                bikerAnimationController.maxHipPosOffset = present_bac.maxHipPosOffset;
                bikerAnimationController.maxHipRotOffset = present_bac.maxHipRotOffset;
                bikerAnimationController.maxSpineRotOffset = present_bac.maxSpineRotOffset;
                bikerAnimationController.maxKneeOffset = present_bac.maxKneeOffset;
                bikerAnimationController.spineTipOffset = present_bac.spineTipOffset;
                bikerAnimationController.normalSpeedThreshold = present_bac.normalSpeedThreshold;
                bikerAnimationController.highSpeedThreshold = present_bac.highSpeedThreshold;
                bikerAnimationController.leanSpeed = present_bac.leanSpeed;
                bikerAnimationController.transitionSpeed = present_bac.transitionSpeed;
                bikerAnimationController.useReverseLegAnimation = present_bac.useReverseLegAnimation;
                bikerAnimationController.reverseStepDistance = present_bac.reverseStepDistance;
                bikerAnimationController.reverseStepHeight = present_bac.reverseStepHeight;
                bikerAnimationController.reverseStepSpeed = present_bac.reverseStepSpeed;

            }

            if (bikeController.gameObject.GetComponentInChildren<BikerRigReferences>() != null)
            {
                BikerRigReferences present_bir = bikeController.gameObject.GetComponentInChildren<BikerRigReferences>();

                //match hint positions

                biker_rigs.leftLegHint.localPosition = present_bir.leftLegHint.localPosition;
                biker_rigs.rightLegHint.localPosition = present_bir.rightLegHint.localPosition;

                biker_rigs.leftHandHint.localPosition = present_bir.leftHandHint.localPosition;
                biker_rigs.rightHandHint.localPosition = present_bir.rightHandHint.localPosition;
            }

        }

    }

}
