using UnityEngine;

namespace MotoSquid.Track
{
    public class ObjectFloat : MonoBehaviour
    {
        [SerializeField] private float speed = 1.0f;
        [SerializeField] private float height = 0.5f;

        private Vector3 startPos;
        private Rigidbody rb;

        void Start()
        {
            startPos = transform.position;

            rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        void Update()
        {
            if (rb != null) return; // Rigidbody path runs in FixedUpdate
            float newY = startPos.y + Mathf.PingPong(Time.time * speed, height);
            transform.Rotate(Vector3.up, 50f * Time.deltaTime);
            transform.position = new Vector3(startPos.x, newY, startPos.z);
        }

        void FixedUpdate()
        {
            if (rb == null) return;
            float newY = startPos.y + Mathf.PingPong(Time.time * speed, height);
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, 50f * Time.fixedDeltaTime, 0f));
            rb.MovePosition(new Vector3(startPos.x, newY, startPos.z));
        }
    }
}
