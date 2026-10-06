using UnityEngine;

namespace MotoSquid.Bike
{
public class TrailLag : MonoBehaviour
{
    [Header("Spring")]
    public float stiffness = 600f;
    public float damping   = 0.45f;
    public float maxLag    = 4f;     // Metres, so a teleport or respawn cannot stretch it across the map

    Transform m_Parent;
    Vector3   m_HomeLocal;
    Vector3   m_Velocity;
    TrailRenderer m_Trail;

    void Awake()
    {
        m_Parent    = transform.parent;
        m_HomeLocal = transform.localPosition;
        m_Trail     = GetComponent<TrailRenderer>();
    }

    Vector3 Anchor => m_Parent != null ? m_Parent.TransformPoint(m_HomeLocal) : transform.position;

    void OnEnable()
    {
        if (m_Parent != null) transform.position = Anchor;
        m_Velocity = Vector3.zero;
        if (m_Trail != null) m_Trail.Clear();
    }

    void LateUpdate()
    {
        if (m_Parent == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 toAnchor = Anchor - transform.position;

        if (toAnchor.magnitude > maxLag)
        {
            transform.position = Anchor;
            m_Velocity = Vector3.zero;
            if (m_Trail != null) m_Trail.Clear();
            return;
        }
        m_Velocity += (toAnchor * stiffness - m_Velocity * (2f * damping * Mathf.Sqrt(stiffness))) * dt;
        transform.position += m_Velocity * dt;
    }
}
}
