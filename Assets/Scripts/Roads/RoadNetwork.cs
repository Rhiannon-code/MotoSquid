using UnityEngine;

namespace MotoSquid.Roads
{
    // Every Road under this object is built into one surface mesh and collider.
    public class RoadNetwork : MonoBehaviour
    {
        public Material surfaceMaterial;
        public Material barrierMaterial;
        [Tooltip("Metres covered by one repeat of the surface texture.")]
        public float uvTileSize = 4f;

        public Road[] Roads => GetComponentsInChildren<Road>();
    }
}
