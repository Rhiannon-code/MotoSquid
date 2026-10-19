using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MotoSquid.Roads
{
    // The Road Plan as a PNG for reviewing a layout away from the editor: the same height shading, higher roads
    // over lower ones, and a red ring on every problem. North is up.
    static class RoadPlanImage
    {
        const int MaxSize = 4096;
        const float Margin = 100f;
        const int Ring = 12;

        public static void Save(string file, List<PlanChunk> chunks, List<RoadIssue> issues, float minHeight, float maxHeight)
        {
            if (chunks.Count == 0) return;
            float xMin = float.MaxValue, zMin = float.MaxValue, xMax = float.MinValue, zMax = float.MinValue;
            foreach (var c in chunks)
            {
                xMin = Mathf.Min(xMin, c.Bounds.xMin); zMin = Mathf.Min(zMin, c.Bounds.yMin);
                xMax = Mathf.Max(xMax, c.Bounds.xMax); zMax = Mathf.Max(zMax, c.Bounds.yMax);
            }
            xMin -= Margin; zMin -= Margin; xMax += Margin; zMax += Margin;
            float perMetre = MaxSize / Mathf.Max(xMax - xMin, zMax - zMin);
            int width = Mathf.Max(1, Mathf.CeilToInt((xMax - xMin) * perMetre));
            int height = Mathf.Max(1, Mathf.CeilToInt((zMax - zMin) * perMetre));
            var pixels = new Color32[width * height];
            Color32 background = new Color(0.13f, 0.14f, 0.15f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;

            Vector2 Px(Vector3 w) => new Vector2((w.x - xMin) * perMetre, (w.z - zMin) * perMetre);
            void Segment(Vector2 a, Vector2 b, float radius, Color32 colour)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius)), x1 = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius)), y1 = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius));
                Vector2 ab = b - a;
                float length = ab.sqrMagnitude;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = length > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / length) : 0f;
                    if ((p - (a + ab * t)).sqrMagnitude <= radius * radius) pixels[y * width + x] = colour;
                }
            }

            foreach (var layer in RoadPlanWindow.Layers(chunks, c => c.Height))
                for (int pass = 0; pass < 2; pass++)
                    foreach (var chunk in layer)
                    {
                        float radius = Mathf.Max(1f, chunk.Width * perMetre / 2f);
                        Color32 fill = RoadPlanWindow.Shade(chunk.Height, minHeight, maxHeight);
                        for (int i = 1; i < chunk.Points.Length; i++)
                            Segment(Px(chunk.Points[i - 1]), Px(chunk.Points[i]), pass == 0 ? radius + 1f : radius, pass == 0 ? new Color32(13, 13, 13, 255) : fill);
                    }
            foreach (var issue in issues)
            {
                Vector2 c = Px(issue.Position);
                for (int y = -Ring; y <= Ring; y++)
                for (int x = -Ring; x <= Ring; x++)
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    int px = Mathf.RoundToInt(c.x) + x, py = Mathf.RoundToInt(c.y) + y;
                    if (d >= Ring - 3 && d <= Ring && px >= 0 && py >= 0 && px < width && py < height) pixels[py * width + px] = new Color32(255, 40, 40, 255);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(file, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
