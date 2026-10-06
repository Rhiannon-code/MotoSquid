using MotoSquid.Bike;
using MotoSquid.Characters;
using MotoSquid.Core;
using MotoSquid.Rider;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.AI
{
    // Owns the policy of who the AI are, not the spawning: RaceManager instantiates the grid and asks
    // this for one bike+character combo per slot
    [DefaultExecutionOrder(-100)]
    public class AIRacerRandomiser : MonoBehaviour
    {
        public struct Combo
        {
            public GameObject bikePrefab;
            public string character;
            public override string ToString() =>
                (character ?? "?") + " on " + (bikePrefab != null ? bikePrefab.name : "?");
        }

        [Header("Bikes the AI may ride, one entry per bike in the roster")]
        [SerializeField] BikeScriptableObject[] bikes = new BikeScriptableObject[0];

        [Header("Characters the AI may ride as")]
        [SerializeField] CharacterLibrary characterLibrary;
        [SerializeField] string[] eligibleCharacters = new string[0];

        [Header("Never hand the AI the player's exact bike + character pairing")]
        [SerializeField] bool excludePlayerCombo = true;
        [SerializeField] bool excludePlayerCharacter;

        [Header("Bikes already placed in the scene, if any still are")]
        [SerializeField] GameObject[] aiBikes = new GameObject[0];

        // Every combo is dealt once before any repeats, so a small roster still spreads across the grid
        public Combo[] Draw(int count)
        {
            var pool = BuildCombos();
            if (pool.Count == 0)
            {
                Debug.LogWarning("[AIRacerRandomiser] No eligible bike+character combos, the grid will " +
                                 "fall back to RaceManager's single aiPrefab.", this);
                return new Combo[0];
            }

            var drawn = new Combo[count];
            var bag = new List<Combo>();
            for (int i = 0; i < count; i++)
            {
                if (bag.Count == 0) { bag.AddRange(pool); Shuffle(bag); }
                drawn[i] = bag[bag.Count - 1];
                bag.RemoveAt(bag.Count - 1);
            }
            return drawn;
        }

        public struct RosterPick
        {
            public BikeScriptableObject bike;
            public string character;
        }

        // Split screen P2 draws from the same roster the AI do, so there is one definition of who can
        // race. It returns the bike DEFINITION rather than a prefab because P2 needs playerPrefab where
        // the AI take aiPrefab. Both differing is preferred over only one, so P2 reads as a distinct
        // racer rather than P1 in a different jacket
        public bool TryDrawDistinctFrom(BikeScriptableObject avoidBike, string avoidCharacter, out RosterPick pick)
        {
            pick = default;

            var characters = BuildCharacterPool();
            if (characters.Count == 0 || bikes == null || bikes.Length == 0) return false;

            var bothDiffer = new List<RosterPick>();
            var eitherDiffers = new List<RosterPick>();

            foreach (var bike in bikes)
            {
                if (bike == null || bike.playerPrefab == null) continue;

                foreach (string character in characters)
                {
                    var candidate = new RosterPick { bike = bike, character = character };
                    bool bikeDiffers = bike != avoidBike;
                    bool charDiffers = !string.Equals(character, avoidCharacter,
                                                      System.StringComparison.OrdinalIgnoreCase);

                    if (bikeDiffers && charDiffers) bothDiffer.Add(candidate);
                    if (bikeDiffers || charDiffers) eitherDiffers.Add(candidate);
                }
            }

            var pool = bothDiffer.Count > 0 ? bothDiffer : eitherDiffers;
            if (pool.Count == 0) return false;

            pick = pool[Random.Range(0, pool.Count)];
            return true;
        }

        List<Combo> BuildCombos()
        {
            var characters = BuildCharacterPool();
            var combos = new List<Combo>();

            foreach (var bike in bikes)
            {
                if (bike == null || bike.aiPrefab == null) continue;
                foreach (var character in characters)
                    combos.Add(new Combo { bikePrefab = bike.aiPrefab, character = character });
            }

            if (!excludePlayerCombo) return combos;

            var session = GameSession.Instance;
            var playerBike = session != null ? session.SelectedBike : null;
            string playerCharacter = session != null ? session.SelectedCharacterName : null;
            if (playerBike == null || playerBike.aiPrefab == null || string.IsNullOrEmpty(playerCharacter))
                return combos;

            // Only when something is left, a one-bike one-character roster must still fill the grid
            var trimmed = combos.FindAll(c => c.bikePrefab != playerBike.aiPrefab ||
                                              !string.Equals(c.character, playerCharacter,
                                                             System.StringComparison.OrdinalIgnoreCase));
            return trimmed.Count > 0 ? trimmed : combos;
        }

        List<string> BuildCharacterPool()
        {
            var pool = new List<string>();

            if (eligibleCharacters != null && eligibleCharacters.Length > 0)
                pool.AddRange(eligibleCharacters);
            else if (characterLibrary != null)
                foreach (var character in characterLibrary.characters)
                    if (character != null) pool.Add(character.characterName);

            pool.RemoveAll(string.IsNullOrEmpty);

            var session = GameSession.Instance;
            if (excludePlayerCharacter && session != null && pool.Count > 1)
                pool.RemoveAll(n => string.Equals(n, session.SelectedCharacterName,
                                                  System.StringComparison.OrdinalIgnoreCase));

            return pool;
        }

        static void Shuffle(List<Combo> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // Scene placed AI are still supported, they get a character but keep the bike they were built as
        void Awake()
        {
            if (aiBikes == null || aiBikes.Length == 0) return;

            var characters = BuildCharacterPool();
            if (characters.Count == 0) return;

            var bag = new List<string>();
            foreach (var bike in aiBikes)
            {
                if (bike == null) continue;
                if (bag.Count == 0) bag.AddRange(characters);

                int pick = Random.Range(0, bag.Count);
                string characterName = bag[pick];
                bag.RemoveAt(pick);

                var switcher = bike.GetComponentInChildren<RiderSwitch>(true);
                if (switcher == null)
                {
                    Debug.LogError($"[AIRacerRandomiser] '{bike.name}' has no RiderSwitch.", bike);
                    continue;
                }
                if (!switcher.SetRider(characterName))
                    Debug.LogWarning($"[AIRacerRandomiser] '{bike.name}' carries no rider for '{characterName}'.", bike);
            }
        }
    }
}
