using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    // Probes the actual colliders the bike rides on, so the checker measures what physics sees. With tiled
    // colliders, only those whose bounds cover the ray are tried, and the nearest hit wins.
    public sealed class ColliderProbe : ISurfaceProbe
    {
        readonly IReadOnlyList<Collider> colliders;

        public ColliderProbe(IReadOnlyList<Collider> colliders) => this.colliders = colliders;

        public bool Raycast(Vector3 origin, Vector3 direction, float distance, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;
            float best = float.MaxValue;
            var ray = new Ray(origin, direction);
            foreach (var collider in colliders)
            {
                var bounds = collider.bounds;
                if (origin.x < bounds.min.x || origin.x > bounds.max.x || origin.z < bounds.min.z || origin.z > bounds.max.z) continue;
                if (collider.Raycast(ray, out RaycastHit hit, distance) && hit.distance < best)
                {
                    best = hit.distance;
                    point = hit.point;
                    normal = hit.normal;
                }
            }
            return best < float.MaxValue;
        }
    }
}
