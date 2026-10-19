using UnityEngine;

namespace MotoSquid.Roads
{
    // A handle on one knot of its road's spline, moved with the ordinary Move tool or in the Road Plan window.
    // Tagged EditorOnly, so builds leave it out.
    [DisallowMultipleComponent]
    public class RoadNode : MonoBehaviour
    {
        [HideInInspector] public int index;
    }
}
