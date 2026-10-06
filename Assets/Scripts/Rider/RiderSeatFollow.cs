using UnityEngine;

namespace MotoSquid.Rider
{
    [DefaultExecutionOrder(20000)]
    public class RiderSeatFollow : MonoBehaviour
    {
        public Transform seatAnchor;   
        public Transform hips;         
        public Vector3 placementOffset;
        public bool debug = false;     

        void LateUpdate()
        {
            if (seatAnchor == null || hips == null) return;

            Vector3 offset = seatAnchor.position - hips.position;   
            if (placementOffset != Vector3.zero)
                offset += seatAnchor.TransformVector(placementOffset);
            transform.position += offset;                          

            if (debug)
            {
                Debug.DrawLine(hips.position, seatAnchor.position, Color.yellow);
                if (Time.frameCount % 60 == 0)
                    Debug.Log($"[SeatFollow] pre correct hip->seat offset = {offset.magnitude:F3} m  {offset}");
            }
        }
    }
}
