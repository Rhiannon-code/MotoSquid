using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MotoSquid.Roads
{
    // The open world's real-route data (tools/roads/build_world.py -> RoadData/world_roads.txt): each road's
    // simplified centreline in world metres, and the interchange sites where the real ramps were.
    public sealed class RoadWorldData
    {
        public const string DefaultFile = "RoadData/world_roads.txt";

        public sealed class Road
        {
            public string Id, Name;
            public int Class, Lanes;
            public readonly List<Vector2> Points = new List<Vector2>();
            public readonly List<bool> Elevated = new List<bool>();
        }

        public sealed class Site
        {
            public bool System;
            public string Freeway, Other;
            public Vector2 At;
        }

        public readonly List<Road> Roads = new List<Road>();
        public readonly List<Site> Sites = new List<Site>();

        public static RoadWorldData Load(string path)
        {
            var data = new RoadWorldData();
            Road road = null;
            foreach (var raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("road ", StringComparison.Ordinal))
                {
                    var f = line.Substring(5).Split('|');
                    road = new Road { Id = f[0], Name = f[1], Class = Int(f[2]), Lanes = Int(f[3]) };
                    data.Roads.Add(road);
                }
                else if (line.StartsWith("site ", StringComparison.Ordinal))
                {
                    var f = line.Substring(5).Split('|');
                    data.Sites.Add(new Site { System = f[0] == "system", Freeway = f[1], Other = f[2], At = new Vector2(Float(f[3]), Float(f[4])) });
                }
                else
                {
                    var v = line.Split(' ');
                    road.Points.Add(new Vector2(Float(v[0]), Float(v[1])));
                    road.Elevated.Add(v.Length > 2 && v[2] == "1");
                }
            }
            return data;
        }

        static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);
        static float Float(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    }
}
