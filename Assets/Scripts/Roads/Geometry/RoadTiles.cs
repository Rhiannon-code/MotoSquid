using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    public sealed class TileMesh
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<Vector3> Normals = new List<Vector3>();
    }

    // Cuts a generated mesh into square tiles on the XZ plane. A triangle belongs to the tile its centre lies
    // in, so render tiles never overlap; with `overlap`, a triangle also joins every tile whose square, grown
    // by that much, it touches, so collider tiles share a band of surface and a wheel crossing a tile edge
    // always has whole surface under it. Shared vertices keep their exact positions, so tile edges match;
    // normals worked out on the whole mesh first keep the shading seamless across them too.
    public static class RoadTiles
    {
        public static Dictionary<(int x, int z), TileMesh> Split(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles,
                                                               IReadOnlyList<Vector2> uvs, IReadOnlyList<Vector3> normals,
                                                               float size, float overlap = 0f)
        {
            var tiles = new Dictionary<(int, int), TileMesh>();
            var remap = new Dictionary<(int, int), Dictionary<int, int>>();
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                if (overlap <= 0f)
                {
                    Vector3 centre = (a + b + c) / 3f;
                    Add((Cell(centre.x, size), Cell(centre.z, size)), t);
                    continue;
                }
                int x0 = Cell(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - overlap, size), x1 = Cell(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + overlap, size);
                int z0 = Cell(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - overlap, size), z1 = Cell(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) + overlap, size);
                for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    Add((x, z), t);
            }
            return tiles;

            void Add((int, int) key, int t)
            {
                if (!tiles.TryGetValue(key, out var tile))
                {
                    tiles[key] = tile = new TileMesh();
                    remap[key] = new Dictionary<int, int>();
                }
                var map = remap[key];
                for (int k = 0; k < 3; k++)
                {
                    int source = triangles[t + k];
                    if (!map.TryGetValue(source, out int local))
                    {
                        local = map[source] = tile.Vertices.Count;
                        tile.Vertices.Add(vertices[source]);
                        if (uvs != null && uvs.Count > source) tile.Uvs.Add(uvs[source]);
                        if (normals != null && normals.Count > source) tile.Normals.Add(normals[source]);
                    }
                    tile.Triangles.Add(local);
                }
            }
        }

        public static int Cell(float coordinate, float size) => Mathf.FloorToInt(coordinate / size);
    }
}
