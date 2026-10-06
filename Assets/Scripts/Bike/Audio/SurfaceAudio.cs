using UnityEngine;

namespace MotoSquid.Bike
{
    public class SurfaceAudio : MonoBehaviour
    {
        [Header("References")]
        public BikeController bikeController;

        [Header("Surface Detection")]
        public LayerMask roadLayers = ~0;
        public string roadTag = "Road";
        public float probeDistance = 2f;

        [Header("Dust")]
        public ParticleSystem roadDust;
        public ParticleSystem offRoadDust;

        [Header("Skid Trigger")]
        public float slideSpeed = 4f;

        void Awake()
        {
            if (bikeController == null) bikeController = GetComponentInParent<BikeController>(true);
        }

        void Update()
        {
            if (bikeController == null) return;

            bool grounded = bikeController.bikeIsGrounded;
            float lateral = Mathf.Abs(bikeController.localBikeVelocity.x);
            bool braking  = bikeController.bikeInput != null && bikeController.bikeInput.HandBrake > 0f;
            bool skidding = grounded && (lateral > slideSpeed || (braking && lateral > slideSpeed * 0.5f));

            if (!skidding) { StopDust(); return; }

            EmitDust(ProbeSurface());
        }

        bool ProbeSurface()
        {
            Vector3 origin = bikeController.transform.position + Vector3.up * 0.2f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeDistance,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider is TerrainCollider) return false;   
                bool layerIsRoad = (roadLayers.value & (1 << hit.collider.gameObject.layer)) != 0;
                bool tagIsRoad   = !string.IsNullOrEmpty(roadTag) && hit.collider.CompareTag(roadTag);
                return layerIsRoad || tagIsRoad;
            }
            return true;   
        }

        void EmitDust(bool onRoad)
        {
            var want  = onRoad ? roadDust : offRoadDust;
            var other = onRoad ? offRoadDust : roadDust;
            if (other != null && other.isEmitting) other.Stop();
            if (want  != null && !want.isEmitting) want.Play();
        }

        void StopDust()
        {
            if (roadDust    != null && roadDust.isEmitting)    roadDust.Stop();
            if (offRoadDust != null && offRoadDust.isEmitting) offRoadDust.Stop();
        }
    }
}
