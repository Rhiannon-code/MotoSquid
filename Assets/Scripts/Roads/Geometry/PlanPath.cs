using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    // A road's plan view built from straights and circular arcs, in metres on the XZ plane.
    public sealed class PlanPath
    {
        readonly Vector2 start;
        readonly Vector2 startDirection;
        readonly List<(float length, float curvature)> segments = new List<(float, float)>();

        public PlanPath(Vector2 start, Vector2 direction)
        {
            this.start = start;
            startDirection = direction.normalized;
        }

        public float Length { get; private set; }

        public PlanPath Straight(float length) => Add(length, 0f);

        // Positive degrees turn left.
        public PlanPath Arc(float radius, float degrees) =>
            Add(radius * Mathf.Abs(degrees) * Mathf.Deg2Rad, Mathf.Sign(degrees) / radius);

        public void Evaluate(float station, out Vector2 position, out Vector2 direction)
        {
            position = start;
            direction = startDirection;
            float left = Mathf.Clamp(station, 0f, Length);
            foreach (var (length, curvature) in segments)
            {
                float run = Mathf.Min(left, length);
                Advance(ref position, ref direction, run, curvature);
                left -= run;
                if (left <= 0f) break;
            }
        }

        PlanPath Add(float length, float curvature)
        {
            segments.Add((length, curvature));
            Length += length;
            return this;
        }

        static void Advance(ref Vector2 position, ref Vector2 direction, float run, float curvature)
        {
            if (Mathf.Abs(curvature) < 1e-9f)
            {
                position += direction * run;
                return;
            }
            float radius = 1f / curvature;
            var leftNormal = new Vector2(-direction.y, direction.x);
            Vector2 centre = position + leftNormal * radius;
            float angle = run * curvature;
            position = centre + Rotate(position - centre, angle);
            direction = Rotate(direction, angle);
        }

        static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
