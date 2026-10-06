using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.DevTools
{
    public static class RaceSelection
    {
        public struct Pick
        {
            public string character;
            public string bike;
            public bool IsSet => !string.IsNullOrEmpty(character) && !string.IsNullOrEmpty(bike);
            public override string ToString() => character + " on " + bike;
        }

        public static readonly List<Pick> Players = new List<Pick>();

        public static int AiRacers = 5;

        public static void Clear() { Players.Clear(); }

        public static void SetPlayer(int index, string character, string bike)
        {
            while (Players.Count <= index) Players.Add(default);
            Players[index] = new Pick { character = character, bike = bike };
        }
        
        public static bool Ready
        {
            get
            {
                if (Players.Count == 0) return false;
                foreach (var p in Players) if (!p.IsSet) return false;
                return true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnBoot() => Clear();
    }
}
