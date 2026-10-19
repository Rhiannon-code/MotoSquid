using System;
using System.Collections.Generic;
using UnityEngine;
using static MotoSquid.Roads.LayoutLegs;
using static MotoSquid.Roads.RoadDesign;

namespace MotoSquid.Roads
{
    [Serializable]
    public class DiamondSettings
    {
        [Tooltip("Freeway height through the diamond (m). Below zero sits in a cutting, above zero stands on walls.")]
        public float freewayHeight;
        [Tooltip("Ramp grades as rise over run.")]
        public float eastboundOffRamp;
        public float eastboundOnRamp;
        public float westboundOffRamp;
        public float westboundOnRamp;
    }

    // The pieces a freeway is built from: diamonds, the system interchange, technical bend sets and hops.
    // Each feature is drawn about x = 0 beside a freeway running along +x, then placed and bent onto the
    // stretch of freeway it sits on (Place + BendOnto). Both directions share one surface wherever they run
    // side by side at the same level (no divider), and part into one-way roads only where they must.
    // Traffic keeps left.
    public static class RoadFeatures
    {
        public const float Margin = 60f;
        const float FreewayBand = 60f;
        const float TerminalLevel = 40f;
        internal const float MergeLevel = 20f;
        const float IntoCrossStreet = 5f;
        const float Overlap = 60f;
        const float JunctionClear = 100f;
        internal const float BridgeClear = 8f;
        const float FlyoverBendRadius = 200f;
        const float FlyoverBendDegrees = 60f;
        const float NorthHalfMedian = 200f;
        const float TollwaySpread = 25f;

        // Curves the features are bent onto, and how far out from West Gate the bending reaches before
        // things move rigidly with the feature's centre.
        public const float DiamondBendRadius = 700f;
        public const float DiamondBendDegrees = 25f;
        public const float DiamondBlendFrom = 60f;
        public const float DiamondBlendTo = 150f;
        public const float InterchangeBendRadius = 2000f;
        // Turning left puts the loop and the flyover on the outside, where bending lengthens them and eases their grades.
        public const float InterchangeBendDegrees = 20f;
        public const float InterchangeBlendFrom = 300f;
        public const float InterchangeBlendTo = 600f;

        // Technical bend sets. Each one ends back on the line it started on, at the same heading.
        public static readonly Leg[] Esses = { Leg.A(220f, 40f), Leg.S(50f), Leg.A(130f, -80f), Leg.S(50f), Leg.A(220f, 40f) };
        public static readonly Leg[] Kink = { Leg.A(180f, -50f), Leg.S(40f), Leg.A(120f, 100f), Leg.S(40f), Leg.A(180f, -50f) };
        public static readonly Leg[] Sweeper = { Leg.A(300f, 30f), Leg.S(80f), Leg.A(160f, -60f), Leg.S(80f), Leg.A(300f, 30f) };
        public static readonly Leg[] Dogleg = { Leg.A(250f, 35f), Leg.S(80f), Leg.A(150f, -70f), Leg.S(80f), Leg.A(250f, 35f) };
        // Free ends: a road that finishes beyond its last bend.
        static readonly Leg[] StreetEndA = { Leg.A(150f, 60f), Leg.S(80f), Leg.A(120f, -60f), Leg.S(150f) };
        static readonly Leg[] StreetEndB = { Leg.A(140f, -55f), Leg.S(90f), Leg.A(120f, 55f), Leg.S(150f) };

        // A hop: the original PoC's shape (about 1.3 s of air at top speed, landing on its own downslope).
        public const float HopRise = 4f;
        public const float HopLength = 200f;
        public const float HopCrestRadius = 500f;
        public const float HopSagRadius = 2000f;
        public const float HopRun = 460f;
        public const float FreeEnd = 150f;
        const float StreetStraightHalf = 150f;
        public static float HopSagHalf => HopSagRadius * HopRise / HopLength;

        // A half diamond's ramps move further out than a diamond's; a steeper taper keeps them about as long.
        static readonly Taper HalfDiamondTaper = new Taper(500f, 10f);

        // The freeway every feature is drawn beside.
        static readonly RoadSettings WestGate = RoadSettings.FreewayTwoWay();
        static readonly RoadSettings Carriageway = RoadSettings.Freeway();
        static readonly RoadSettings Ramp1 = RoadSettings.Ramp();
        static readonly RoadSettings Ramp2 = RoadSettings.Ramp(2);

        // A stretch of freeway with one curve in the middle of it.
        public static Leg[] Curve(float span, float radius, float degrees)
        {
            float arc = radius * Mathf.Abs(degrees) * Mathf.Deg2Rad;
            float side = Mathf.Max(0f, (span - arc) / 2f);
            return new[] { Leg.S(side), Leg.A(radius, degrees), Leg.S(side) };
        }

        // Moves a feature drawn about x = 0 along its straight line, then bends it onto its curve. A level
        // feature's roads don't lean into the bend: its curve is gentle, and its ramps run beside a level freeway.
        public static IEnumerable<RoadPocLayout.RoadDefinition> Place(IEnumerable<RoadPocLayout.RoadDefinition> roads, float alongX, RoadBend bend, bool level)
        {
            foreach (var road in roads)
            {
                var placed = road;
                if (level) placed.Settings.maxBank = 0f;
                placed.Recipe.start += new Vector2(alongX, 0f);
                placed.Recipe.bend = bend;
                placed.Knots = placed.Recipe.Knots();
                yield return placed;
            }
        }

        // The bend that carries a feature onto any freeway: the freeway's own centreline over the feature's
        // span, which starts `station` metres along it.
        public static RoadBend BendOnto(Vector2 start, Vector2 direction, Leg[] freeway, float station, float span,
                                        float centre, float blendFrom, float blendTo)
        {
            Build(start, direction, freeway).Evaluate(station, out Vector2 at, out Vector2 along);
            return RoadBend.Along(at, along, Slice(freeway, station, span), centre, blendFrom, blendTo);
        }

        public static float FreewayChange(float height) => VerticalProfile.ChangeLength(height, FreewayGrade, FreewayVerticalRadius);

        public static AirZone HopZone(float bumpFrom) => new AirZone { from = bumpFrom - 60f, to = bumpFrom + HopLength + 160f, airClass = AirClass.Hop };

        // How far a set of legs carries a road forward along its starting direction.
        static float Forward(Leg[] legs) => Displacement(legs, Vector2.right).x;

        // A road that must arrive at `end` heading `direction`, with `traced` laid out backwards from there:
        // where it starts, which way it starts, and its legs in driving order.
        public static (Vector2 start, Vector2 direction, Leg[] legs) Toward(Vector2 end, Vector2 direction, Leg[] traced)
        {
            var back = Build(end, -direction, traced);
            back.Evaluate(back.Length, out Vector2 start, out Vector2 away);
            return (start, -away, Reverse(traced));
        }

        // How far a feature reaches along its freeway: every knot of its roads near it.
        public static (float west, float east) Footprint(IEnumerable<RoadPocLayout.RoadDefinition> roads)
        {
            float west = float.MaxValue, east = float.MinValue;
            foreach (var road in roads)
                foreach (var knot in road.Knots)
                    if (Mathf.Abs(knot.Position.z) < FreewayBand)
                    {
                        west = Mathf.Min(west, knot.Position.x);
                        east = Mathf.Max(east, knot.Position.x);
                    }
            return (west, east);
        }

        // A cross street over or under the freeway and four ramps meeting it at T-junctions. The eastbound
        // pair is laid out north of the freeway and turned half a circle for the westbound pair.
        public static IEnumerable<RoadPocLayout.RoadDefinition> Diamond(string name, float x, DiamondSettings d)
        {
            var street = RoadSettings.Highway();
            street.maxBank = 0f;
            var (start, heading, southEnd) = Toward(new Vector2(x, -StreetStraightHalf), Vector2.up, StreetEndA);
            yield return Definition($"{name} cross street", start, heading,
                Concat(southEnd, new[] { Leg.S(2f * StreetStraightHalf) }, StreetEndB), VerticalProfile.Stepped(0f, 0f), street);

            foreach (var ramp in DiamondRamps(name, x, d, street.Width / 2f)) yield return ramp;
        }

        // The diamond's four ramps alone, meeting a cross street `streetHalf` wide either side of x.
        public static IEnumerable<RoadPocLayout.RoadDefinition> DiamondRamps(string name, float x, DiamondSettings d, float streetHalf)
        {
            yield return OffRamp($"{name} eastbound off-ramp", x, 0, d.freewayHeight, d.eastboundOffRamp, streetHalf);
            yield return OnRamp($"{name} eastbound on-ramp", x, 0, d.freewayHeight, d.eastboundOnRamp, streetHalf);
            yield return OffRamp($"{name} westbound off-ramp", x, 2, d.freewayHeight, d.westboundOffRamp, streetHalf);
            yield return OnRamp($"{name} westbound on-ramp", x, 2, d.freewayHeight, d.westboundOnRamp, streetHalf);
        }

        // Only the pair on the freeway's +z side, level with it: a zone street ending beside the freeway. They run
        // `wider` metres further out than a diamond's, so the street can end across them without touching the freeway.
        public static IEnumerable<RoadPocLayout.RoadDefinition> HalfDiamondRamps(string name, float streetHalf, float grade, float wider)
        {
            yield return OffRamp($"{name} eastbound off-ramp", 0f, 0, 0f, grade, streetHalf, wider);
            yield return OnRamp($"{name} eastbound on-ramp", 0f, 0, 0f, grade, streetHalf, wider);
        }

        // Out of the left lane, alongside the freeway while it climbs or drops to street level, then into the
        // side of the cross street.
        static RoadPocLayout.RoadDefinition OffRamp(string name, float x, int quarters, float freewayHeight, float grade, float streetHalf, float wider = 0f)
        {
            float clear = Clear(WestGate, Ramp1) + wider;
            var exit = (wider > 0f ? HalfDiamondTaper : Merge).Exit(clear);
            float run = MergeLevel + VerticalProfile.ChangeLength(freewayHeight, grade, RampVerticalRadius) + TerminalLevel + IntoCrossStreet;
            var start = new Vector2(-streetHalf + IntoCrossStreet - Displacement(exit, Vector2.right).x - run, LeftLane(WestGate));
            var profile = VerticalProfile.Stepped(freewayHeight, RampVerticalRadius, (Length(exit) + MergeLevel, 0f, grade));
            return Turned(name, x, quarters, start, Concat(exit, new[] { Leg.S(run) }), profile, grade);
        }

        // Out of the side of the cross street, down or up to the freeway, then a taper into its left lane.
        static RoadPocLayout.RoadDefinition OnRamp(string name, float x, int quarters, float freewayHeight, float grade, float streetHalf, float wider = 0f)
        {
            float clear = Clear(WestGate, Ramp1) + wider;
            float run = IntoCrossStreet + TerminalLevel + VerticalProfile.ChangeLength(freewayHeight, grade, RampVerticalRadius) + MergeLevel;
            var start = new Vector2(streetHalf - IntoCrossStreet, LeftLane(WestGate) + clear);
            var profile = VerticalProfile.Stepped(0f, RampVerticalRadius, (IntoCrossStreet + TerminalLevel, freewayHeight, grade));
            return Turned(name, x, quarters, start, Concat(new[] { Leg.S(run) }, (wider > 0f ? HalfDiamondTaper : Merge).In(clear)), profile, grade);
        }

        static RoadPocLayout.RoadDefinition Turned(string name, float x, int quarters, Vector2 start, Leg[] legs, VerticalProfile profile, float grade) =>
            Definition(name, new Vector2(x, 0f) + Rotate(start, quarters), Rotate(Vector2.right, quarters), legs, profile, Ramp1, grade);

        // Where the compact T's stem ends: on its bridge just past the far edge of the freeway it crosses.
        public static float StemEnd => -WestGate.Width / 2f - BridgeClear;

        // West Gate / CityLink, drawn for riding (the T plus CityLink's own northern half, see below).
        public static List<RoadPocLayout.RoadDefinition> Interchange(float h, float g)
        {
            var roads = CompactT(h, g, out float level);
            float southEnd = StemEnd;
            var cityLink = RoadSettings.FreewayTwoWay();

            // CityLink stays at bridge height past the ramps, drops to street level, kinks, then parts into
            // one-way roads through the tollway interchange.
            float descent = VerticalProfile.ChangeLength(h, FreewayGrade, FreewayVerticalRadius);
            var cityLinkLegs = Concat(new[] { Leg.S(level + descent + RollClear) }, Kink, new[] { Leg.S(Overlap + RollClear) });
            roads.Add(Definition("CityLink", new Vector2(0f, southEnd), Vector2.up, cityLinkLegs,
                VerticalProfile.Stepped(h, FreewayVerticalRadius, (level, 0f, FreewayGrade)), cityLink));

            float partZ = southEnd + Displacement(cityLinkLegs, Vector2.up).y;
            float half = PairOffset(cityLink, Carriageway);
            float straightZ = partZ + SweepZ(NorthHalfMedian - half);
            roads.AddRange(NorthNode(straightZ + 200f, h, g, out float topZ));
            roads.Add(Definition("CityLink northbound (to tollway)", new Vector2(-half, partZ - Overlap), Vector2.up,
                Concat(new[] { Leg.S(Overlap) }, Sweep(NorthHalfMedian - half, 1f), new[] { Leg.S(topZ - straightZ) }),
                VerticalProfile.Stepped(0f, 0f), Carriageway));
            roads.Add(Definition("CityLink southbound (from tollway)", new Vector2(NorthHalfMedian, topZ), Vector2.down,
                Concat(new[] { Leg.S(topZ - straightZ) }, Sweep(NorthHalfMedian - half, -1f), new[] { Leg.S(Overlap) }),
                VerticalProfile.Stepped(0f, 0f), Carriageway));
            return roads;
        }

        // The compact T, drawn for riding: the stem (the road that ends here) arrives along +z as one two-way road
        // and crosses the through freeway on its own bridge. The stem to the freeway's westbound side is a tight 270°
        // loop south of the freeway that comes out under its own approach; westbound to the stem is a flyover that
        // sweeps round the outside of that loop, with a hop on its way up. The left turns are ramps north of the
        // freeway. Nothing weaves and nothing crosses at the same level. Returns the four ramps: the stem must run at
        // `h` from StemEnd for `stemLevel` metres along +z before it drops to street level.
        public static List<RoadPocLayout.RoadDefinition> CompactT(float h, float g, out float stemLevel)
        {
            float change = VerticalProfile.ChangeLength(h, g, RampVerticalRadius);
            var cityLink = RoadSettings.FreewayTwoWay();
            float outerPair = PairOffset(cityLink, Ramp2);
            float besideWestbound = -LeftLane(WestGate) - Clear(WestGate, Ramp2);
            float southEnd = StemEnd;
            float inside = southEnd + Overlap;
            var roads = new List<RoadPocLayout.RoadDefinition>();

            var loopCentre = new Vector2(outerPair + LoopRadius, besideWestbound - LoopRadius);
            float loopIn = inside - loopCentre.y;
            float loopRollIn = Ramp2.maxBank / RollRateFor(Ramp2.Width) + RollClear / 2f;
            roads.Add(Definition("CityLink to West Gate westbound (loop)", new Vector2(outerPair, inside), Vector2.down,
                Concat(new[] { Leg.S(loopIn), Leg.A(LoopRadius, 270f), Leg.S(RollClear) }, Merge.In(Clear(WestGate, Ramp2))),
                VerticalProfile.Stepped(h, RampVerticalRadius, (loopIn + loopRollIn, 0f, g)), Ramp2, g));

            float sweepRadius = loopCentre.x + outerPair;
            float bottomZ = loopCentre.y - sweepRadius;
            float westboundLeft = -LeftLane(WestGate);
            float a = FlyoverBendDegrees * Mathf.Deg2Rad;
            var dropBase = Concat(Merge.Out, new[] { Leg.A(FlyoverBendRadius, FlyoverBendDegrees - Merge.degrees), Leg.A(FlyoverBendRadius, -FlyoverBendDegrees) });
            float diagonal = (westboundLeft + Displacement(dropBase, Vector2.left).y - bottomZ) / Mathf.Sin(a);
            var drop = Concat(Merge.Out, new[] { Leg.A(FlyoverBendRadius, FlyoverBendDegrees - Merge.degrees), Leg.S(diagonal), Leg.A(FlyoverBendRadius, -FlyoverBendDegrees) });
            float run = RollClear + 2f * HopSagHalf + HopLength + Margin + change + RollClear;
            float flyoverStartX = loopCentre.x + run - Displacement(drop, Vector2.left).x;
            float hopFrom = Length(drop) + RollClear + HopSagHalf;
            var flyover = Ramp2.Clone();
            flyover.airZones.Add(HopZone(hopFrom));
            roads.Add(Definition("West Gate westbound to CityLink (flyover)", new Vector2(flyoverStartX, westboundLeft), Vector2.left,
                Concat(drop, new[] { Leg.S(run), Leg.A(sweepRadius, -90f), Leg.S(inside - loopCentre.y) }),
                new VerticalProfile().Point(0f, 0f)
                    .Bump(hopFrom, HopRise, HopLength, HopCrestRadius, HopSagRadius)
                    .Step(hopFrom + HopLength + HopSagHalf + Margin, h, g, RampVerticalRadius)
                    .Level(), flyover, g));

            float cityLinkLeft = LeftLane(cityLink);
            float eastboundLeft = LeftLane(WestGate);
            var leftTurn = Concat(Merge.Out, new[] { Leg.A(ConnectorRadius, 90f - Merge.degrees) });
            var intoCityLink = Merge.In(Clear(cityLink, Ramp1));
            Vector2 turned = Displacement(leftTurn, Vector2.right);
            float onEnd = eastboundLeft + turned.y + RollClear + change + MergeLevel + Displacement(intoCityLink, Vector2.up).y;
            roads.Add(Definition("West Gate eastbound to CityLink", new Vector2(-cityLinkLeft - Clear(cityLink, Ramp1) - turned.x, eastboundLeft), Vector2.right,
                Concat(leftTurn, new[] { Leg.S(RollClear + change + MergeLevel) }, intoCityLink),
                VerticalProfile.Stepped(0f, RampVerticalRadius, (Length(leftTurn) + RollClear, h, g)), Ramp1, g));

            float down = MergeLevel + change + RollClear;
            float alongside = eastboundLeft + Clear(WestGate, Ramp1);
            float offStart = alongside - Displacement(leftTurn, Vector2.down).y + down * Mathf.Cos(Merge.degrees * Mathf.Deg2Rad);
            roads.Add(Definition("CityLink to West Gate eastbound", new Vector2(cityLinkLeft, offStart), Vector2.down,
                Concat(Merge.Out, new[] { Leg.S(down), Leg.A(ConnectorRadius, 90f - Merge.degrees) }, Merge.In(Clear(WestGate, Ramp1))),
                VerticalProfile.Stepped(h, RampVerticalRadius, (Length(Merge.Out) + MergeLevel, 0f, g)), Ramp1, g));

            stemLevel = Mathf.Max(onEnd, offStart) + JunctionClear - southEnd;
            return roads;
        }

        // Two freeways crossing, drawn for riding: A runs along x and drops to `depth` in a cutting, B runs along
        // z at street level, and every movement has its own road, so nothing weaves. B's right turns are 270°
        // loops (south-east and north-west); the left turns in those quadrants wrap round the loops as
        // three-corner S-ramps; A's right turns are flyovers at `h` that sweep through the other two quadrants
        // and run alongside B until the S-ramps are in. One half is drawn and turned half a circle for the
        // other. Returns the ramps only: A must sit at `depth` within `core` metres either side of B.
        public static List<RoadPocLayout.RoadDefinition> Cloverstack(float depth, float h, float g, out float core)
        {
            var b = RoadSettings.FreewayTwoWay();
            float aLane = LeftLane(WestGate), bLane = LeftLane(b);
            float change = VerticalProfile.ChangeLength(h, g, RampVerticalRadius);
            var half = new List<(string name, string turned, Vector2 start, Vector2 direction, Leg[] legs, VerticalProfile profile, RoadSettings road, float grade)>();

            // South-east loop: out of B southbound's left side once it has crossed A, round 270° and down into
            // A westbound under B.
            var loopExit = Merge.Exit(Clear(b, Ramp2));
            var loopIn = Merge.In(Clear(WestGate, Ramp2));
            var loopCentre = new Vector2(bLane + Clear(b, Ramp2) + LoopRadius, -aLane - Clear(WestGate, Ramp2) - LoopRadius);
            float loopRollIn = Ramp2.maxBank / RollRateFor(Ramp2.Width) + RollClear / 2f;
            half.Add(("B southbound to A westbound (loop)", "B northbound to A eastbound (loop)",
                new Vector2(bLane, loopCentre.y - Displacement(loopExit, Vector2.down).y), Vector2.down,
                Concat(loopExit, new[] { Leg.A(LoopRadius, 270f), Leg.S(RollClear) }, loopIn),
                // The loop drops while leaning: rounded any tighter, its outer lane leaves the road.
                VerticalProfile.Stepped(0f, LoopVerticalRadius, (Length(loopExit) + loopRollIn, depth, g)), Ramp2, g));
            core = -(loopCentre.x - RollClear + Displacement(loopIn, Vector2.left).x) + Margin;
            float aLevelFrom = core + FreewayChange(depth) + Margin;

            // North-east corner: out of B southbound before it crosses A, round to the east and along the top of
            // A's cutting until A is back at street level, then into A eastbound.
            var cornerTurn = Concat(Merge.Out, new[] { Leg.S(2f * RollClear), Leg.A(ConnectorRadius, 90f - Merge.degrees) });
            Vector2 turned = Displacement(cornerTurn, Vector2.down);
            float cornerRun = Mathf.Max(RollClear, aLevelFrom - bLane - turned.x);
            half.Add(("B southbound to A eastbound", "B northbound to A westbound",
                new Vector2(bLane, aLane + Clear(WestGate, Ramp1) - turned.y), Vector2.down,
                Concat(cornerTurn, new[] { Leg.S(cornerRun) }, Merge.In(Clear(WestGate, Ramp1))), VerticalProfile.Stepped(0f, 0f), Ramp1, 0f));

            // South-east S-ramp: out of A westbound at street level, then left, right and left round the loop and
            // into B southbound below it.
            float loopClear = Ramp2.Width / 2f + Ramp1.Width / 2f + BarrierGap;
            float southX = loopCentre.x + LoopRadius + loopClear;
            float corner2X = southX - LoopRadius;
            float corner2Z = loopCentre.y - Mathf.Sqrt(Mathf.Pow(2f * LoopRadius + loopClear, 2f) - Mathf.Pow(corner2X - loopCentre.x, 2f));
            float sRampX = bLane + Clear(b, Ramp1), besideA = -aLane - Clear(WestGate, Ramp1);
            var sExit = Merge.Exit(Clear(WestGate, Ramp1));
            var sIn = Merge.In(Clear(b, Ramp1));
            float sExitAlong = -Displacement(sExit, Vector2.left).x;
            float sStartX = Mathf.Max(southX + ConnectorRadius + RollClear, aLevelFrom) + sExitAlong;
            half.Add(("A westbound to B southbound", "A eastbound to B northbound",
                new Vector2(sStartX, -aLane), Vector2.left,
                Concat(sExit, new[]
                {
                    Leg.S(sStartX - sExitAlong - southX - ConnectorRadius), Leg.A(ConnectorRadius, 90f),
                    Leg.S(besideA - ConnectorRadius - corner2Z), Leg.A(LoopRadius, -90f),
                    Leg.S(Mathf.Max(0f, corner2X - sRampX - LoopRadius)), Leg.A(LoopRadius, 90f)
                }, sIn),
                VerticalProfile.Stepped(0f, 0f), Ramp1, 0f));
            float sMergeEnd = corner2Z - 2f * LoopRadius + Displacement(sIn, Vector2.down).y;

            // A westbound to B northbound: out of A's left side beyond the S-ramp, up to `h`, right over A, left
            // and a hop, right again over B to run north beside it over the far S-ramp, then down into B northbound.
            var flyExit = Merge.Exit(Clear(WestGate, Ramp2));
            float climb = MergeLevel + change + RollClear;
            float besideB = -bLane - Clear(b, Ramp2);
            float hopRun = 2f * (RollClear + HopSagHalf) + HopLength;
            float turnX = Mathf.Max(sStartX + Margin, besideB + 3f * FlyoverBendRadius + hopRun);
            float flyStartX = turnX + climb - Displacement(flyExit, Vector2.left).x;
            float crossZ = -aLane - Clear(WestGate, Ramp2) + 2f * FlyoverBendRadius + RollClear;
            float northRun = -sMergeEnd + Margin - crossZ - FlyoverBendRadius;
            var toHop = Concat(flyExit, new[] { Leg.S(climb), Leg.A(FlyoverBendRadius, -90f), Leg.S(RollClear), Leg.A(FlyoverBendRadius, 90f) });
            var flyLegs = Concat(toHop, new[] { Leg.S(turnX - 3f * FlyoverBendRadius - besideB), Leg.A(FlyoverBendRadius, -90f), Leg.S(northRun) });
            float hopFrom = Length(toHop) + RollClear + HopSagHalf;
            var flyover = Ramp2.Clone();
            flyover.airZones.Add(HopZone(hopFrom));
            half.Add(("A westbound to B northbound (flyover)", "A eastbound to B southbound (flyover)",
                new Vector2(flyStartX, -aLane), Vector2.left,
                Concat(flyLegs, new[] { Leg.S(change + MergeLevel) }, Merge.In(Clear(b, Ramp2))),
                new VerticalProfile().Point(0f, 0f)
                    .Step(Length(flyExit) + MergeLevel, h, g, RampVerticalRadius)
                    .Bump(hopFrom, HopRise, HopLength, HopCrestRadius, HopSagRadius)
                    .Step(Length(flyLegs), 0f, g, RampVerticalRadius)
                    .Level(), flyover, g));

            var roads = new List<RoadPocLayout.RoadDefinition>();
            foreach (var r in half)
            {
                roads.Add(Definition(r.name, r.start, r.direction, r.legs, r.profile, r.road, r.grade));
                roads.Add(Definition(r.turned, Rotate(r.start, 2), Rotate(r.direction, 2), r.legs, r.profile, r.road, r.grade));
            }
            return roads;
        }

        static List<RoadPocLayout.RoadDefinition> NorthNode(float nodeZ, float h, float g, out float topZ)
        {
            var roads = new List<RoadPocLayout.RoadDefinition>();
            float pair = PairOffset(Carriageway, Ramp2);
            float clear = Clear(Carriageway, Ramp2, pair);
            float change = VerticalProfile.ChangeLength(h, g, RampVerticalRadius);
            var turnRight = Concat(Mirror(Merge.Out), new[] { Leg.A(ConnectorRadius, -(90f - Merge.degrees)) });

            // Tollway westbound: out of CityLink southbound's right pair, a right turn west over CityLink
            // northbound, down to street level, then into the two-way tollway's westbound side.
            var tollway = RoadSettings.Highway();
            float tollwayPair = PairOffset(tollway, Ramp2);
            var startB = new Vector2(NorthHalfMedian - pair, nodeZ - TollwaySpread - Displacement(turnRight, Vector2.down).y);
            Vector2 turnedB = startB + Displacement(turnRight, Vector2.down);
            float descendB = Length(turnRight) + (turnedB.x + NorthHalfMedian + 150f);
            float taperFromX = -NorthHalfMedian - 150f - change - MergeLevel;
            var intoTollway = Merge.In(TollwaySpread - tollwayPair);
            float joinX = taperFromX + Displacement(intoTollway, Vector2.left).x + Overlap;
            roads.Add(Definition("Tollway westbound", startB, Vector2.down, Concat(turnRight, new[] { Leg.S(turnedB.x - taperFromX) }, intoTollway),
                VerticalProfile.Stepped(0f, RampVerticalRadius, (Length(turnRight) + 10f, h, g), (descendB, 0f, g)), Ramp2, g));

            var (tollwayStart, tollwayHeading, tollwayLegs) = Toward(new Vector2(joinX, nodeZ), Vector2.right,
                Concat(new[] { Leg.S(Overlap + RollClear) }, Dogleg, new[] { Leg.S(FreeEnd) }));
            roads.Add(Definition("Tollway", tollwayStart, tollwayHeading, tollwayLegs, VerticalProfile.Stepped(0f, 0f), tollway));

            // Tollway eastbound: out of the two-way tollway's eastbound side, a left turn north, a taper into
            // CityLink northbound's left pair of lanes.
            var outOfTollway = Merge.Exit(TollwaySpread - tollwayPair);
            float arcStartX = -NorthHalfMedian - pair - clear - ConnectorRadius;
            var startE = new Vector2(joinX - Overlap, nodeZ + tollwayPair);
            float exitEndX = startE.x + Displacement(outOfTollway, Vector2.right).x;
            var taperIn = Merge.In(clear);
            float mergeEnd = nodeZ + TollwaySpread + ConnectorRadius + Displacement(taperIn, Vector2.up).y;
            roads.Add(Definition("Tollway eastbound", startE, Vector2.right,
                Concat(outOfTollway, new[] { Leg.S(arcStartX - exitEndX), Leg.A(ConnectorRadius, 90f) }, taperIn), VerticalProfile.Stepped(0f, 0f), Ramp2));

            // City Link eastbound: out of CityLink northbound's right pair after the merge, a right turn east over CityLink southbound.
            var startA = new Vector2(-NorthHalfMedian + pair, mergeEnd + JunctionClear);
            Vector2 turnedA = startA + Displacement(turnRight, Vector2.up);
            float descendA = Length(turnRight) + (NorthHalfMedian + 150f - turnedA.x);
            float levelFromX = NorthHalfMedian + 150f + change + RollClear;
            roads.Add(Definition("City Link eastbound", startA, Vector2.up,
                Concat(turnRight, new[] { Leg.S(levelFromX - turnedA.x) }, Esses, new[] { Leg.S(FreeEnd) }),
                VerticalProfile.Stepped(0f, RampVerticalRadius, (Length(turnRight) + 10f, h, g), (descendA, 0f, g)), Ramp2, g));

            topZ = turnedA.y + 100f;
            return roads;
        }

        // An S-bend that moves a road `shift` metres sideways: `side` +1 turns left first, -1 right first.
        static Leg[] Sweep(float shift, float side)
        {
            float degrees = SweepAngle(shift);
            return new[] { Leg.A(SweepRadius, side * degrees), Leg.S(SweepDiagonal(shift, degrees)), Leg.A(SweepRadius, -side * degrees) };
        }

        static float SweepZ(float shift)
        {
            float degrees = SweepAngle(shift);
            float a = degrees * Mathf.Deg2Rad;
            return 2f * SweepRadius * Mathf.Sin(a) + SweepDiagonal(shift, degrees) * Mathf.Cos(a);
        }

        static float SweepDiagonal(float shift, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return Mathf.Max(0f, (shift - 2f * SweepRadius * (1f - Mathf.Cos(a))) / Mathf.Sin(a));
        }

        // Small shifts don't need the full sweep angle: two arcs alone cover them.
        static float SweepAngle(float shift) =>
            Mathf.Min(SweepDegrees, Mathf.Acos(Mathf.Clamp(1f - shift / (2f * SweepRadius), -1f, 1f)) * Mathf.Rad2Deg);

        public static RoadPocLayout.RoadDefinition Definition(string name, Vector2 start, Vector2 direction, Leg[] legs, VerticalProfile profile,
                                                       RoadSettings road, float grade = 0f)
        {
            var settings = road.Clone();
            if (grade > 0f) settings.maxGrade = grade;
            settings.maxRollRate = Mathf.Min(settings.maxRollRate, RollRateFor(settings.Width));
            settings.bankZones.AddRange(LevelTapers(legs));
            var recipe = RoadRecipe.From(start, direction, legs, profile);
            return new RoadPocLayout.RoadDefinition { Name = name, Knots = recipe.Knots(), Settings = settings, Recipe = recipe };
        }

        static IEnumerable<BankZone> LevelTapers(Leg[] legs)
        {
            float at = 0f;
            foreach (var leg in legs)
            {
                float length = leg.radius > 0f ? leg.radius * Mathf.Abs(leg.degrees) * Mathf.Deg2Rad : leg.straight;
                if (Mathf.Approximately(leg.radius, Merge.radius) && Mathf.Approximately(Mathf.Abs(leg.degrees), Merge.degrees))
                    yield return new BankZone { from = at - TaperBankMargin, to = at + length + TaperBankMargin, degrees = 0f };
                at += length;
            }
        }

        internal static float LeftLane(RoadSettings road) => road.Width / 2f - road.shoulder - road.laneWidth / 2f;

        // Where a narrower road sits inside a wider one when it fills the outer lanes on one side.
        static float PairOffset(RoadSettings road, RoadSettings inner) => road.Width / 2f - road.shoulder - inner.lanesForward * inner.laneWidth / 2f;

        // How far a ramp's centre must sit sideways from where it joins a road (by default the road's left
        // lane) before their surfaces stop touching.
        internal static float Clear(RoadSettings road, RoadSettings ramp) => Clear(road, ramp, LeftLane(road));
        static float Clear(RoadSettings road, RoadSettings ramp, float joinOffset) => road.Width / 2f + ramp.Width / 2f + BarrierGap - joinOffset;
    }
}
