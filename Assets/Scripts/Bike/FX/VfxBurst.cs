using MotoSquid.Combat;
using UnityEngine;
using UnityEngine.VFX;

namespace MotoSquid.Bike
{
public class VfxBurst : MonoBehaviour
{
    [Header("References (found on this bike if left empty)")]
    public CombatSystem combatSystem;

    [Header("Effects")]
    public GameObject[] hitImpacts;    // One picked at random per landed hit
    public GameObject   swingWhoosh;

    [Header("Pool")]
    public int copiesPerEffect = 3;
    public float whooshForwardOffset = 0.25f;
    public float impactScale = 0.45f;
    public float whooshScale = 1f;
    public System.Collections.Generic.IReadOnlyList<GameObject> WhooshCopies => m_Whoosh;

    GameObject[][] m_Impacts;
    int[]          m_ImpactNext;
    GameObject[]   m_Whoosh;
    int            m_WhooshNext;
    Transform      m_Holder;

    void Start()
    {
        if (combatSystem == null) combatSystem = GetComponentInChildren<CombatSystem>(true);
        if (combatSystem == null)
        {
            Debug.LogError("VfxBurst: no CombatSystem on this bike.", this);
            enabled = false;
            return;
        }

        m_Holder = new GameObject($"{name} VFX").transform;

        if (hitImpacts != null && hitImpacts.Length > 0)
        {
            m_Impacts    = new GameObject[hitImpacts.Length][];
            m_ImpactNext = new int[hitImpacts.Length];
            for (int i = 0; i < hitImpacts.Length; i++) m_Impacts[i] = Pool(hitImpacts[i]);
        }

        m_Whoosh = Pool(swingWhoosh);

        combatSystem.onHitLandedAt += PlayImpact;
        combatSystem.onSwungAt     += PlayWhoosh;
    }

    void OnDestroy()
    {
        if (combatSystem == null) return;
        combatSystem.onHitLandedAt -= PlayImpact;
        combatSystem.onSwungAt     -= PlayWhoosh;
    }

    GameObject[] Pool(GameObject prefab)
    {
        if (prefab == null) return null;

        var copies = new GameObject[Mathf.Max(1, copiesPerEffect)];
        for (int i = 0; i < copies.Length; i++)
        {
            copies[i] = Instantiate(prefab, m_Holder);
            Halt(copies[i]);
        }
        return copies;
    }

    void PlayImpact(Transform struck, Vector3 point, Vector3 direction)
    {
        if (m_Impacts == null) return;

        int slot = Random.Range(0, m_Impacts.Length);
        if (m_Impacts[slot] == null) return;

        GameObject go = m_Impacts[slot][m_ImpactNext[slot]];
        go.transform.SetParent(struck != null ? struck : m_Holder, true);
        Fire(go, point, direction, impactScale);
        m_ImpactNext[slot] = (m_ImpactNext[slot] + 1) % m_Impacts[slot].Length;
    }

    void PlayWhoosh(Transform weapon, int dir)
    {
        if (m_Whoosh == null || weapon == null) return;

        GameObject go = m_Whoosh[m_WhooshNext];
        m_WhooshNext  = (m_WhooshNext + 1) % m_Whoosh.Length;

        go.transform.SetParent(weapon, false);
        go.transform.localPosition = Vector3.forward * whooshForwardOffset;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one * Mathf.Max(0.01f, whooshScale);
        Play(go);
    }

    static void Fire(GameObject go, Vector3 point, Vector3 direction, float scale)
    {
        if (go == null) return;

        go.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        go.transform.SetPositionAndRotation(
            point, direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction, Vector3.up)
                : Quaternion.identity);
        Play(go);
    }

    static void Play(GameObject go)
    {
        var vfx = go.GetComponentInChildren<VisualEffect>(true);
        if (vfx != null) { vfx.Reinit(); vfx.Play(); return; }

        var ps = go.GetComponentInChildren<ParticleSystem>(true);
        if (ps != null) { ps.Clear(true); ps.Play(true); return; }

        var slash = go.GetComponentInChildren<SlashFlash>(true);
        if (slash != null) { slash.Play(); return; }

        Debug.LogWarning($"VfxBurst: '{go.name}' has no VisualEffect, ParticleSystem or " +
                         "SlashFlash anywhere on it, so nothing will play.", go);
    }

    static void Halt(GameObject go)
    {
        var vfx = go.GetComponentInChildren<VisualEffect>(true);
        if (vfx != null) { vfx.Stop(); return; }

        var ps = go.GetComponentInChildren<ParticleSystem>(true);
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
}
