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
    public class SplitScreenCharacterSelectManager : MenuManagerBase
    {
        [Header("Panel Manager")]
        [SerializeField] private PanelManager panelManager;
        [SerializeField] private TrackSelectManager trackSelectManager;

        [Header("Bike Data")]
        [SerializeField] private List<BikeEntry> bikes = new();

        [Header("Player 1 UI (left side)")]
        [SerializeField] private TMP_Text p1BikeNameText;
        [SerializeField] private TMP_Text p1BikeDescriptionText;
        [SerializeField] private TMP_Text p1PageText;
        [SerializeField] private TMP_Text p1StatusText;
        [SerializeField] private Transform p1PreviewSpawnPoint;

        [Header("Player 2 UI (right side)")]
        [SerializeField] private TMP_Text p2BikeNameText;
        [SerializeField] private TMP_Text p2BikeDescriptionText;
        [SerializeField] private TMP_Text p2PageText;
        [SerializeField] private TMP_Text p2StatusText;
        [SerializeField] private Transform p2PreviewSpawnPoint;

        [Header("Feel")]
        [SerializeField] private float navigateDebounce     = 0.2f;
        [SerializeField] private float previewRotationSpeed = 30f;
        [SerializeField] private string readyText           = "READY!";
        [SerializeField] private string waitingText         = "Press Confirm";

        [Header("Input: P1 buttons (assign in order, e.g. HOME, SETTINGS, NEXT, PREVIOUS, CONFIRM)")]
        [SerializeField] private Button[] p1NavButtons;

        [Header("Input: P2 buttons (assign in order, e.g. NEXT, PREVIOUS, CONFIRM)")]
        [SerializeField] private Button[] p2NavButtons;

        private int   p1Index = 0, p2Index = 0;
        private bool  p1Ready = false, p2Ready = false;
        private float p1NavTimer = 0f, p2NavTimer = 0f;
        private int   _p1Focus = 0, _p2Focus = 0;
        private bool  _loadingStarted = false;
        private bool  _isPanelOpen    = false;

        private GameObject p1Preview, p2Preview;

        public void OnPanelOpen()
        {
            _isPanelOpen    = true;
            _loadingStarted = false;
            p1Index = 0; p2Index = 0;
            p1Ready = false; p2Ready = false;
            p1NavTimer = 0f; p2NavTimer = 0f;
            _p1Focus = 0; _p2Focus = 0;

            RefreshP1();
            RefreshP2();
            SetStatus(p1StatusText, false);
            SetStatus(p2StatusText, false);

            SetFocus(p1NavButtons, _p1Focus);
        }

        void OnDestroy()
        {
            CleanupPreviews();
            StopAllCoroutines();
        }

        void Update()
        {
            if (!_isPanelOpen || _loadingStarted) return;

            if (p1NavTimer > 0f) p1NavTimer -= Time.deltaTime;
            if (p2NavTimer > 0f) p2NavTimer -= Time.deltaTime;

            if (p1Preview != null)
                p1Preview.transform.Rotate(Vector3.up, previewRotationSpeed * Time.deltaTime, Space.World);
            if (p2Preview != null)
                p2Preview.transform.Rotate(Vector3.up, previewRotationSpeed * Time.deltaTime, Space.World);

            var kb = Keyboard.current;

            Gamepad p1Gamepad, p2Gamepad;
            bool    p1KeyboardEnabled;

            int gamepadCount = Gamepad.all.Count;
            if (gamepadCount >= 2)
            {
                p1Gamepad         = Gamepad.all[0];
                p2Gamepad         = Gamepad.all[1];

                p1KeyboardEnabled = false;
            }
            else if (gamepadCount == 1)
            {
                p1Gamepad         = null;
                p2Gamepad         = Gamepad.all[0];
                p1KeyboardEnabled = true;
            }
            else
            {
                p1Gamepad         = null;
                p2Gamepad         = null;
                p1KeyboardEnabled = true;
            }

            HandleP1Input(p1KeyboardEnabled ? kb : null, p1Gamepad);
            HandleP2Input(kb, p2Gamepad);

            if (!p1Ready && !p2Ready && kb != null && kb.escapeKey.wasPressedThisFrame)
                OnBack();
        }

        void HandleP1Input(Keyboard kb, Gamepad gp)
        {
            if (p1Ready || p1NavButtons == null || p1NavButtons.Length == 0) return;

            if (p1NavTimer > 0f) return;

            bool navPrev = (gp != null && (gp.dpad.left.isPressed   || gp.dpad.up.isPressed    || gp.leftStick.x.ReadValue() < -0.5f || gp.leftStick.y.ReadValue() >  0.5f))
                        || (kb != null && (kb.leftArrowKey.isPressed  || kb.upArrowKey.isPressed));
            bool navNext = (gp != null && (gp.dpad.right.isPressed  || gp.dpad.down.isPressed   || gp.leftStick.x.ReadValue() >  0.5f || gp.leftStick.y.ReadValue() < -0.5f))
                        || (kb != null && (kb.rightArrowKey.isPressed || kb.downArrowKey.isPressed));

            if (navPrev)
            {
                _p1Focus = (_p1Focus - 1 + p1NavButtons.Length) % p1NavButtons.Length;
                SetFocus(p1NavButtons, _p1Focus);
                p1NavTimer = navigateDebounce;
            }
            else if (navNext)
            {
                _p1Focus = (_p1Focus + 1) % p1NavButtons.Length;
                SetFocus(p1NavButtons, _p1Focus);
                p1NavTimer = navigateDebounce;
            }

            bool submit = (gp != null && gp.buttonSouth.wasPressedThisFrame)
                       || (kb != null && kb.enterKey.wasPressedThisFrame);
            if (submit)
                InvokeButton(p1NavButtons[_p1Focus]);
        }

        void HandleP2Input(Keyboard kb, Gamepad gp)
        {
            if (p2Ready || p2NavButtons == null || p2NavButtons.Length == 0) return;

            if (p2NavTimer > 0f) return;

            bool useKb = kb != null && gp == null;

            bool navPrev = (gp != null && (gp.dpad.left.isPressed  || gp.dpad.up.isPressed    || gp.leftStick.x.ReadValue() < -0.5f || gp.leftStick.y.ReadValue() >  0.5f))
                        || (useKb && kb.aKey.isPressed);
            bool navNext = (gp != null && (gp.dpad.right.isPressed || gp.dpad.down.isPressed   || gp.leftStick.x.ReadValue() >  0.5f || gp.leftStick.y.ReadValue() < -0.5f))
                        || (useKb && kb.dKey.isPressed);

            if (navPrev)
            {
                _p2Focus = (_p2Focus - 1 + p2NavButtons.Length) % p2NavButtons.Length;
                SetFocus(p2NavButtons, _p2Focus);
                p2NavTimer = navigateDebounce;
            }
            else if (navNext)
            {
                _p2Focus = (_p2Focus + 1) % p2NavButtons.Length;
                SetFocus(p2NavButtons, _p2Focus);
                p2NavTimer = navigateDebounce;
            }

            bool submit = (gp != null && gp.buttonSouth.wasPressedThisFrame)
                       || (useKb && kb.rightShiftKey.wasPressedThisFrame);
            if (submit)
                InvokeButton(p2NavButtons[_p2Focus]);
        }

        static void SetFocus(Button[] buttons, int index)
        {
            if (buttons == null || index < 0 || index >= buttons.Length) return;
            if (buttons[index] != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(buttons[index].gameObject);
        }

        static void InvokeButton(Button btn)
        {
            if (btn != null && btn.interactable)
                btn.onClick.Invoke();
        }

        void TryAdvance()
        {
            if (!p1Ready || !p2Ready || _loadingStarted) return;
            _loadingStarted = true;

            var session = GameSession.Instance;
            if (session != null)
            {
                session.SelectedBikeIndex   = p1Index;
                session.SelectedBikeIndexP2 = p2Index;

                if (p1Index >= 0 && p1Index < bikes.Count) session.SelectedCharacterName   = bikes[p1Index].Identity;
                if (p2Index >= 0 && p2Index < bikes.Count) session.SelectedCharacterNameP2 = bikes[p2Index].Identity;
            }

            _isPanelOpen = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            CleanupPreviews();
            trackSelectManager?.OnPanelOpen();
            panelManager?.OpenPanel("Track Select");
        }

        public void OnBack()
        {
            _isPanelOpen = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            CleanupPreviews();
            panelManager?.OpenPanel("Home");
        }

        public void P1OnNext()     { if (!p1Ready) { p1Index = (p1Index + 1) % bikes.Count; RefreshP1(); } }
        public void P1OnPrevious() { if (!p1Ready) { p1Index = (p1Index - 1 + bikes.Count) % bikes.Count; RefreshP1(); } }
        public void P1OnConfirm()  { if (!p1Ready) { p1Ready = true; SetStatus(p1StatusText, true); TryAdvance(); } }

        public void P2OnNext()     { if (!p2Ready) { p2Index = (p2Index + 1) % bikes.Count; RefreshP2(); } }
        public void P2OnPrevious() { if (!p2Ready) { p2Index = (p2Index - 1 + bikes.Count) % bikes.Count; RefreshP2(); } }
        public void P2OnConfirm()  { if (!p2Ready) { p2Ready = true; SetStatus(p2StatusText, true); TryAdvance(); } }

        void RefreshP1() => RefreshDisplay(p1Index, p1BikeNameText, p1BikeDescriptionText, p1PageText, p1PreviewSpawnPoint, ref p1Preview);
        void RefreshP2() => RefreshDisplay(p2Index, p2BikeNameText, p2BikeDescriptionText, p2PageText, p2PreviewSpawnPoint, ref p2Preview);

        void RefreshDisplay(int index, TMP_Text nameText, TMP_Text descText, TMP_Text pageText, Transform spawnPoint, ref GameObject preview)
        {
            if (bikes.Count == 0) return;

            var entry = bikes[index];
            if (nameText != null) nameText.text = entry.Label;
            if (descText != null) descText.text = entry.description;
            if (pageText != null) pageText.text = $"{index + 1} / {bikes.Count}";

            SpawnPreview(entry.previewPrefab, spawnPoint, ref preview);
        }

        void SpawnPreview(GameObject prefab, Transform spawnPoint, ref GameObject preview)
        {
            if (preview != null) { Destroy(preview); preview = null; }
            if (prefab == null || spawnPoint == null) return;

            preview = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            foreach (var rb  in preview.GetComponentsInChildren<Rigidbody>())
                rb.isKinematic = true;
            foreach (var col in preview.GetComponentsInChildren<Collider>())
                col.enabled = false;

            SetLayerRecursive(preview, LayerMask.NameToLayer("BikePreview"));
        }

        public void CleanupPreviews()
        {
            if (p1Preview != null) { Destroy(p1Preview); p1Preview = null; }
            if (p2Preview != null) { Destroy(p2Preview); p2Preview = null; }
        }

        void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        void SetStatus(TMP_Text text, bool ready)
        {
            if (text == null) return;
            text.text = ready ? readyText : waitingText;
        }
    }
}
