using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    public interface ISurfaceProbe
    {
        bool Raycast(Vector3 origin, Vector3 direction, float distance, out Vector3 point, out Vector3 normal);
    }

    [Serializable]
    public class RoadCheckSettings
    {
        [Tooltip("Speed the air-time simulation rides every lane at (m/s). 100 = 360 km/h.")]
        public float speed = 100f;
        public float step = 0.5f;
        [Tooltip("Largest change in slope between neighbouring samples, as a height step (m).")]
        public float maxStep = 0.01f;
        public float maxStepOnJumps = 0.03f;
        [Tooltip("Largest angle between neighbouring surface normals (degrees).")]
        public float maxNormalTurn = 2f;
        public float maxNormalTurnOnJumps = 4f;
        public float maxGrade = 0.055f;
        public float maxGradeOnJumps = 0.3f;
        public float bankTolerance = 0.5f;
        public float rollRateTolerance = 1.1f;
        public float groundedMaxAir = 0.05f;
        public float hopMaxAir = 1.5f;
        public float maxLandingAngle = 10f;
        public float clearance = 6f;
        [Tooltip("How far the built surface may sit from the road's own design height (m).")]
        public float offSurfaceTolerance = 0.1f;
    }

    public enum IssueKind { Gap, Bump, Kink, OffSurface, Grade, Bank, RollRate, Air, Landing, Clearance }

    public struct RoadIssue
    {
        public IssueKind Kind;
        public string Road;
        public int Lane;
        public float Station;
        public Vector3 Position;
        public float Value;
        public float Limit;

        public override string ToString() =>
            $"{Kind} on {Road} lane {Lane + 1} at {Station:0} m: {Value:0.###} (limit {Limit:0.###})";
    }

    public struct AirEvent
    {
        public string Road;
        public int Lane;
        public float Takeoff;
        public float Landing;
        public float Seconds;
        public float LandingAngle;
        public AirClass Class;

        public override string ToString() =>
            $"{Road} lane {Lane + 1}: {Class} air {Seconds:0.00} s from {Takeoff:0} m to {Landing:0} m, landing {LandingAngle:0.0}° off the slope";
    }

    public sealed class RoadCheckResult
    {
        public readonly List<RoadIssue> Issues = new List<RoadIssue>();
        public readonly List<AirEvent> Air = new List<AirEvent>();
        public int Samples;
        public bool Passed => Issues.Count == 0;

        public Dictionary<IssueKind, (int count, float worst)> Summary()
        {
            var summary = new Dictionary<IssueKind, (int, float)>();
            foreach (var issue in Issues)
            {
                summary.TryGetValue(issue.Kind, out var s);
                summary[issue.Kind] = (s.Item1 + 1, Mathf.Max(s.Item2, Mathf.Abs(issue.Value)));
            }
            return summary;
        }
    }

    // Rides every lane of every road at sample spacing, measuring the built surface itself (through the
    // probe), not the design. Consecutive breaches of the same kind on a lane are reported once, at the worst.
    public static class RoadChecker
    {
        const float Gravity = 9.81f;
        const float MinAirTime = 0.02f;
        const float IssueGap = 5f;

        public static RoadCheckResult Run(IReadOnlyList<RoadPath> paths, IReadOnlyList<JunctionZone> zones,
                                          ISurfaceProbe probe, RoadCheckSettings cfg)
        {
            var result = new RoadCheckResult();
            foreach (var path in paths)
            {
                CheckBank(path, cfg, result);
                int lane = 0;
                foreach (var (offset, forward) in path.Settings.Lanes())
                    CheckLane(path, zones, lane++, offset, forward, probe, cfg, result);
            }
            return result;
        }

        static void CheckBank(RoadPath path, RoadCheckSettings cfg, RoadCheckResult result)
        {
            var bank = new IssueRun(result, path.Name, -1);
            var roll = new IssueRun(result, path.Name, -1);
            float maxRoll = path.Settings.maxRollRate * cfg.rollRateTolerance;
            for (int i = 0; i < path.Count; i++)
            {
                float s = i * path.Spacing;
                Vector3 at = path.Centre[i];
                bank.Report(Mathf.Abs(path.Bank[i]) > path.Settings.maxBank + cfg.bankTolerance, IssueKind.Bank, s, at, path.Bank[i], path.Settings.maxBank);
                if (i > 0)
                {
                    float rate = Mathf.Abs(path.Bank[i] - path.Bank[i - 1]) / path.Spacing;
                    roll.Report(rate > maxRoll, IssueKind.RollRate, s, at, rate, maxRoll);
                }
            }
            bank.Flush();
            roll.Flush();
        }

        static void CheckLane(RoadPath path, IReadOnlyList<JunctionZone> zones, int lane, float offset, bool forward,
                              ISurfaceProbe probe, RoadCheckSettings cfg, RoadCheckResult result)
        {
            int count = Mathf.FloorToInt(path.Length / cfg.step) + 1;
            var stations = new float[count];
            var heights = new float[count];
            var distance = new float[count];
            var normals = new Vector3[count];
            var hit = new bool[count];
            var runs = new Dictionary<IssueKind, IssueRun>();
            IssueRun Run(IssueKind kind) => runs.TryGetValue(kind, out var r) ? r : runs[kind] = new IssueRun(result, path.Name, lane);

            Vector3 previous = default;
            for (int k = 0; k < count; k++)
            {
                float s = forward ? k * cfg.step : path.Length - k * cfg.step;
                stations[k] = s;
                Vector3 design = path.PointAt(s, offset);
                hit[k] = probe.Raycast(design + Vector3.up * 2f, Vector3.down, 4f, out Vector3 point, out normals[k]);
                result.Samples++;
                Run(IssueKind.Gap).Report(!hit[k], IssueKind.Gap, s, design, 1f, 0f);
                if (!hit[k]) { point = design; }
                heights[k] = point.y;
                distance[k] = k == 0 ? 0f : distance[k - 1] + RoadPath.Flat(point - previous).magnitude;
                previous = point;
                if (!hit[k]) continue;

                Run(IssueKind.OffSurface).Report(Mathf.Abs(point.y - design.y) > cfg.offSurfaceTolerance,
                    IssueKind.OffSurface, s, point, point.y - design.y, cfg.offSurfaceTolerance);
                bool covered = probe.Raycast(point + Vector3.up * 0.3f, Vector3.up, cfg.clearance - 0.3f, out Vector3 above, out _);
                Run(IssueKind.Clearance).Report(covered, IssueKind.Clearance, s, point,
                    covered ? above.y - point.y : cfg.clearance, cfg.clearance);
            }

            for (int k = 1; k < count; k++)
            {
                if (!hit[k] || !hit[k - 1]) continue;
                bool jump = ClassAt(path, zones, stations[k]) == AirClass.Jump;
                float run = Mathf.Max(distance[k] - distance[k - 1], 1e-4f);
                float grade = (heights[k] - heights[k - 1]) / run;
                float gradeLimit = jump ? cfg.maxGradeOnJumps : cfg.maxGrade;
                Run(IssueKind.Grade).Report(Mathf.Abs(grade) > gradeLimit, IssueKind.Grade, stations[k], At(path, stations[k], offset, heights[k]), grade, gradeLimit);

                float turn = Vector3.Angle(normals[k - 1], normals[k]);
                float turnLimit = jump ? cfg.maxNormalTurnOnJumps : cfg.maxNormalTurn;
                Run(IssueKind.Kink).Report(turn > turnLimit, IssueKind.Kink, stations[k], At(path, stations[k], offset, heights[k]), turn, turnLimit);

                if (k + 1 < count && hit[k + 1])
                {
                    float step = heights[k + 1] - 2f * heights[k] + heights[k - 1];
                    float stepLimit = jump ? cfg.maxStepOnJumps : cfg.maxStep;
                    Run(IssueKind.Bump).Report(Mathf.Abs(step) > stepLimit, IssueKind.Bump, stations[k], At(path, stations[k], offset, heights[k]), step, stepLimit);
                }
            }
            foreach (var r in runs.Values) r.Flush();

            SimulateAir(path, zones, lane, stations, heights, distance, hit, cfg, result);
        }

        // A point mass at speed along the lane: it leaves the road wherever the road falls away faster than
        // gravity can pull it down, and lands where its flight path meets the road again.
        static void SimulateAir(RoadPath path, IReadOnlyList<JunctionZone> zones, int lane, float[] stations, float[] heights,
                                float[] distance, bool[] hit, RoadCheckSettings cfg, RoadCheckResult result)
        {
            float v = cfg.speed;
            bool flying = false;
            float y = 0f, vy = 0f, time = 0f, takeoff = 0f;
            AirClass takeoffClass = AirClass.Grounded;
            for (int k = 2; k < heights.Length; k++)
            {
                if (!hit[k] || !hit[k - 1] || !hit[k - 2]) { flying = false; continue; }
                float run = distance[k] - distance[k - 1];
                float dt = run / v;
                if (!flying)
                {
                    float slope = (heights[k - 1] - heights[k - 2]) / Mathf.Max(distance[k - 1] - distance[k - 2], 1e-4f);
                    float ballistic = heights[k - 1] + slope * run - 0.5f * Gravity * dt * dt;
                    if (ballistic > heights[k])
                    {
                        flying = true;
                        y = ballistic;
                        vy = slope * v - Gravity * dt;
                        time = dt;
                        takeoff = stations[k - 1];
                        takeoffClass = ClassAt(path, zones, takeoff);
                    }
                    continue;
                }

                y += vy * dt - 0.5f * Gravity * dt * dt;
                vy -= Gravity * dt;
                time += dt;
                if (y > heights[k]) continue;

                flying = false;
                if (time < MinAirTime) continue;
                float roadSlope = (heights[k] - heights[k - 1]) / Mathf.Max(run, 1e-4f);
                float angle = Mathf.Abs(Mathf.Atan(vy / v) - Mathf.Atan(roadSlope)) * Mathf.Rad2Deg;
                var air = new AirEvent
                {
                    Road = path.Name, Lane = lane, Takeoff = takeoff, Landing = stations[k],
                    Seconds = time, LandingAngle = angle, Class = takeoffClass
                };
                result.Air.Add(air);

                Vector3 at = path.PointAt(takeoff, 0f);
                float allowed = takeoffClass == AirClass.Grounded ? cfg.groundedMaxAir
                              : takeoffClass == AirClass.Hop ? cfg.hopMaxAir : float.MaxValue;
                if (time > allowed)
                    result.Issues.Add(new RoadIssue { Kind = IssueKind.Air, Road = path.Name, Lane = lane, Station = takeoff, Position = at, Value = time, Limit = allowed });
                if (angle > cfg.maxLandingAngle)
                    result.Issues.Add(new RoadIssue { Kind = IssueKind.Landing, Road = path.Name, Lane = lane, Station = stations[k], Position = path.PointAt(stations[k], 0f), Value = angle, Limit = cfg.maxLandingAngle });
            }
        }

        static AirClass ClassAt(RoadPath path, IReadOnlyList<JunctionZone> zones, float station)
        {
            foreach (var zone in zones)
                if (zone.Contains(path, station))
                    return AirClass.Grounded;
            return path.Settings.AirClassAt(station);
        }

        static Vector3 At(RoadPath path, float station, float offset, float height)
        {
            Vector3 p = path.PointAt(station, offset);
            p.y = height;
            return p;
        }

        sealed class IssueRun
        {
            readonly RoadCheckResult result;
            readonly string road;
            readonly int lane;
            RoadIssue worst;
            bool open;
            float lastStation;

            public IssueRun(RoadCheckResult result, string road, int lane)
            {
                this.result = result;
                this.road = road;
                this.lane = lane;
            }

            public void Report(bool breached, IssueKind kind, float station, Vector3 position, float value, float limit)
            {
                if (!breached) return;
                if (open && Mathf.Abs(station - lastStation) > IssueGap) Flush();
                if (!open || Mathf.Abs(value) > Mathf.Abs(worst.Value))
                    worst = new RoadIssue { Kind = kind, Road = road, Lane = lane, Station = station, Position = position, Value = value, Limit = limit };
                open = true;
                lastStation = station;
            }

            public void Flush()
            {
                if (open) result.Issues.Add(worst);
                open = false;
            }
        }
    }
}
