using MotoSquid.Combat;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.DevTools
{
    [CreateAssetMenu(fileName = "RacerRoster", menuName = "Rig Lab/Racer Roster")]
    public class RacerRoster : ScriptableObject
    {
        [Serializable]
        public class Character
        {
            public string displayName = "Voodoo";
            public GameObject riderModel;
            public Color tint = Color.white;
            public MeleeWeapon weapon;
            public bool Ready => riderModel != null;
            public string RiderObjectName => riderModel != null ? riderModel.name : displayName;
        }

        [Serializable]
        public class Bike
        {
            public string displayName = "Voodoo Bike";
            public GameObject bikePrefab;
            public bool Ready => bikePrefab != null;
        }

        public List<Character> characters = new List<Character>();
        public List<Bike> bikes = new List<Bike>();

        [Header("Grid")]
        public int aiRacers = 5;
        public IEnumerable<Character> ReadyCharacters
        {
            get { foreach (var c in characters) if (c != null && c.Ready) yield return c; }
        }

        public IEnumerable<Bike> ReadyBikes
        {
            get { foreach (var b in bikes) if (b != null && b.Ready) yield return b; }
        }

        public static string PrefabName(Character c, Bike b, bool ai) =>
            Safe(c.displayName) + "_" + Safe(b.displayName) + (ai ? "_AI" : "_Player");

        static string Safe(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Unnamed";
            var chars = s.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }
    }
}
