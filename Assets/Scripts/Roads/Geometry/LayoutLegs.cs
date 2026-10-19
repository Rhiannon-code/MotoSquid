using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    // Plan-view building blocks for designed roads: straights and arcs as legs, and the straight tapers a
    // ramp uses to leave or join the carriageway beside it.
    public static class LayoutLegs
    {
        [Serializable]
        public struct Leg
        {
            [Tooltip("Arc radius (m). Zero makes this leg a straight.")]
            public float radius;
            [Tooltip("Arc angle; positive turns left.")]
            public float degrees;
            [Tooltip("Straight length (m), used when the radius is zero.")]
            public float straight;

            public static Leg S(float length) => new Leg { straight = length };
            public static Leg A(float radius, float degrees) => new Leg { radius = radius, degrees = degrees };
        }

        [Serializable]
        public struct Taper
        {
            public float radius;
            public float degrees;

            public Taper(float radius, float degrees)
            {
                this.radius = radius;
                this.degrees = degrees;
            }

            // Out of a carriageway's left lane, ready for the turn away from it.
            public Leg[] Out => new[] { Leg.S(60f), Leg.A(radius, degrees), Leg.S(120f) };

            // Out of a carriageway's left lane to run alongside it, `shift` metres further left.
            public Leg[] Exit(float shift) => new[] { Leg.S(60f), Leg.A(radius, degrees), Leg.S(Straight(shift)), Leg.A(radius, -degrees) };

            // Joins a carriageway from alongside it: a turn in, a straight long enough to cover `shift` metres
            // sideways, a turn back, then a run inside the carriageway's lane.
            public Leg[] In(float shift) => new[] { Leg.A(radius, -degrees), Leg.S(Straight(shift)), Leg.A(radius, degrees), Leg.S(60f) };

            public float Straight(float shift)
            {
                float a = degrees * Mathf.Deg2Rad;
                return Mathf.Max(0f, (shift - 2f * radius * (1f - Mathf.Cos(a))) / Mathf.Sin(a));
            }
        }

        // The same legs turning the other way: a taper or turn laid out for the left, used on the right.
        public static Leg[] Mirror(Leg[] legs)
        {
            var mirrored = new Leg[legs.Length];
            for (int i = 0; i < legs.Length; i++) mirrored[i] = new Leg { radius = legs[i].radius, degrees = -legs[i].degrees, straight = legs[i].straight };
            return mirrored;
        }

        // The same path travelled the other way: legs in reverse order, each turn the other way round.
        public static Leg[] Reverse(Leg[] legs)
        {
            var reversed = new Leg[legs.Length];
            for (int i = 0; i < legs.Length; i++)
            {
                var leg = legs[legs.Length - 1 - i];
                reversed[i] = new Leg { radius = leg.radius, degrees = -leg.degrees, straight = leg.straight };
            }
            return reversed;
        }

        public static PlanPath Build(Vector2 start, Vector2 direction, IEnumerable<Leg> legs)
        {
            var plan = new PlanPath(start, direction);
            foreach (var leg in legs)
            {
                if (leg.radius > 0f) plan.Arc(leg.radius, leg.degrees);
                else plan.Straight(leg.straight);
            }
            return plan;
        }

        public static Vector2 Displacement(Leg[] legs, Vector2 direction)
        {
            var plan = Build(Vector2.zero, direction, legs);
            plan.Evaluate(plan.Length, out Vector2 end, out _);
            return end;
        }

        public static float Length(Leg[] legs) => Build(Vector2.zero, Vector2.right, legs).Length;

        // The stretch of a road `length` metres long starting `from` metres along it, legs cut where it ends.
        public static Leg[] Slice(Leg[] legs, float from, float length)
        {
            const float Sliver = 0.001f;
            var slice = new List<Leg>();
            float at = 0f, to = from + length;
            foreach (var leg in legs)
            {
                float legLength = leg.radius > 0f ? leg.radius * Mathf.Abs(leg.degrees) * Mathf.Deg2Rad : leg.straight;
                float overlap = Mathf.Min(to, at + legLength) - Mathf.Max(from, at);
                if (overlap > Sliver)
                    slice.Add(leg.radius > 0f ? Leg.A(leg.radius, leg.degrees * overlap / legLength) : Leg.S(overlap));
                at += legLength;
            }
            return slice.ToArray();
        }

        public static Leg[] Concat(params Leg[][] parts)
        {
            var all = new List<Leg>();
            foreach (var part in parts) all.AddRange(part);
            return all.ToArray();
        }

        // Quarter turns anticlockwise seen from above: east becomes north.
        public static Vector2 Rotate(Vector2 v, int quarters)
        {
            for (int i = 0; i < quarters; i++) v = new Vector2(-v.y, v.x);
            return v;
        }
    }
}
