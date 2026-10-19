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
        bool corridor = args.Length > 1 && args[1] == "corridor";
        bool world = args.Length > 1 && args[1] == "world";
        bool cloverstack = args.Length > 1 && args[1] == "cloverstack";
        bool mergesplit = args.Length > 1 && args[1] == "mergesplit";
        bool plan = args.Length > 1 && args[1] == "plan";
        if (args.Length > 1 && args[1] == "free") { PrintFree(Environment.GetEnvironmentVariable("PLAN") ?? Path.Combine(args[2], "RoadData/world_plan.json")); return 0; }
        var report = new List<string>();
        var definitions = interchange ? RoadDataLayout.Load(Path.Combine(args[2], RoadDataLayout.DefaultFile), args.Length > 4 ? float.Parse(args[3]) : 6f, args.Length > 4 ? float.Parse(args[4]) : 3f, args.Length > 5 ? float.Parse(args[5]) : 120f)
                        : designed ? RoadInterchangeLayout.Roads()
                        : corridor ? RoadCorridorLayout.Roads(new CorridorSettings())
                        : cloverstack ? RoadCloverstackLayout.Roads()
                        : mergesplit ? RoadMergeSplitLayout.Roads()
                        : plan ? WorldPlanRoads(Environment.GetEnvironmentVariable("PLAN") ?? Path.Combine(args[2], "RoadData/world_plan.json"), report)
                        : world ? RoadWorldLayout.Roads(RoadWorldData.Load(Path.Combine(args[2], RoadWorldData.DefaultFile)), new WorldSettings(), report)
                        : RoadPocLayout.Roads();
        foreach (var line in report) Console.WriteLine("  layout: " + line);
        // OFFSET="dx,dz,degrees": the whole layout moved and turned, to see what distance from the origin does.
        var moved = Environment.GetEnvironmentVariable("OFFSET");
        if (moved != null)
        {
            var o = moved.Split(',').Select(float.Parse).ToArray();
            float a = o[2] * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            definitions = definitions.Select(d =>
            {
                var r = d.Recipe; var st = r.start;
                r.start = new Vector2(o[0] + st.x * c - st.y * sn, o[1] + st.x * sn + st.y * c);
                r.heading += o[2];
                d.Knots = r.Knots();
                return d;
            }).ToList();
        }
        var paths = definitions.Select(r => Dense(r)).ToList();
        // PLANONLY=1: the mesh-free plan check alone (seconds, not minutes), for the street generator's repair loop.
        if (plan && Environment.GetEnvironmentVariable("PLANONLY") == "1")
        {
            var quick = RoadPlanCheck.Run(paths, out _);
            Console.WriteLine($"plan check: {quick.Count} problems");
            foreach (var i in quick) Console.WriteLine("  plan: " + i);
            return 0;
        }
        foreach (var p in paths.Take(interchange || world || plan ? 0 : paths.Count))
            Console.WriteLine($"{p.Name}: {p.Length:0} m, {p.Count} stations, bank {p.Bank.Min():0.0}..{p.Bank.Max():0.0}, y {p.Centre.Min(c => c.y):0.00}..{p.Centre.Max(c => c.y):0.00}");
        if (args.Length > 1 && Environment.GetEnvironmentVariable("DENSITY") == "1") DensityEstimate(paths);
        var dbg = Environment.GetEnvironmentVariable("PROFILE");
        if (dbg != null) foreach (var p in paths.Where(p => p.Name == dbg))
            for (int i = 0; i < p.Count; i += 4) Console.WriteLine($"PROF {i * p.Spacing:0} y {p.Centre[i].y:0.000} lift {p.Point(i, 0f).y - p.Centre[i].y:0.000} bank {p.Bank[i]:0.00} k {p.Curvature[i] * 1000:0.00}");
        var surface = RoadSurfaceBuilder.Build(paths, 4f, corridor || world || cloverstack ? new Earthworks { enabled = true } : null);
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
        WriteObj(Path.ChangeExtension(args.Length > 0 ? args[0] : "surface.obj", null) + "-ground.obj", surface.GroundVertices, surface.GroundTriangles);
        using (var csv = new StreamWriter(Path.ChangeExtension(args.Length > 0 ? args[0] : "surface.obj", null) + "-centrelines.csv"))
            foreach (var p in paths)
                for (int i = 0; i < p.Count; i += 5)
                    csv.WriteLine($"{p.Name},{p.Centre[i].x:0.0},{p.Centre[i].y:0.00},{p.Centre[i].z:0.0},{p.HalfWidth}");
        var tiles = RoadTiles.Split(surface.Vertices, surface.Triangles, surface.Uvs, null, 1000f);
        var colliderTiles = RoadTiles.Split(surface.Vertices, surface.Triangles, surface.Uvs, null, 1000f, 10f);
        Console.WriteLine($"tiles: {tiles.Count} render (largest {tiles.Values.Max(t => t.Triangles.Count) / 3} tris), {colliderTiles.Count} collider (largest {colliderTiles.Values.Max(t => t.Triangles.Count) / 3} tris, {colliderTiles.Values.Sum(t => t.Triangles.Count) / 3 - surface.Triangles.Count / 3} overlap tris)");
        // Probe the collider tiles, as Unity's checker does: overlap and tile edges included.
        var tiledVertices = new List<Vector3>(); var tiledTriangles = new List<int>();
        foreach (var tile in colliderTiles.Values)
        {
            int offset = tiledVertices.Count;
            tiledVertices.AddRange(tile.Vertices);
            foreach (int index in tile.Triangles) tiledTriangles.Add(index + offset);
        }
        var probe = new SurfaceGrid(tiledVertices, tiledTriangles);
        var pr = Environment.GetEnvironmentVariable("PROBE");
        if (pr != null)
        {
            // name:lateral:from:to[:step]; the name may itself contain colons, so the numbers are read from the end.
            var f = pr.Split(':'); int nums = f.Length >= 5 && float.TryParse(f[f.Length - 4], out _) ? 4 : 3;
            var rp = paths.First(q => q.Name == string.Join(":", f.Take(f.Length - nums))); var n = f.Skip(f.Length - nums).Select(float.Parse).ToArray();
            float off = n[0];
            for (float st = n[1]; st <= n[2]; st += nums == 4 ? n[3] : 0.5f)
            {
                Vector3 d = rp.PointAt(st, off);
                probe.Raycast(d + Vector3.up * 2f, Vector3.down, 4f, out Vector3 hp, out _);
                Console.WriteLine($"PRB {st:0.0} design {d.y:0.0000} mesh {hp.y:0.0000} x {hp.x:0.0000} z {hp.z:0.0000}");
            }
        }
        if (plan)
        {
            var planIssues = RoadPlanCheck.Run(paths, out _);
            Console.WriteLine($"plan check: {planIssues.Count} problems");
            foreach (var i in planIssues) Console.WriteLine("  plan: " + i);
        }
        if (Environment.GetEnvironmentVariable("BARRIERS") == "1") BarrierAudit(surface, paths);
        var result = RoadChecker.Run(paths, surface.Zones, probe, new RoadCheckSettings());
        Console.WriteLine($"checker: {(result.Passed ? "PASSED" : result.Issues.Count + " breaches")} over {result.Samples} samples, {(DateTime.Now - t0).TotalSeconds:0.0}s");
        var byName = paths.ToDictionary(p => p.Name);
        int inZone = result.Issues.Count(i => byName.TryGetValue(i.Road, out var rp) && surface.Zones.Any(z => z.Contains(rp, i.Station)));
        int nearZone = result.Issues.Count(i => byName.TryGetValue(i.Road, out var rp) && surface.Zones.Any(z => z.Parts.Any(pt => pt.Path == rp && i.Station >= (pt.From - 60) * rp.Spacing && i.Station <= (pt.To + 60) * rp.Spacing)));
        Console.WriteLine($"  breaches inside junction zones: {inZone}, within 60 m of one: {nearZone}, elsewhere: {result.Issues.Count - nearZone}");
        foreach (var kv in result.Summary().OrderBy(k => k.Key)) Console.WriteLine($"  {kv.Key}: {kv.Value.count} places, worst {kv.Value.worst:0.###}");
        foreach (var a in result.Air) Console.WriteLine("  air: " + a);
        foreach (var i in result.Issues.Take(Environment.GetEnvironmentVariable("ALL") == "1" ? int.MaxValue : 80)) Console.WriteLine("  " + i + $" @ ({i.Position.x:0},{i.Position.y:0.0},{i.Position.z:0})");
        return 0;
    }

    // How many triangles a road would need if stations were kept only where linear interpolation between them
    // misses the surface by more than a tolerance, and the ribbon were 1 m across only where the bank changes.
    static void DensityEstimate(List<RoadPath> paths)
    {
        foreach (float tolerance in new[] { 0.002f, 0.005f, 0.01f })
        {
            long now = 0, adaptive = 0, stationsNow = 0, stationsKept = 0;
            foreach (var p in paths)
            {
                int across = Math.Max(1, (int)Math.Ceiling(p.Settings.Width / 1f));
                now += 2L * across * (p.Count - 1);
                stationsNow += p.Count;
                var lateral = new[] { -p.HalfWidth, -p.HalfWidth / 2, 0f, p.HalfWidth / 2, p.HalfWidth };
                int a = 0;
                while (a < p.Count - 1)
                {
                    int b = a + 1;
                    while (b + 1 < p.Count && b + 1 - a <= 50 && Fits(p, a, b + 1, lateral, tolerance)) b++;
                    bool twists = Math.Abs(p.Bank[b] - p.Bank[a]) > 0.05f;
                    adaptive += 2L * (twists ? across * (b - a) : 1);
                    stationsKept++;
                    a = b;
                }
            }
            Console.WriteLine($"density @ {tolerance * 1000:0} mm: stations {stationsNow:N0} -> {stationsKept:N0}, road tris {now:N0} -> {adaptive:N0} ({(double)now / adaptive:0.0}x fewer)");
        }
    }

    static bool Fits(RoadPath p, int a, int b, float[] lateral, float tolerance)
    {
        foreach (float l in lateral)
        {
            Vector3 pa = p.Point(a, l), pb = p.Point(b, l);
            for (int i = a + 1; i < b; i++)
                if (Vector3.Distance(p.Point(i, l), Vector3.Lerp(pa, pb, (i - a) / (float)(b - a))) > tolerance) return false;
        }
        return true;
    }

    static void WriteObj(string path, List<Vector3> vertices, List<int> triangles)
    {
        using var obj = new StreamWriter(path);
        foreach (var v in vertices) obj.WriteLine($"v {v.x} {v.y} {v.z}");
        for (int i = 0; i < triangles.Count; i += 3) obj.WriteLine($"f {triangles[i] + 1} {triangles[i + 1] + 1} {triangles[i + 2] + 1}");
    }

    // Free straight stretches on each freeway once the plan's interchanges have claimed theirs: where a diamond or an
    // overpass could still go. Prints "FREE road from to (x,z) (x,z) length".
    static void PrintFree(string file)
    {
        var data = System.Text.Json.JsonSerializer.Deserialize<PlanFile>(File.ReadAllText(file), new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
        var routes = data.roads.Where(r => r.points.Count >= 2).ToDictionary(r => r.name, r => new WorldRoute(r));
        var features = new List<RoadPocLayout.RoadDefinition>();
        foreach (var site in data.sites) WorldInterchanges.Place(site, routes, features, null);
        foreach (var route in routes.Values.Where(r => data.roads.First(w => w.name == r.Name).kind == WorldRoadKind.Freeway))
        {
            float at = 0f;
            foreach (var c in route.Claimed.OrderBy(c => c.from).Append((site: "end", from: route.Length, to: route.Length)))
            {
                if (c.from - at > 1f)
                {
                    Vector2 a = route.PointAt(at), b = route.PointAt(c.from);
                    Console.WriteLine($"FREE {route.Name}|{at:0}|{c.from:0}|{a.x:0}|{a.y:0}|{b.x:0}|{b.y:0}|{c.from - at:0}");
                }
                at = Mathf.Max(at, c.to);
            }
        }
    }

    sealed class PlanFile { public List<WorldRoad> roads = new List<WorldRoad>(); public List<WorldSite> sites = new List<WorldSite>(); }

    // The World Plan's own data file, read the way the importer reads it into the World Plan asset.
    static List<RoadPocLayout.RoadDefinition> WorldPlanRoads(string file, List<string> report)
    {
        var data = System.Text.Json.JsonSerializer.Deserialize<PlanFile>(File.ReadAllText(file), new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
        return WorldPlanLayout.Roads(data.roads, data.sites, report);
    }

    // Barrier pieces that look wrong: closed rings (holes in a junction's surface), very short stubs, and barriers
    // standing on another road's driving surface at its own level.
    static void BarrierAudit(RoadSurface surface, List<RoadPath> paths)
    {
        int rings = 0, stubs = 0, onRoad = 0;
        var notes = new List<string>();
        foreach (var line in surface.BarrierLines)
        {
            var e = line.Edge;
            float length = 0f; for (int i = 1; i < e.Count; i++) length += Vector3.Distance(e[i - 1], e[i]);
            if (length < 0.05f) continue;
            bool closed = e.Count > 2 && Vector3.Distance(e[0], e[e.Count - 1]) < 0.5f;
            Vector3 mid = e[e.Count / 2];
            string over = null;
            foreach (var p in paths)
                for (int i = 0; i < p.Count; i += 2)
                {
                    Vector3 d = mid - p.Centre[i];
                    if (Mathf.Abs(d.y) > 1f) continue;
                    float along = Vector3.Dot(RoadPath.Flat(d), p.Forward[i]), across = Mathf.Abs(Vector3.Dot(RoadPath.Flat(d), p.Right[i]));
                    if (Mathf.Abs(along) < 1.5f && across < p.HalfWidth - p.Settings.shoulder - 0.5f) { over = p.Name; break; }
                }
            string kind = closed ? "ring" : length < 15f ? "stub" : over != null ? "on road" : null;
            if (kind == null) continue;
            if (closed) rings++; else if (length < 15f) stubs++; else onRoad++;
            notes.Add($"  barrier {kind}: {length:0.0} m at ({mid.x:0},{mid.y:0.0},{mid.z:0}){(over != null ? " over " + over : "")}");
        }
        Console.WriteLine($"barriers: {surface.BarrierLines.Count} lines; {rings} closed rings, {stubs} stubs under 15 m, {onRoad} longer pieces standing on a road");
        foreach (var n in notes.Take(Environment.GetEnvironmentVariable("ALL") == "1" ? 1000 : 40)) Console.WriteLine(n);
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
