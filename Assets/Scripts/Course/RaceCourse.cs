using MotoSquid.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Course
{
    // The source of a race course (rule 13): an ordered run of roads from the open scene's RoadNetwork.
    // RaceCourseBuilder turns it into the scene objects a race needs; rebuilding replaces them.
    [CreateAssetMenu(fileName = "RaceCourse", menuName = "MotoSquid/Race Course")]
    public class RaceCourse : ScriptableObject
    {
        [Serializable]
        public class Leg
        {
            // The Road object's name under the Road Network
            public string road;
            // Ride the road against its spline direction (the other carriageway on a two-way road)
            public bool reverse;
            // Stations along the road in metres; a negative value means "where the neighbouring leg joins"
            public float fromStation = -1f;
            public float toStation = -1f;
        }

        public string displayName = "New Course";
        public TrackType trackType = TrackType.Circuit;
        public GameMode gameMode = GameMode.Race;
        [Min(1)] public int laps = 3;
        public List<Leg> legs = new List<Leg>();

        [Header("Start")]
        // Metres into the first leg. The grid forms behind the line, 10 m plus 6 m per racer
        public float startLineStation = 80f;

        [Header("Racing line")]
        // Metres between race waypoints; the road is sampled every metre
        public float waypointSpacing = 5f;
        // Lateral offset from the road centreline; 0 = centre of the travelled carriageway
        public float lateralOffset;

        [Header("Checkpoint time extension")]
        public bool timeExtension;
        public float startingTimeSeconds = 60f;
        public float gateSpacing = 400f;
        public float gateTimeBonus = 5f;
    }
}
