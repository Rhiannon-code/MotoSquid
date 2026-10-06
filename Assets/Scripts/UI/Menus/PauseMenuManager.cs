using MotoSquid.Combat;
using Michsky.UI.Heat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MotoSquid.UI
{
    public class PauseMenuManager : MenuManagerBase
    {
        [Header("Pause Menu")]
        [SerializeField] private UIPopup      pauseMenuContainer;
        [SerializeField] private PanelManager panelManager;

        [Header("Blur")]
        [SerializeField] private Volume pauseBlurVolume;

        [Header("Scene Names")]
        [SerializeField] private string mainMenuScene = "MainMenu";

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        private InputAction _pauseAction;
        private InputAction _cancelAction;

        private bool _isPaused;
        private bool _isOnPausePage;

        void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("[PauseMenuManager] inputActions is not assigned, pause menu disabled.", this);
                enabled = false;
                return;
            }

            var menuMap     = inputActions.FindActionMap("Menu", throwIfNotFound: true);
            _pauseAction    = menuMap.FindAction("Pause",    throwIfNotFound: true);
            _cancelAction   = menuMap.FindAction("Cancel",   throwIfNotFound: true);
        }

        void OnEnable()
        {
            _pauseAction.Enable();
            _pauseAction.performed += OnPausePerformed;
        }

        void OnDisable()
        {
            _pauseAction.performed -= OnPausePerformed;
            _pauseAction.Disable();
            DisableMenuNavigation();
        }

        void OnDestroy()
        {
            if (_isPaused) { Time.timeScale = 1f; AudioListener.pause = false; }
            StopAllCoroutines();
        }

        void OnPausePerformed(InputAction.CallbackContext ctx) => TogglePause();

        void EnableMenuNavigation()
        {
            _cancelAction.Enable();
            _cancelAction.performed += OnCancel;
        }

        void DisableMenuNavigation()
        {
            _cancelAction.performed -= OnCancel;
            _cancelAction.Disable();
        }

        void OnCancel(InputAction.CallbackContext ctx)
        {
            if (!_isOnPausePage)
                OnSettingsBack();
            else
                OnContinue();
        }

        // OpenPanel skips selection when the panel is already current, so opening the pause page never selects on its own
        void SelectPauseFirst()
        {
            var pausePanel = panelManager?.panels.Find(p => p.panelName == "Pause");
            if (pausePanel == null || ControllerManager.instance == null) return;
            EventSystem.current?.SetSelectedGameObject(null);
            ControllerManager.instance.SelectUIObject(pausePanel.firstSelected);
        }

        public void TogglePause()
        {
            if (_isPaused) OnContinue();
            else Pause();
        }

        void Pause()
        {
            _isPaused        = true;
            _isOnPausePage   = true;
            // Cancel any in progress hit stop first so its restore can't later un pause us
            HitStopController.Instance?.CancelActiveFreeze();
            Time.timeScale   = 0f;
            AudioListener.pause = true;   // Freeze ALL game audio (engine, music, wind, sfx) while paused
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            if (pauseBlurVolume != null) pauseBlurVolume.enabled = true;
            ShowPanel(pauseMenuContainer);
            panelManager?.OpenPanel("Pause");
            EnableMenuNavigation();
            SelectPauseFirst();
        }

        public void OnContinue()
        {
            _isPaused        = false;
            _isOnPausePage   = false;
            Time.timeScale   = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            if (pauseBlurVolume != null) pauseBlurVolume.enabled = false;
            HidePanel(pauseMenuContainer);
            DisableMenuNavigation();
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void OnSettings()
        {
            _isOnPausePage = false;
            panelManager?.OpenPanel("Settings");
        }

        public void OnSettingsBack()
        {
            _isOnPausePage = true;
            panelManager?.OpenPanel("Pause");
        }

        // Reload the active race scene from the pause menu ("Restart Race").
        public void RestartRace()
        {
            _isPaused      = false;
            _isOnPausePage = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            if (pauseBlurVolume != null) pauseBlurVolume.enabled = false;
            DisableMenuNavigation();
            EventSystem.current?.SetSelectedGameObject(null);
            LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        public void OnMainMenu()
        {
            _isPaused      = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (pauseBlurVolume != null) pauseBlurVolume.enabled = false;
            DisableMenuNavigation();
            LoadScene(mainMenuScene);
        }

        public void OnQuit()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
