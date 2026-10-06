using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MotoSquid.Roads;
using UnityEngine;

// Builds the proof-of-concept roads and runs the road checker outside Unity, against the built mesh.
static class Program
{
    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        var t0 = DateTime.Now;
        bool interchange = args.Length > 1 && args[1] == "interchange";
        bool designed = args.Length > 1 && args[1] == "designed";
        var definitions = interchange ? RoadDataLayout.Load(Path.Combine(args[2], RoadDataLayout.DefaultFile), args.Length > 4 ? float.Parse(args[3]) : 6f, args.Length > 4 ? float.Parse(args[4]) : 3f, args.Length > 5 ? float.Parse(args[5]) : 120f)
                        : designed ? RoadInterchangeLayout.Roads() : RoadPocLayout.Roads();
        var paths = definitions.Select(r => Dense(r)).ToList();
        foreach (var p in paths.Take(interchange ? 0 : paths.Count))
            Console.WriteLine($"{p.Name}: {p.Length:0} m, {p.Count} stations, bank {p.Bank.Min():0.0}..{p.Bank.Max():0.0}, y {p.Centre.Min(c => c.y):0.00}..{p.Centre.Max(c => c.y):0.00}");
        var surface = RoadSurfaceBuilder.Build(paths, 4f);
        Console.WriteLine($"surface: {surface.Vertices.Count} verts, {surface.Triangles.Count / 3} tris, {surface.Zones.Count} zones, {surface.BarrierTriangles.Count / 3} barrier tris, {(DateTime.Now - t0).TotalSeconds:0.0}s");
        foreach (var z in surface.Zones) Console.WriteLine("  zone: " + RoadSurfaceBuilder.Describe(z));
        foreach (var w in surface.Warnings) Console.WriteLine("  WARNING " + w);
        Console.WriteLine($"  layout conflicts: {surface.Conflicts.Count}");
        foreach (var c in surface.Conflicts.Take(12)) Console.WriteLine("    " + c);
        using (var obj = new StreamWriter(args.Length > 0 ? args[0] : "surface.obj"))
        {
            foreach (var v in surface.Vertices) obj.WriteLine($"v {v.x} {v.y} {v.z}");
            for (int i = 0; i < surface.Triangles.Count; i += 3) obj.WriteLine($"f {surface.Triangles[i] + 1} {surface.Triangles[i + 1] + 1} {surface.Triangles[i + 2] + 1}");
        }
        using (var obj = new StreamWriter(Path.ChangeExtension(args.Length > 0 ? args[0] : "surface.obj", null) + "-barriers.obj"))
        {
            foreach (var v in surface.BarrierVertices) obj.WriteLine($"v {v.x} {v.y} {v.z}");
            for (int i = 0; i < surface.BarrierTriangles.Count; i += 3) obj.WriteLine($"f {surface.BarrierTriangles[i] + 1} {surface.BarrierTriangles[i + 1] + 1} {surface.BarrierTriangles[i + 2] + 1}");
        }
        var probe = new TriangleProbe(surface.Vertices, surface.Triangles);
        var result = RoadChecker.Run(paths, surface.Zones, probe, new RoadCheckSettings());
        Console.WriteLine($"checker: {(result.Passed ? "PASSED" : result.Issues.Count + " breaches")} over {result.Samples} samples, {(DateTime.Now - t0).TotalSeconds:0.0}s");
        var byName = paths.ToDictionary(p => p.Name);
        int inZone = result.Issues.Count(i => byName.TryGetValue(i.Road, out var rp) && surface.Zones.Any(z => z.Contains(rp, i.Station)));
        int nearZone = result.Issues.Count(i => byName.TryGetValue(i.Road, out var rp) && surface.Zones.Any(z => z.Parts.Any(pt => pt.Path == rp && i.Station >= (pt.From - 60) * rp.Spacing && i.Station <= (pt.To + 60) * rp.Spacing)));
        Console.WriteLine($"  breaches inside junction zones: {inZone}, within 60 m of one: {nearZone}, elsewhere: {result.Issues.Count - nearZone}");
        foreach (var kv in result.Summary().OrderBy(k => k.Key)) Console.WriteLine($"  {kv.Key}: {kv.Value.count} places, worst {kv.Value.worst:0.###}");
        foreach (var a in result.Air) Console.WriteLine("  air: " + a);
        foreach (var i in result.Issues.Take(80)) Console.WriteLine("  " + i + $" @ ({i.Position.x:0},{i.Position.y:0.0},{i.Position.z:0})");
        return 0;
    }

    static RoadPath Dense(RoadPocLayout.RoadDefinition r)
    {
        var pts = new List<Vector3>();
        var k = r.Knots;
        for (int c = 0; c < k.Length - 1; c++)
        {
            Vector3 p0 = k[c].Position, p1 = (Vector3)(k[c].Position + k[c].TangentOut), p2 = (Vector3)(k[c + 1].Position + k[c + 1].TangentIn), p3 = k[c + 1].Position;
            int n = Math.Max(4, (int)Math.Ceiling(Vector3.Distance(p0, p3) / 0.25f));
            for (int i = c == 0 ? 0 : 1; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                pts.Add(u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3);
            }
        }
        Vector3 start = (Vector3)k[0].TangentOut, end = -(Vector3)k[k.Length - 1].TangentIn;
        return RoadPath.FromPoints(r.Name, pts.ToArray(), start, end, r.Settings);
    }
}

// Vertical rays against the triangle mesh, through a 2D grid.
sealed class TriangleProbe : ISurfaceProbe
{
    const float Cell = 8f;
    readonly List<Vector3> v; readonly List<int> t;
    readonly Dictionary<(int, int), List<int>> grid = new Dictionary<(int, int), List<int>>();
    public TriangleProbe(List<Vector3> vertices, List<int> triangles)
    {
        v = vertices; t = triangles;
        for (int i = 0; i < t.Count; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / Cell), x1 = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / Cell);
            int z0 = Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) / Cell), z1 = Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) / Cell);
            for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
            { if (!grid.TryGetValue((x, z), out var l)) grid[(x, z)] = l = new List<int>(); l.Add(i); }
        }
    }
    public bool Raycast(Vector3 origin, Vector3 direction, float distance, out Vector3 point, out Vector3 normal)
    {
        point = default; normal = Vector3.up; float best = float.MaxValue; bool found = false;
        if (!grid.TryGetValue((Mathf.FloorToInt(origin.x / Cell), Mathf.FloorToInt(origin.z / Cell)), out var list)) return false;
        foreach (int i in list)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            double d = (double)(b.z - c.z) * (a.x - c.x) + (double)(c.x - b.x) * (a.z - c.z);
            if (Math.Abs(d) < 1e-12) continue;
            double l1 = ((b.z - c.z) * (origin.x - c.x) + (c.x - b.x) * (origin.z - c.z)) / d;
            double l2 = ((c.z - a.z) * (origin.x - c.x) + (a.x - c.x) * (origin.z - c.z)) / d;
            double l3 = 1 - l1 - l2;
            if (l1 < -1e-7 || l2 < -1e-7 || l3 < -1e-7) continue;
            float y = (float)(l1 * a.y + l2 * b.y + l3 * c.y);
            float along = (y - origin.y) * direction.y;
            if (along < 0 || along > distance || along >= best) continue;
            best = along; found = true; point = new Vector3(origin.x, y, origin.z);
            normal = Vector3.Cross(b - a, c - a).normalized; if (normal.y < 0) normal = -normal;
        }
        return found;
    }
}
