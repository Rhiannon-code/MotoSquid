using UnityEngine;

namespace MotoSquid.Roads
{
    // Probes the actual collider the bike rides on, so the checker measures what physics sees.
    public sealed class ColliderProbe : ISurfaceProbe
    {
        readonly Collider collider;

        public ColliderProbe(Collider collider) => this.collider = collider;

        public bool Raycast(Vector3 origin, Vector3 direction, float distance, out Vector3 point, out Vector3 normal)
        {
            if (collider.Raycast(new Ray(origin, direction), out RaycastHit hit, distance))
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }
            point = default;
            normal = Vector3.up;
            return false;
        }
    }
}
