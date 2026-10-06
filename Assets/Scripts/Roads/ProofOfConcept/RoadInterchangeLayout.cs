using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    // A designed system interchange in the spirit of West Gate / CityLink, laid out for riding rather than
    // copied from the map. Freeway A runs east-west at ground level; freeway B runs north-south and rises
    // over it. Four left-turn ramps link them at ground level, one per quadrant; two flyovers on a third
    // level make the right turns, sweeping over both freeways. Every carriageway is its own one-way road.
    // Traffic keeps left, so the eastbound carriageway is the northern one.
    public static class RoadInterchangeLayout
    {
        const float HalfLength = 2000f;
        const float Median = 6f;
        const float RampEntryStart = 560f;
        const float FlyoverStart = -1400f;
        const float FlyoverHeight = 16f;
        const float FreewayBRise = 230f;
        const float FreewayBPeak = 10f;   // the crest rounds this down to about 8.6 m over freeway A

        struct Leg
        {
            public float radius, degrees, straight;
            public static Leg S(float length) => new Leg { straight = length };
            public static Leg A(float radius, float degrees) => new Leg { radius = radius, degrees = degrees };
        }

        const float TaperRadius = 1000f;
        const float TaperDegrees = 3f;
        const float BarrierGap = 2f;

        static readonly Leg[] TaperOut = { Leg.S(60f), Leg.A(TaperRadius, TaperDegrees), Leg.S(120f) };

        public static List<RoadPocLayout.RoadDefinition> Roads()
        {
            var freeway = RoadSettings.Freeway();
            var ramp = RoadSettings.Ramp();
            float carriageway = freeway.Width / 2f + Median / 2f;
            float leftLane = freeway.Width / 2f - freeway.shoulder - freeway.laneWidth / 2f;
            // How far a ramp must sit from its freeway's left lane before the two surfaces stop touching.
            float clear = freeway.Width / 2f + ramp.Width / 2f + BarrierGap - leftLane;
            var roads = new List<RoadPocLayout.RoadDefinition>();

            var flat = RoadPocLayout.Level();
            var raised = new VerticalProfile()
                .Point(0f, 0f).Point(HalfLength - FreewayBRise, 0f, 2000f).Point(HalfLength, FreewayBPeak, 1500f)
                .Point(HalfLength + FreewayBRise, 0f, 2000f).Point(2f * HalfLength, 0f);
            roads.Add(Freeway("A eastbound", new Vector2(-HalfLength, carriageway), Vector2.right, flat, freeway));
            roads.Add(Freeway("A westbound", new Vector2(HalfLength, -carriageway), Vector2.left, flat, freeway));
            roads.Add(Freeway("B northbound", new Vector2(-carriageway, -HalfLength), Vector2.up, raised, freeway));
            roads.Add(Freeway("B southbound", new Vector2(carriageway, HalfLength), Vector2.down, raised, freeway));

            string[] leftTurns = { "A eastbound to B northbound", "B northbound to A westbound", "A westbound to B southbound", "B southbound to A eastbound" };
            for (int quarter = 0; quarter < 4; quarter++)
                roads.Add(LeftTurn(leftTurns[quarter], quarter, carriageway, leftLane, clear, ramp));

            roads.Add(Flyover("Flyover A eastbound to B southbound", 0, carriageway, leftLane, clear, ramp));
            roads.Add(Flyover("Flyover A westbound to B northbound", 2, carriageway, leftLane, clear, ramp));
            return roads;
        }

        static RoadPocLayout.RoadDefinition Freeway(string name, Vector2 start, Vector2 direction, VerticalProfile profile, RoadSettings settings)
        {
            var plan = new PlanPath(start, direction).Straight(2f * HalfLength);
            return new RoadPocLayout.RoadDefinition { Name = name, Knots = RoadPocLayout.FromPlan(plan, profile), Settings = Copy(settings) };
        }

        // Laid out once for the north-west quadrant (A eastbound to B northbound), then turned a quarter at a time.
        static RoadPocLayout.RoadDefinition LeftTurn(string name, int quarter, float carriageway, float leftLane, float clear, RoadSettings settings)
        {
            var turn = Concat(TaperOut, new[] { Leg.A(300f, 87f) });
            var taperIn = TaperIn(clear);
            Vector2 turned = Displacement(turn, Vector2.right);
            Vector2 tapered = Displacement(taperIn, Vector2.up);
            float startX = -(carriageway + leftLane) - tapered.x - turned.x;
            float startZ = carriageway + leftLane;
            float run = RampEntryStart - (startZ + turned.y);
            var legs = Concat(turn, new[] { Leg.S(run) }, taperIn);
            var plan = Build(Rotate(new Vector2(startX, startZ), quarter), Rotate(Vector2.right, quarter), legs);
            return new RoadPocLayout.RoadDefinition { Name = name, Knots = RoadPocLayout.FromPlan(plan, RoadPocLayout.Level()), Settings = Copy(settings) };
        }

        // Laid out for A eastbound to B southbound: out of the left lane, up to the third level, a right turn
        // over freeway A, an S-bend over freeway B, back down, and in on B southbound's left.
        static RoadPocLayout.RoadDefinition Flyover(string name, int quarter, float carriageway, float leftLane, float clear, RoadSettings settings)
        {
            var exit = new[] { Leg.S(60f), Leg.A(TaperRadius, TaperDegrees), Leg.S(TaperStraight(clear)), Leg.A(TaperRadius, -TaperDegrees) };
            var taperIn = TaperIn(clear);
            var overA = new[] { Leg.A(350f, -90f) };
            var overB = new[] { Leg.A(600f, 25f), Leg.A(600f, -25f), Leg.S(400f) };
            var withoutRun = Concat(exit, overA, overB, taperIn);
            float startZ = carriageway + leftLane;
            float run = (carriageway + leftLane) - FlyoverStart - Displacement(withoutRun, Vector2.right).x;
            var legs = Concat(exit, new[] { Leg.S(run) }, overA, overB, taperIn);

            float exitLength = Length(exit);
            float overBEnd = exitLength + run + Length(overA) + Length(overB) - 400f;
            var profile = new VerticalProfile()
                .Point(0f, 0f).Point(exitLength + 75f, 0f, 3000f).Point(exitLength + 75f + FlyoverHeight / 0.05f, FlyoverHeight, 3000f)
                .Point(overBEnd, FlyoverHeight, 3000f).Point(overBEnd + FlyoverHeight / 0.05f, 0f, 3000f).Point(Length(legs), 0f);

            var plan = Build(Rotate(new Vector2(FlyoverStart, startZ), quarter), Rotate(Vector2.right, quarter), legs);
            return new RoadPocLayout.RoadDefinition { Name = name, Knots = RoadPocLayout.FromPlan(plan, profile), Settings = Copy(settings) };
        }

        // Joins a freeway from alongside it: a 3° turn in, a straight taper long enough to cover `shift`
        // metres sideways, a 3° turn back, then a run inside the freeway's lane.
        static Leg[] TaperIn(float shift) =>
            new[] { Leg.A(TaperRadius, -TaperDegrees), Leg.S(TaperStraight(shift)), Leg.A(TaperRadius, TaperDegrees), Leg.S(60f) };

        static float TaperStraight(float shift)
        {
            float a = TaperDegrees * Mathf.Deg2Rad;
            return Mathf.Max(0f, (shift - 2f * TaperRadius * (1f - Mathf.Cos(a))) / Mathf.Sin(a));
        }

        static PlanPath Build(Vector2 start, Vector2 direction, Leg[] legs)
        {
            var plan = new PlanPath(start, direction);
            foreach (var leg in legs)
            {
                if (leg.radius > 0f) plan.Arc(leg.radius, leg.degrees);
                else plan.Straight(leg.straight);
            }
            return plan;
        }

        static Vector2 Displacement(Leg[] legs, Vector2 direction)
        {
            var plan = Build(Vector2.zero, direction, legs);
            plan.Evaluate(plan.Length, out Vector2 end, out _);
            return end;
        }

        static float Length(Leg[] legs) => Build(Vector2.zero, Vector2.right, legs).Length;

        static Leg[] Concat(params Leg[][] parts)
        {
            var all = new List<Leg>();
            foreach (var part in parts) all.AddRange(part);
            return all.ToArray();
        }

        // Quarter turns anticlockwise seen from above: east becomes north.
        static Vector2 Rotate(Vector2 v, int quarters)
        {
            for (int i = 0; i < quarters; i++) v = new Vector2(-v.y, v.x);
            return v;
        }

        static RoadSettings Copy(RoadSettings s) => new RoadSettings
        {
            lanesForward = s.lanesForward, lanesBackward = s.lanesBackward, laneWidth = s.laneWidth, shoulder = s.shoulder,
            maxBank = s.maxBank, fullBankRadius = s.fullBankRadius, maxRollRate = s.maxRollRate, bankSmoothing = s.bankSmoothing,
            barrierHeight = s.barrierHeight, barrierThickness = s.barrierThickness, airClass = s.airClass,
        };
    }
}
