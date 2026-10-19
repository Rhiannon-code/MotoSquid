using System.Collections.Generic;
using UnityEngine;
using static MotoSquid.Roads.LayoutLegs;

namespace MotoSquid.Roads
{
    // Bend sets that break up a long straight and end back on its own line at its own heading, so spliced into a
    // straight they move nothing beyond it: a long sweep for flowing freeway stretches, and the corridor's tight
    // technical sets for variety.
    public static class RoadSweeps
    {
        // Out one way, back across through the middle, and out again: `side` +1 bulges left first. The long sweep
        // needs about 2 km of straight; the medium one fits the shorter stretches between interchanges.
        public static Leg[] Long(float degrees, float side) => Sweep(1200f, 1000f, 300f, degrees, side);
        public static Leg[] Medium(float degrees, float side) => Sweep(900f, 700f, 120f, degrees, side);

        static Leg[] Sweep(float outer, float middle, float straight, float degrees, float side) => new[]
        {
            Leg.A(outer, side * degrees), Leg.S(straight), Leg.A(middle, -2f * side * degrees),
            Leg.S(straight), Leg.A(outer, side * degrees)
        };

        public static readonly Leg[][] Technical = { RoadFeatures.Esses, RoadFeatures.Sweeper, RoadFeatures.Dogleg };

        public static Leg[] Mirrored(Leg[] set, float side) => side > 0f ? set : Mirror(set);

        // How far along its line a set carries the road (it ends back on that line).
        public static float Forward(Leg[] set) => Displacement(set, Vector2.right).x;

        // The set's centreline every few metres, starting at `start` heading `direction`.
        public static IEnumerable<Vector2> Points(Vector2 start, Vector2 direction, Leg[] set, float step = 20f)
        {
            var plan = Build(start, direction, set);
            for (float s = 0f; s <= plan.Length; s += step)
            {
                plan.Evaluate(s, out Vector2 p, out _);
                yield return p;
            }
        }
    }
}
