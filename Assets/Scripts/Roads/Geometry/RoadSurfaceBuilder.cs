using System;
using System.Collections.Generic;
using Clipper2Lib;
using UnityEngine;
using P2T = Poly2Tri;

namespace MotoSquid.Roads
{
    // Where roads at the same height share ground: every road's station range inside it.
    public sealed class JunctionZone
    {
        public struct Part
        {
            public RoadPath Path;
            public int From;
            public int To;
        }

        public readonly List<Part> Parts = new List<Part>();

        public bool Contains(RoadPath path, float station)
        {
            foreach (var part in Parts)
                if (part.Path == path && station >= part.From * path.Spacing && station <= part.To * path.Spacing)
                    return true;
            return false;
        }
    }

    public sealed class RoadSurface
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<Vector3> BarrierVertices = new List<Vector3>();
        public readonly List<int> BarrierTriangles = new List<int>();
        public readonly List<Vector2> BarrierUvs = new List<Vector2>();
        public readonly List<JunctionZone> Zones = new List<JunctionZone>();
        public readonly List<string> Conflicts = new List<string>();
        public readonly List<string> Warnings = new List<string>();
    }

    // Roads become ribbons. Where roads join (one ends on another, or two cross at the same height), their
    // overlapping outlines are merged into one polygon and triangulated as a single surface: a merge, split,
    // T, X or Y at any angle and any lane count is the same operation. Ribbons and junctions share their
    // cut-line corners exactly, so the whole network is one welded mesh. Roads that overlap without joining
    // are layout conflicts: they are reported, never fused.
    public static class RoadSurfaceBuilder
    {
        const float LevelTolerance = 1f;
        const float ZoneMargin = 8f;
        const float CrossStep = 1f;
        const float SteinerSpacing = 2.5f;
        const float SteinerClearance = 0.6f;
        const float HashCell = 20f;
        const int ClusterGap = 3;
        const float CrossingAngle = 25f;
        const int EndReach = 2;
        const double Mm = 1000.0;

        // Finds the junction zones and flattens bank into them. Must run before building or checking.
        public static List<JunctionZone> Prepare(IReadOnlyList<RoadPath> paths) => Prepare(paths, new List<string>());

        public static List<JunctionZone> Prepare(IReadOnlyList<RoadPath> paths, List<string> conflicts)
        {
            var zones = FindZones(paths, conflicts);
            FlattenBankAtZones(paths, zones);
            return zones;
        }

        public static RoadSurface Build(IReadOnlyList<RoadPath> paths, float uvTileSize)
        {
            var surface = new RoadSurface();
            surface.Zones.AddRange(Prepare(paths, surface.Conflicts));
            var mesh = new Welder(surface, uvTileSize);
            var barriers = new Barriers(surface);
            var corners = new Dictionary<(long, long), float>();

            foreach (var path in paths)
                foreach (var (from, to) in OutsideZones(path, surface.Zones))
                    AddRibbon(mesh, barriers, path, from, to, corners);

            for (int z = 0; z < surface.Zones.Count; z++)
            {
                try { AddJunction(mesh, barriers, surface.Zones[z], corners); }
                catch (Exception e) { surface.Warnings.Add($"Junction {z + 1} ({Describe(surface.Zones[z])}) failed: {e.Message}"); }
            }
            return surface;
        }

        public static string Describe(JunctionZone zone)
        {
            var names = new List<string>();
            foreach (var part in zone.Parts) names.Add($"{part.Path.Name} {part.From * part.Path.Spacing:0}-{part.To * part.Path.Spacing:0} m");
            return string.Join(", ", names);
        }

        static List<JunctionZone> FindZones(IReadOnlyList<RoadPath> paths, List<string> conflicts)
        {
            var cells = new Dictionary<(int, int), List<(int path, int station)>>();
            float widest = 0f;
            for (int p = 0; p < paths.Count; p++)
            {
                widest = Mathf.Max(widest, paths[p].HalfWidth);
                for (int i = 0; i < paths[p].Count; i++)
                {
                    var key = Cell(paths[p].Centre[i]);
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<(int, int)>();
                    list.Add((p, i));
                }
            }

            var contacts = new Dictionary<(int, int), List<(int ia, int ib)>>();
            for (int pa = 0; pa < paths.Count; pa++)
            {
                var a = paths[pa];
                int reach = Mathf.CeilToInt((a.HalfWidth + widest) / HashCell);
                for (int ia = 0; ia < a.Count; ia++)
                {
                    var (cx, cz) = Cell(a.Centre[ia]);
                    for (int dx = -reach; dx <= reach; dx++)
                    for (int dz = -reach; dz <= reach; dz++)
                    {
                        if (!cells.TryGetValue((cx + dx, cz + dz), out var list)) continue;
                        foreach (var (pb, ib) in list)
                        {
                            if (pb <= pa) continue;
                            var b = paths[pb];
                            Vector3 d = b.Centre[ib] - a.Centre[ia];
                            if (Mathf.Abs(d.y) >= LevelTolerance) continue;
                            if (RoadPath.Flat(d).magnitude >= a.HalfWidth + b.HalfWidth) continue;
                            if (!contacts.TryGetValue((pa, pb), out var found)) contacts[(pa, pb)] = found = new List<(int, int)>();
                            found.Add((ia, ib));
                        }
                    }
                }
            }

            var zones = new List<Dictionary<int, (int from, int to)>>();
            foreach (var kv in contacts)
                foreach (var (fromA, toA, fromB, toB, joined) in Overlaps(paths[kv.Key.Item1], paths[kv.Key.Item2], kv.Value))
                {
                    var a = paths[kv.Key.Item1];
                    var b = paths[kv.Key.Item2];
                    if (!joined)
                    {
                        conflicts.Add($"{a.Name} {fromA * a.Spacing:0}-{toA * a.Spacing:0} m overlaps {b.Name} " +
                                      $"{fromB * b.Spacing:0}-{toB * b.Spacing:0} m without joining it");
                        continue;
                    }
                    zones.Add(new Dictionary<int, (int, int)>
                    {
                        [kv.Key.Item1] = WithMargin(a, fromA, toA),
                        [kv.Key.Item2] = WithMargin(b, fromB, toB),
                    });
                }

            for (bool merged = true; merged;)
            {
                merged = false;
                for (int x = 0; x < zones.Count && !merged; x++)
                for (int y = x + 1; y < zones.Count && !merged; y++)
                {
                    foreach (var kv in zones[y])
                        if (zones[x].TryGetValue(kv.Key, out var r) && r.from <= kv.Value.to && kv.Value.from <= r.to)
                            merged = true;
                    if (!merged) continue;
                    foreach (var kv in zones[y])
                        zones[x][kv.Key] = zones[x].TryGetValue(kv.Key, out var r)
                            ? (Mathf.Min(r.from, kv.Value.from), Mathf.Max(r.to, kv.Value.to)) : kv.Value;
                    zones.RemoveAt(y);
                }
            }

            var result = new List<JunctionZone>();
            foreach (var z in zones)
            {
                var zone = new JunctionZone();
                foreach (var kv in z)
                    zone.Parts.Add(new JunctionZone.Part { Path = paths[kv.Key], From = kv.Value.from, To = kv.Value.to });
                result.Add(zone);
            }
            return result;
        }

        // Each separate stretch where two roads overlap at the same height, and whether it is a real junction:
        // one road ends inside it, or the two centrelines cross there at an angle.
        static IEnumerable<(int fromA, int toA, int fromB, int toB, bool joined)> Overlaps(RoadPath a, RoadPath b, List<(int ia, int ib)> found)
        {
            // Contacts within a few stations of each other on both roads belong to the same overlap.
            var parent = new int[found.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            var grid = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < found.Count; i++)
            {
                var key = (found[i].ia / ClusterGap, found[i].ib / ClusterGap);
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                list.Add(i);
            }
            for (int i = 0; i < found.Count; i++)
            {
                var (ca, cb) = (found[i].ia / ClusterGap, found[i].ib / ClusterGap);
                for (int da = -1; da <= 1; da++)
                for (int db = -1; db <= 1; db++)
                    if (grid.TryGetValue((ca + da, cb + db), out var near))
                        foreach (int j in near)
                            if (Mathf.Abs(found[i].ia - found[j].ia) <= ClusterGap && Mathf.Abs(found[i].ib - found[j].ib) <= ClusterGap)
                                Union(parent, i, j);
            }
            var byRoot = new Dictionary<int, List<(int ia, int ib)>>();
            for (int i = 0; i < found.Count; i++)
            {
                int root = Find(parent, i);
                if (!byRoot.TryGetValue(root, out var g)) byRoot[root] = g = new List<(int, int)>();
                g.Add(found[i]);
            }
            var groups = byRoot.Values;

            foreach (var g in groups)
            {
                int fromA = int.MaxValue, toA = int.MinValue, fromB = int.MaxValue, toB = int.MinValue;
                bool joined = false;
                foreach (var (ia, ib) in g)
                {
                    fromA = Mathf.Min(fromA, ia); toA = Mathf.Max(toA, ia);
                    fromB = Mathf.Min(fromB, ib); toB = Mathf.Max(toB, ib);
                    joined |= IsEnd(a, ia) || IsEnd(b, ib) || Crosses(a, ia, b, ib);
                }
                yield return (fromA, toA, fromB, toB, joined);
            }
        }

        static bool IsEnd(RoadPath path, int station) =>
            !path.Closed && (station <= EndReach || station >= path.Count - 1 - EndReach);

        static bool Crosses(RoadPath a, int ia, RoadPath b, int ib)
        {
            float gap = RoadPath.Flat(b.Centre[ib] - a.Centre[ia]).magnitude;
            float angle = Vector3.Angle(a.Forward[ia], b.Forward[ib]);
            return gap < Mathf.Max(a.Spacing, b.Spacing) && angle > CrossingAngle && angle < 180f - CrossingAngle;
        }

        static int Find(int[] parent, int x)
        {
            while (parent[x] != x) x = parent[x] = parent[parent[x]];
            return x;
        }

        static void Union(int[] parent, int a, int b) => parent[Find(parent, a)] = Find(parent, b);

        static (int, int) WithMargin(RoadPath path, int from, int to)
        {
            int margin = Mathf.CeilToInt(ZoneMargin / path.Spacing);
            return (Mathf.Max(0, from - margin), Mathf.Min(path.Count - 1, to + margin));
        }

        static void FlattenBankAtZones(IReadOnlyList<RoadPath> paths, List<JunctionZone> zones)
        {
            foreach (var path in paths)
            {
                var distance = new float[path.Count];
                for (int i = 0; i < path.Count; i++) distance[i] = float.MaxValue;
                foreach (var zone in zones)
                    foreach (var part in zone.Parts)
                    {
                        if (part.Path != path) continue;
                        for (int i = 0; i < path.Count; i++)
                        {
                            int outside = i < part.From ? part.From - i : i > part.To ? i - part.To : 0;
                            distance[i] = Mathf.Min(distance[i], outside * path.Spacing);
                        }
                    }
                path.FlattenTowards(distance);
            }
        }

        static IEnumerable<(int from, int to)> OutsideZones(RoadPath path, List<JunctionZone> zones)
        {
            var covered = new List<(int from, int to)>();
            foreach (var zone in zones)
                foreach (var part in zone.Parts)
                    if (part.Path == path) covered.Add((part.From, part.To));
            covered.Sort((a, b) => a.from.CompareTo(b.from));

            int at = 0;
            foreach (var (from, to) in covered)
            {
                if (from > at) yield return (at, from);
                at = Mathf.Max(at, to);
            }
            if (at < path.Count - 1) yield return (at, path.Count - 1);
        }

        // Vertices every metre or so across the road as well as along it: a road twisting into its bank
        // is not flat across, and two vertices per section would fold each quad along its diagonal.
        static float[] CrossOffsets(RoadPath path)
        {
            int segments = Mathf.Max(1, Mathf.CeilToInt(path.Settings.Width / CrossStep));
            var offsets = new float[segments + 1];
            for (int j = 0; j <= segments; j++) offsets[j] = -path.HalfWidth + path.Settings.Width * j / segments;
            return offsets;
        }

        static void AddRibbon(Welder mesh, Barriers barriers, RoadPath path, int from, int to, Dictionary<(long, long), float> corners)
        {
            bool cutStart = from > 0, cutEnd = to < path.Count - 1;
            var offsets = CrossOffsets(path);
            int[] previous = null, current = new int[offsets.Length];
            var leftEdge = new List<Vector3>();
            var rightEdge = new List<Vector3>();
            var leftOut = new List<Vector3>();
            var rightOut = new List<Vector3>();
            for (int i = from; i <= to; i++)
            {
                bool cut = (i == from && cutStart) || (i == to && cutEnd);
                for (int j = 0; j < offsets.Length; j++)
                {
                    Vector3 v = path.Point(i, offsets[j]);
                    if (cut) v = Corner(v, corners);
                    current[j] = mesh.Vertex(v);
                    if (j == 0) { leftEdge.Add(v); leftOut.Add(-path.Right[i]); }
                    if (j == offsets.Length - 1) { rightEdge.Add(v); rightOut.Add(path.Right[i]); }
                }
                if (previous != null)
                    for (int j = 0; j < offsets.Length - 1; j++)
                    {
                        mesh.Triangle(previous[j], current[j], previous[j + 1]);
                        mesh.Triangle(previous[j + 1], current[j], current[j + 1]);
                    }
                previous = (int[])current.Clone();
            }
            barriers.Add(leftEdge, leftOut, path.Settings.barrierHeight, path.Settings.barrierThickness);
            barriers.Add(rightEdge, rightOut, path.Settings.barrierHeight, path.Settings.barrierThickness);
        }

        // A ribbon end the junction polygon will share: snapped to the millimetre grid the polygon uses.
        static Vector3 Corner(Vector3 v, Dictionary<(long, long), float> corners)
        {
            long x = Snap(v.x), z = Snap(v.z);
            corners[(x, z)] = v.y;
            return new Vector3((float)(x / Mm), v.y, (float)(z / Mm));
        }

        static void AddJunction(Welder mesh, Barriers barriers, JunctionZone zone, Dictionary<(long, long), float> corners)
        {
            var outlines = new Paths64();
            foreach (var part in zone.Parts)
            {
                var path = part.Path;
                var offsets = CrossOffsets(path);
                int last = offsets.Length - 1;
                var outline = new Path64();
                for (int i = part.From; i <= part.To; i++) outline.Add(ToPoint(path.Point(i, offsets[0])));
                for (int j = 1; j < last; j++) outline.Add(ToPoint(path.Point(part.To, offsets[j])));
                for (int i = part.To; i >= part.From; i--) outline.Add(ToPoint(path.Point(i, offsets[last])));
                for (int j = last - 1; j > 0; j--) outline.Add(ToPoint(path.Point(part.From, offsets[j])));
                if (!Clipper.IsPositive(outline)) outline.Reverse();
                outlines.Add(outline);
            }

            var clipper = new Clipper64 { PreserveCollinear = true };
            clipper.AddSubject(outlines);
            var tree = new PolyTree64();
            clipper.Execute(ClipType.Union, FillRule.NonZero, tree);
            for (int i = 0; i < tree.Count; i++)
                Triangulate(mesh, zone, tree.Child(i), corners);

            float height = 0f, thickness = 0f;
            foreach (var part in zone.Parts)
            {
                height = Mathf.Max(height, part.Path.Settings.barrierHeight);
                thickness = Mathf.Max(thickness, part.Path.Settings.barrierThickness);
            }
            AddJunctionBarriers(barriers, zone, tree, corners, height, thickness);
        }

        // Walls round the outside of the merged surface. Edges between two cut-line points are where a road
        // carries on out of the junction, so they stay open.
        static void AddJunctionBarriers(Barriers barriers, JunctionZone zone, PolyPath64 node, Dictionary<(long, long), float> corners,
                                        float height, float thickness)
        {
            for (int c = 0; c < node.Count; c++)
            {
                var child = node.Child(c);
                var polygon = child.Polygon;
                int n = polygon.Count;
                bool Open(int i) => corners.ContainsKey((polygon[i].X, polygon[i].Y)) && corners.ContainsKey((polygon[(i + 1) % n].X, polygon[(i + 1) % n].Y));
                int start = 0;
                while (start < n && !Open(start)) start++;
                bool ring = start == n;
                if (ring) start = 0;

                var edge = new List<Vector3>();
                var outward = new List<Vector3>();
                for (int k = 0; k <= n; k++)
                {
                    int i = (start + k + (ring ? 0 : 1)) % n;
                    bool open = !ring && Open(i);
                    edge.Add(BoundaryPoint(zone, polygon[i], corners));
                    outward.Add(Outward(polygon, i));
                    if (open || k == n)
                    {
                        barriers.Add(edge, outward, height, thickness);
                        edge = new List<Vector3>();
                        outward = new List<Vector3>();
                    }
                }
                AddJunctionBarriers(barriers, zone, child, corners, height, thickness);
            }
        }

        static Vector3 BoundaryPoint(JunctionZone zone, Point64 p, Dictionary<(long, long), float> corners)
        {
            float x = (float)(p.X / Mm), z = (float)(p.Y / Mm);
            return new Vector3(x, corners.TryGetValue((p.X, p.Y), out float y) ? y : BlendedHeight(zone, x, z), z);
        }

        // Outer boundaries run anticlockwise and holes clockwise, so the road is always on the left.
        static Vector3 Outward(Path64 polygon, int i)
        {
            int n = polygon.Count;
            Point64 previous = polygon[(i - 1 + n) % n], here = polygon[i], next = polygon[(i + 1) % n];
            var a = new Vector3(here.X - previous.X, 0f, here.Y - previous.Y).normalized;
            var b = new Vector3(next.X - here.X, 0f, next.Y - here.Y).normalized;
            var right = new Vector3(a.z + b.z, 0f, -(a.x + b.x));
            return right.sqrMagnitude > 1e-8f ? right.normalized : new Vector3(b.z, 0f, -b.x);
        }

        static void Triangulate(Welder mesh, JunctionZone zone, PolyPath64 outer, Dictionary<(long, long), float> corners)
        {
            var boundaries = new List<Path64> { outer.Polygon };
            var shape = new P2T.Shape(ToTriPoints(outer.Polygon));
            for (int h = 0; h < outer.Count; h++)
            {
                var hole = outer.Child(h).Polygon;
                boundaries.Add(hole);
                shape.Holes.Add(new P2T.Shape(ToTriPoints(hole)));
            }
            AddSteinerPoints(shape, boundaries);

            var triangles = new List<P2T.Triangle>();
            shape.Triangulate(triangles);

            var index = new Dictionary<P2T.TriPoint, int>(new ReferenceComparer());
            foreach (var t in triangles)
            {
                int a = VertexFor(t.Points[0]), b = VertexFor(t.Points[1]), c = VertexFor(t.Points[2]);
                mesh.Triangle(a, b, c);
            }

            for (int h = 0; h < outer.Count; h++)
                for (int island = 0; island < outer.Child(h).Count; island++)
                    Triangulate(mesh, zone, outer.Child(h).Child(island), corners);

            int VertexFor(P2T.TriPoint p)
            {
                if (index.TryGetValue(p, out int i)) return i;
                long x = (long)Math.Round(p.X * Mm), z = (long)Math.Round(p.Y * Mm);
                float wx = (float)(x / Mm), wz = (float)(z / Mm);
                float y = corners.TryGetValue((x, z), out float exact) ? exact : BlendedHeight(zone, wx, wz);
                return index[p] = mesh.Vertex(new Vector3(wx, y, wz));
            }
        }

        static void AddSteinerPoints(P2T.Shape shape, List<Path64> boundaries)
        {
            long minX = long.MaxValue, minZ = long.MaxValue, maxX = long.MinValue, maxZ = long.MinValue;
            foreach (var pt in boundaries[0])
            {
                minX = Math.Min(minX, pt.X); maxX = Math.Max(maxX, pt.X);
                minZ = Math.Min(minZ, pt.Y); maxZ = Math.Max(maxZ, pt.Y);
            }
            long step = (long)(SteinerSpacing * Mm), clearance = (long)(SteinerClearance * Mm);
            for (long x = minX + step / 2; x < maxX; x += step)
            for (long z = minZ + step / 2; z < maxZ; z += step)
            {
                var p = new Point64(x, z);
                if (Clipper.PointInPolygon(p, boundaries[0]) != PointInPolygonResult.IsInside) continue;
                bool inHole = false;
                for (int h = 1; h < boundaries.Count && !inHole; h++)
                    inHole = Clipper.PointInPolygon(p, boundaries[h]) != PointInPolygonResult.IsOutside;
                if (inHole || NearAnyEdge(p, boundaries, clearance)) continue;
                shape.SteinerPoints.Add(new P2T.TriPoint(x / Mm, z / Mm));
            }
        }

        static bool NearAnyEdge(Point64 p, List<Path64> boundaries, long clearance)
        {
            double limit = (double)clearance * clearance;
            foreach (var path in boundaries)
                for (int i = 0, j = path.Count - 1; i < path.Count; j = i++)
                {
                    if (Math.Min(path[i].X, path[j].X) - clearance > p.X || Math.Max(path[i].X, path[j].X) + clearance < p.X) continue;
                    if (Math.Min(path[i].Y, path[j].Y) - clearance > p.Y || Math.Max(path[i].Y, path[j].Y) + clearance < p.Y) continue;
                    if (SquaredDistance(p, path[j], path[i]) < limit) return true;
                }
            return false;
        }

        static double SquaredDistance(Point64 p, Point64 a, Point64 b)
        {
            double abx = b.X - a.X, aby = b.Y - a.Y, apx = p.X - a.X, apy = p.Y - a.Y;
            double lengthSq = abx * abx + aby * aby;
            double t = lengthSq > 0 ? Math.Max(0, Math.Min(1, (apx * abx + apy * aby) / lengthSq)) : 0;
            double dx = apx - abx * t, dy = apy - aby * t;
            return dx * dx + dy * dy;
        }

        // Height of a junction point: the roads it lies on, weighted by how far inside each one it is.
        static float BlendedHeight(JunctionZone zone, float x, float z)
        {
            double sum = 0, weights = 0;
            var p = new Vector3(x, 0f, z);
            foreach (var part in zone.Parts)
            {
                var path = part.Path;
                int search = Mathf.CeilToInt(20f / path.Spacing);
                int from = Mathf.Max(0, part.From - search), to = Mathf.Min(path.Count - 1, part.To + search);
                int nearest = from;
                float best = float.MaxValue;
                for (int i = from; i <= to; i++)
                {
                    float d = RoadPath.Flat(path.Centre[i] - p).sqrMagnitude;
                    if (d < best) { best = d; nearest = i; }
                }
                Vector3 offset = RoadPath.Flat(p - path.Centre[nearest]);
                float station = nearest * path.Spacing + Vector3.Dot(offset, path.Forward[nearest]);
                float lateral = Vector3.Dot(offset, path.Right[nearest]);
                float height = path.PointAt(station, lateral).y;
                float outside = Mathf.Abs(lateral) - path.HalfWidth;
                double weight = outside <= 0f ? 1.0 - outside : 1.0 / (1.0 + outside * outside);
                sum += height * weight;
                weights += weight;
            }
            return (float)(sum / weights);
        }

        static List<P2T.TriPoint> ToTriPoints(Path64 path)
        {
            var points = new List<P2T.TriPoint>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                var a = path[i];
                var b = path[(i + 1) % path.Count];
                if (a.X == b.X && a.Y == b.Y) continue;
                points.Add(new P2T.TriPoint(a.X / Mm, a.Y / Mm));
            }
            return points;
        }

        static Point64 ToPoint(Vector3 v) => new Point64(Snap(v.x), Snap(v.z));
        static long Snap(float value) => (long)Math.Round(value * Mm);
        static (int, int) Cell(Vector3 v) => (Mathf.FloorToInt(v.x / HashCell), Mathf.FloorToInt(v.z / HashCell));

        sealed class ReferenceComparer : IEqualityComparer<P2T.TriPoint>
        {
            public bool Equals(P2T.TriPoint a, P2T.TriPoint b) => ReferenceEquals(a, b);
            public int GetHashCode(P2T.TriPoint p) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p);
        }

        // Concrete walls on the road edges: an inner face, a top and an outer face, each flat-shaded.
        sealed class Barriers
        {
            readonly RoadSurface surface;

            public Barriers(RoadSurface surface) => this.surface = surface;

            public void Add(List<Vector3> edge, List<Vector3> outward, float height, float thickness)
            {
                if (height <= 0f || edge.Count < 2) return;
                var up = Vector3.up * height;
                var faces = new (Func<int, Vector3> a, Func<int, Vector3> b, Func<int, Vector3> normal)[]
                {
                    (i => edge[i], i => edge[i] + up, i => -outward[i]),
                    (i => edge[i] + up, i => edge[i] + up + outward[i] * thickness, i => Vector3.up),
                    (i => edge[i] + up + outward[i] * thickness, i => edge[i] + outward[i] * thickness, i => outward[i]),
                };
                foreach (var (a, b, normal) in faces)
                {
                    int first = surface.BarrierVertices.Count;
                    float along = 0f;
                    for (int i = 0; i < edge.Count; i++)
                    {
                        if (i > 0) along += Vector3.Distance(edge[i], edge[i - 1]);
                        surface.BarrierVertices.Add(a(i));
                        surface.BarrierVertices.Add(b(i));
                        surface.BarrierUvs.Add(new Vector2(along / 4f, 0f));
                        surface.BarrierUvs.Add(new Vector2(along / 4f, Vector3.Distance(a(i), b(i)) / 4f));
                    }
                    for (int i = 0; i < edge.Count - 1; i++)
                    {
                        int p = first + 2 * i, q = p + 2;
                        Triangle(p, p + 1, q, normal(i));
                        Triangle(q, p + 1, q + 1, normal(i));
                    }
                }
            }

            void Triangle(int a, int b, int c, Vector3 facing)
            {
                var v = surface.BarrierVertices;
                if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), facing) < 0f) (b, c) = (c, b);
                surface.BarrierTriangles.Add(a);
                surface.BarrierTriangles.Add(b);
                surface.BarrierTriangles.Add(c);
            }
        }

        // Shares vertices that land on the same millimetre, so ribbons and junctions weld into one mesh.
        sealed class Welder
        {
            readonly RoadSurface surface;
            readonly float uvTileSize;
            readonly Dictionary<(long, long, long), int> index = new Dictionary<(long, long, long), int>();

            public Welder(RoadSurface surface, float uvTileSize)
            {
                this.surface = surface;
                this.uvTileSize = uvTileSize;
            }

            public int Vertex(Vector3 v)
            {
                var key = (Snap(v.x), Snap(v.y), Snap(v.z));
                if (index.TryGetValue(key, out int i)) return i;
                surface.Vertices.Add(v);
                surface.Uvs.Add(new Vector2(v.x, v.z) / uvTileSize);
                return index[key] = surface.Vertices.Count - 1;
            }

            public void Triangle(int a, int b, int c)
            {
                if (a == b || b == c || a == c) return;
                var v = surface.Vertices;
                Vector3 normal = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (normal.sqrMagnitude < 1e-10f) return;
                if (normal.y < 0f) (b, c) = (c, b);
                surface.Triangles.Add(a);
                surface.Triangles.Add(b);
                surface.Triangles.Add(c);
            }
        }
    }
}
