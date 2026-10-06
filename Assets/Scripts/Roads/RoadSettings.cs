using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    public enum AirClass { Grounded, Hop, Jump }

    [Serializable]
    public struct AirZone
    {
        public float from;
        public float to;
        public AirClass airClass;
    }

    [Serializable]
    public class RoadSettings
    {
        [Header("Cross-section")]
        [Min(0)] public int lanesForward = 2;
        [Min(0)] public int lanesBackward;
        public float laneWidth = 6f;
        public float shoulder = 3f;

        [Header("Banking")]
        [Range(0f, 20f)] public float maxBank = 13.8f;
        public float fullBankRadius = 414f;
        public float maxRollRate = 0.15f;
        public float bankSmoothing = 100f;

        [Header("Height")]
        public float heightSmoothing;

        [Header("Barriers")]
        public float barrierHeight = 1.1f;
        public float barrierThickness = 0.5f;

        [Header("Air")]
        public AirClass airClass = AirClass.Grounded;
        public List<AirZone> airZones = new List<AirZone>();

        public float Width => (lanesForward + lanesBackward) * laneWidth + 2f * shoulder;

        // One carriageway of a 6-lane freeway: two of these, one each way, with a median between
        public static RoadSettings Freeway() => new RoadSettings { lanesForward = 3 };
        public static RoadSettings Highway() => new RoadSettings { lanesForward = 2, lanesBackward = 2 };
        public static RoadSettings Ramp() => new RoadSettings { lanesForward = 1 };

        public AirClass AirClassAt(float station)
        {
            foreach (var zone in airZones)
                if (station >= zone.from && station <= zone.to)
                    return zone.airClass;
            return airClass;
        }

        public IEnumerable<(float offset, bool forward)> Lanes()
        {
            float left = -Width / 2f + shoulder;
            for (int i = 0; i < lanesForward + lanesBackward; i++)
                yield return (left + laneWidth * (i + 0.5f), i < lanesForward);
        }
    }
}
