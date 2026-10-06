using MotoSquid.Traffic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.AI
{
    public class AITrafficPlanner
    {
        public struct RouteFrame
        {
            public Vector3 position;
            public Vector3 tangent;
            public Vector3 left;
        }

        public float horizon      = 3f;
        public float step         = 0.1f;
        // The AI steers at a point lookahead metres up the road, so a new line is approached along an
        // exponential with time constant lookahead / speed, about 0.75 s at racing speed, not at a
        // constant rate. A constant 6 m/s assumed a lane in 0.6 s where the bike needs about 2 s
        public float lateralTimeConstant = 0.75f;
        public float slotSpacing  = 1f;
        public float margin       = 0.6f;
        public float stickiness   = 0.5f;
        public float bikeHalfWidth  = 0.45f;
        public float bikeHalfLength = 1.1f;

        public float TargetLateral { get; private set; }
        public float Urgency       { get; private set; }
        public bool  Blocked       { get; private set; }
        public float SpeedCapMs    { get; private set; } = float.MaxValue;

        struct Occupancy
        {
            public float time;
            public float lateral;
            public float halfWidth;
            public float alongSpeed;
            public SplineMover mover;
        }

        float _planTime, _planFromLateral;

        readonly List<RouteFrame> _frames = new List<RouteFrame>();
        readonly List<Occupancy>  _occupied = new List<Occupancy>();

        // Slots are limited to [minLateral, maxLateral]: the carriageway whose traffic runs with the bike,
        // so the only cars it meets close at the speed difference rather than head on. Only when that is
        // blocked does it search the whole road [roadMin, roadMax], where oncoming cars are still obstacles
        public void Plan(Vector3 bikePos, float speedMs, float currentLateral, float preferredLateral,
                         float minLateral, float maxLateral, float roadMin, float roadMax,
                         Func<float, RouteFrame> routeAt)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(horizon / step));
            speedMs = Mathf.Max(speedMs, 0f);

            _planTime = Time.time;
            _planFromLateral = currentLateral;

            _frames.Clear();
            for (int k = 0; k <= steps; k++)
                _frames.Add(routeAt(speedMs * k * step));

            GatherOccupancy(bikePos, speedMs, steps);

            float currentHit = FirstHit(currentLateral, currentLateral, out _);
            Urgency = float.IsPositiveInfinity(currentHit) ? 0f : Mathf.Clamp01(1f - currentHit / horizon);

            float latestHit = -1f, latestSlot = currentLateral, latestSpeed = 0f;
            float bestClear = Search(minLateral, maxLateral, currentLateral, preferredLateral,
                                     ref latestHit, ref latestSlot, ref latestSpeed);
            if (float.IsNaN(bestClear) && (roadMin < minLateral || roadMax > maxLateral))
                bestClear = Search(roadMin, roadMax, currentLateral, preferredLateral,
                                   ref latestHit, ref latestSlot, ref latestSpeed);

            Blocked = float.IsNaN(bestClear);
            TargetLateral = Blocked ? latestSlot : bestClear;
            // Nowhere clear: hold the gap by matching the car we would reach last, rather than hit it
            SpeedCapMs = Blocked ? Mathf.Max(0f, latestSpeed) : float.MaxValue;
        }

        // Clear slot nearest the preference, or NaN; blocked slots update the one reached last
        float Search(float lo, float hi, float currentLateral, float preferredLateral,
                     ref float latestHit, ref float latestSlot, ref float latestSpeed)
        {
            float bestCost = float.MaxValue, best = float.NaN;
            for (float slot = lo; slot <= hi + 0.001f; slot += slotSpacing)
            {
                float hit = FirstHit(currentLateral, slot, out float blockerSpeed);
                if (float.IsPositiveInfinity(hit))
                {
                    float cost = Mathf.Abs(slot - preferredLateral) + stickiness * Mathf.Abs(slot - TargetLateral);
                    if (cost < bestCost) { bestCost = cost; best = slot; }
                }
                else if (hit > latestHit ||
                         (Mathf.Approximately(hit, latestHit) && Mathf.Abs(slot - preferredLateral) < Mathf.Abs(latestSlot - preferredLateral)))
                {
                    latestHit = hit; latestSlot = slot; latestSpeed = blockerSpeed;
                }
            }
            return best;
        }

        // Only vehicle positions that overlap the bike's own length at the same moment matter, so each
        // is reduced to its lateral position on the route at that step
        void GatherOccupancy(Vector3 bikePos, float speedMs, int steps)
        {
            _occupied.Clear();
            // Covers an oncoming car too, which closes from the far side at its own speed
            float reach = (speedMs + 45f) * horizon + 20f;
            float reachSq = reach * reach;

            var movers = SplineMover.Active;
            for (int i = 0; i < movers.Count; i++)
            {
                var m = movers[i];
                if (m == null) continue;

                Vector3 p0 = m.transform.position;
                Vector3 flat = p0 - bikePos;
                flat.y = 0f;
                if (flat.sqrMagnitude > reachSq) continue;

                Vector3 v = m.Velocity;
                v.y = 0f;
                Vector2 half = m.HalfSize;
                float window = half.y + bikeHalfLength;
                var spawner = TrafficSpawner.Instance;
                Vector3 prevPos = p0;
                // Lane samples are 40 m chords, so anchor the prediction on where the car really is now
                Vector3 laneNow = default;
                bool onLane = spawner != null && spawner.TryPredict(m, 0f, out laneNow);
                Vector3 anchor = onLane ? p0 - laneNow : Vector3.zero;

                // Closing head-on at 150 m/s a car moves 15 m per step against a window of a few metres,
                // so each step tests the span swept since the last one, not a single sample
                float prevAlong = 0f;
                for (int k = 0; k <= steps; k++)
                {
                    float t = k * step;
                    RouteFrame f = _frames[k];
                    // Along its own lane where the spawner knows it, else a straight line (trams, lane changes)
                    Vector3 pos = onLane && spawner.TryPredict(m, t, out Vector3 ahead) ? ahead + anchor : p0 + v * t;
                    if (k > 0) { v = (pos - prevPos) / step; v.y = 0f; }
                    prevPos = pos;

                    Vector3 rel = pos - f.position;
                    rel.y = 0f;

                    float along = Vector3.Dot(rel, f.tangent);
                    float lo = k == 0 ? along : Mathf.Min(along, prevAlong);
                    float hi = k == 0 ? along : Mathf.Max(along, prevAlong);
                    prevAlong = along;
                    if (lo > window || hi < -window) continue;

                    _occupied.Add(new Occupancy
                    {
                        time       = t,
                        lateral    = Vector3.Dot(rel, f.left),
                        halfWidth  = half.x,
                        alongSpeed = Vector3.Dot(v, f.tangent),
                        mover      = m,
                    });
                }
            }
        }

        // For the crash log: what the last plan knew about the vehicle the bike has just hit
        public string Describe(Component hit)
        {
            var mover = hit != null ? hit.GetComponentInParent<SplineMover>() : null;
            string car;
            if (mover == null) car = "not a registered vehicle";
            else
            {
                float first = float.PositiveInfinity, lat = 0f;
                foreach (var o in _occupied)
                    if (o.mover == mover && o.time < first) { first = o.time; lat = o.lateral; }
                car = float.IsPositiveInfinity(first)
                    ? "registered but never on the route in the last plan"
                    : $"predicted on the route at t={first:F1}s, lateral {lat:F1}";
            }

            return $"plan {Time.time - _planTime:F2}s old, lateral {_planFromLateral:F1} -> {TargetLateral:F1}, " +
                   $"urgency {Urgency:F2}, blocked {Blocked}, tau {lateralTimeConstant:F2}s; hit car {car}";
        }

        float FirstHit(float fromLateral, float toLateral, out float blockerAlongSpeed)
        {
            float first = float.PositiveInfinity;
            blockerAlongSpeed = 0f;

            for (int i = 0; i < _occupied.Count; i++)
            {
                Occupancy o = _occupied[i];
                if (o.time >= first) continue;

                float bikeLat = toLateral + (fromLateral - toLateral) * Mathf.Exp(-o.time / Mathf.Max(lateralTimeConstant, 0.05f));
                if (Mathf.Abs(bikeLat - o.lateral) < o.halfWidth + bikeHalfWidth + margin)
                {
                    first = o.time;
                    blockerAlongSpeed = o.alongSpeed;
                }
            }
            return first;
        }
    }
}
