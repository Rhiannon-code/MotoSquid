using UnityEngine;

namespace MotoSquid.Bike
{
    [DefaultExecutionOrder(10000)]
    public class WheelVisualSpin : MonoBehaviour
    {
        public Transform frontWheel, rearWheel;
        public Transform frontAxisRef, rearAxisRef;
        public float frontRadius = 0.3f, rearRadius = 0.3f;

        IBikeAudioState _bike;

        void Awake()
        {
            _bike = GetComponentInParent<IBikeAudioState>() as IBikeAudioState;
        }

        void LateUpdate()
        {
            if (_bike == null) return;
            float v = _bike.SpeedMs;
            Spin(frontWheel, frontAxisRef, frontRadius, v);
            Spin(rearWheel,  rearAxisRef,  rearRadius,  v);
        }

        void Spin(Transform wheel, Transform axleRef, float radius, float speedMs)
        {
            if (wheel == null || radius < 0.0001f) return;
            float deg = speedMs / (2f * Mathf.PI * radius) * 360f * Time.deltaTime;
            Vector3 axis = axleRef != null ? axleRef.right : transform.right;
            wheel.Rotate(axis, deg, Space.World);
        }
    }
}
