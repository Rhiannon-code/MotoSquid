using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArcadeBP_Pro
{
    public class RagdollSetupTool : EditorWindow
    {
        private const string GeneratedColliderRootName = "RagdollColliders";
        private const float MinimumSegmentLength = 0.0001f;

        public RagdollReference sourceRagdoll;
        public Animator targetAnimator;

        [MenuItem("Tools/Ash Tools/Arcade Bike Physics Pro/Ragdoll Setup Tool")]
        public static void ShowWindow()
        {
            GetWindow<RagdollSetupTool>("Ragdoll Setup Tool");
        }

        private void OnGUI()
        {
            GUILayout.Label("Ragdoll Setup", EditorStyles.boldLabel);

            sourceRagdoll = (RagdollReference)EditorGUILayout.ObjectField("Source Ragdoll", sourceRagdoll, typeof(RagdollReference), true);
            targetAnimator = (Animator)EditorGUILayout.ObjectField("Target Animator", targetAnimator, typeof(Animator), true);

            if (GUILayout.Button("Apply Ragdoll Configuration"))
            {
                if (sourceRagdoll == null || targetAnimator == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign both Source Ragdoll and Target Animator.", "OK");
                    return;
                }

                ApplyRagdollConfiguration(sourceRagdoll, targetAnimator);
            }
        }

        private void ApplyRagdollConfiguration(RagdollReference source, Animator animator)
        {
            if (!TryBuildBindings(source, animator, out List<PartBinding> bindings, out string validationError))
            {
                EditorUtility.DisplayDialog("Ragdoll Setup", validationError, "OK");
                return;
            }

            bool replaceExisting = HasExistingRagdoll(bindings, animator);
            if (replaceExisting && !EditorUtility.DisplayDialog(
                    "Replace Existing Ragdoll?",
                    "The target already contains ragdoll physics. Replacing it removes Rigidbody, CharacterJoint, and Collider components owned by the mapped Humanoid bones, then regenerates them.",
                    "Replace",
                    "Cancel"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Ragdoll Configuration");

            try
            {
                if (replaceExisting)
                {
                    RemoveExistingRagdoll(bindings);
                }

                RenameTargetRoot(animator);

                Dictionary<Rigidbody, Rigidbody> rigidbodyMap = CreateRigidbodiesAndColliders(bindings);
                CreateJoints(bindings, rigidbodyMap);
                ConfigureTargetReferences(source, animator, rigidbodyMap);
                EnsureAdjustmentTool(animator);

                if (animator.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);
                }

                Undo.CollapseUndoOperations(undoGroup);
                EditorUtility.DisplayDialog("Success", "Ragdoll configuration applied successfully.", "OK");
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Ragdoll Setup Failed", exception.Message, "OK");
            }
        }

        private static bool TryBuildBindings(RagdollReference source, Animator animator, out List<PartBinding> bindings, out string error)
        {
            bindings = new List<PartBinding>(15);
            List<string> problems = new List<string>();

            ValidateTargetContext(source, animator, problems);
            ValidateSourceReferences(source, problems);

            if (problems.Count > 0)
            {
                error = string.Join("\n", problems);
                return false;
            }

            TargetSkeleton target = new TargetSkeleton(animator);
            target.Validate(problems);

            if (problems.Count > 0)
            {
                error = string.Join("\n", problems);
                return false;
            }

            CalculateBodyAxes(
                source.transform,
                source.leftUpperArm.transform,
                source.rightUpperArm.transform,
                source.hips.transform,
                source.chest.transform,
                out Vector3 sourceForward,
                out Vector3 sourceRight);

            CalculateBodyAxes(
                animator.transform,
                target.leftUpperArm,
                target.rightUpperArm,
                target.hips,
                target.chest,
                out Vector3 targetForward,
                out Vector3 targetRight);
            AddBinding(bindings, problems, "Hips", source.hips, target.hips, source.chest.transform.position - source.hips.transform.position, target.chest.position - target.hips.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "LeftUpperLeg", source.leftUpperLeg, target.leftUpperLeg, source.leftLowerLeg.transform.position - source.leftUpperLeg.transform.position, target.leftLowerLeg.position - target.leftUpperLeg.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "LeftLowerLeg", source.leftLowerLeg, target.leftLowerLeg, source.leftFoot.transform.position - source.leftLowerLeg.transform.position, target.leftFoot.position - target.leftLowerLeg.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "LeftFoot", source.leftFoot, target.leftFoot, source.leftFoot.transform.position - source.leftLowerLeg.transform.position, target.leftFoot.position - target.leftLowerLeg.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "RightUpperLeg", source.rightUpperLeg, target.rightUpperLeg, source.rightLowerLeg.transform.position - source.rightUpperLeg.transform.position, target.rightLowerLeg.position - target.rightUpperLeg.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "RightLowerLeg", source.rightLowerLeg, target.rightLowerLeg, source.rightFoot.transform.position - source.rightLowerLeg.transform.position, target.rightFoot.position - target.rightLowerLeg.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "RightFoot", source.rightFoot, target.rightFoot, source.rightFoot.transform.position - source.rightLowerLeg.transform.position, target.rightFoot.position - target.rightLowerLeg.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "Chest", source.chest, target.chest, source.chest.transform.position - source.hips.transform.position, target.chest.position - target.hips.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "LeftUpperArm", source.leftUpperArm, target.leftUpperArm, source.leftLowerArm.transform.position - source.leftUpperArm.transform.position, target.leftLowerArm.position - target.leftUpperArm.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "LeftLowerArm", source.leftLowerArm, target.leftLowerArm, source.leftHand.transform.position - source.leftLowerArm.transform.position, target.leftHand.position - target.leftLowerArm.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "LeftHand", source.leftHand, target.leftHand, source.leftHand.transform.position - source.leftLowerArm.transform.position, target.leftHand.position - target.leftLowerArm.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "RightUpperArm", source.rightUpperArm, target.rightUpperArm, source.rightLowerArm.transform.position - source.rightUpperArm.transform.position, target.rightLowerArm.position - target.rightUpperArm.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "RightLowerArm", source.rightLowerArm, target.rightLowerArm, source.rightHand.transform.position - source.rightLowerArm.transform.position, target.rightHand.position - target.rightLowerArm.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "RightHand", source.rightHand, target.rightHand, source.rightHand.transform.position - source.rightLowerArm.transform.position, target.rightHand.position - target.rightLowerArm.position, sourceForward, targetForward, sourceRight, targetRight);
            AddBinding(bindings, problems, "Head", source.head, target.head, source.head.transform.position - source.chest.transform.position, target.head.position - target.chest.position, sourceForward, targetForward, sourceRight, targetRight);

            error = problems.Count == 0 ? string.Empty : string.Join("\n", problems);
            return problems.Count == 0;
        }

        private static void ValidateTargetContext(RagdollReference source, Animator animator, List<string> problems)
        {
            if (source.transform.root == animator.transform.root)
            {
                problems.Add("Source Ragdoll and Target Animator must belong to different hierarchies.");
            }

            if (animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
            {
                problems.Add("Target Animator must use a valid Humanoid Avatar.");
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && animator.gameObject.scene != prefabStage.scene)
            {
                problems.Add("The Target Animator is outside the currently open Prefab Stage.");
            }            else if (prefabStage == null && EditorUtility.IsPersistent(animator))
            {
                problems.Add("Open the target prefab in Prefab Mode or use a scene instance before applying the ragdoll.");
            }
        }

        private static void ValidateSourceReferences(RagdollReference source, List<string> problems)
        {
            RequireSource(source.hips, "Hips", problems);
            RequireSource(source.leftUpperLeg, "Left Upper Leg", problems);
            RequireSource(source.leftLowerLeg, "Left Lower Leg", problems);
            RequireSource(source.leftFoot, "Left Foot", problems);
            RequireSource(source.rightUpperLeg, "Right Upper Leg", problems);
            RequireSource(source.rightLowerLeg, "Right Lower Leg", problems);
            RequireSource(source.rightFoot, "Right Foot", problems);
            RequireSource(source.chest, "Chest", problems);
            RequireSource(source.leftUpperArm, "Left Upper Arm", problems);
            RequireSource(source.leftLowerArm, "Left Lower Arm", problems);
            RequireSource(source.leftHand, "Left Hand", problems);
            RequireSource(source.rightUpperArm, "Right Upper Arm", problems);
            RequireSource(source.rightLowerArm, "Right Lower Arm", problems);
            RequireSource(source.rightHand, "Right Hand", problems);
            RequireSource(source.head, "Head", problems);
        }

        private static void RequireSource(Rigidbody body, string label, List<string> problems)
        {
            if (body == null)
            {
                problems.Add($"Source Ragdoll is missing {label}.");
            }
        }

        private static void CalculateBodyAxes(Transform root, Transform leftShoulder, Transform rightShoulder, Transform hips, Transform chest, out Vector3 forward, out Vector3 right)
        {
            right = rightShoulder.position - leftShoulder.position;
            Vector3 up = chest.position - hips.position;

            if (right.sqrMagnitude < MinimumSegmentLength * MinimumSegmentLength)
            {
                right = root.right;
            }

            if (up.sqrMagnitude < MinimumSegmentLength * MinimumSegmentLength)
            {
                up = root.up;
            }

            right.Normalize();
            up.Normalize();
            forward = Vector3.Cross(right, up);

            if (forward.sqrMagnitude < MinimumSegmentLength * MinimumSegmentLength)
            {
                forward = root.forward;
            }
            else
            {
                forward.Normalize();
                if (Vector3.Dot(forward, root.forward) < 0f)
                {
                    forward = -forward;
                }
            }
        }

        private static void AddBinding(
            List<PartBinding> bindings,
            List<string> problems,
            string name,
            Rigidbody sourceBody,
            Transform targetBone,
            Vector3 sourceDirection,
            Vector3 targetDirection,
            Vector3 sourceForward,
            Vector3 targetForward,
            Vector3 sourceRight,
            Vector3 targetRight)
        {
            if (sourceDirection.magnitude < MinimumSegmentLength)
            {
                problems.Add($"{name} has a zero-length source segment.");
                return;
            }

            if (targetDirection.magnitude < MinimumSegmentLength)
            {
                problems.Add($"{name} has a zero-length target segment.");
                return;
            }

            bindings.Add(new PartBinding(
                name,
                sourceBody,
                targetBone,
                new AnatomicalFrame(sourceBody.transform.position, sourceDirection, sourceForward, sourceRight),
                new AnatomicalFrame(targetBone.position, targetDirection, targetForward, targetRight)));
        }

        private static bool HasExistingRagdoll(List<PartBinding> bindings, Animator animator)
        {
            if (animator.TryGetComponent(out RagdollReference _) || animator.TryGetComponent(out ColliderAdjustmentTool _))
            {
                return true;
            }

            foreach (PartBinding binding in bindings)
            {
                if (binding.targetBone.TryGetComponent(out Rigidbody _) ||
                    binding.targetBone.TryGetComponent(out CharacterJoint _) ||
                    binding.targetBone.TryGetComponent(out Collider _))
                {
                    return true;
                }

                for (int index = 0; index < binding.targetBone.childCount; index++)
                {
                    if (binding.targetBone.GetChild(index).name == GeneratedColliderRootName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void RemoveExistingRagdoll(List<PartBinding> bindings)
        {
            foreach (PartBinding binding in bindings)
            {
                RemoveOwnedColliders(binding.targetBone);
                DestroyComponents<CharacterJoint>(binding.targetBone.gameObject);
                DestroyComponents<Rigidbody>(binding.targetBone.gameObject);
                DestroyGeneratedColliderRoots(binding.targetBone);
            }
        }

        private static Rigidbody GetOwningRigidbody(Collider collider)
        {
            for (Transform current = collider.transform; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out Rigidbody body))
                {
                    return body;
                }
            }

            return null;
        }

        private static void RemoveOwnedColliders(Transform bone)
        {
            bone.TryGetComponent(out Rigidbody owner);
            Collider[] colliders = bone.GetComponentsInChildren<Collider>(true);

            foreach (Collider collider in colliders)
            {
                bool isOwned = owner != null ? GetOwningRigidbody(collider) == owner : collider.transform == bone;
                if (isOwned)
                {
                    Undo.DestroyObjectImmediate(collider);
                }
            }
        }

        private static void DestroyComponents<T>(GameObject gameObject) where T : Component
        {
            T[] components = gameObject.GetComponents<T>();
            foreach (T component in components)
            {
                Undo.DestroyObjectImmediate(component);
            }
        }

        private static void DestroyGeneratedColliderRoots(Transform bone)
        {
            for (int index = bone.childCount - 1; index >= 0; index--)
            {
                Transform child = bone.GetChild(index);
                if (child.name == GeneratedColliderRootName)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private static void RenameTargetRoot(Animator animator)
        {
            Transform root = animator.transform.root;
            if (root.name.StartsWith("Ragdoll_", StringComparison.Ordinal))
            {
                return;
            }

            Undo.RecordObject(root.gameObject, "Rename Ragdoll Root");
            root.name = $"Ragdoll_{root.name}";
            RecordPrefabModification(root.gameObject);
        }

        private static Dictionary<Rigidbody, Rigidbody> CreateRigidbodiesAndColliders(List<PartBinding> bindings)
        {
            Dictionary<Rigidbody, Rigidbody> rigidbodyMap = new Dictionary<Rigidbody, Rigidbody>(bindings.Count);

            foreach (PartBinding binding in bindings)
            {
                Rigidbody targetBody = Undo.AddComponent<Rigidbody>(binding.targetBone.gameObject);
                CopyRigidbody(binding.sourceBody, targetBody);
                rigidbodyMap.Add(binding.sourceBody, targetBody);

                Transform colliderRoot = CreateChild(binding.targetBone, GeneratedColliderRootName).transform;
                Collider[] sourceColliders = binding.sourceBody.GetComponentsInChildren<Collider>(true);
                int generatedIndex = 0;

                foreach (Collider sourceCollider in sourceColliders)
                {
                    if (GetOwningRigidbody(sourceCollider) != binding.sourceBody)
                    {
                        continue;
                    }

                    CreateRetargetedCollider(sourceCollider, binding, colliderRoot, generatedIndex);
                    generatedIndex++;
                }

                targetBody.ResetCenterOfMass();
                targetBody.ResetInertiaTensor();
                RecordPrefabModification(targetBody);
            }

            return rigidbodyMap;
        }

        private static void CopyRigidbody(Rigidbody source, Rigidbody target)
        {
            target.mass = source.mass;
            target.linearDamping = source.linearDamping;
            target.angularDamping = source.angularDamping;
            target.useGravity = source.useGravity;
            target.isKinematic = source.isKinematic;
            target.interpolation = source.interpolation;
            target.collisionDetectionMode = source.collisionDetectionMode;            target.constraints = source.constraints;
            target.detectCollisions = source.detectCollisions;
            target.maxAngularVelocity = source.maxAngularVelocity;
            target.maxDepenetrationVelocity = source.maxDepenetrationVelocity;
            target.sleepThreshold = source.sleepThreshold;
            target.solverIterations = source.solverIterations;
            target.solverVelocityIterations = source.solverVelocityIterations;
        }

        private static void CreateRetargetedCollider(Collider sourceCollider, PartBinding binding, Transform colliderRoot, int index)
        {
            string holderName = $"{binding.name}_RagdollCollider_{index}";
            Transform holder = CreateChild(colliderRoot, holderName).transform;
            float scaleRatio = binding.targetFrame.length / binding.sourceFrame.length;

            if (sourceCollider is BoxCollider sourceBox)
            {
                Vector3 sourceCenter = sourceBox.transform.TransformPoint(sourceBox.center);
                ConfigureHolder(holder, MapPoint(sourceCenter, binding), MapRotation(sourceBox.transform.rotation, binding), Vector3.one);

                BoxCollider targetBox = Undo.AddComponent<BoxCollider>(holder.gameObject);
                Vector3 sourceWorldSize = Vector3.Scale(sourceBox.size, Abs(sourceBox.transform.lossyScale));
                targetBox.center = Vector3.zero;
                targetBox.size = Divide(sourceWorldSize * scaleRatio, Abs(holder.lossyScale));
                CopyColliderProperties(sourceBox, targetBox);
                RecordPrefabModification(targetBox);
                return;
            }

            if (sourceCollider is SphereCollider sourceSphere)
            {
                Vector3 sourceCenter = sourceSphere.transform.TransformPoint(sourceSphere.center);
                ConfigureHolder(holder, MapPoint(sourceCenter, binding), MapRotation(sourceSphere.transform.rotation, binding), Vector3.one);

                SphereCollider targetSphere = Undo.AddComponent<SphereCollider>(holder.gameObject);
                float sourceWorldRadius = sourceSphere.radius * MaxAbsComponent(sourceSphere.transform.lossyScale);
                targetSphere.center = Vector3.zero;
                targetSphere.radius = sourceWorldRadius * scaleRatio / MaxAbsComponent(holder.lossyScale);
                CopyColliderProperties(sourceSphere, targetSphere);
                RecordPrefabModification(targetSphere);
                return;
            }

            if (sourceCollider is CapsuleCollider sourceCapsule)
            {
                Vector3 sourceCenter = sourceCapsule.transform.TransformPoint(sourceCapsule.center);
                Quaternion sourceShapeRotation = sourceCapsule.transform.rotation * CapsuleDirectionRotation(sourceCapsule.direction);
                ConfigureHolder(holder, MapPoint(sourceCenter, binding), MapRotation(sourceShapeRotation, binding), Vector3.one);

                CapsuleCollider targetCapsule = Undo.AddComponent<CapsuleCollider>(holder.gameObject);
                Vector3 sourceScale = Abs(sourceCapsule.transform.lossyScale);
                float sourceWorldHeight = sourceCapsule.height * GetAxisComponent(sourceScale, sourceCapsule.direction);
                float sourceWorldRadius = sourceCapsule.radius * GetPerpendicularScale(sourceScale, sourceCapsule.direction);
                Vector3 targetScale = Abs(holder.lossyScale);
                float targetRadiusScale = Mathf.Max(targetScale.x, targetScale.z);
                targetCapsule.center = Vector3.zero;
                targetCapsule.direction = 1;
                targetCapsule.radius = sourceWorldRadius * scaleRatio / Mathf.Max(targetRadiusScale, MinimumSegmentLength);
                targetCapsule.height = Mathf.Max(sourceWorldHeight * scaleRatio / Mathf.Max(targetScale.y, MinimumSegmentLength), targetCapsule.radius * 2f);
                CopyColliderProperties(sourceCapsule, targetCapsule);
                RecordPrefabModification(targetCapsule);
                return;
            }

            if (sourceCollider is MeshCollider sourceMesh)
            {
                Vector3 desiredWorldScale = Abs(sourceMesh.transform.lossyScale) * scaleRatio;
                ConfigureHolder(holder, MapPoint(sourceMesh.transform.position, binding), MapRotation(sourceMesh.transform.rotation, binding), desiredWorldScale);

                MeshCollider targetMesh = Undo.AddComponent<MeshCollider>(holder.gameObject);
                targetMesh.sharedMesh = sourceMesh.sharedMesh;
                targetMesh.convex = sourceMesh.convex;
                targetMesh.cookingOptions = sourceMesh.cookingOptions;
                CopyColliderProperties(sourceMesh, targetMesh);
                RecordPrefabModification(targetMesh);
                return;
            }

            Undo.DestroyObjectImmediate(holder.gameObject);            Debug.LogWarning($"Unsupported collider type {sourceCollider.GetType().Name} on {sourceCollider.name}.", sourceCollider);
        }

        private static void CopyColliderProperties(Collider source, Collider target)
        {
            target.enabled = source.enabled;
            target.isTrigger = source.isTrigger;
            target.sharedMaterial = source.sharedMaterial;
            target.contactOffset = source.contactOffset;
        }

        private static Quaternion CapsuleDirectionRotation(int direction)
        {
            switch (direction)
            {
                case 0:
                    return Quaternion.FromToRotation(Vector3.up, Vector3.right);
                case 2:
                    return Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                default:
                    return Quaternion.identity;
            }
        }

        private static float GetAxisComponent(Vector3 value, int direction)
        {
            switch (direction)
            {
                case 0:
                    return value.x;
                case 2:
                    return value.z;
                default:
                    return value.y;
            }
        }

        private static float GetPerpendicularScale(Vector3 value, int direction)
        {
            switch (direction)
            {
                case 0:
                    return Mathf.Max(value.y, value.z);
                case 2:
                    return Mathf.Max(value.x, value.y);
                default:
                    return Mathf.Max(value.x, value.z);
            }
        }

        private static Vector3 MapPoint(Vector3 sourceWorldPoint, PartBinding binding)
        {
            Vector3 sourceFramePoint = Quaternion.Inverse(binding.sourceFrame.rotation) * (sourceWorldPoint - binding.sourceFrame.origin);
            float scaleRatio = binding.targetFrame.length / binding.sourceFrame.length;
            return binding.targetFrame.origin + binding.targetFrame.rotation * (sourceFramePoint * scaleRatio);
        }

        private static Quaternion MapRotation(Quaternion sourceWorldRotation, PartBinding binding)
        {
            Quaternion frameRelativeRotation = Quaternion.Inverse(binding.sourceFrame.rotation) * sourceWorldRotation;
            return binding.targetFrame.rotation * frameRelativeRotation;
        }

        private static Vector3 MapDirection(Vector3 sourceWorldDirection, PartBinding binding)
        {
            Vector3 frameRelativeDirection = Quaternion.Inverse(binding.sourceFrame.rotation) * sourceWorldDirection;
            return (binding.targetFrame.rotation * frameRelativeDirection).normalized;
        }

        private static void ConfigureHolder(Transform holder, Vector3 worldPosition, Quaternion worldRotation, Vector3 desiredWorldScale)
        {
            holder.position = worldPosition;
            holder.rotation = worldRotation;
            holder.localScale = Vector3.one;
            Vector3 currentWorldScale = Abs(holder.lossyScale);
            holder.localScale = Divide(desiredWorldScale, currentWorldScale);
            RecordPrefabModification(holder);
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name)
            {
                layer = parent.gameObject.layer
            };

            Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
            Undo.SetTransformParent(child.transform, parent, $"Parent {name}");
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private static void CreateJoints(List<PartBinding> bindings, Dictionary<Rigidbody, Rigidbody> rigidbodyMap)
        {
            Dictionary<Rigidbody, PartBinding> bindingMap = new Dictionary<Rigidbody, PartBinding>(bindings.Count);
            foreach (PartBinding binding in bindings)
            {
                bindingMap.Add(binding.sourceBody, binding);
            }

            foreach (PartBinding binding in bindings)
            {
                if (!binding.sourceBody.TryGetComponent(out CharacterJoint sourceJoint))
                {
                    continue;
                }

                CharacterJoint targetJoint = Undo.AddComponent<CharacterJoint>(binding.targetBone.gameObject);
                targetJoint.anchor = binding.targetBone.InverseTransformPoint(MapPoint(sourceJoint.transform.TransformPoint(sourceJoint.anchor), binding));
                targetJoint.axis = binding.targetBone.InverseTransformDirection(MapDirection(sourceJoint.transform.TransformDirection(sourceJoint.axis), binding)).normalized;                targetJoint.swingAxis = binding.targetBone.InverseTransformDirection(MapDirection(sourceJoint.transform.TransformDirection(sourceJoint.swingAxis), binding)).normalized;
                targetJoint.lowTwistLimit = sourceJoint.lowTwistLimit;
                targetJoint.highTwistLimit = sourceJoint.highTwistLimit;
                targetJoint.swing1Limit = sourceJoint.swing1Limit;
                targetJoint.swing2Limit = sourceJoint.swing2Limit;
                targetJoint.twistLimitSpring = sourceJoint.twistLimitSpring;
                targetJoint.swingLimitSpring = sourceJoint.swingLimitSpring;
                targetJoint.breakForce = sourceJoint.breakForce;
                targetJoint.breakTorque = sourceJoint.breakTorque;
                targetJoint.enableCollision = sourceJoint.enableCollision;
                targetJoint.enablePreprocessing = sourceJoint.enablePreprocessing;
                targetJoint.massScale = sourceJoint.massScale;
                targetJoint.connectedMassScale = sourceJoint.connectedMassScale;
                targetJoint.autoConfigureConnectedAnchor = sourceJoint.autoConfigureConnectedAnchor;

                if (sourceJoint.connectedBody != null && rigidbodyMap.TryGetValue(sourceJoint.connectedBody, out Rigidbody connectedBody))
                {
                    targetJoint.connectedBody = connectedBody;

                    if (!sourceJoint.autoConfigureConnectedAnchor && bindingMap.TryGetValue(sourceJoint.connectedBody, out PartBinding connectedBinding))
                    {
                        Vector3 sourceConnectedAnchor = sourceJoint.connectedBody.transform.TransformPoint(sourceJoint.connectedAnchor);
                        Vector3 targetConnectedAnchor = MapPoint(sourceConnectedAnchor, connectedBinding);
                        targetJoint.connectedAnchor = connectedBody.transform.InverseTransformPoint(targetConnectedAnchor);
                    }
                }
                else
                {
                    targetJoint.connectedBody = null;
                }

                RecordPrefabModification(targetJoint);
            }
        }

        private static void ConfigureTargetReferences(RagdollReference source, Animator animator, Dictionary<Rigidbody, Rigidbody> rigidbodyMap)
        {
            RagdollReference targetReference = animator.TryGetComponent(out RagdollReference existingReference)
                ? existingReference
                : Undo.AddComponent<RagdollReference>(animator.gameObject);

            SerializedObject serializedReference = new SerializedObject(targetReference);
            SetBodyReference(serializedReference, "hips", source.hips, rigidbodyMap);
            SetBodyReference(serializedReference, "leftUpperLeg", source.leftUpperLeg, rigidbodyMap);
            SetBodyReference(serializedReference, "leftLowerLeg", source.leftLowerLeg, rigidbodyMap);
            SetBodyReference(serializedReference, "leftFoot", source.leftFoot, rigidbodyMap);
            SetBodyReference(serializedReference, "rightUpperLeg", source.rightUpperLeg, rigidbodyMap);
            SetBodyReference(serializedReference, "rightLowerLeg", source.rightLowerLeg, rigidbodyMap);
            SetBodyReference(serializedReference, "rightFoot", source.rightFoot, rigidbodyMap);
            SetBodyReference(serializedReference, "chest", source.chest, rigidbodyMap);
            SetBodyReference(serializedReference, "leftUpperArm", source.leftUpperArm, rigidbodyMap);
            SetBodyReference(serializedReference, "leftLowerArm", source.leftLowerArm, rigidbodyMap);
            SetBodyReference(serializedReference, "leftHand", source.leftHand, rigidbodyMap);
            SetBodyReference(serializedReference, "rightUpperArm", source.rightUpperArm, rigidbodyMap);
            SetBodyReference(serializedReference, "rightLowerArm", source.rightLowerArm, rigidbodyMap);
            SetBodyReference(serializedReference, "rightHand", source.rightHand, rigidbodyMap);
            SetBodyReference(serializedReference, "head", source.head, rigidbodyMap);
            serializedReference.ApplyModifiedProperties();
            RecordPrefabModification(targetReference);
        }

        private static void SetBodyReference(SerializedObject serializedReference, string propertyName, Rigidbody sourceBody, Dictionary<Rigidbody, Rigidbody> rigidbodyMap)
        {
            SerializedProperty property = serializedReference.FindProperty(propertyName);
            property.objectReferenceValue = rigidbodyMap[sourceBody];
        }

        private static void EnsureAdjustmentTool(Animator animator)
        {
            ColliderAdjustmentTool tool = animator.TryGetComponent(out ColliderAdjustmentTool existingTool)
                ? existingTool
                : Undo.AddComponent<ColliderAdjustmentTool>(animator.gameObject);

            EditorUtility.SetDirty(tool);
            RecordPrefabModification(tool);
        }
        private static void RecordPrefabModification(UnityEngine.Object target)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static Vector3 Divide(Vector3 numerator, Vector3 denominator)
        {
            return new Vector3(
                numerator.x / Mathf.Max(Mathf.Abs(denominator.x), MinimumSegmentLength),
                numerator.y / Mathf.Max(Mathf.Abs(denominator.y), MinimumSegmentLength),
                numerator.z / Mathf.Max(Mathf.Abs(denominator.z), MinimumSegmentLength));
        }

        private static float MaxAbsComponent(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z), MinimumSegmentLength);
        }

        private sealed class PartBinding
        {
            public readonly string name;
            public readonly Rigidbody sourceBody;
            public readonly Transform targetBone;
            public readonly AnatomicalFrame sourceFrame;
            public readonly AnatomicalFrame targetFrame;

            public PartBinding(string name, Rigidbody sourceBody, Transform targetBone, AnatomicalFrame sourceFrame, AnatomicalFrame targetFrame)
            {
                this.name = name;
                this.sourceBody = sourceBody;
                this.targetBone = targetBone;
                this.sourceFrame = sourceFrame;
                this.targetFrame = targetFrame;
            }
        }

        private readonly struct AnatomicalFrame
        {
            public readonly Vector3 origin;
            public readonly Quaternion rotation;
            public readonly float length;

            public AnatomicalFrame(Vector3 origin, Vector3 primaryDirection, Vector3 forwardHint, Vector3 rightHint)
            {
                this.origin = origin;
                length = primaryDirection.magnitude;

                Vector3 up = primaryDirection / length;
                Vector3 forward = Vector3.ProjectOnPlane(forwardHint, up);

                if (forward.sqrMagnitude < MinimumSegmentLength * MinimumSegmentLength)
                {
                    forward = Vector3.ProjectOnPlane(rightHint, up);
                }

                if (forward.sqrMagnitude < MinimumSegmentLength * MinimumSegmentLength)
                {
                    forward = Vector3.ProjectOnPlane(Vector3.forward, up);
                }

                if (forward.sqrMagnitude < MinimumSegmentLength * MinimumSegmentLength)
                {
                    forward = Vector3.ProjectOnPlane(Vector3.right, up);
                }

                forward.Normalize();
                Vector3 right = Vector3.Cross(up, forward).normalized;
                forward = Vector3.Cross(right, up).normalized;
                rotation = Quaternion.LookRotation(forward, up);
            }
        }

        private sealed class TargetSkeleton
        {
            public readonly Transform hips;
            public readonly Transform leftUpperLeg;
            public readonly Transform rightUpperLeg;
            public readonly Transform leftLowerLeg;
            public readonly Transform rightLowerLeg;
            public readonly Transform leftFoot;
            public readonly Transform rightFoot;
            public readonly Transform chest;
            public readonly Transform leftUpperArm;
            public readonly Transform rightUpperArm;
            public readonly Transform leftLowerArm;
            public readonly Transform rightLowerArm;
            public readonly Transform leftHand;
            public readonly Transform rightHand;
            public readonly Transform head;

            public TargetSkeleton(Animator animator)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);                rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            public void Validate(List<string> problems)
            {
                RequireTarget(hips, "Hips", problems);
                RequireTarget(leftUpperLeg, "Left Upper Leg", problems);
                RequireTarget(rightUpperLeg, "Right Upper Leg", problems);
                RequireTarget(leftLowerLeg, "Left Lower Leg", problems);
                RequireTarget(rightLowerLeg, "Right Lower Leg", problems);
                RequireTarget(leftFoot, "Left Foot", problems);
                RequireTarget(rightFoot, "Right Foot", problems);
                RequireTarget(chest, "Chest or Spine", problems);
                RequireTarget(leftUpperArm, "Left Upper Arm", problems);
                RequireTarget(rightUpperArm, "Right Upper Arm", problems);
                RequireTarget(leftLowerArm, "Left Lower Arm", problems);
                RequireTarget(rightLowerArm, "Right Lower Arm", problems);
                RequireTarget(leftHand, "Left Hand", problems);
                RequireTarget(rightHand, "Right Hand", problems);
                RequireTarget(head, "Head", problems);
            }

            private static void RequireTarget(Transform bone, string label, List<string> problems)
            {
                if (bone == null)
                {
                    problems.Add($"Target Humanoid Avatar is missing {label}.");
                }
            }
        }
    }
}