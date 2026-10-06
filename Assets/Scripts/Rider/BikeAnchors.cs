using UnityEngine;

namespace MotoSquid.Rider
{
    public class BikeAnchors : MonoBehaviour
    {
        public const string SeatName = "SeatAnchor";
        public const string GripLeftName = "GripAnchor_L";
        public const string GripRightName = "GripAnchor_R";
        public const string PegLeftName = "FootPegGrip_L";
        public const string PegRightName = "FootPegGrip_R";

        [Header("Contact points: leave empty to bind to the bike art's own anchors")]
        public Transform seat;
        public Transform pegLeft;
        public Transform pegRight;
        public Transform gripLeft;
        public Transform gripRight;
        public Transform footDownLeft;

        [Header("Clearance")]
        public float seatClearance = 0.06f;
        public float tankClearance = 0.04f;

        [Header("Per pose offsets from the seat, in bike space")]
        public Vector3 hipIdle = new Vector3(0f, 0f, -0.02f);
        public Vector3 hipNormal = Vector3.zero;
        public Vector3 hipHigh = new Vector3(0f, -0.01f, 0.06f);

        [Header("Spine lean per pose, degrees forward")]
        public float spineIdleTuck = 0f;
        public float spineNormalTuck = 8f;
        public float spineHighTuck = 18f;

        public bool IsComplete
        {
            get
            {
                return seat != null && pegLeft != null && pegRight != null
                    && gripLeft != null && gripRight != null;
            }
        }

        void Awake() { Resolve(); }
        void OnValidate() { Resolve(); }

        public void Resolve()
        {
            if (seat == null) seat = FindDeep(transform, SeatName);
            if (gripLeft == null) gripLeft = FindDeep(transform, GripLeftName);
            if (gripRight == null) gripRight = FindDeep(transform, GripRightName);
            if (pegLeft == null) pegLeft = FindDeep(transform, PegLeftName);
            if (pegRight == null) pegRight = FindDeep(transform, PegRightName);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var hit = FindDeep(t.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        void OnDrawGizmosSelected()
        {
            Draw(seat, Color.cyan, 0.05f);
            Draw(pegLeft, Color.green, 0.035f);
            Draw(pegRight, Color.green, 0.035f);
            Draw(gripLeft, Color.yellow, 0.035f);
            Draw(gripRight, Color.yellow, 0.035f);
            Draw(footDownLeft, new Color(1f, 0.5f, 0f), 0.035f);

            if (seat == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(seat.position, seat.position + seat.up * seatClearance);
        }

        static void Draw(Transform t, Color c, float r)
        {
            if (t == null) return;
            Gizmos.color = c;
            Gizmos.DrawWireSphere(t.position, r);
            Gizmos.DrawRay(t.position, t.forward * r * 2f);
        }
    }
}
