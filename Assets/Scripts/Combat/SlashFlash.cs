using UnityEngine;

namespace MotoSquid.Combat
{
public class SlashFlash : MonoBehaviour
{
    [Header("Timing")]
    public float duration = 0.3f;

    [Header("Shape")]
    public float startScale = 0.55f;
    public float endScale   = 1.25f;

    [Header("Fade")]
    public float peakAlpha = 1f;
    public AnimationCurve fade =
        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f));
    public string colorProperty = "_UnlitColor";

    Renderer[] m_Renderers;
    MaterialPropertyBlock m_Block;
    Color m_Base;
    int   m_ColorId;
    float m_Timer = -1f;


    void Awake()
    {
        m_Renderers = GetComponentsInChildren<Renderer>(true);
        m_Block     = new MaterialPropertyBlock();
        m_ColorId   = Shader.PropertyToID(colorProperty);


        if (m_Renderers.Length > 0 && m_Renderers[0].sharedMaterial != null &&
            m_Renderers[0].sharedMaterial.HasProperty(m_ColorId))
            m_Base = m_Renderers[0].sharedMaterial.GetColor(m_ColorId);
        else
            m_Base = Color.white;

        SetVisible(false);
    }

    public void Play()
    {
        if (m_Renderers == null || m_Renderers.Length == 0)
        {
            Debug.LogWarning($"SlashFlash on '{name}' has no Renderer on it or any child, " +
                             "so there is nothing to show.", this);
            return;
        }

        gameObject.SetActive(true);
        m_Timer = 0f;
        SetVisible(true);
    }

    void LateUpdate()
    {
        if (m_Timer < 0f) return;

        m_Timer += Time.deltaTime;
        float t = duration > 0.001f ? m_Timer / duration : 1f;

        if (t >= 1f)
        {
            m_Timer = -1f;
            SetVisible(false);
            return;
        }

        float k = Mathf.Lerp(startScale, endScale, t);
        transform.localScale = new Vector3(k, k, k);

        Color c = m_Base;
        c.a = fade.Evaluate(t) * peakAlpha;
        m_Block.SetColor(m_ColorId, c);
        for (int i = 0; i < m_Renderers.Length; i++)
            if (m_Renderers[i] != null) m_Renderers[i].SetPropertyBlock(m_Block);
    }

    void SetVisible(bool on)
    {
        for (int i = 0; i < m_Renderers.Length; i++)
            if (m_Renderers[i] != null) m_Renderers[i].enabled = on;
    }
}
}
