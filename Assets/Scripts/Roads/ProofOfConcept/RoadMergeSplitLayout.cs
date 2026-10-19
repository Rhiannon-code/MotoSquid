using System.Collections.Generic;
using UnityEngine;
using static MotoSquid.Roads.LayoutLegs;
using static MotoSquid.Roads.RoadFeatures;

namespace MotoSquid.Roads
{
    // The merge / split on its own: a through freeway along x, and a freeway arriving from the north that ends on it.
    public static class RoadMergeSplitLayout
    {
        public static List<RoadPocLayout.RoadDefinition> Roads()
        {
            var roads = RoadMergeSplit.Roads("Split", RoadDesign.LevelSeparation, 0.07f, out float approachEnd);
            var freeway = RoadSettings.FreewayTwoWay();
            roads.Insert(0, Definition("Through freeway", new Vector2(-1500f, 0f), Vector2.right, new[] { Leg.S(3000f) }, VerticalProfile.Stepped(0f, 0f), freeway));
            roads.Insert(1, Definition("Ending freeway", new Vector2(0f, approachEnd + 1500f), Vector2.down, new[] { Leg.S(1500f) }, VerticalProfile.Stepped(0f, 0f), freeway));
            return roads;
        }
    }
}
