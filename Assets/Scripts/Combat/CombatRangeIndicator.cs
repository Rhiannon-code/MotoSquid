using UnityEngine;

namespace MotoSquid.Combat
{
public class CombatRangeIndicator : MonoBehaviour
{
    public CombatSystem combatSystem;
    public GameObject inRangeVisual;   // Shown while an opponent is within swinging reach
    public Transform  marker;          // Optional, parked over the target while it is in reach
    public float markerHeight = 1.6f;
    public bool playerOnly = true;

    private CombatSystem _shown;

    void Awake()
    {
        if (combatSystem == null) combatSystem = GetComponentInParent<CombatSystem>();
        Show(null);
    }

    void LateUpdate()
    {
        if (combatSystem == null) { enabled = false; return; }
        if (playerOnly && !combatSystem.isPlayer) { Show(null); enabled = false; return; }

        CombatSystem target = combatSystem.TargetInRange;
        if (target != _shown) Show(target);
        if (target != null && marker != null)
            marker.position = target.transform.position + Vector3.up * markerHeight;
    }

    void Show(CombatSystem target)
    {
        _shown = target;
        if (inRangeVisual != null) inRangeVisual.SetActive(target != null);
        if (marker != null && marker.gameObject != inRangeVisual)
            marker.gameObject.SetActive(target != null);
    }
}
}
