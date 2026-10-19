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

        // Level stretches joined by straight grades. Each change starts its rounding at `from` and runs at
        // `grade` to `height`; the road stays level after the last one. A change too short to reach its grade
        // between the two roundings uses the steepest grade at which they just meet.
        public static VerticalProfile Stepped(float startHeight, float radius, params (float from, float height, float grade)[] changes)
        {
            var profile = new VerticalProfile().Point(0f, startHeight);
            foreach (var (from, to, grade) in changes) profile.Step(from, to, grade, radius);
            return profile.Level();
        }

        float LastHeight => pvis.Count > 0 ? pvis[pvis.Count - 1].height : 0f;
        float LastStation => pvis.Count > 0 ? pvis[pvis.Count - 1].station : 0f;

        // A change from the current height to `to`, its rounding starting at `from` (see Stepped).
        public VerticalProfile Step(float from, float to, float grade, float radius)
        {
            float height = LastHeight;
            if (Mathf.Approximately(to, height)) return this;
            float g = ReachableGrade(to - height, grade, radius);
            float first = from + radius * g / 2f;
            return Point(first, height, radius).Point(first + Mathf.Abs(to - height) / g, to, radius);
        }

        // A rise and fall back to the current height over `length` metres from `from`. A crest tighter than
        // v²/g gives air on purpose (a hop): the far side is the landing slope.
        public VerticalProfile Bump(float from, float rise, float length, float crestRadius, float sagRadius)
        {
            float height = LastHeight;
            return Point(from, height, sagRadius).Point(from + length / 2f, height + rise, crestRadius).Point(from + length, height, sagRadius);
        }

        // Level from the last point on.
        public VerticalProfile Level() => Point(LastStation + 1e6f, LastHeight);

        public static VerticalProfile FromPoints(IEnumerable<HeightPoint> points)
        {
            var profile = new VerticalProfile();
            foreach (var p in points) profile.Point(p.station, p.height, p.radius);
            return profile;
        }

        // The height points, the last one moved in to the road's end so it reads sensibly in an inspector.
        public List<HeightPoint> HeightPoints(float roadLength)
        {
            var points = new List<HeightPoint>();
            foreach (var p in pvis) points.Add(new HeightPoint { station = p.station, height = p.height, radius = p.radius });
            if (points.Count > 1 && points[points.Count - 1].station > roadLength)
            {
                var last = points[points.Count - 1];
                last.station = Mathf.Max(roadLength, points[points.Count - 2].station + 1f);
                points[points.Count - 1] = last;
            }
            return points;
        }

        // Stations where the height stops being one straight grade or one rounding: where every rounding
        // starts and ends, and at every sharp point.
        public IEnumerable<float> Breaks()
        {
            for (int i = 1; i < pvis.Count - 1; i++)
            {
                float length = pvis[i].radius * Mathf.Abs(Grade(i) - Grade(i - 1));
                yield return pvis[i].station - length / 2f;
                if (length > 0f) yield return pvis[i].station + length / 2f;
            }
        }

        // Metres from the start of a change's rounding to the end of it.
        public static float ChangeLength(float rise, float grade, float radius)
        {
            if (Mathf.Approximately(rise, 0f)) return 0f;
            float g = ReachableGrade(rise, grade, radius);
            return radius * g + Mathf.Abs(rise) / g;
        }

        static float ReachableGrade(float rise, float grade, float radius) =>
            radius > 0f ? Mathf.Min(grade, Mathf.Sqrt(Mathf.Abs(rise) / radius)) : grade;

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
