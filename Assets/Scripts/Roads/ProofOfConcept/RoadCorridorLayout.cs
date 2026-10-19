using System;
using System.Collections.Generic;
using UnityEngine;
using static MotoSquid.Roads.LayoutLegs;
using static MotoSquid.Roads.RoadDesign;
using static MotoSquid.Roads.RoadFeatures;

namespace MotoSquid.Roads
{
    [Serializable]
    public class CorridorSettings
    {
        // Bumped when the defaults change; an asset saved with older numbers is reset to the new ones.
        public const int CurrentVersion = 2;
        [HideInInspector] public int version;

        public DiamondSettings sunkenDiamond = new DiamondSettings
        {
            freewayHeight = -LevelSeparation, eastboundOffRamp = 0.08f, eastboundOnRamp = 0.07f, westboundOffRamp = 0.08f, westboundOnRamp = 0.07f
        };
        public DiamondSettings raisedDiamond = new DiamondSettings
        {
            freewayHeight = LevelSeparation, eastboundOffRamp = 0.07f, eastboundOnRamp = 0.08f, westboundOffRamp = 0.07f, westboundOnRamp = 0.08f
        };
        [Tooltip("Height of the flyovers, the C-D road and CityLink at the system interchanges (m).")]
        public float connectorHeight = LevelSeparation;
        [Tooltip("Grade every system-interchange connector climbs or drops at, as rise over run.")]
        public float connectorGrade = 0.07f;
    }

    // A West Gate corridor, west to east: a diamond with the freeway sunk in a cutting under its cross street,
    // the West Gate / CityLink interchange, and a diamond with the freeway raised on walls over its cross
    // street, with technical bends between them and hops on the approaches. Each feature is laid out along a
    // straight line, then bent onto a curved stretch of West Gate. CityLink runs north to a second interchange
    // with the tollway. Real Melbourne only taught what connects; the shapes are drawn for arcade riding.
    // Both directions share one surface wherever they run side by side at the same level (no divider), and
    // part into one-way roads only where they must. Traffic keeps left.
    public static class RoadCorridorLayout
    {
        static readonly RoadSettings WestGate = RoadSettings.FreewayTwoWay();

        public static List<RoadPocLayout.RoadDefinition> Roads(CorridorSettings settings)
        {
            var interchange = Interchange(settings.connectorHeight, settings.connectorGrade);
            var (nodeWest, nodeEast) = Footprint(interchange);
            var sunk = settings.sunkenDiamond;
            var raised = settings.raisedDiamond;
            var (sunkWest, sunkEast) = Footprint(Diamond("", 0f, sunk));
            var (raisedWest, raisedEast) = Footprint(Diamond("", 0f, raised));

            // Each feature's stretch of West Gate, measured along it: the diamonds include the freeway's climb
            // or drop either side of them.
            float sunkChange = FreewayChange(sunk.freewayHeight), raisedChange = FreewayChange(raised.freewayHeight);
            float sunkSpan = 2f * (sunkChange + Margin) + sunkEast - sunkWest;
            float nodeSpan = 2f * Margin + nodeEast - nodeWest;
            float raisedSpan = 2f * (raisedChange + Margin) + raisedEast - raisedWest;
            float sunkCentre = sunkChange + Margin - sunkWest;
            float nodeCentre = Margin - nodeWest;
            float raisedCentre = raisedChange + Margin - raisedWest;
            var sunkLine = Curve(sunkSpan, DiamondBendRadius, DiamondBendDegrees);
            var nodeLine = Curve(nodeSpan, InterchangeBendRadius, InterchangeBendDegrees);
            var raisedLine = Curve(raisedSpan, DiamondBendRadius, DiamondBendDegrees);
            var gapWest = Concat(new[] { Leg.S(RollClear) }, Esses, new[] { Leg.S(RollClear) });
            var gapEast = Concat(new[] { Leg.S(RollClear) }, Sweeper, new[] { Leg.S(RollClear) });
            var approachWest = Concat(new[] { Leg.S(HopRun) }, Kink, new[] { Leg.S(FreeEnd) });
            var approachEast = Concat(new[] { Leg.S(HopRun) }, Dogleg, new[] { Leg.S(FreeEnd) });

            var (start, heading, westLegs) = Toward(Vector2.zero, Vector2.right, approachWest);
            var legs = Concat(westLegs, sunkLine, gapWest, nodeLine, gapEast, raisedLine, approachEast);
            float s0 = Length(westLegs);
            float s1 = s0 + sunkSpan + Length(gapWest);
            float s2 = s1 + nodeSpan + Length(gapEast);
            float s3 = s2 + raisedSpan;
            float hopWest = s0 - HopRun + RollClear + HopSagHalf;
            float hopEast = s3 + RollClear + HopSagHalf;
            var profile = new VerticalProfile().Point(0f, 0f)
                .Bump(hopWest, HopRise, HopLength, HopCrestRadius, HopSagRadius)
                .Step(s0, sunk.freewayHeight, FreewayGrade, FreewayVerticalRadius)
                .Step(s0 + sunkSpan - sunkChange, 0f, FreewayGrade, FreewayVerticalRadius)
                .Step(s2, raised.freewayHeight, FreewayGrade, FreewayVerticalRadius)
                .Step(s3 - raisedChange, 0f, FreewayGrade, FreewayVerticalRadius)
                .Bump(hopEast, HopRise, HopLength, HopCrestRadius, HopSagRadius)
                .Level();
            var westGate = WestGate.Clone();
            westGate.airZones.Add(HopZone(hopWest));
            westGate.airZones.Add(HopZone(hopEast));
            // Level across through every feature: a banked 42 m freeway lifts its high lanes into the bridges above.
            foreach (var (from, span) in new[] { (s0, sunkSpan), (s1, nodeSpan), (s2, raisedSpan) })
                westGate.bankZones.Add(new BankZone { from = from, to = from + span, degrees = 0f });

            var plan = Build(start, heading, legs);
            RoadBend BendAt(float station, Leg[] line, float centre, float from, float to)
            {
                plan.Evaluate(station, out Vector2 at, out Vector2 along);
                return RoadBend.Along(at, along, line, centre, from, to);
            }

            var roads = new List<RoadPocLayout.RoadDefinition> { Definition("West Gate", start, heading, legs, profile, westGate) };
            roads.AddRange(Place(Diamond("Sunken diamond", sunkCentre, sunk), 0f, BendAt(s0, sunkLine, sunkCentre, DiamondBlendFrom, DiamondBlendTo), level: true));
            roads.AddRange(Place(interchange, nodeCentre, BendAt(s1, nodeLine, nodeCentre, InterchangeBlendFrom, InterchangeBlendTo), level: false));
            roads.AddRange(Place(Diamond("Raised diamond", raisedCentre, raised), 0f, BendAt(s2, raisedLine, raisedCentre, DiamondBlendFrom, DiamondBlendTo), level: true));
            return roads;
        }
    }
}
