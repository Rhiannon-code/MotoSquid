using System;
using System.Linq;
using UnityEngine;

namespace MotoSquid.Performance
{

    public static class LightBakePolicy
    {
        public const string PrefabRoot = "Assets/Prefabs";

        public static readonly string[] RealtimeNames = { "Sun", "Moon", "Headlight", "Taillight", "Tailight" };

        public static bool StaysRealtime(Light light) =>
            light.type == LightType.Directional
            || RealtimeNames.Any(n => light.name.StartsWith(n, StringComparison.OrdinalIgnoreCase));

        public static LightmapBakeType ModeFor(Light light) =>
            StaysRealtime(light) ? LightmapBakeType.Realtime : LightmapBakeType.Baked;

        public static bool IsCorrect(Light light) => light.lightmapBakeType == ModeFor(light);

        public static string PathOf(Transform t) =>
            t.parent == null ? t.name : $"{PathOf(t.parent)}/{t.name}";
    }
}
