using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MotoSquid.Rider
{
    public static class BikeClipBindingCheck
    {
        const string AIPath     = "Assets/Prefabs/Characters/Voodoo_R_AI.prefab";
        const string PlayerPath = "Assets/Prefabs/Characters/Voodoo_R_Player.prefab";

        [MenuItem("MotoSquid/Voodoo/Diagnose Bike Clip Binding (AI)")]
        public static void DiagnoseAI() => Diagnose(AIPath);

        [MenuItem("MotoSquid/Voodoo/Diagnose Bike Clip Binding (Player)")]
        public static void DiagnosePlayer() => Diagnose(PlayerPath);

        static void Diagnose(string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogError($"[BindCheck] Prefab not found: {prefabPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Animator bikeAnim = null;
                foreach (var a in root.GetComponentsInChildren<Animator>(true))
                {
                    var rac = a.runtimeAnimatorController;
                    if (rac != null && rac.name.StartsWith("BikeMaster")) { bikeAnim = a; break; }
                }
                if (bikeAnim == null)
                {
                    Debug.LogError("[BindCheck] No Animator running a BikeMaster controller was found on the prefab. " +
                                   "The bike art may be missing its Animator/controller, that alone would stop the lean.");
                    return;
                }

                var ac = bikeAnim.runtimeAnimatorController as AnimatorController;
                Debug.Log($"[BindCheck] Bike Animator at '{PathUnder(bikeAnim.transform, root.transform)}' " +
                          $"running '{ac.name}'. Checking {ac.animationClips.Distinct().Count()} clip(s) against the art.");

                foreach (var clip in ac.animationClips.Distinct())
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip); 
                    int bound = 0, unbound = 0;
                    var missing = new List<string>();
                    foreach (var b in bindings)
                    {
                        var t = string.IsNullOrEmpty(b.path) ? bikeAnim.transform : bikeAnim.transform.Find(b.path);
                        if (t != null) bound++;
                        else { unbound++; if (missing.Count < 8 && !missing.Contains(b.path)) missing.Add(b.path); }
                    }
                    string verdict = bound == 0 ? "✗ NOTHING BINDS" : unbound == 0 ? "✓ all bind" : "⚠ partial";
                    Debug.Log($"[BindCheck] '{clip.name}': {bound} bound / {unbound} unbound  {verdict}" +
                              (missing.Count > 0 ? $"\n   unresolved e.g.: {string.Join("  |  ", missing)}" : ""));
                }

                Debug.Log("[BindCheck] Tip: '0 bound' on every clip => Animator sits above the clip's root " +
                          "(needs a path prefix or to move down one node). 'all bind' but no lean => the pose is a " +
                          "frame-0/root bake/state not active problem instead.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static string PathUnder(Transform t, Transform root)
        {
            if (t == root) return t.name;
            string p = t.name;
            while (t.parent != null && t.parent != root) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
