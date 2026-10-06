using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace MotoSquid.Traffic
{
    public class TramController : MonoBehaviour
    {
        [Header("Path")]
        public TrafficLaneNetwork laneNetwork;
        public int roadIndex = 0;
        public int[] laneIndices = { 2, 3 };

        [Header("Trams")]
        public GameObject tramPrefab;
        public int tramsPerLane = 3;
        public float speedKmh = 50f;
        public float respawnDelay = 3f;

        private struct TramState
        {
            public GameObject              obj;
            public SplineMover    mover;
            public SplineContainer         spline;
            public float                   respawnTimer;
        }

        private readonly List<TramState> _trams = new List<TramState>();

        IEnumerator Start()
        {
            if (laneNetwork == null)
            {
                Debug.LogError("[TramController] No laneNetwork assigned.", this);
                yield break;
            }
            if (tramPrefab == null)
            {
                Debug.LogError("[TramController] No tramPrefab assigned.", this);
                yield break;
            }

            yield return null;


            foreach (int laneIdx in laneIndices)
            {
                var spline = laneNetwork.GetLaneSpline(roadIndex, laneIdx);
                if (spline == null)
                {
                    Debug.LogError($"[TramController] Null spline for road {roadIndex} lane {laneIdx}. " +
                                   $"Road has {laneNetwork.laneSplines.Count} road entries.", this);
                    continue;
                }

                float len = spline.Spline.GetLength();

                if (len < 0.1f)
                {
                    Debug.LogError($"[TramController] Spline for lane {laneIdx} has near zero length ({len}). Skipping.", this);
                    continue;
                }

                for (int i = 0; i < tramsPerLane; i++)
                {
                    float startT = (float)i / tramsPerLane;
                    _trams.Add(SpawnTram(spline, startT));
                }
            }

        }

        void Update()
        {
            for (int i = 0; i < _trams.Count; i++)
            {
                TramState s = _trams[i];
                if (s.obj == null) continue;

                if (s.respawnTimer >= 0f)
                {
                    s.respawnTimer -= Time.deltaTime;
                    if (s.respawnTimer <= 0f)
                    {
                        SetRenderersEnabled(s.obj, true);
                        s.mover.Setup(s.spline, 0f, speedKmh / 3.6f, loop: false);
                        SnapToSpline(s.obj.transform, s.spline, 0f);
                        s.respawnTimer = -1f;
                    }
                    _trams[i] = s;
                    continue;
                }

                if (!s.mover.IsPlaying)
                {
                    SetRenderersEnabled(s.obj, false);
                    s.respawnTimer = respawnDelay;
                    _trams[i] = s;
                }
            }
        }

        private TramState SpawnTram(SplineContainer spline, float startT)
        {
            GameObject obj = Instantiate(tramPrefab);

            var mover = obj.GetComponent<SplineMover>();
            if (mover == null) mover = obj.AddComponent<SplineMover>();

            mover.Setup(spline, startT, speedKmh / 3.6f, loop: false);
            SnapToSpline(obj.transform, spline, startT);

            return new TramState { obj = obj, mover = mover, spline = spline, respawnTimer = -1f };
        }

        private static void SnapToSpline(Transform t, SplineContainer spline, float splineT)
        {
            spline.Spline.Evaluate(splineT, out float3 localPos, out float3 localTan, out _);
            Vector3 worldPos = spline.transform.TransformPoint((Vector3)localPos);
            Vector3 worldFwd = spline.transform.TransformDirection((Vector3)localTan);
            if (worldFwd.sqrMagnitude > 0.001f)
                t.SetPositionAndRotation(worldPos, Quaternion.LookRotation(worldFwd, Vector3.up));
            else
                t.position = worldPos;
        }

        private static void SetRenderersEnabled(GameObject obj, bool on)
        {
            foreach (var r in obj.GetComponentsInChildren<Renderer>())
                r.enabled = on;
        }
    }
}
