using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    public enum AirClass { Grounded, Hop, Jump }

    [Serializable]
    public struct BankZone
    {
        public float from;
        public float to;
        [Tooltip("Lean in degrees, replacing the automatic bank here. Positive leans into a left turn. Capped at Max Bank.")]
        public float degrees;
    }

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
        [Tooltip("Corners whose lean you set by hand (stations in metres along the road).")]
        public List<BankZone> bankZones = new List<BankZone>();

        [Header("Plan")]
        [Tooltip("The tightest corner this road may have (m); the plan check allows 10% under it.")]
        public float minRadius = RoadDesign.MinPlanRadius;

        [Header("Height")]
        public float heightSmoothing;
        [Tooltip("Steepest grade this road is laid out to, as rise over run. The checker allows 10% over it.")]
        public float maxGrade = 0.05f;

        [Header("Barriers")]
        public float barrierHeight = 1.1f;
        public float barrierThickness = 0.5f;

        [Header("Air")]
        public AirClass airClass = AirClass.Grounded;
        public List<AirZone> airZones = new List<AirZone>();

        public float Width => (lanesForward + lanesBackward) * laneWidth + 2f * shoulder;

        // One carriageway of a 6-lane freeway: two of these, one each way, with a median between
        public static RoadSettings Freeway() => new RoadSettings { lanesForward = 3 };
        // Both directions of a freeway on one surface, no divider: riders can always cross into oncoming traffic.
        public static RoadSettings FreewayTwoWay() => new RoadSettings { lanesForward = 3, lanesBackward = 3 };
        public static RoadSettings Highway() => new RoadSettings { lanesForward = 2, lanesBackward = 2 };
        public static RoadSettings Arterial() => new RoadSettings { lanesForward = 2, lanesBackward = 2, maxBank = 0f, minRadius = RoadDesign.StreetMinRadius };
        public static RoadSettings Street() => new RoadSettings { lanesForward = 1, lanesBackward = 1, maxBank = 0f, minRadius = RoadDesign.StreetMinRadius };
        public static RoadSettings Ramp(int lanes = 1) => new RoadSettings { lanesForward = lanes };

        public RoadSettings Clone()
        {
            var copy = (RoadSettings)MemberwiseClone();
            copy.airZones = new List<AirZone>(airZones);
            copy.bankZones = new List<BankZone>(bankZones);
            return copy;
        }

        public bool TryBankAt(float station, out float degrees)
        {
            foreach (var zone in bankZones)
                if (station >= zone.from && station <= zone.to)
                {
                    degrees = Mathf.Clamp(zone.degrees, -maxBank, maxBank);
                    return true;
                }
            degrees = 0f;
            return false;
        }

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
