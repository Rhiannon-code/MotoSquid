using UnityEngine;

namespace MotoSquid.Combat
{
    public class WeaponSocket : MonoBehaviour
    {
        public WeaponHand hand = WeaponHand.Right;

        [Header("Per character correction")]
        public Vector3 localPositionOffset = Vector3.zero;
        public Vector3 localEulerOffset    = Vector3.zero;
        public float   scaleMultiplier     = 1f;

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = hand == WeaponHand.Left ? Color.cyan : new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, 0.04f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.25f);
        }
#endif
    }
}
