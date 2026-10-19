using UnityEngine;

namespace MotoSquid.Roads
{
    // Every Road under this object is built into one surface mesh and collider.
    public class RoadNetwork : MonoBehaviour
    {
        public Material surfaceMaterial;
        public Material barrierMaterial;
        public Material groundMaterial;
        [Tooltip("Metres covered by one repeat of the surface texture.")]
        public float uvTileSize = 4f;
        [Tooltip("The generated meshes and colliders are cut into square tiles this size (m), so the camera and physics only deal with the ones nearby.")]
        public float tileSize = 1000f;
        public Earthworks earthworks = new Earthworks();
        // The roads changed after the surface was last built, so the 3D surface no longer matches them.
        [HideInInspector] public bool surfaceStale;

        public Road[] Roads => GetComponentsInChildren<Road>();
    }
}
