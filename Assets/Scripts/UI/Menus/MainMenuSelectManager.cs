using MotoSquid.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotoSquid.UI
{
    public class MainMenuSelectManager : MenuManagerBase
    {
        [Header("Selectors")]
        public CharacterSelect characterSelect;
        public BikeSelect bikeSelect;
        public GameModeSelect gameModeSelect;

        [Header("Player 2 (split screen, optional)")]
        // Left empty, P2 inherits P1's bike and character, which is enough to get two players racing.
        // Assign a second pair of selectors when the 2P select screen exists.
        public CharacterSelect characterSelectP2;
        public BikeSelect bikeSelectP2;

        [Header("Destination")]
        public string loadingScene = "LoadingScreen";
        public string raceScene    = "Main 2";
        public TrackType raceTrackType = TrackType.PointToPoint;

        [Header("Split screen needs a controller")]
        // Shown when Split Screen is confirmed with no gamepad connected. Optional, the start is refused either way
        public GameObject noControllerNotice;
        public float noticeSeconds = 3f;

        public void OnConfirm()
        {
            if (characterSelect == null || bikeSelect == null || gameModeSelect == null)
            {
                Debug.LogError("One or more required select components are not assigned in MainMenuSelectManager.", this);
                return;
            }

            // With no pad, SplitScreenSetup has to put both players on the keyboard, and the keyboard then drives both bikes
            if (gameModeSelect.GetSelectedGameMode() == GameMode.SplitScreen && Gamepad.all.Count == 0)
            {
                Debug.LogWarning("[MainMenuSelectManager] Split Screen refused: no controller connected.", this);
                if (noControllerNotice != null)
                {
                    StopAllCoroutines();
                    StartCoroutine(ShowNoControllerNotice());
                }
                return;
            }

            var session = GameSession.Instance;
            if (session != null)
            {
                var character = characterSelect.CurrentCharacter;
                if (character != null) session.SelectedCharacterName = character.characterName;

                session.SelectedBike      = bikeSelect.CurrentBike;
                session.SelectedBikeIndex = bikeSelect.currentIndex;
                // Split Screen is resolved here and nowhere else: downstream it is an ordinary Race that
                // happens to have two humans, so no gameplay code needs to know the mode exists. RaceManager
                // already drops every AI when IsSplitScreen is set.
                var mode = gameModeSelect.GetSelectedGameMode();
                bool twoPlayer = mode == GameMode.SplitScreen;

                session.SelectedGameMode = twoPlayer ? GameMode.Race : mode;

                session.SelectedTrackScene = raceScene;
                session.SelectedTrackType  = raceTrackType;
                session.IsSplitScreen      = twoPlayer;

                if (twoPlayer)
                {
                    // Left EMPTY when there is no 2P select screen: PlayerRacerSetup then draws P2 a
                    // random bike + character from the AI roster instead of handing them a copy of P1.
                    var characterP2 = characterSelectP2 != null ? characterSelectP2.CurrentCharacter : null;
                    session.SelectedCharacterNameP2 = characterP2 != null ? characterP2.characterName : null;

                    session.SelectedBikeP2      = bikeSelectP2 != null ? bikeSelectP2.CurrentBike : null;
                    session.SelectedBikeIndexP2 = bikeSelectP2 != null ? bikeSelectP2.currentIndex : 0;
                }
            }

            LoadScene(loadingScene);
        }

        System.Collections.IEnumerator ShowNoControllerNotice()
        {
            noControllerNotice.SetActive(true);
            yield return new WaitForSecondsRealtime(noticeSeconds);
            noControllerNotice.SetActive(false);
        }
    }
}
