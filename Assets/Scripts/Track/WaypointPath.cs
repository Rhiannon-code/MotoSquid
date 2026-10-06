using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Track
{
    // Baked race waypoints, one list per road section. Road-tool neutral: a generator for the
    // current road tool fills roadWaypoints, and everything at runtime reads only this.
    public class WaypointPath : MonoBehaviour
    {
        public List<RoadWaypointList> roadWaypoints = new List<RoadWaypointList>();

        public int RoadCount => roadWaypoints.Count;

        public RaceWaypoint[] GetWaypoints(int roadIndex)
        {
            if (roadIndex < 0 || roadIndex >= roadWaypoints.Count) return null;
            return roadWaypoints[roadIndex]?.waypoints;
        }

        // Closest point on the road's waypoint polyline, measured horizontally so jumps and ramps
        // don't read as off track.
        public bool TryGetClosestPoint(int roadIndex, Vector3 pos, out Vector3 closest)
        {
            closest = default;
            var wps = GetWaypoints(roadIndex);
            if (wps == null || wps.Length < 2) return false;

            float bestSq = float.MaxValue;
            for (int i = 0; i < wps.Length - 1; i++)
            {
                Vector3 a = wps[i].position, ab = wps[i + 1].position - a;
                Vector3 abFlat = new Vector3(ab.x, 0f, ab.z);
                float len2 = abFlat.sqrMagnitude;
                float t = len2 > 1e-4f ? Mathf.Clamp01(Vector3.Dot(new Vector3(pos.x - a.x, 0f, pos.z - a.z), abFlat) / len2) : 0f;
                Vector3 p = a + ab * t;
                float sq = (pos.x - p.x) * (pos.x - p.x) + (pos.z - p.z) * (pos.z - p.z);
                if (sq < bestSq)
                {
                    bestSq = sq;
                    closest = p;
                }
            }
            return true;
        }

        [ContextMenu("Clear Waypoints")]
        public void ClearAll() => roadWaypoints.Clear();

        void OnDrawGizmos()
        {
            foreach (var entry in roadWaypoints)
            {
                var wps = entry?.waypoints;
                if (wps == null || wps.Length < 2) continue;
                for (int i = 0; i < wps.Length; i++)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(wps[i].position, 0.5f);
                    if (i == 0) continue;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(wps[i - 1].position, wps[i].position);
                }
            }
        }
    }
}
