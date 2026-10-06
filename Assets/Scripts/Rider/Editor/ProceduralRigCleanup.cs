using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MotoSquid.Rider
{
    public static class ProceduralRigCleanup
    {
        static readonly string[] LegacySubtreeNames = { "Dummy_Mannequin", "Biker Rig" };

        [MenuItem("MotoSquid/Characters/Audit Legacy Target Blend Rig (Selected)")]
        public static void AuditSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null) { Debug.LogError("[LegacyRig] Select a bike root in the scene first."); return; }
            var found = new List<string>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (LegacySubtreeNames.Contains(t.name)) found.Add($"legacy subtree '{t.name}'");

            Debug.Log(found.Count == 0
                ? $"[LegacyRig] {go.name}: clean, no authored era subtrees left."
                : $"[LegacyRig] {go.name}: {found.Count} leftover(s):\n  " + string.Join("\n  ", found));
        }

    }
}
