using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace MotoSquid.Roads
{
    // The proof-of-concept course: a one-way 3-lane loop with an off-ramp split, an on-ramp merge,
    // a 4-way crossing, banked curves at 20° and 13.8°, a grounded bridge crest, a hop and a jump.
    public static class RoadPocLayout
    {
        public struct RoadDefinition
        {
            public string Name;
            public BezierKnot[] Knots;
            public RoadSettings Settings;
        }

        const float KnotSpacing = 10f;
        const float DesignSpeed = 100f;

        public static List<RoadDefinition> Roads()
        {
            var loop = Loop();
            var link = Link(loop.Settings, out Vector2 crossing);
            return new List<RoadDefinition> { loop, link, CrossStreet(crossing) };
        }

        static RoadDefinition Loop()
        {
            float sBend = 600f * (1f - Mathf.Cos(30f * Mathf.Deg2Rad));
            float westRadius = (600f + 2f * sBend) / 2f;
            var plan = new PlanPath(new Vector2(-1200f, 0f), Vector2.right)
                .Straight(2400f).Arc(300f, 180f).Straight(600f).Arc(600f, -30f).Arc(600f, 30f)
                .Straight(1200f).Arc(westRadius, 180f);

            var jump = new VerticalProfile.JumpShape
            {
                launchGrade = 0.06f, flight = 200f, rampBeforeContact = 60f, landingGradeOffset = 0.06f,
                runOut = 20f, sagRadius = 1000f, designSpeed = DesignSpeed
            };
            float lipHeight = VerticalProfile.LipHeightFor(0f, jump);
            float lip = 4700f + lipHeight / jump.launchGrade;
            var profile = new VerticalProfile()
                .Point(0f, 0f)
                .Point(640f, 0f, 2000f).Point(820f, 9f, 2000f).Point(980f, 9f, 2000f).Point(1160f, 0f, 2000f)
                .Point(1950f, 0f, 2000f).Point(2050f, 4f, 500f).Point(2150f, 0f, 2000f)
                .Point(4700f, 0f, 1500f).Point(lip, lipHeight);
            float jumpEnd = profile.AddJump(lip, jump);
            profile.Point(jumpEnd, 0f).Point(plan.Length, 0f);

            var settings = RoadSettings.Freeway();
            settings.maxBank = 20f;
            settings.airZones.Add(new AirZone { from = 1900f, to = 2250f, airClass = AirClass.Hop });
            settings.airZones.Add(new AirZone { from = 4650f, to = jumpEnd + 50f, airClass = AirClass.Jump });
            return new RoadDefinition { Name = "Loop", Knots = FromPlan(plan, profile), Settings = settings };
        }

        // Off-ramp out of the loop's left lane and back in as an on-ramp, laid out like a real ramp: a run
        // inside the loop, a gentle 3° turn, a straight taper (so the edge leaves the loop as a straight
        // line), then the turn away. The way back is the same shape mirrored, through a 4-way crossing.
        static RoadDefinition Link(RoadSettings loop, out Vector2 crossing)
        {
            var settings = RoadSettings.Ramp();
            float leftLane = loop.Width / 2f - loop.shoulder - loop.laneWidth / 2f;
            var plan = new PlanPath(new Vector2(-1050f, leftLane), Vector2.right);
            var outbound = new (float radius, float degrees, float straight)[]
            {
                (0f, 0f, 60f), (1000f, 3f, 0f), (0f, 0f, 150f), (200f, 80f, 0f), (200f, -83f, 0f), (0f, 0f, 60f)
            };
            foreach (var leg in outbound) Add(plan, leg);
            for (int i = outbound.Length - 1; i >= 0; i--) Add(plan, outbound[i]);

            plan.Evaluate(plan.Length / 2f, out crossing, out _);
            return new RoadDefinition { Name = "Link", Knots = FromPlan(plan, Level()), Settings = settings };
        }

        // Two-way, passing under the loop's bridge and through the crossing on the link.
        static RoadDefinition CrossStreet(Vector2 crossing)
        {
            var plan = new PlanPath(new Vector2(crossing.x, -400f), Vector2.up).Straight(Mathf.Min(crossing.y + 200f, 560f) + 400f);
            var settings = RoadSettings.Highway();
            settings.maxBank = 0f;
            return new RoadDefinition { Name = "Cross Street", Knots = FromPlan(plan, Level()), Settings = settings };
        }

        static void Add(PlanPath plan, (float radius, float degrees, float straight) leg)
        {
            if (leg.radius > 0f) plan.Arc(leg.radius, leg.degrees);
            else plan.Straight(leg.straight);
        }

        internal static VerticalProfile Level() => new VerticalProfile().Point(0f, 0f);

        // Every knot carries explicit tangents, so the curve is fully defined by the knots alone.
        public static Spline ToSpline(BezierKnot[] knots)
        {
            var spline = new Spline();
            foreach (var knot in knots) spline.Add(knot, TangentMode.Broken);
            return spline;
        }

        internal static BezierKnot[] FromPlan(PlanPath plan, VerticalProfile profile)
        {
            int count = Mathf.CeilToInt(plan.Length / KnotSpacing);
            float spacing = plan.Length / count;
            var knots = new BezierKnot[count + 1];
            for (int k = 0; k <= count; k++)
            {
                plan.Evaluate(spacing * k, out Vector2 xz, out Vector2 dir);
                float y = profile.Height(spacing * k, out float slope);
                var tangent = new float3(dir.x, slope, dir.y) * (spacing / 3f);
                var position = new float3(xz.x, y, xz.y);
                if (k == count && math.distance(position, knots[0].Position) < 0.01f) position = knots[0].Position;
                knots[k] = new BezierKnot(position, k > 0 ? -tangent : float3.zero, k < count ? tangent : float3.zero);
            }
            return knots;
        }
    }
}
