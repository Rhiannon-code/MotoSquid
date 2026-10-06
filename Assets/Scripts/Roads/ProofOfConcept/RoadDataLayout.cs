using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Mathematics;
using UnityEngine.Splines;

namespace MotoSquid.Roads
{
    // Roads from the real-data pipeline (tools/roads/build_network.py -> RoadData/spline_roads.txt):
    // each road's evenly spaced points become spline knots with auto-smooth tangents.
    public static class RoadDataLayout
    {
        public const string DefaultFile = "RoadData/spline_roads.txt";

        // The data's grades change over about 50 m, which would crest bikes at speed; average them out.
        const float HeightSmoothing = 120f;

        public static List<RoadPocLayout.RoadDefinition> Load(string path, float laneWidth = 6f, float shoulder = 3f, float heightSmoothing = HeightSmoothing)
        {
            var roads = new List<RoadPocLayout.RoadDefinition>();
            string[] header = null;
            var points = new List<float3>();

            foreach (var raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("road ", StringComparison.Ordinal))
                {
                    Flush();
                    header = line.Substring(5).Split('|');
                    continue;
                }
                var v = line.Split(' ');
                points.Add(new float3(Parse(v[0]), Parse(v[1]), Parse(v[2])));
            }
            Flush();
            return roads;

            void Flush()
            {
                if (header != null && points.Count >= 2)
                {
                    bool oneWay = header[3] == "1";
                    int lanes = int.Parse(header[4], CultureInfo.InvariantCulture);
                    var settings = new RoadSettings
                    {
                        lanesForward = oneWay ? lanes : (lanes + 1) / 2,
                        lanesBackward = oneWay ? 0 : lanes / 2,
                        laneWidth = laneWidth,
                        shoulder = shoulder,
                        heightSmoothing = heightSmoothing,
                    };
                    roads.Add(new RoadPocLayout.RoadDefinition { Name = $"{header[1]} [{header[0]}]", Knots = Knots(points), Settings = settings });
                }
                points.Clear();
            }
        }

        static float Parse(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static BezierKnot[] Knots(List<float3> points)
        {
            var knots = new BezierKnot[points.Count];
            for (int k = 0; k < points.Count; k++)
            {
                var previous = points[Math.Max(0, k - 1)];
                var next = points[Math.Min(points.Count - 1, k + 1)];
                var tangent = SplineUtility.GetAutoSmoothTangent(previous, points[k], next);
                knots[k] = new BezierKnot(points[k], -tangent, tangent);
            }
            return knots;
        }
    }
}
