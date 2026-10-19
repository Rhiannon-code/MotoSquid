using System.Collections.Generic;
using UnityEngine;
using static MotoSquid.Roads.LayoutLegs;
using static MotoSquid.Roads.RoadDesign;
using static MotoSquid.Roads.RoadFeatures;

namespace MotoSquid.Roads
{
    // The cloverstack on its own: freeway A west to east through a cutting, freeway B south to north at street
    // level, both running straight on past the ramps to free ends.
    public static class RoadCloverstackLayout
    {
        public static List<RoadPocLayout.RoadDefinition> Roads()
        {
            float depth = -LevelSeparation;
            var roads = Cloverstack(depth, LevelSeparation, 0.07f, out float core);
            float reachX = 0f, reachZ = 0f;
            foreach (var road in roads)
                foreach (var knot in road.Knots)
                {
                    reachX = Mathf.Max(reachX, Mathf.Abs(knot.Position.x));
                    reachZ = Mathf.Max(reachZ, Mathf.Abs(knot.Position.z));
                }

            var freeway = RoadSettings.FreewayTwoWay();
            float halfA = reachX + FreeEnd, halfB = reachZ + FreeEnd;
            float change = FreewayChange(depth);
            var profileA = new VerticalProfile().Point(0f, 0f)
                .Step(halfA - core - change, depth, FreewayGrade, FreewayVerticalRadius)
                .Step(halfA + core, 0f, FreewayGrade, FreewayVerticalRadius)
                .Level();
            roads.Insert(0, Definition("Freeway A", new Vector2(-halfA, 0f), Vector2.right, new[] { Leg.S(2f * halfA) }, profileA, freeway));
            roads.Insert(1, Definition("Freeway B", new Vector2(0f, -halfB), Vector2.up, new[] { Leg.S(2f * halfB) }, VerticalProfile.Stepped(0f, 0f), freeway));
            return roads;
        }
    }
}
