using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace MotoSquid.Traffic
{
    // The traffic lane splines, one list per road, rebuilt from the GeneratedTrafficLane children a
    // road-tool generator leaves behind. Everything at runtime reads only this.
    public class TrafficLaneNetwork : MonoBehaviour
    {
        [HideInInspector] public List<List<SplineContainer>> laneSplines = new List<List<SplineContainer>>();
        [HideInInspector] public List<SplineContainer> raceLineSplines = new List<SplineContainer>();

        void Awake() => RebuildFromChildren();

        public void RebuildFromChildren()
        {
            laneSplines.Clear();
            raceLineSplines.Clear();

            var lanes = GetComponentsInChildren<GeneratedTrafficLane>();
            int maxRoad = -1;
            foreach (var lane in lanes)
                maxRoad = Mathf.Max(maxRoad, lane.roadIndex);
            for (int r = 0; r <= maxRoad; r++)
                laneSplines.Add(new List<SplineContainer>());

            foreach (var lane in lanes)
            {
                var container = lane.GetComponent<SplineContainer>();
                if (container == null) continue;
                var roadList = laneSplines[lane.roadIndex];
                while (roadList.Count <= lane.laneIndex)
                    roadList.Add(null);
                roadList[lane.laneIndex] = container;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child.name.EndsWith("_RaceLine")) continue;
                var container = child.GetComponent<SplineContainer>();
                if (container != null) raceLineSplines.Add(container);
            }
        }

        [ContextMenu("Clear Generated Splines")]
        public void ClearGenerated()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (!child.name.Contains("_Lane") && !child.name.Contains("_RaceLine")) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            laneSplines.Clear();
            raceLineSplines.Clear();
        }

        public SplineContainer GetLaneSpline(int roadIndex, int laneIndex)
        {
            if (roadIndex < 0 || roadIndex >= laneSplines.Count) return null;
            var roadLanes = laneSplines[roadIndex];
            if (laneIndex < 0 || laneIndex >= roadLanes.Count) return null;
            return roadLanes[laneIndex];
        }

        public SplineContainer GetRaceLineSpline(int roadIndex)
        {
            if (roadIndex < 0 || roadIndex >= raceLineSplines.Count) return null;
            return raceLineSplines[roadIndex];
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            const int segments = 300;
            foreach (var sc in raceLineSplines)
            {
                if (sc == null || sc.Spline == null || sc.Spline.Count < 2) continue;
                Vector3 prev = sc.transform.TransformPoint((Vector3)(float3)SplineUtility.EvaluatePosition(sc.Spline, 0f));
                for (int i = 1; i <= segments; i++)
                {
                    Vector3 cur = sc.transform.TransformPoint((Vector3)(float3)SplineUtility.EvaluatePosition(sc.Spline, (float)i / segments));
                    Gizmos.DrawLine(prev, cur);
                    prev = cur;
                }
            }
        }
    }
}
