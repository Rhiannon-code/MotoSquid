using UnityEngine;
using UnityEngine.Splines;

namespace MotoSquid.Traffic
{
    public class PrePopulatedTraffic : MonoBehaviour
    {
        public SplineContainer laneContainer;
        public float startOffset;
        public float speed = 25f;

        public string vehicleLayerName = "Traffic";

        void Start()
        {
            // Apply layer so the AI's trafficLayerMask sphere casts can detect this vehicle
            int layer = LayerMask.NameToLayer(vehicleLayerName);
            if (layer >= 0) TrafficSpawner.SetLayerRecursive(gameObject, layer);
            if (laneContainer == null) return;

            SplineMover mover = GetComponent<SplineMover>();
            if (mover == null) mover = gameObject.AddComponent<SplineMover>();

            mover.Setup(laneContainer, startOffset, speed, false);

            var following = GetComponent<VehicleFollowing>();
            if (following != null)
            {
                following.nominalSpeed = speed;
                following.ResetForSpawn();
            }
        }
    }
}
