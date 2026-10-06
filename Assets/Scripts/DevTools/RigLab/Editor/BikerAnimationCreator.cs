using MotoSquid.Bike;
using MotoSquid.Rider;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations.Rigging;
using Object = UnityEngine.Object;


namespace MotoSquid.DevTools
{
    public class BikerAnimationCreator : EditorWindow
    {
        private Animator targetAnimator;

        private GameObject BikerRig;
        private BikeController bikeController;

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

        [MenuItem("Tools/Rig Lab/2d. Biker Animation Creator (vendor rig builder)")]
        public static void ShowWindow()
        {
            GetWindow(typeof(BikerAnimationCreator));
        }

        private void OnGUI()
        {
            GUILayout.Label("Biker Animation Creator", EditorStyles.boldLabel);
            targetAnimator = (Animator)EditorGUILayout.ObjectField("Target Animator", targetAnimator, typeof(Animator), true);

            bikeController = (BikeController)EditorGUILayout.ObjectField("Arcade Bike Controller", bikeController, typeof(BikeController), true);


            if (GUILayout.Button("Build Rig"))
            {
                BuildRig();
            }
        }

        public static bool Build(Animator animator, BikeController bike)
        {
            if (animator == null || bike == null)
            {
                Debug.LogError("Rig Lab: rig build needs both a rider Animator and a bike.");
                return false;
            }

            var window = CreateInstance<BikerAnimationCreator>();
            try
            {
                window.targetAnimator = animator;
                window.bikeController = bike;
                window.BuildRig();
                return true;
            }
            finally { DestroyImmediate(window); }
        }

        static RuntimeAnimatorController FindRideController()
        {
            var c = FindAsset<RuntimeAnimatorController>("Biker Animation Controller", "AnimatorController");
            if (c == null)
                Debug.LogError("Rig Lab: 'Biker Animation Controller' not found. Animation Rigging only " +
                               "evaluates while the Animator is playing something, so the rig will not run.");
            return c;
        }

        static T FindAsset<T>(string name, string type) where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets("\"" + name + "\" t:" + type))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null && a.name == name) return a;
            }
            return null;
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

            BikerRig = FindAsset<GameObject>("Biker Rig", "Prefab");

            GameObject tempControlRig = Instantiate(BikerRig, targetAnimator.transform);

            Rig rig = tempControlRig.GetComponent<Rig>();
            rigBuilder.layers.Clear();
            rigBuilder.layers.Add(new RigLayer(rig, true));


            BikerRigReferences biker_rigs = tempControlRig.GetComponent<BikerRigReferences>();

            biker_rigs.hipRig.data.constrainedObject = hip;
            biker_rigs.spineRootRig.data.constrainedObject = spineRoot;
            biker_rigs.spineTipRig.data.constrainedObject = spineTip;
            biker_rigs.RightLegRig.data.tip = rightFoot;
            biker_rigs.RightLegRig.data.mid = rightLowerLeg;
            biker_rigs.RightLegRig.data.root = rightUpperLeg;
            biker_rigs.LeftLegRig.data.tip = leftFoot;
            biker_rigs.LeftLegRig.data.mid = leftLowerLeg;
            biker_rigs.LeftLegRig.data.root = leftUpperLeg;
            biker_rigs.RightHandRig.data.tip = rightHand;
            biker_rigs.RightHandRig.data.mid = rightLowerArm;
            biker_rigs.RightHandRig.data.root = rightUpperArm;
            biker_rigs.LeftHandRig.data.tip = leftHand;
            biker_rigs.LeftHandRig.data.mid = leftLowerArm;
            biker_rigs.LeftHandRig.data.root = leftUpperArm;
            biker_rigs.headRig.data.constrainedObject = head;

            setRigTargetTransforms(biker_rigs);
            AddBikerAnimationControllerSetup(biker_rigs);

            targetAnimator.transform.parent = bikeController.bikeReferences.BikeModel;
            targetAnimator.transform.localPosition = Vector3.zero;
            targetAnimator.transform.localRotation = Quaternion.identity;
            targetAnimator.runtimeAnimatorController = FindRideController();
            targetAnimator.applyRootMotion = false;

        }

        private void setRigTargetTransforms(BikerRigReferences rig_References)
        {
            rig_References.hipRig.data.sourceObjects[0].transform.position = rig_References.hipRig.data.constrainedObject.position;
            rig_References.spineRootRig.data.sourceObjects[0].transform.position = rig_References.spineRootRig.data.constrainedObject.position;
            rig_References.spineTipRig.data.sourceObjects[0].transform.position = rig_References.spineTipRig.data.constrainedObject.position;
            rig_References.spineRootRig.data.sourceObjects[0].transform.localRotation = Quaternion.identity;
            rig_References.spineTipRig.data.sourceObjects[0].transform.localRotation = Quaternion.identity;
            rig_References.RightLegRig.data.target.position = rig_References.RightLegRig.data.tip.position;
            rig_References.LeftLegRig.data.target.position = rig_References.LeftLegRig.data.tip.position;
            rig_References.RightHandRig.data.target.position = rig_References.RightHandRig.data.tip.position;
            rig_References.LeftHandRig.data.target.position = rig_References.LeftHandRig.data.tip.position;
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
            BikeAnimationController bikerAnimationController = targetAnimator.gameObject.AddComponent<BikeAnimationController>();
            bikerAnimationController.bikeControllerRhiannon = bikeController;
            bikerAnimationController.hipTargetRig = biker_rigs.hipTarget;
            bikerAnimationController.spineRootTargetRig = biker_rigs.spineRootTarget;
            bikerAnimationController.spineTipTargetRig = biker_rigs.spineTipTarget;
            bikerAnimationController.rightLegTargetRig = biker_rigs.rightLegTarget;
            bikerAnimationController.rightLegHintRig = biker_rigs.rightLegHint;
            bikerAnimationController.leftLegTargetRig = biker_rigs.leftLegTarget;
            bikerAnimationController.leftLegHintRig = biker_rigs.leftLegHint;
            bikerAnimationController.rightHandTargetRig = biker_rigs.rightHandTarget;
            bikerAnimationController.leftHandTargetRig = biker_rigs.leftHandTarget;
            bikerAnimationController.headLookAtTargetRig = biker_rigs.headLookAtTarget;

            var bikerAnimationTargets = bikeController.bikeReferences.bikeAnimationTargets;

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


        void configureBikerAnimationController(BikeAnimationController bikerAnimationController, BikerRigReferences biker_rigs)
        {
            if (bikeController.gameObject.GetComponentInChildren<BikeAnimationController>() != null)
            {
                BikeAnimationController present_bac = bikeController.gameObject.GetComponentInChildren<BikeAnimationController>();

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

                biker_rigs.leftLegHint.localPosition = present_bir.leftLegHint.localPosition;
                biker_rigs.rightLegHint.localPosition = present_bir.rightLegHint.localPosition;
                biker_rigs.leftHandHint.localPosition = present_bir.leftHandHint.localPosition;
                biker_rigs.rightHandHint.localPosition = present_bir.rightHandHint.localPosition;
            }

        }

    }

}
