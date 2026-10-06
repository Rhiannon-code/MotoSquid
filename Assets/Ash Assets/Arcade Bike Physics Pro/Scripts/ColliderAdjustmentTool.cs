using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR


namespace ArcadeBP_Pro
{
    [ExecuteInEditMode]
    public class ColliderAdjustmentTool : MonoBehaviour
    {
        public bool editMode = false;
        public bool mirrorEnabled = false;
        [HideInInspector]
        public Collider[] childColliders;
        private Collider selectedCollider;

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            UpdateChildColliders();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnValidate()
        {
            UpdateChildColliders();
        }

        private void UpdateChildColliders()
        {
            childColliders = GetComponentsInChildren<Collider>();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (editMode)
            {
                foreach (var collider in childColliders)
                {
                    if (collider != null && collider.enabled)
                    {
                        DrawColliderSelectionHandle(collider);
                    }
                }

                if (selectedCollider != null)
                {
                    DrawColliderHandles(selectedCollider);
                }
            }
        }

        private void DrawColliderSelectionHandle(Collider collider)
        {
            Vector3 center = collider.transform.TransformPoint(GetColliderCenter(collider));
            Handles.color = Color.white;
            if (Handles.Button(center, Quaternion.identity, HandleUtility.GetHandleSize(center) * 0.15f, HandleUtility.GetHandleSize(center) * 0.15f, Handles.SphereHandleCap))
            {
                selectedCollider = collider;
            }
        }

        private void DrawColliderHandles(Collider collider)
        {
            Handles.color = Color.green;
            EditorGUI.BeginChangeCheck();

            switch (Tools.current)
            {
                case Tool.Move:
                    HandleMoveTool(collider);
                    break;
                case Tool.Rotate:
                    HandleRotateTool(collider);
                    break;
                case Tool.Scale:
                    if (collider is BoxCollider || collider is SphereCollider)
                    {
                        HandleScaleTool(collider);
                    }
                    else if (collider is CapsuleCollider)
                    {
                        HandleCapsuleScaleTool((CapsuleCollider)collider);
                    }
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(collider, "Edit Collider");
                if (mirrorEnabled)
                {
                    MirrorCollider(collider);
                }
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                selectedCollider = null;
                Event.current.Use();
            }
        }

        private void HandleMoveTool(Collider collider)
        {
            Vector3 center = collider.transform.TransformPoint(GetColliderCenter(collider));
            center = Handles.PositionHandle(center, collider.transform.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(collider, "Move Collider");
                SetColliderCenter(collider, collider.transform.InverseTransformPoint(center));
            }
        }

        private void HandleRotateTool(Collider collider)
        {
            Transform colliderTransform = collider.transform;
            Vector3 center = colliderTransform.TransformPoint(GetColliderCenter(collider));
            Quaternion newRotation = Handles.RotationHandle(colliderTransform.rotation, center);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(colliderTransform, "Rotate Collider");
                Vector3 currentPosition = colliderTransform.position;
                colliderTransform.rotation = newRotation;
                Vector3 newPosition = center - colliderTransform.TransformPoint(GetColliderCenter(collider));
                colliderTransform.position += newPosition;
            }
        }

        private void HandleScaleTool(Collider collider)
        {
            if (collider is BoxCollider box)
            {
                Vector3 center = box.transform.TransformPoint(box.center);
                Vector3 size = box.size;
                Handles.Label(center, "Scale Box Collider");
                Vector3 newSize = Handles.ScaleHandle(size, center, box.transform.rotation, HandleUtility.GetHandleSize(center));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(box, "Scale Box Collider");
                    box.size = newSize;
                }
            }
            else if (collider is SphereCollider sphere)
            {
                Vector3 center = sphere.transform.TransformPoint(sphere.center);
                float radius = sphere.radius * Mathf.Max(sphere.transform.lossyScale.x, sphere.transform.lossyScale.y, sphere.transform.lossyScale.z);
                radius = Handles.RadiusHandle(sphere.transform.rotation, center, radius);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(sphere, "Scale Sphere Collider");
                    sphere.radius = radius / Mathf.Max(sphere.transform.lossyScale.x, sphere.transform.lossyScale.y, sphere.transform.lossyScale.z);
                }
            }
        }

        private void HandleCapsuleScaleTool(CapsuleCollider capsule)
        {
            Vector3 center = capsule.transform.TransformPoint(capsule.center);
            Quaternion rotation = capsule.transform.rotation;

            Handles.Label(center, "Scale Capsule Collider");

            EditorGUI.BeginChangeCheck();

            // Calculate the current scale of the capsule collider
            float maxLossyScale = Mathf.Max(capsule.transform.lossyScale.x, capsule.transform.lossyScale.z);
            Vector3 currentScale = new Vector3(capsule.radius * maxLossyScale, capsule.height * capsule.transform.lossyScale.y, capsule.radius * maxLossyScale);

            // Use a single scale handle in the middle of the collider
            Vector3 newScale = Handles.ScaleHandle(currentScale, center, rotation, HandleUtility.GetHandleSize(center));

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(capsule, "Scale Capsule Collider");

                // Update the radius based on the X and Z axes of the scale handle
                float newRadius = 0.5f * (newScale.x + newScale.z) / maxLossyScale;
                capsule.radius = newRadius;

                // Update the height based on the Y axis of the scale handle
                float newHeight = newScale.y / capsule.transform.lossyScale.y;
                capsule.height = newHeight;
            }
        }

        private Vector3 GetColliderCenter(Collider collider)
        {
            if (collider is BoxCollider box)
            {
                return box.center;
            }
            else if (collider is SphereCollider sphere)
            {
                return sphere.center;
            }
            else if (collider is CapsuleCollider capsule)
            {
                return capsule.center;
            }
            return Vector3.zero;
        }

        private void SetColliderCenter(Collider collider, Vector3 center)
        {
            if (collider is BoxCollider box)
            {
                box.center = center;
            }
            else if (collider is SphereCollider sphere)
            {
                sphere.center = center;
            }
            else if (collider is CapsuleCollider capsule)
            {
                capsule.center = center;
            }
        }

        private void OnDrawGizmos()
        {
            if (editMode && childColliders != null)
            {
                foreach (var collider in childColliders)
                {
                    Vector3 center = collider.transform.TransformPoint(GetColliderCenter(collider));
                    Handles.color = Color.green;
                    Handles.SphereHandleCap(0, center, Quaternion.identity, HandleUtility.GetHandleSize(center) * 0.15f, EventType.Repaint);

                    Gizmos.color = Color.green;
                    if (collider is BoxCollider boxCollider)
                    {
                        DrawWireBoxCollider(boxCollider);
                    }
                    else if (collider is SphereCollider sphereCollider)
                    {
                        Gizmos.DrawWireSphere(sphereCollider.transform.TransformPoint(sphereCollider.center), sphereCollider.radius);
                    }
                    else if (collider is CapsuleCollider capsuleCollider)
                    {
                        DrawWireCapsule(capsuleCollider.bounds.center, capsuleCollider.transform.rotation, capsuleCollider.radius, capsuleCollider.height);
                    }
                }
            }
        }

        private void DrawWireBoxCollider(BoxCollider boxCollider)
        {
            Vector3 center = boxCollider.center;
            Vector3 size = boxCollider.size;

            Vector3[] vertices = new Vector3[8];
            vertices[0] = boxCollider.transform.TransformPoint(center + new Vector3(-size.x, -size.y, -size.z) * 0.5f);
            vertices[1] = boxCollider.transform.TransformPoint(center + new Vector3(size.x, -size.y, -size.z) * 0.5f);
            vertices[2] = boxCollider.transform.TransformPoint(center + new Vector3(size.x, -size.y, size.z) * 0.5f);
            vertices[3] = boxCollider.transform.TransformPoint(center + new Vector3(-size.x, -size.y, size.z) * 0.5f);
            vertices[4] = boxCollider.transform.TransformPoint(center + new Vector3(-size.x, size.y, -size.z) * 0.5f);
            vertices[5] = boxCollider.transform.TransformPoint(center + new Vector3(size.x, size.y, -size.z) * 0.5f);
            vertices[6] = boxCollider.transform.TransformPoint(center + new Vector3(size.x, size.y, size.z) * 0.5f);
            vertices[7] = boxCollider.transform.TransformPoint(center + new Vector3(-size.x, size.y, size.z) * 0.5f);

            Gizmos.DrawLine(vertices[0], vertices[1]);
            Gizmos.DrawLine(vertices[1], vertices[2]);
            Gizmos.DrawLine(vertices[2], vertices[3]);
            Gizmos.DrawLine(vertices[3], vertices[0]);

            Gizmos.DrawLine(vertices[4], vertices[5]);
            Gizmos.DrawLine(vertices[5], vertices[6]);
            Gizmos.DrawLine(vertices[6], vertices[7]);
            Gizmos.DrawLine(vertices[7], vertices[4]);

            Gizmos.DrawLine(vertices[0], vertices[4]);
            Gizmos.DrawLine(vertices[1], vertices[5]);
            Gizmos.DrawLine(vertices[2], vertices[6]);
            Gizmos.DrawLine(vertices[3], vertices[7]);
        }

        private void DrawWireCapsule(Vector3 position, Quaternion rotation, float radius, float height)
        {
            float pointOffset = (height / 2) - radius;

            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;

            Vector3 topSphere = position + (up * pointOffset);
            Vector3 bottomSphere = position - (up * pointOffset);

            Handles.DrawWireArc(topSphere, right, -forward, 180, radius);
            Handles.DrawWireArc(bottomSphere, right, forward, 180, radius);
            Handles.DrawWireDisc(topSphere, up, radius);
            Handles.DrawWireDisc(bottomSphere, up, radius);

            Handles.DrawWireArc(topSphere, forward, -right, 180, radius);
            Handles.DrawWireArc(bottomSphere, forward, right, 180, radius);
            Handles.DrawLine(topSphere + (right * radius), bottomSphere + (right * radius));
            Handles.DrawLine(topSphere + (-right * radius), bottomSphere + (-right * radius));
            Handles.DrawLine(topSphere + (forward * radius), bottomSphere + (forward * radius));
            Handles.DrawLine(topSphere + (-forward * radius), bottomSphere + (-forward * radius));
        }

        private void MirrorCollider(Collider collider)
        {
            string mirrorName = GetMirrorName(collider.name);
            Collider mirrorCollider = null;

            foreach (var col in childColliders)
            {
                if (col != null && col.name == mirrorName)
                {
                    mirrorCollider = col;
                    break;
                }
            }

            if (mirrorCollider == null)
            {
                return;
            }

            // Mirror collider properties
            Vector3 center = GetColliderCenter(collider);
            Vector3 mirroredCenter = new Vector3(-center.x, center.y, center.z);

            SetColliderCenter(mirrorCollider, mirroredCenter);

            if (collider is BoxCollider box)
            {
                BoxCollider mirrorBox = (BoxCollider)mirrorCollider;
                mirrorBox.size = box.size;
            }
            else if (collider is SphereCollider sphere)
            {
                SphereCollider mirrorSphere = (SphereCollider)mirrorCollider;
                mirrorSphere.radius = sphere.radius;
            }
            else if (collider is CapsuleCollider capsule)
            {
                CapsuleCollider mirrorCapsule = (CapsuleCollider)mirrorCollider;
                mirrorCapsule.radius = capsule.radius;
                mirrorCapsule.height = capsule.height;
            }

            // Mirror transform properties
            Transform colliderTransform = collider.transform;
            Transform mirrorTransform = mirrorCollider.transform;

            mirrorTransform.localPosition = new Vector3(-colliderTransform.localPosition.x, colliderTransform.localPosition.y, colliderTransform.localPosition.z);
            mirrorTransform.localRotation = new Quaternion(colliderTransform.localRotation.x, -colliderTransform.localRotation.y, -colliderTransform.localRotation.z, colliderTransform.localRotation.w);
        }

        private string GetMirrorName(string name)
        {
            if (name.Contains("Left"))
            {
                return name.Replace("Left", "Right");
            }
            else if (name.Contains("Right"))
            {
                return name.Replace("Right", "Left");
            }
            return name;
        }
    }

    [CustomEditor(typeof(ColliderAdjustmentTool))]
    public class ColliderAdjustmentToolEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            ColliderAdjustmentTool tool = (ColliderAdjustmentTool)target;

            tool.editMode = EditorGUILayout.Toggle("Edit Mode", tool.editMode);
            tool.mirrorEnabled = EditorGUILayout.Toggle("Mirror Enabled", tool.mirrorEnabled);

            if (tool.editMode && tool.childColliders != null)
            {
                EditorGUILayout.LabelField("Edit Mode Enabled");
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(tool);
            }
        }
    }

}

#endif