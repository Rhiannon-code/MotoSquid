using System;
using System.Collections.Generic;
using Clipper2Lib;
using UnityEngine;
using P2T = Poly2Tri;

namespace MotoSquid.Roads
{
    [Serializable]
    public class Earthworks
    {
        [Tooltip("Cut sunken roads into a generated ground with retaining walls up to it, stand raised roads on walls down to it, and close bridges with decks and abutments.")]
        public bool enabled;
        public float groundHeight;
        [Tooltip("How far the generated ground reaches past the outermost road (m).")]
        public float groundMargin = 300f;
        public float deckThickness = 1.2f;
    }

    // A barrier along a road edge: points on the edge, each with the flat direction away from the road.
    public struct BarrierLine
    {
        public List<Vector3> Edge;
        public List<Vector3> Outward;
        public float Height;
        public float Thickness;
    }

    // Everything concrete and earth around the road surface. Without earthworks, barriers are walls of their
    // own height. With them, the barrier is the retaining wall: up to ground level (plus the barrier height
    // as a parapet) beside a sunken road, down to the ground under a raised one. Where another road passes
    // underneath, the wall stops at deck depth, a deck underside covers the span and abutments close the
    // embankment either side of it. The ground is generated with the cuttings left open.
    public static class RoadStructures
    {
        const float Below = 0.05f;
        const float ProbeStart = 0.5f;
        const float ProbeReach = 500f;
        const float AbutmentProbeStep = 2f;
        const float GroundUvTile = 10f;
        const double Mm = 1000.0;
        const double SimplifyMm = 20.0;

        public static void Build(RoadSurface surface, IReadOnlyList<RoadPath> paths, Earthworks earthworks)
        {
            var walls = new Walls(surface);
            if (earthworks == null || !earthworks.enabled)
            {
                foreach (var line in surface.BarrierLines) AddPlain(walls, line);
                return;
            }

            var grid = new SurfaceGrid(surface.Vertices, surface.Triangles);
            foreach (var line in surface.BarrierLines) AddRetaining(walls, line, grid, earthworks);
            foreach (var path in paths) AddAbutments(walls, path, grid, earthworks);
            AddDeckUndersides(walls, surface, grid, earthworks);
            try { AddGround(surface, paths, earthworks); }
            catch (Exception e) { surface.Warnings.Add($"Ground failed: {e.Message}"); }
        }

        static void AddPlain(Walls walls, BarrierLine line)
        {
            if (line.Height <= 0f || line.Edge.Count < 2) return;
            int n = line.Edge.Count;
            var bottom = new float[n];
            var top = new float[n];
            for (int i = 0; i < n; i++)
            {
                bottom[i] = line.Edge[i].y;
                top[i] = line.Edge[i].y + line.Height;
            }
            walls.Add(line, bottom, top, bottom);
        }

        static void AddRetaining(Walls walls, BarrierLine line, SurfaceGrid grid, Earthworks earthworks)
        {
            int n = line.Edge.Count;
            if (n < 2) return;
            float ground = earthworks.groundHeight, deck = earthworks.deckThickness;
            var inner = new float[n];
            var top = new float[n];
            var outer = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 edge = line.Edge[i];
                Vector3 middle = edge + line.Outward[i] * (line.Thickness / 2f);
                float y = edge.y;
                bool overRoad = grid.Raycast(middle + Vector3.down * ProbeStart, Vector3.down, ProbeReach, out _, out _);
                bool underRoad = grid.Raycast(middle + Vector3.up * ProbeStart, Vector3.up, ProbeReach, out Vector3 above, out _);

                inner[i] = y;
                top[i] = Mathf.Max(y, ground) + line.Height;
                if (underRoad) top[i] = Mathf.Max(y, Mathf.Min(top[i], above.y - deck));
                bool sunken = y < ground - Below;
                outer[i] = Mathf.Min(top[i], !sunken && overRoad ? y - deck : ground);
            }
            walls.Add(line, inner, top, outer);
        }

        // Closes a raised road's embankment where it opens over a road passing underneath.
        static void AddAbutments(Walls walls, RoadPath path, SurfaceGrid grid, Earthworks earthworks)
        {
            float ground = earthworks.groundHeight, deck = earthworks.deckThickness;
            int across = Mathf.Max(1, Mathf.CeilToInt(path.Settings.Width / AbutmentProbeStep));
            var raised = new bool[path.Count];
            var open = new bool[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                raised[i] = path.Centre[i].y > ground + deck + ProbeStart;
                if (!raised[i]) continue;
                for (int j = 0; j <= across && !open[i]; j++)
                {
                    Vector3 p = path.Point(i, -path.HalfWidth + path.Settings.Width * j / across);
                    open[i] = grid.Raycast(p + Vector3.down * ProbeStart, Vector3.down, ProbeReach, out _, out _);
                }
            }

            for (int i = 0; i < path.Count; i++)
            {
                if (!raised[i] || open[i]) continue;
                if (i + 1 < path.Count && raised[i + 1] && open[i + 1]) Abutment(i, path.Forward[i]);
                if (i > 0 && raised[i - 1] && open[i - 1]) Abutment(i, -path.Forward[i]);
            }

            void Abutment(int i, Vector3 facing)
            {
                var upper = new List<Vector3>();
                var lower = new List<Vector3>();
                for (int j = 0; j <= across; j++)
                {
                    Vector3 p = path.Point(i, -path.HalfWidth + path.Settings.Width * j / across);
                    upper.Add(new Vector3(p.x, p.y - deck, p.z));
                    lower.Add(new Vector3(p.x, ground, p.z));
                }
                walls.Strip(upper, lower, facing);
            }
        }

        // The underside of every stretch of road that spans another road.
        static void AddDeckUndersides(Walls walls, RoadSurface surface, SurfaceGrid grid, Earthworks earthworks)
        {
            float ground = earthworks.groundHeight, deck = earthworks.deckThickness;
            var v = surface.Vertices;
            var t = surface.Triangles;
            var down = Vector3.down * deck;
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                if (Mathf.Min(a.y, Mathf.Min(b.y, c.y)) < ground - Below) continue;
                Vector3 centre = (a + b + c) / 3f;
                if (!grid.Raycast(centre + Vector3.down * ProbeStart, Vector3.down, ProbeReach, out _, out _)) continue;
                walls.Underside(a + down, b + down, c + down);
            }
        }

        // Flat ground over the whole network, with a hole wherever a road (and its wall) sits below it. Built
        // as a grid of cells: a cell clear of every hole is two triangles; a cell a hole reaches into is cut
        // and triangulated on its own, so one awkward hole can only spoil its own cell.
        static void AddGround(RoadSurface surface, IReadOnlyList<RoadPath> paths, Earthworks earthworks)
        {
            float ground = earthworks.groundHeight;
            var holes = new Paths64();
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var path in paths)
            {
                foreach (var c in path.Centre)
                {
                    minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                    minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z);
                }
                holes.AddRange(SunkenOutlines(path, ground));
            }
            var merged = Clipper.SimplifyPaths(Clipper.Union(holes, FillRule.NonZero), SimplifyMm);
            var bounds = new List<Rect64>();
            foreach (var hole in merged) bounds.Add(Clipper.GetBounds(hole));

            float m = earthworks.groundMargin;
            minX -= m; minZ -= m; maxX += m; maxZ += m;
            int failed = 0;
            for (float x = minX; x < maxX; x += GroundCell)
            for (float z = minZ; z < maxZ; z += GroundCell)
            {
                float x1 = Mathf.Min(x + GroundCell, maxX), z1 = Mathf.Min(z + GroundCell, maxZ);
                var cell = new Path64 { Point(x, z), Point(x1, z), Point(x1, z1), Point(x, z1) };
                var cellBounds = Clipper.GetBounds(cell);
                var reaching = new Paths64();
                for (int h = 0; h < merged.Count; h++)
                    if (bounds[h].left < cellBounds.right && bounds[h].right > cellBounds.left && bounds[h].top < cellBounds.bottom && bounds[h].bottom > cellBounds.top)
                        reaching.Add(merged[h]);
                if (reaching.Count == 0)
                {
                    GroundQuad(surface, x, z, x1, z1, ground);
                    continue;
                }
                var clipper = new Clipper64();
                clipper.AddSubject(new Paths64 { cell });
                clipper.AddClip(reaching);
                var tree = new PolyTree64();
                clipper.Execute(ClipType.Difference, FillRule.NonZero, tree);
                for (int i = 0; i < tree.Count; i++)
                {
                    try { TriangulateGround(surface, tree.Child(i), ground); }
                    catch (Exception) { failed++; }
                }
            }
            if (failed > 0) surface.Warnings.Add($"Ground: {failed} cell pieces could not be triangulated and are left open.");
        }

        const float GroundCell = 100f;

        static void GroundQuad(RoadSurface surface, float x0, float z0, float x1, float z1, float ground)
        {
            int first = surface.GroundVertices.Count;
            foreach (var (x, z) in new[] { (x0, z0), (x1, z0), (x1, z1), (x0, z1) })
            {
                surface.GroundVertices.Add(new Vector3(x, ground, z));
                surface.GroundUvs.Add(new Vector2(x, z) / GroundUvTile);
            }
            surface.GroundTriangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
        }

        // Each run of a road whose surface dips to within Below of the ground or under it, out to the back of
        // its walls; any closer and the ground would be drawn over the road.
        static IEnumerable<Path64> SunkenOutlines(RoadPath path, float ground)
        {
            var sunken = new bool[path.Count];
            for (int i = 0; i < path.Count; i++)
                sunken[i] = Mathf.Min(path.Point(i, -path.HalfWidth).y, path.Point(i, path.HalfWidth).y) < ground + Below;

            float back = path.Settings.barrierThickness;
            for (int i = 0; i < path.Count; i++)
            {
                if (!sunken[i]) continue;
                int from = i;
                while (i + 1 < path.Count && sunken[i + 1]) i++;
                int a = Mathf.Max(0, from - 1), b = Mathf.Min(path.Count - 1, i + 1);
                var outline = new Path64();
                for (int k = a; k <= b; k++) outline.Add(Point(path.Point(k, -path.HalfWidth) - path.Right[k] * back));
                for (int k = b; k >= a; k--) outline.Add(Point(path.Point(k, path.HalfWidth) + path.Right[k] * back));
                if (!Clipper.IsPositive(outline)) outline.Reverse();
                yield return outline;
            }
        }

        static void TriangulateGround(RoadSurface surface, PolyPath64 outer, float ground)
        {
            var shape = new P2T.Shape(ToTriPoints(outer.Polygon));
            for (int h = 0; h < outer.Count; h++) shape.Holes.Add(new P2T.Shape(ToTriPoints(outer.Child(h).Polygon)));
            var triangles = new List<P2T.Triangle>();
            shape.Triangulate(triangles);

            var index = new Dictionary<P2T.TriPoint, int>();
            foreach (var tri in triangles)
            {
                int a = VertexFor(tri.Points[0]), b = VertexFor(tri.Points[1]), c = VertexFor(tri.Points[2]);
                var gv = surface.GroundVertices;
                bool up = Vector3.Cross(gv[b] - gv[a], gv[c] - gv[a]).y > 0f;
                surface.GroundTriangles.Add(a);
                surface.GroundTriangles.Add(up ? b : c);
                surface.GroundTriangles.Add(up ? c : b);
            }

            for (int h = 0; h < outer.Count; h++)
                for (int island = 0; island < outer.Child(h).Count; island++)
                    TriangulateGround(surface, outer.Child(h).Child(island), ground);

            int VertexFor(P2T.TriPoint p)
            {
                if (index.TryGetValue(p, out int i)) return i;
                var vertex = new Vector3((float)p.X, ground, (float)p.Y);
                surface.GroundVertices.Add(vertex);
                surface.GroundUvs.Add(new Vector2(vertex.x, vertex.z) / GroundUvTile);
                return index[p] = surface.GroundVertices.Count - 1;
            }
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

        static Point64 Point(Vector3 v) => Point(v.x, v.z);
        static Point64 Point(float x, float z) => new Point64((long)Math.Round(x * Mm), (long)Math.Round(z * Mm));

        // Concrete walls: an inner face, a top and an outer face, each flat-shaded, with their own bottoms.
        sealed class Walls
        {
            readonly RoadSurface surface;

            public Walls(RoadSurface surface) => this.surface = surface;

            public void Add(BarrierLine line, float[] innerBottom, float[] top, float[] outerBottom)
            {
                var edge = line.Edge;
                var outward = line.Outward;
                float thickness = line.Thickness;
                Vector3 Inner(int i, float y) => new Vector3(edge[i].x, y, edge[i].z);
                Vector3 Outer(int i, float y) => new Vector3(edge[i].x + outward[i].x * thickness, y, edge[i].z + outward[i].z * thickness);
                var faces = new (Func<int, Vector3> a, Func<int, Vector3> b, Func<int, Vector3> normal)[]
                {
                    (i => Inner(i, innerBottom[i]), i => Inner(i, top[i]), i => -outward[i]),
                    (i => Inner(i, top[i]), i => Outer(i, top[i]), i => Vector3.up),
                    (i => Outer(i, top[i]), i => Outer(i, outerBottom[i]), i => outward[i]),
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

            public void Strip(List<Vector3> upper, List<Vector3> lower, Vector3 facing)
            {
                int first = surface.BarrierVertices.Count;
                float along = 0f;
                for (int i = 0; i < upper.Count; i++)
                {
                    if (i > 0) along += Vector3.Distance(upper[i], upper[i - 1]);
                    surface.BarrierVertices.Add(upper[i]);
                    surface.BarrierVertices.Add(lower[i]);
                    surface.BarrierUvs.Add(new Vector2(along / 4f, upper[i].y / 4f));
                    surface.BarrierUvs.Add(new Vector2(along / 4f, lower[i].y / 4f));
                }
                for (int i = 0; i < upper.Count - 1; i++)
                {
                    int p = first + 2 * i, q = p + 2;
                    Triangle(p, p + 1, q, facing);
                    Triangle(q, p + 1, q + 1, facing);
                }
            }

            public void Underside(Vector3 a, Vector3 b, Vector3 c)
            {
                int first = surface.BarrierVertices.Count;
                surface.BarrierVertices.Add(a);
                surface.BarrierVertices.Add(b);
                surface.BarrierVertices.Add(c);
                surface.BarrierUvs.Add(new Vector2(a.x, a.z) / 4f);
                surface.BarrierUvs.Add(new Vector2(b.x, b.z) / 4f);
                surface.BarrierUvs.Add(new Vector2(c.x, c.z) / 4f);
                Triangle(first, first + 1, first + 2, Vector3.down);
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
    }
}
