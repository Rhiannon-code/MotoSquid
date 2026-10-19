using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    // Vertical rays (straight up or down) against a triangle mesh, through a 2D grid.
    public sealed class SurfaceGrid : ISurfaceProbe
    {
        const float Cell = 8f;

        readonly IReadOnlyList<Vector3> v;
        readonly IReadOnlyList<int> t;
        readonly Dictionary<(int, int), List<int>> grid = new Dictionary<(int, int), List<int>>();

        public SurfaceGrid(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles)
        {
            v = vertices;
            t = triangles;
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / Cell), x1 = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / Cell);
                int z0 = Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) / Cell), z1 = Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) / Cell);
                for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    if (!grid.TryGetValue((x, z), out var list)) grid[(x, z)] = list = new List<int>();
                    list.Add(i);
                }
            }
        }

        public bool Raycast(Vector3 origin, Vector3 direction, float distance, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;
            float best = float.MaxValue;
            bool found = false;
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
                best = along;
                found = true;
                point = new Vector3(origin.x, y, origin.z);
                normal = Vector3.Cross(b - a, c - a).normalized;
                if (normal.y < 0) normal = -normal;
            }
            return found;
        }
    }
}
