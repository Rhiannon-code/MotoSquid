using MotoSquid.Core;
using Michsky.UI.Heat;
using UnityEngine;

namespace MotoSquid.UI
{
    public class MainMenuManager : MenuManagerBase
    {
        [Header("Panel Manager")]
        [SerializeField] private PanelManager panelManager;

        [Header("Sub Panel Managers")]
        [SerializeField] private CharacterSelectManager characterSelectManager;
        [SerializeField] private SplitScreenCharacterSelectManager splitScreenCharacterSelectManager;

        void OnDestroy() => StopAllCoroutines();

        public void OnPlaySinglePlayer()
        {
            splitScreenCharacterSelectManager?.CleanupPreviews();
            if (GameSession.Instance != null)
                GameSession.Instance.IsSplitScreen = false;
            characterSelectManager?.OnPanelOpen();
            panelManager?.OpenPanel("Character Select");
        }

        public void OnPlaySplitScreen()
        {
            characterSelectManager?.CleanupPreview();
            if (GameSession.Instance != null)
                GameSession.Instance.IsSplitScreen = true;
            splitScreenCharacterSelectManager?.OnPanelOpen();
            panelManager?.OpenPanel("Split Screen Character Select");
        }

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OnSettings()
        {
            panelManager?.OpenPanel("Settings");
        }

        public void OnSettingsBack()
        {
            panelManager?.OpenPanel("Home");
        }
    }
}
