using UnityEngine;

namespace MotoSquid.Bike
{
public class SlipstreamCone : MonoBehaviour
{
    [Header("References (built on this bike if left empty)")]
    public SlipstreamSystem slipstream;
    public LineRenderer[] lines;
    public bool disableTrails = true;

    [Header("Cone")]
    public float length     = 20f;    // Metres behind the tail
    public float startSpread = 0.05f; // Lateral offset at the bike
    public float endSpread   = 1.8f;  // ...and at the far end, the difference IS the cone
    public AnimationCurve spreadShape =
        new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.05f),
                           new Keyframe(0.7f, 0.25f, 0.6f, 0.6f),
                           new Keyframe(1f, 1f, 2.6f, 0f));
    public float rise        = 0.5f;  // How far it lifts over that distance
    [Range(2, 32)] public int segments = 12;

    [Header("Width")]
    public float width = 0.5f;
    public AnimationCurve widthShape =
        new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(1f, 1f));
    public float showsAt = 0.12f;

    Vector3[] m_Points;
    float[]   m_Side;

    void Start()
    {
        if (slipstream == null) slipstream = GetComponentInParent<SlipstreamSystem>(true);

        if (slipstream == null)
        {
            Debug.LogError("SlipstreamCone: no SlipstreamSystem in its parents.", this);
            enabled = false;
            return;
        }

        if (lines == null || lines.Length == 0) lines = GetComponentsInChildren<LineRenderer>(true);
        if (lines.Length == 0) lines = BuildFromTrails();

        if (lines.Length == 0)
        {
            Debug.LogError("SlipstreamCone: found the slipstream system but it has no trail " +
                           "emitters to build lines on, and there is no LineRenderer on a child.", this);
            enabled = false;
            return;
        }

        m_Side   = new float[lines.Length];
        m_Points = new Vector3[segments + 1];
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == null) continue;
            m_Side[i] = lines[i].transform.localPosition.x < 0f ? -1f : 1f;
            lines[i].useWorldSpace = false;
            lines[i].positionCount = m_Points.Length;
            lines[i].widthCurve    = widthShape;
            lines[i].enabled       = false;
        }
    }

    LineRenderer[] BuildFromTrails()
    {
        TrailRenderer[] trails = slipstream.trails;
        if (trails == null) return new LineRenderer[0];

        var built = new System.Collections.Generic.List<LineRenderer>();
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] == null) continue;

            GameObject host = trails[i].gameObject;
            LineRenderer line = host.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = host.AddComponent<LineRenderer>();
                line.sharedMaterial = trails[i].sharedMaterial;
            }

            if (disableTrails) trails[i].enabled = false;
            built.Add(line);
        }
        return built.ToArray();
    }

    void LateUpdate()
    {
        float strength = slipstream.VisualsEnabled
            ? Mathf.InverseLerp(showsAt, 1f, slipstream.DraftStrength)
            : 0f;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == null) continue;

            if (strength <= 0f) { lines[i].enabled = false; continue; }

            lines[i].enabled         = true;
            lines[i].widthMultiplier = width * strength;
            Build(m_Side[i], strength);
            lines[i].SetPositions(m_Points);
        }
    }

    void Build(float side, float strength)
    {
        for (int p = 0; p < m_Points.Length; p++)
        {
            float t = (float)p / segments;
            float fan = Mathf.Lerp(startSpread, endSpread, spreadShape.Evaluate(t));
            m_Points[p] = new Vector3(
                side * fan * strength,
                rise * t * strength,
                -length * t * strength);
        }
    }

    void OnDisable()
    {
        if (lines == null) return;
        for (int i = 0; i < lines.Length; i++)
            if (lines[i] != null) lines[i].enabled = false;
    }
}
}
