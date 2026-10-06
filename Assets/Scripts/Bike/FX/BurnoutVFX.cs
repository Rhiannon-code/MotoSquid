using UnityEngine;
using UnityEngine.VFX;

namespace MotoSquid.Bike
{
public class BurnoutVFX : MonoBehaviour
{
    [Header("References (found on this bike if left empty)")]
    public VisualEffect effect;

    [Header("Exposed Properties (blank = not driven)")]
    public string rateProperty     = "SmokeRate";
    public string sizeProperty     = "SmokeSize";
    public string velocityProperty = "SmokeVelocity";

    [Header("Look")]
    public string gradientProperty = "SmokeGradient";
    [Range(0f, 1f)] public float opacity = 0.2f;
    public string opacityProperty = "Opacity";
    public string fresnelProperty = "FresnelPower";
    public float  fresnel = 0f;

    [Header("Full Burnout")]
    public float rate     = 30f;
    public float size     = 0.35f;
    public float velocity = 2.5f;
    public float idleShare = 0.35f;   // What a just started burnout runs at before any charge builds

    // The legacy TireSmoke particle material is a built in shader HDRP never draws, so a handbrake drift
    // had no visible smoke at all. This effect already renders, so it covers the drift too, lighter
    [Header("Drift")]
    public bool  smokeWhileDrifting = true;
    [Range(0f, 1f)] public float driftShare = 0.6f;

    BikeController   m_Player;
    BikeAIController m_Ai;
    bool m_Playing;

    Gradient m_Source;
    float    m_AppliedOpacity = -1f;

    void Start()
    {
        if (effect == null) effect = GetComponentInChildren<VisualEffect>(true);

        m_Player = GetComponentInParent<BikeController>(true);
        if (m_Player == null) m_Ai = GetComponentInParent<BikeAIController>(true);

        if (effect == null || (m_Player == null && m_Ai == null))
        {
            Debug.LogError("BurnoutVFX: needs a bike controller in its parents and a " +
                           "VisualEffect on it or a child.", this);
            enabled = false;
            return;
        }

        if (m_Player != null) m_Player.burnoutSmokeSuppressed = true;
        else                  m_Ai.burnoutSmokeSuppressed     = true;

        if (!string.IsNullOrEmpty(gradientProperty) && effect.HasGradient(gradientProperty))
            m_Source = effect.GetGradient(gradientProperty);

        effect.Stop();
    }

    void LateUpdate()
    {
        bool burning = m_Player != null ? m_Player.IsDoingBurnout : m_Ai.IsDoingBurnout;
        float drift  = !smokeWhileDrifting ? 0f
                     : m_Player != null ? (m_Player.isDrifting ? m_Player.DriftIntensity : 0f)
                     : (m_Ai.isAIDrifting ? 1f : 0f);

        if (!burning && drift <= 0f)
        {
            if (m_Playing) { effect.Stop(); m_Playing = false; }
            return;
        }

        if (!m_Playing) { effect.Play(); m_Playing = true; }

        float charge   = m_Player != null ? m_Player.LaunchCharge : 1f;
        float strength = burning ? Mathf.Lerp(idleShare, 1f, charge) : drift * driftShare;

        Set(rateProperty,     rate     * strength);
        Set(sizeProperty,     size     * strength);
        Set(velocityProperty, velocity * strength);
        if (fresnel > 0f) Set(fresnelProperty, fresnel);
        ApplyOpacity();
    }

    void ApplyOpacity()
    {
        if (Mathf.Approximately(opacity, m_AppliedOpacity)) return;
        m_AppliedOpacity = opacity;

        if (!string.IsNullOrEmpty(opacityProperty) && effect.HasFloat(opacityProperty))
        {
            effect.SetFloat(opacityProperty, opacity);
            return;
        }

        if (m_Source == null) return;

        GradientColorKey[] colours = m_Source.colorKeys;
        for (int i = 0; i < colours.Length; i++) colours[i].color *= opacity;

        GradientAlphaKey[] alphas = m_Source.alphaKeys;
        for (int i = 0; i < alphas.Length; i++) alphas[i].alpha *= opacity;

        var faded = new Gradient();
        faded.SetKeys(colours, alphas);
        effect.SetGradient(gradientProperty, faded);
    }

    void Set(string property, float value)
    {
        if (string.IsNullOrEmpty(property) || !effect.HasFloat(property)) return;
        effect.SetFloat(property, value);
    }

    void OnDisable()
    {
        if (effect != null) effect.Stop();
        m_Playing = false;
    }
}
}
