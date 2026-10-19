namespace MotoSquid.Roads
{
    // Map-wide sizing rules for designed roads and interchanges (ADR 0012), taken from the original MotoSquid
    // tracks (tightest corners ~120 m, hill grades 8-10%) and the bike at 100 m/s (~66 m turning radius).
    public static class RoadDesign
    {
        public static readonly LayoutLegs.Taper Merge = new LayoutLegs.Taper(500f, 6f);

        public const float BarrierGap = 2f;
        // v²/g = 1,020 m at 100 m/s; closer to it than this, the 1 m mesh facets chain into hops.
        public const float RampVerticalRadius = 1500f;
        public const float FreewayVerticalRadius = 2000f;
        public const float FreewayGrade = 0.05f;
        // A cloverstack loop drops while leaning; the freeway's 2,000 m left its outer lane right at the air limit.
        public const float LoopVerticalRadius = 2600f;
        // Long enough that a loop's lean is fully in for a full level change in its middle.
        public const float LoopRadius = 125f;
        // The tightest corner any road may have: the old tracks' tightest.
        public const float MinPlanRadius = 120f;
        // Zone streets are laid out for a lower speed, so city blocks and their junction turns fit.
        public const float StreetMinRadius = 30f;
        public const float ConnectorRadius = 150f;
        public const float SweepRadius = 600f;
        public const float SweepDegrees = 45f;
        public const float LevelSeparation = 7f;
        // Lane-change tapers stay level across: rolling into a lean that brief only lifts the high side.
        public const float TaperBankMargin = 20f;
        // Lean rolls in and out no faster than keeps the road's high edge within this grade: on a wide road a
        // quick roll lifts the outer lanes enough to throw the bike.
        public const float MaxEdgeRollGrade = 0.03f;

        public static float RollRateFor(float width) => MaxEdgeRollGrade / (width * UnityEngine.Mathf.Deg2Rad);
        // Room for a lean to roll out before the road starts to climb or drop.
        public const float RollClear = 60f;
        // About 3 s at top speed between one junction finishing and the next one starting.
        public const float DecisionGap = 300f;
    }
}
