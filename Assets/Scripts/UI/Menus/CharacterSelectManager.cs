using MotoSquid.Bike;
using MotoSquid.Core;
using System.Collections.Generic;
using Michsky.UI.Heat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace MotoSquid.UI
{
    public class CharacterSelectManager : MenuManagerBase
    {
        [Header("Panel Manager")]
        [SerializeField] private PanelManager panelManager;
        [SerializeField] private TrackSelectManager trackSelectManager;

        [Header("Bike Data")]
        [SerializeField] private List<BikeEntry> bikes = new();

        [Header("Preview")]
        [SerializeField] private Transform previewSpawnPoint;
        [SerializeField] private float previewRotationSpeed = 30f;

        [Header("UI")]
        [SerializeField] private TMP_Text bikeNameText;
        [SerializeField] private TMP_Text bikeDescriptionText;
        [SerializeField] private TMP_Text pageText;

        [Header("Input")]
        [SerializeField] private Button[] navButtons;
        [SerializeField] private float    navigateDebounce = 0.2f;

        private int   currentIndex = 0;
        private bool  _isPanelOpen = false;
        private int   _focusIndex  = 0;
        private float _navTimer    = 0f;
        private GameObject currentPreview;

        void OnDestroy() => CleanupPreview();

        public void OnPanelOpen()
        {
            _isPanelOpen = true;
            _navTimer    = 0f;
            _focusIndex  = 0;
            SetFocus(_focusIndex);
            ShowBike(currentIndex);
        }

        void Update()
        {
            if (currentPreview != null)
                currentPreview.transform.Rotate(Vector3.up, previewRotationSpeed * Time.deltaTime, Space.World);

            if (!_isPanelOpen || navButtons == null || navButtons.Length == 0) return;
            if (_navTimer > 0f) { _navTimer -= Time.deltaTime; return; }

            var gp = Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
            var kb = Keyboard.current;

            bool navPrev = (gp != null && (gp.dpad.left.isPressed   || gp.dpad.up.isPressed    || gp.leftStick.x.ReadValue() < -0.5f || gp.leftStick.y.ReadValue() >  0.5f))
                        || (kb != null && (kb.leftArrowKey.isPressed  || kb.upArrowKey.isPressed));
            bool navNext = (gp != null && (gp.dpad.right.isPressed  || gp.dpad.down.isPressed   || gp.leftStick.x.ReadValue() >  0.5f || gp.leftStick.y.ReadValue() < -0.5f))
                        || (kb != null && (kb.rightArrowKey.isPressed || kb.downArrowKey.isPressed));

            if (navPrev)
            {
                _focusIndex = (_focusIndex - 1 + navButtons.Length) % navButtons.Length;
                SetFocus(_focusIndex);
                _navTimer = navigateDebounce;
            }
            else if (navNext)
            {
                _focusIndex = (_focusIndex + 1) % navButtons.Length;
                SetFocus(_focusIndex);
                _navTimer = navigateDebounce;
            }

            bool submit = (gp != null && gp.buttonSouth.wasPressedThisFrame)
                       || (kb != null && kb.enterKey.wasPressedThisFrame);
            if (submit)
                InvokeButton(navButtons[_focusIndex]);
        }

        void SetFocus(int index)
        {
            if (navButtons == null || index < 0 || index >= navButtons.Length) return;
            if (navButtons[index] != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(navButtons[index].gameObject);
        }

        void ClearFocus()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        static void InvokeButton(Button btn)
        {
            if (btn != null && btn.interactable)
                btn.onClick.Invoke();
        }

        public void OnNext()     => ShowBike((currentIndex + 1) % bikes.Count);
        public void OnPrevious() => ShowBike((currentIndex - 1 + bikes.Count) % bikes.Count);

        public void OnConfirm()
        {
            if (bikes.Count == 0) return;
            _isPanelOpen = false;
            ClearFocus();

            if (GameSession.Instance != null)
            {
                GameSession.Instance.SelectedBikeIndex     = currentIndex;
                GameSession.Instance.SelectedCharacterName = bikes[currentIndex].Identity;
            }

            // Persist the choice so the menus can default back to it next launch.
            if (SaveManager.Instance != null)
                SaveManager.Instance.SetLastSelectedCharacter(bikes[currentIndex].Identity);

            CleanupPreview();
            trackSelectManager?.OnPanelOpen();
            panelManager?.OpenPanel("Track Select");
        }

        public void OnBack()
        {
            _isPanelOpen = false;
            ClearFocus();
            CleanupPreview();
            panelManager?.OpenPanel("Home");
        }

        void ShowBike(int index)
        {
            if (bikes.Count == 0) return;

            currentIndex = index;
            BikeEntry entry = bikes[currentIndex];

            if (bikeNameText        != null) bikeNameText.text        = entry.Label;
            if (bikeDescriptionText != null) bikeDescriptionText.text = entry.description;
            if (pageText            != null) pageText.text            = $"{currentIndex + 1} / {bikes.Count}";

            SpawnPreview(entry.previewPrefab);
        }

        void SpawnPreview(GameObject prefab)
        {
            CleanupPreview();
            if (prefab == null || previewSpawnPoint == null) return;

            currentPreview = Instantiate(prefab, previewSpawnPoint.position, previewSpawnPoint.rotation);

            foreach (var rb  in currentPreview.GetComponentsInChildren<Rigidbody>())
                rb.isKinematic = true;
            foreach (var col in currentPreview.GetComponentsInChildren<Collider>())
                col.enabled = false;

            SetLayerRecursive(currentPreview, LayerMask.NameToLayer("BikePreview"));
        }

        public void CleanupPreview()
        {
            if (currentPreview != null)
            {
                Destroy(currentPreview);
                currentPreview = null;
            }
        }

        void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
