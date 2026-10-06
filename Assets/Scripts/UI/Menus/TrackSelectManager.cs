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
    public class TrackSelectManager : MenuManagerBase
    {
        [System.Serializable]
        public class TrackEntry
        {
            public string displayName;
            [TextArea] public string description;
            public string sceneName;
            public Sprite previewImage;
            public TrackType trackType = TrackType.Circuit;
        }

        [Header("Panel Manager")]
        [SerializeField] private PanelManager panelManager;

        [Header("Back Navigation")]
        [SerializeField] private CharacterSelectManager characterSelectManager;
        [SerializeField] private SplitScreenCharacterSelectManager splitScreenCharacterSelectManager;

        [Header("Track Data")]
        [SerializeField] private List<TrackEntry> tracks = new();

        [Header("UI")]
        [SerializeField] private TMP_Text trackNameText;
        [SerializeField] private TMP_Text trackDescriptionText;
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private Image    trackPreviewImage;
        [SerializeField] private TMP_Text gameModeText;

        private GameMode selectedMode = GameMode.Race;

        [Header("Loading Scene")]
        [SerializeField] private string loadingScene = "LoadingScreen";

        [Header("Input")]
        [SerializeField] private Button[] navButtons;
        [SerializeField] private float    navigateDebounce = 0.2f;

        private int   currentIndex = 0;
        private bool  _isPanelOpen = false;
        private int   _focusIndex  = 0;
        private float _navTimer    = 0f;

        public void OnPanelOpen()
        {
            _isPanelOpen = true;
            _navTimer    = 0f;
            _focusIndex  = 0;
            SetFocus(_focusIndex);
            ShowTrack(currentIndex);
            UpdateGameModeUI();
        }

        public void ToggleGameMode()
        {
            selectedMode = selectedMode == GameMode.Race ? GameMode.TimeTrial : GameMode.Race;
            UpdateGameModeUI();
        }
        public void SetRaceMode()      { selectedMode = GameMode.Race;      UpdateGameModeUI(); }
        public void SetTimeTrialMode() { selectedMode = GameMode.TimeTrial; UpdateGameModeUI(); }

        void UpdateGameModeUI()
        {
            if (gameModeText != null)
                gameModeText.text = selectedMode == GameMode.TimeTrial ? "TIME TRIAL" : "RACE";
        }

        void Update()
        {
            if (!_isPanelOpen || navButtons == null || navButtons.Length == 0) return;
            if (_navTimer > 0f) { _navTimer -= Time.deltaTime; return; }

            var kb = Keyboard.current;

            bool navPrev = kb != null && (kb.leftArrowKey.isPressed  || kb.upArrowKey.isPressed);
            bool navNext = kb != null && (kb.rightArrowKey.isPressed || kb.downArrowKey.isPressed);
            bool submit  = kb != null && kb.enterKey.wasPressedThisFrame;

            foreach (var gp in Gamepad.all)
            {
                navPrev |= gp.dpad.left.isPressed   || gp.dpad.up.isPressed   || gp.leftStick.x.ReadValue() < -0.5f || gp.leftStick.y.ReadValue() >  0.5f;
                navNext |= gp.dpad.right.isPressed  || gp.dpad.down.isPressed || gp.leftStick.x.ReadValue() >  0.5f || gp.leftStick.y.ReadValue() < -0.5f;
                submit  |= gp.buttonSouth.wasPressedThisFrame;
            }

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

        public void OnNext()     => ShowTrack((currentIndex + 1) % tracks.Count);
        public void OnPrevious() => ShowTrack((currentIndex - 1 + tracks.Count) % tracks.Count);

        public void OnConfirm()
        {
            if (tracks.Count == 0) return;
            _isPanelOpen = false;
            ClearFocus();

            if (GameSession.Instance != null)
            {
                GameSession.Instance.SelectedTrackScene = tracks[currentIndex].sceneName;
                GameSession.Instance.SelectedTrackType  = tracks[currentIndex].trackType;
                GameSession.Instance.SelectedGameMode   = selectedMode;
            }

            if (SaveManager.Instance != null)
                SaveManager.Instance.SetLastSelectedTrack(tracks[currentIndex].sceneName);

            LoadScene(loadingScene);
        }

        public void OnBack()
        {
            _isPanelOpen = false;
            ClearFocus();
            bool isSplitScreen = GameSession.Instance != null && GameSession.Instance.IsSplitScreen;
            if (isSplitScreen)
            {
                splitScreenCharacterSelectManager?.OnPanelOpen();
                panelManager?.OpenPanel("Split Screen Character Select");
            }
            else
            {
                characterSelectManager?.OnPanelOpen();
                panelManager?.OpenPanel("Character Select");
            }
        }

        void ShowTrack(int index)
        {
            if (tracks.Count == 0) return;

            currentIndex = index;
            TrackEntry entry = tracks[currentIndex];

            if (trackNameText        != null) trackNameText.text        = entry.displayName;
            if (trackDescriptionText != null) trackDescriptionText.text = entry.description;
            if (pageText             != null) pageText.text             = $"{currentIndex + 1} / {tracks.Count}";

            if (trackPreviewImage != null)
            {
                trackPreviewImage.enabled = entry.previewImage != null;
                if (entry.previewImage != null)
                    trackPreviewImage.sprite = entry.previewImage;
            }
        }
    }
}
