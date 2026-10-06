using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    // Road height along its length, the way road designers lay it out: straight grades between
    // points of vertical intersection (PVIs), rounded by parabolic curves of a given radius.
    // A jump replaces the profile after its lip: the road drops into a pit below the bike's flight path
    // at design speed, then a straight landing ramp, slightly shallower than the flight path, meets the
    // bike at the contact point and runs out into a sag back to level.
    public sealed class VerticalProfile
    {
        const float Gravity = 9.81f;

        struct Pvi { public float station, height, radius; }

        public struct JumpShape
        {
            public float launchGrade;
            public float flight;              // metres from the lip to where a bike at design speed lands
            public float rampBeforeContact;   // metres before that point where the landing ramp starts
            public float landingGradeOffset;  // how much shallower the ramp is than the flight path, as a grade
            public float runOut;
            public float sagRadius;
            public float designSpeed;

            internal float K => Gravity / (2f * designSpeed * designSpeed);
            internal float ContactGrade => launchGrade - 2f * K * flight;
            internal float RampGrade => ContactGrade + landingGradeOffset;
            internal float SagLength => sagRadius * Mathf.Abs(RampGrade);
            internal float Length => flight + runOut + SagLength;
            internal float Flight(float d) => launchGrade * d - K * d * d;
            internal float Ramp(float d) => Flight(flight) + RampGrade * (d - flight);
        }

        struct Jump
        {
            public float lip, height, pitCubicA, pitCubicB;
            public JumpShape shape;
            public float End => lip + shape.Length;
        }

        readonly List<Pvi> pvis = new List<Pvi>();
        readonly List<Jump> jumps = new List<Jump>();

        public VerticalProfile Point(float station, float height, float radius = 0f)
        {
            pvis.Add(new Pvi { station = station, height = height, radius = radius });
            return this;
        }

        // The lip height that brings a jump back to `endHeight` once it levels out.
        public static float LipHeightFor(float endHeight, JumpShape shape) =>
            endHeight - shape.Ramp(shape.flight + shape.runOut) - shape.RampGrade * shape.SagLength / 2f;

        // Add the PVI at the lip first; the jump takes over after it. Returns the station where it ends.
        public float AddJump(float lip, JumpShape shape)
        {
            float height = HeightOfPvis(lip - 0.01f, out _) + shape.launchGrade * 0.01f;
            float knuckle = shape.flight - shape.rampBeforeContact;
            float depth = shape.Flight(knuckle) - shape.Ramp(knuckle);
            float slopeGap = (shape.launchGrade - 2f * shape.K * knuckle) - shape.RampGrade;
            var jump = new Jump
            {
                lip = lip, height = height, shape = shape,
                pitCubicA = 3f * depth / (knuckle * knuckle) - slopeGap / knuckle,
                pitCubicB = slopeGap / (knuckle * knuckle) - 2f * depth / (knuckle * knuckle * knuckle)
            };
            jumps.Add(jump);
            return jump.End;
        }

        public float Height(float station, out float slope)
        {
            foreach (var j in jumps)
                if (station > j.lip && station < j.End)
                    return JumpHeight(j, station - j.lip, out slope);
            return HeightOfPvis(station, out slope);
        }

        static float JumpHeight(Jump j, float d, out float slope)
        {
            var shape = j.shape;
            float knuckle = shape.flight - shape.rampBeforeContact;
            if (d < knuckle)
            {
                float pit = j.pitCubicA * d * d + j.pitCubicB * d * d * d;
                slope = shape.launchGrade - 2f * shape.K * d - (2f * j.pitCubicA * d + 3f * j.pitCubicB * d * d);
                return j.height + shape.Flight(d) - pit;
            }
            float rampEnd = shape.flight + shape.runOut;
            if (d <= rampEnd)
            {
                slope = shape.RampGrade;
                return j.height + shape.Ramp(d);
            }
            float x = d - rampEnd;
            slope = shape.RampGrade - shape.RampGrade * x / shape.SagLength;
            return j.height + shape.Ramp(rampEnd) + shape.RampGrade * x - shape.RampGrade / (2f * shape.SagLength) * x * x;
        }

        float HeightOfPvis(float station, out float slope)
        {
            if (pvis.Count == 0) { slope = 0f; return 0f; }
            if (pvis.Count == 1) { slope = 0f; return pvis[0].height; }

            for (int i = 1; i < pvis.Count - 1; i++)
            {
                float before = Grade(i - 1), after = Grade(i);
                float length = pvis[i].radius * Mathf.Abs(after - before);
                float from = pvis[i].station - length / 2f;
                if (length > 0f && station >= from && station <= from + length)
                {
                    float x = station - from;
                    slope = before + (after - before) * x / length;
                    return pvis[i].height - before * length / 2f + before * x + (after - before) / (2f * length) * x * x;
                }
            }

            int k = 0;
            while (k < pvis.Count - 2 && station > pvis[k + 1].station) k++;
            slope = Grade(k);
            return pvis[k].height + slope * (station - pvis[k].station);
        }

        float Grade(int i) => (pvis[i + 1].height - pvis[i].height) / (pvis[i + 1].station - pvis[i].station);
    }
}
