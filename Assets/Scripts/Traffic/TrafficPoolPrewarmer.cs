using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Traffic
{
    public class TrafficPoolPrewarmer : MonoBehaviour
    {
        public static TrafficPoolPrewarmer Instance { get; private set; }

        [Header("Traffic Vehicles must match TrafficSpawner's vehiclePrefabs (same order)")]
        public GameObject[] vehiclePrefabs;
        public int prewarmCountPerPrefab = 5;
        public readonly List<Queue<GameObject>> Pools = new List<Queue<GameObject>>();

        [Header("One Shot Prefabs instantiated once to trigger physics bake, then discarded")]
        public GameObject[] ragdollPrefabs;
        public GameObject[] dummyBikePrefabs;
        public GameObject skidmarksPrefab;
        public GameObject tireSmokePrefab;

        [Header("Riders enabled by the selection, warmed so the swap does not hitch at the grid")]
        public GameObject[] riderPrefabs;

        [Header("Racer bikes the race scene instantiates, player and AI, every bike in the roster")]
        public GameObject[] racerPrefabs;

        [Header("Instantiations per frame, so the loading bar keeps moving")]
        public int budgetPerFrame = 4;

        public bool Finished { get; private set; }

        // Share of the pooling and baking done, for the loading bar
        public float Progress => Finished ? 1f : _total > 0 ? (float)_done / _total : 0f;
        int _done, _total;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[TrafficPoolPrewarmer] '{name}' is a second prewarmer, '{Instance.name}' " +
                                 "already owns the pool. Removing this component only; delete it from the " +
                                 "scene to clear this warning.", this);
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public IEnumerator PrewarmAsync()
        {
            int budget = Mathf.Max(1, budgetPerFrame);
            int spent  = 0;

            _done  = 0;
            _total = 2;
            if (vehiclePrefabs != null)
                foreach (var prefab in vehiclePrefabs)
                    if (prefab != null) _total += prewarmCountPerPrefab;
            foreach (var _ in Flatten(ragdollPrefabs, dummyBikePrefabs, riderPrefabs, racerPrefabs)) _total++;

            if (vehiclePrefabs != null)
            {
                for (int i = 0; i < vehiclePrefabs.Length; i++)
                {
                    var queue = new Queue<GameObject>();
                    Pools.Add(queue);

                    if (vehiclePrefabs[i] == null) continue;

                    for (int j = 0; j < prewarmCountPerPrefab; j++)
                    {
                        GameObject go = Instantiate(vehiclePrefabs[i]);
                        go.SetActive(false);
                        go.transform.SetParent(transform);
                        queue.Enqueue(go);
                        _done++;

                        if (++spent >= budget) { spent = 0; yield return null; }
                    }
                }
            }

            foreach (var prefab in Flatten(ragdollPrefabs, dummyBikePrefabs, riderPrefabs, racerPrefabs))
            {
                BakeAndDiscard(prefab);
                _done++;
                if (++spent >= budget) { spent = 0; yield return null; }
            }

            BakeAndDiscard(skidmarksPrefab);
            BakeAndDiscard(tireSmokePrefab);

            Finished = true;
        }

        static IEnumerable<GameObject> Flatten(params GameObject[][] groups)
        {
            foreach (var group in groups)
            {
                if (group == null) continue;
                foreach (var prefab in group)
                    if (prefab != null) yield return prefab;
            }
        }

        void BakeAndDiscard(GameObject prefab)
        {
            if (prefab == null) return;
            GameObject go = Instantiate(prefab);
            Destroy(go);
        }

        public void Consume()
        {
            Instance = null;
            Destroy(gameObject);
        }
    }
}
