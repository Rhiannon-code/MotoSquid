using System.Collections.Generic;
using UnityEngine;
using static MotoSquid.Roads.LayoutLegs;
using static MotoSquid.Roads.RoadDesign;

namespace MotoSquid.Roads
{
    // A freeway ending on another where a full interchange won't fit: it splits into two two-way branches side by
    // side. The near branch turns left and tapers into the through freeway's near side, all at street level; the
    // far branch climbs, turns right over the through freeway on a flyover, drops back down running beside it, then
    // tapers into its far side. Nothing crosses at the same level. Drawn like the other features: the through
    // freeway runs along x, and the ending freeway arrives along +z and stops at (0, ApproachEnd). Traffic keeps left.
    public static class RoadMergeSplit
    {
        // Two branches side by side as wide as the two-way freeway they split from, so its edge walls run straight on.
        public static readonly RoadSettings Branch = new RoadSettings { lanesForward = 1, lanesBackward = 1, shoulder = 4.5f };
        static readonly RoadSettings Through = RoadSettings.FreewayTwoWay();
        // The branches start side by side, all but touching: a gap between them would leave a notch in the junction's
        // surface, walled round in the middle of the carriageway. One thin wall divides them instead, like a gore nose.
        static readonly float Spread = Branch.Width / 2f + 0.05f;
        // The far branch climbs only once it is clear of the junction where it leaves the ending freeway: inside it,
        // the surface is blended to the junction's level and a climb there becomes a crest.
        const float ClimbFrom = RollClear + RoadFeatures.MergeLevel;

        public static List<RoadPocLayout.RoadDefinition> Roads(string name, float h, float g, out float approachEnd)
        {
            float change = VerticalProfile.ChangeLength(h, g, RampVerticalRadius);
            float lane = RoadFeatures.LeftLane(Through);
            float clear = RoadFeatures.Clear(Through, Branch);
            // The far branch must be up at h before its turn starts, and its turn starts where its straight ends.
            float approach = Mathf.Max(RollClear, ClimbFrom + change - 2f * lane - 2f * clear);
            approachEnd = lane + clear + ConnectorRadius + approach;
            float farStraight = 2f * lane + 2f * clear + approach;
            float descent = RollClear + change + RoadFeatures.MergeLevel;
            float farTurn = ConnectorRadius * Mathf.PI / 2f;

            var roads = new List<RoadPocLayout.RoadDefinition>
            {
                RoadFeatures.Definition($"{name} near branch", new Vector2(Spread, approachEnd), Vector2.down,
                    Concat(new[] { Leg.S(approach), Leg.A(ConnectorRadius, 90f) }, Merge.In(clear)),
                    VerticalProfile.Stepped(0f, 0f), Branch, g),
                RoadFeatures.Definition($"{name} far branch (flyover)", new Vector2(-Spread, approachEnd), Vector2.down,
                    Concat(new[] { Leg.S(farStraight), Leg.A(ConnectorRadius, -90f), Leg.S(descent) }, Merge.In(clear)),
                    VerticalProfile.Stepped(0f, RampVerticalRadius, (ClimbFrom, h, g), (farStraight + farTurn + RollClear, 0f, g)), Branch, g),
            };
            return roads;
        }

        // How far the branches reach along the through freeway either side of the ending freeway's centre line.
        public static (float back, float ahead) Reach(float h, float g)
        {
            float change = VerticalProfile.ChangeLength(h, g, RampVerticalRadius);
            float into = Displacement(Merge.In(RoadFeatures.Clear(Through, Branch)), Vector2.right).x;
            return (Spread + ConnectorRadius + RollClear + change + RoadFeatures.MergeLevel + into, Spread + ConnectorRadius + into);
        }
    }
}
