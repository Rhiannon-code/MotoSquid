// ============================================================
//  RigBuilderInitializer.cs
//  Place on the same GameObject as RigBuilder (havencore_psy).
//  Ensures RigBuilder.Build() is called on Start so constraint
//  jobs are properly scheduled from the first frame.
// ============================================================

using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MotoSquid.Rider
{
    [RequireComponent(typeof(RigBuilder))]
    public class RigBuilderInitializer : MonoBehaviour
    {
        void Start()
        {
            var rb = GetComponent<RigBuilder>();
            if (rb != null)
                rb.Build();   // schedule constraint jobs from the first frame
        }
    }
}
