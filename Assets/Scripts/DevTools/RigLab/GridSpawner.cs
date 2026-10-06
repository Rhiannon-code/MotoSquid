using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MotoSquid.DevTools
{
    public class GridSpawner : MonoBehaviour
    {
        public RacerRoster roster;
        public Transform[] gridSlots;
        public string racerResourcePath = "Racers";
        public bool requireSelection = true;
        public List<GameObject> Spawned { get; } = new List<GameObject>();

        void Start() => Spawn();

        public void Spawn()
        {
            if (roster == null) { Debug.LogError("[Grid] no roster assigned, nothing to spawn.", this); return; }
            if (gridSlots == null || gridSlots.Length == 0) { Debug.LogError("[Grid] no grid slots assigned.", this); return; }

            var picks = new List<RaceSelection.Pick>(RaceSelection.Players);

            if (!RaceSelection.Ready)
            {
                if (requireSelection)
                {
                    Debug.LogError("[Grid] no complete selection, did the select screen run? " +
                                   "Untick requireSelection to test this scene on its own.", this);
                    return;
                }
                var c = roster.ReadyCharacters.FirstOrDefault();
                var b = roster.ReadyBikes.FirstOrDefault();
                if (c == null || b == null) { Debug.LogError("[Grid] roster has no ready pairing to fall back on.", this); return; }
                picks.Clear();
                picks.Add(new RaceSelection.Pick { character = c.displayName, bike = b.displayName });
                Debug.LogWarning("[Grid] no selection, falling back to " + picks[0], this);
            }

            int slot = 0;
            foreach (var pick in picks)
            {
                if (slot >= gridSlots.Length) { Debug.LogError("[Grid] more players than grid slots.", this); break; }
                SpawnAt(slot++, pick, ai: false);
            }

            var pool = (from c in roster.ReadyCharacters
                        from b in roster.ReadyBikes
                        select new RaceSelection.Pick { character = c.displayName, bike = b.displayName }).ToList();
            if (pool.Count == 0) { Debug.LogError("[Grid] roster has no ready pairings for AI.", this); return; }

            int wanted = Mathf.Min(RaceSelection.AiRacers, gridSlots.Length - slot);
            for (int i = 0; i < wanted; i++)
                SpawnAt(slot++, pool[Random.Range(0, pool.Count)], ai: true);

            Debug.Log("[Grid] " + Spawned.Count + " racer(s) on the grid, " + picks.Count + " player(s), " +
                      (Spawned.Count - picks.Count) + " AI.");
        }

        void SpawnAt(int slot, RaceSelection.Pick pick, bool ai)
        {
            var prefab = Find(pick, ai, out var name);
            if (prefab == null)
            {
                Debug.LogError("[Grid] no prefab '" + name + "' under Resources/" + racerResourcePath +
                               " , build the racer matrix (50), and check the pairing is not still waiting on art.", this);
                return;
            }

            var go = Instantiate(prefab, gridSlots[slot].position, gridSlots[slot].rotation);
            go.name = name;
            Spawned.Add(go);
        }

        public GameObject Find(RaceSelection.Pick pick, bool ai, out string name)
        {
            name = null;
            var character = roster.characters.FirstOrDefault(c => c != null && c.displayName == pick.character);
            var bike = roster.bikes.FirstOrDefault(b => b != null && b.displayName == pick.bike);
            if (character == null || bike == null)
            {
                Debug.LogError("[Grid] '" + pick + "' is not on the roster.", this);
                return null;
            }

            name = RacerRoster.PrefabName(character, bike, ai);
            return Resources.Load<GameObject>(racerResourcePath + "/" + name);
        }
    }
}
