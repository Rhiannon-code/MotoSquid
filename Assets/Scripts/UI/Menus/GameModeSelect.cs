using MotoSquid.Core;
using UnityEngine;
using Michsky.UI.Heat;

namespace MotoSquid.UI
{
    public class GameModeSelect : MonoBehaviour
    {
        public HorizontalSelector gameModeSelector;

        public GameMode[] modes = { GameMode.Race, GameMode.SplitScreen };

        void Start()
        {
            if (gameModeSelector == null)
            {
                Debug.LogError("GameModeSelect has no HorizontalSelector assigned.", this);
                return;
            }

            gameModeSelector.items.Clear();

            foreach (GameMode mode in modes)
                gameModeSelector.CreateNewItem(Prettify(mode.ToString()));

            gameModeSelector.InitializeSelector();
        }

        // The enum names are the labels, so "TimeTrial" reads as one word on screen
        static string Prettify(string name) =>
            System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");

        public GameMode GetSelectedGameMode()
        {
            if (modes == null || modes.Length == 0) return GameMode.Race;

            int index = gameModeSelector != null
                ? Mathf.Clamp(gameModeSelector.index, 0, modes.Length - 1)
                : 0;

            return modes[index];
        }
    }
}
