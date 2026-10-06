using UnityEngine;

namespace MotoSquid.Track
{
    [RequireComponent(typeof(BoxCollider))]
    public class InvisibleBarrier : MonoBehaviour
    {
        public float redirectForce = 0f;
        public Color gizmoColour = new Color(1f, 0.15f, 0.15f, 0.25f);

        private void Awake()
        {
            var rend = GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;
        }

        private void OnCollisionEnter(Collision col)
        {
            if (redirectForce <= 0f || col.rigidbody == null) return;

            Vector3 normal = Vector3.zero;
            foreach (ContactPoint cp in col.contacts)
                normal += cp.normal;

            if (normal.sqrMagnitude > 0f)
                col.rigidbody.AddForce(normal.normalized * redirectForce, ForceMode.Impulse);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var col = GetComponent<BoxCollider>();
            if (col != null && col.isTrigger)
                Debug.LogWarning($"[InvisibleBarrier] '{name}': collider is set to Is Trigger, " +
                                 "AI raycasts ignore triggers. Disable Is Trigger.", this);
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<BoxCollider>();
            if (col == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color  = gizmoColour;
            Gizmos.DrawCube(col.center, col.size);

            Gizmos.color = new Color(gizmoColour.r, gizmoColour.g, gizmoColour.b, 1f);
            Gizmos.DrawWireCube(col.center, col.size);
        }

        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<BoxCollider>();
            if (col == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color  = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawWireCube(col.center, col.size);
        }
#endif
    }
}
