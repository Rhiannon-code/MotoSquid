// =================================================
// Realistic Engine Sounds 2
// Copyright © 2026 Skril Studio
//
// https://skrilstudio.com
// https://www.facebook.com/skrilstudio
// =================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace SkrilStudio
{
    public class UIDragComponent : MonoBehaviour // This script is used only for demonstration purposes in the included demo scenes.
    {
        [Header("UI")]
        public RectTransform radarArea;
        public RectTransform handle;

        [Header("Scene")]
        public Transform targetCar;   
        public Transform listenerTransform;

        [Header("Listener")]
        public float listenerRadius = 5f;
        public float listenerHeight = 1.5f;
        public float smoothSpeed = 10f;
        private RectTransform cameraIcon;
        public RectTransform iconText;

        [Header("Dead Zone")]
        public float deadZoneWidth = 0.5f;
        public float deadZoneHeight = 1.0f;
        [Header("Dead Zone Collision")]
        public float deadZoneWidthPixels = 340;
        public float deadZoneHeightPixels = 146;

        private Camera uiCamera;

        private bool isDragging;
        private Vector3 targetListenerPosition;

        private void Awake()
        {
            cameraIcon = GetComponent<RectTransform>();
            UpdateListenerFromHandle();
        }

        public void BeginDrag()
        {
            isDragging = true;
        }

        public void EndDrag()
        {
            isDragging = false;
        }

        private void Update()
        {
            if (isDragging)
            {
                DragHandle();
            }

            listenerTransform.position = Vector3.Lerp(
                listenerTransform.position,
                targetListenerPosition,
                Time.deltaTime * smoothSpeed);

            if (targetCar != null)
            {
                listenerTransform.LookAt(targetCar);
            }

            // look towards the car
            UpdateCameraIconRotation();
        }
        private void UpdateCameraIconRotation()
        {
            Vector2 dir = handle.anchoredPosition;

            if (dir.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                cameraIcon.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);

                // restore text rotation
                if (iconText != null)
                    iconText.localRotation = Quaternion.Euler(0f, 0f, -(angle - 90f));
            } 
        }
        private void DragHandle()
        {
            Vector2 localMousePosition;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                radarArea,
                Mouse.current.position.ReadValue(),
                uiCamera,
                out localMousePosition);

            float radius = Mathf.Min(
                radarArea.rect.width,
                radarArea.rect.height) * 0.5f;

            if (localMousePosition.magnitude > radius)
            {
                localMousePosition =
                    localMousePosition.normalized * radius;
            }

            //
            float deadHalfWidth = deadZoneWidthPixels /.65f * 0.5f;
            float deadHalfHeight = deadZoneHeightPixels /.65f * 0.5f;

            if (Mathf.Abs(localMousePosition.x) < deadHalfWidth &&
                Mathf.Abs(localMousePosition.y) < deadHalfHeight)
            {
                float leftDist = deadHalfWidth - Mathf.Abs(localMousePosition.x);
                float topDist = deadHalfHeight - Mathf.Abs(localMousePosition.y);

                if (leftDist < topDist)
                    localMousePosition.x = Mathf.Sign(localMousePosition.x) * deadHalfWidth;
                else
                    localMousePosition.y = Mathf.Sign(localMousePosition.y) * deadHalfHeight;
            }
            //

            handle.anchoredPosition = localMousePosition;

            UpdateListenerFromHandle();
        }

        private void UpdateListenerFromHandle()
        {
            float radius = Mathf.Min(
                radarArea.rect.width,
                radarArea.rect.height) * 0.5f;

            Vector2 normalized =
                handle.anchoredPosition / radius;

            float x = normalized.x;
            float y = normalized.y;

            if (Mathf.Abs(x) < deadZoneWidth * 0.5f)
                x = 0f;
            else
                x = Mathf.Sign(x) *
                    ((Mathf.Abs(x) - deadZoneWidth * 0.5f) /
                     (1f - deadZoneWidth * 0.5f));

            if (Mathf.Abs(y) < deadZoneHeight * 0.5f)
                y = 0f;
            else
                y = Mathf.Sign(y) *
                    ((Mathf.Abs(y) - deadZoneHeight * 0.5f) /
                     (1f - deadZoneHeight * 0.5f));

            normalized = new Vector2(x, y);
            Vector3 offset =
                (targetCar.right * normalized.x +
                targetCar.forward * normalized.y) * listenerRadius;

            targetListenerPosition =
                targetCar.position +
                offset +
                Vector3.up * listenerHeight;
        }
    }
}