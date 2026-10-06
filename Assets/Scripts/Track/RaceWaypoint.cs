using UnityEngine;

namespace MotoSquid.Track
{
    [System.Serializable]
    public struct RaceWaypoint
    {
        public Vector3 position;
        public float roadT;
    }

    [System.Serializable]
    public class RoadWaypointList
    {
        public RaceWaypoint[] waypoints = new RaceWaypoint[0];
    }
}
